using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Reads only the accessible battery-status group exposed by the already-open
/// Microsoft Phone Link window. No OCR, private Phone Link files, messages,
/// calls, browser access, Bluetooth commands or UI interactions.
/// The Phone Link window may be minimized.
/// This is a best-effort UI integration, not a documented battery API.
/// </summary>
internal static class PhoneLinkBatteryProvider
{
    private static DateTimeOffset lastAutoLaunch=DateTimeOffset.MinValue;
    public static string LastDiagnostic {get;private set;}="Henüz sorgulanmadı";
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd,int nCmdShow);

    /// <summary>
    /// Optional, disclosed workaround: opens the official Phone Link app using
    /// its own registered shell identity and immediately minimizes the window.
    /// This is NOT an undocumented CrossDevice battery API. No messages are read.
    /// </summary>
    public static async Task<BatteryReading?> TryReadAsync(bool allowAutoMinimize)
    {
        var direct=await Task.Run(TryRead);
        if(direct is not null || !allowAutoMinimize)return direct;
        // Avoid re-opening the UI every minute if sync or accessibility failed.
        if((DateTimeOffset.Now-lastAutoLaunch).TotalMinutes<5)return null;
        lastAutoLaunch=DateTimeOffset.Now;
        try
        {
            using var launch=Process.Start(new ProcessStartInfo{
                FileName="explorer.exe",
                Arguments=@"shell:AppsFolder\Microsoft.YourPhone_8wekyb3d8bbwe!App",
                UseShellExecute=true
            });
            // Phone Link first shows a non-battery SplashScreen. Its WinUI
            // accessibility Group loads several seconds afterward. Read it
            // only once the actual battery status becomes accessible.
            for(int attempt=0;attempt<12;attempt++)
            {
                await Task.Delay(850);
                var ready=await Task.Run(TryRead);
                if(ready is null)continue;
                foreach(var proc in Process.GetProcessesByName("PhoneExperienceHost"))
                {
                    using(proc)
                    {
                        proc.Refresh();
                        if(proc.MainWindowHandle!=IntPtr.Zero)
                            ShowWindow(proc.MainWindowHandle,6); // SW_MINIMIZE
                    }
                }
                return ready;
            }
            LastDiagnostic="Phone Link açıldı ancak pil erişilebilirlik alanı 10 saniyede hazır olmadı.";
            return null;
        }
        catch(Exception ex)
        {
            LastDiagnostic="Phone Link otomatik küçültme açılamadı: "+ex.GetType().Name;
            return null;
        }
    }

    private static readonly Regex PercentBefore = new(
        @"%\s*(?<percent>\d{1,3})(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PercentAfter = new(
        @"(?<!\d)(?<percent>\d{1,3})\s*%",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Checks the UI of PhoneExperienceHost, if running.
    /// Does not start, focus, restore, or change any window.
    /// </summary>
    public static BatteryReading? TryRead()
    {
        try
        {
            using var processList = new ProcessScope("PhoneExperienceHost");
            var pids = processList.ProcessIds;
            if(pids.Length==0)
            {
                LastDiagnostic="PhoneExperienceHost çalışmıyor.";
                return null;
            }

            var root = AutomationElement.RootElement;
            var windows = root.FindAll(TreeScope.Children, Condition.TrueCondition);
            var candidates = new List<BatteryReading>();
            int eligibleWindows=0,accessibleGroups=0;

            foreach (AutomationElement window in windows)
            {
                try
                {
                    if (!pids.Contains(window.Current.ProcessId)) continue;
                    // Avoid reading any other top-level windows or unrelated app UI.
                    if(window.Current.ClassName is not
                        ("WinUIDesktopWin32WindowClass" or "ApplicationFrameWindow"))continue;
                    eligibleWindows++;

                    // Restrict reads to Group control names: the phone battery
                    // is a Group. This deliberately avoids messages/notifications.
                    var groups = window.FindAll(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Group));
                    accessibleGroups+=groups.Count;
                    foreach (AutomationElement group in groups)
                    {
                        try
                        {
                            var name = group.Current.Name;
                            if (TryParseAccessibleBattery(name, out var pct, out var charging))
                            {
                                var detail = charging == true
                                    ? "Şarj oluyor · Phone Link ekranı (senkronizasyon güncelliği doğrulanmadı)"
                                    : charging == false
                                        ? "Şarj olmuyor · Phone Link ekranı (senkronizasyon güncelliği doğrulanmadı)"
                                        : "Phone Link ekranı (senkronizasyon güncelliği doğrulanmadı)";
                                candidates.Add(new BatteryReading(
                                    "Phone", pct, "Microsoft Phone Link (UI Automation)",
                                    detail, DateTimeOffset.Now));
                            }
                        }
                        catch (ElementNotAvailableException) { }
                        catch (InvalidOperationException) { }
                    }
                }
                catch (ElementNotAvailableException) { }
                catch (InvalidOperationException) { }
            }

            // If there are multiple distinct phone batteries, do not guess
            // which one belongs to the configured phone.
            LastDiagnostic=candidates.Count==1?"Phone Link pil alanı okundu.":
                candidates.Count>1?"Birden fazla telefon pil değeri: eşleştirme belirsiz.":
                eligibleWindows==0?"Phone Link arka planda çalışıyor, fakat UIA penceresi yok.":
                accessibleGroups==0?"Phone Link penceresinde erişilebilir grup bulunamadı.":
                "Phone Link pil grubu bulunamadı veya erişilebilirlik etiketi değişti.";
            return candidates.Count == 1 ? candidates[0] : null;
        }
        catch (ElementNotAvailableException) { }
        catch (InvalidOperationException) { }
        catch (System.Runtime.InteropServices.COMException) { }
        catch (System.ComponentModel.Win32Exception) { }
        return null;
    }

    /// <summary>Pure, testable language-independent percentage extraction.</summary>
    internal static bool TryParseAccessibleBattery(string? label,
        out int percent, out bool? charging)
    {
        percent = 0;
        charging = null;
        if (string.IsNullOrWhiteSpace(label) || label.Length > 260) return false;

        string normal = Normalize(label);
        if (!Regex.IsMatch(normal, @"\b(pil|battery)\b",
            RegexOptions.CultureInvariant)) return false;

        var match = PercentBefore.Match(normal);
        if (!match.Success) match = PercentAfter.Match(normal);
        if (!match.Success ||
            !int.TryParse(match.Groups["percent"].Value, out int n) ||
            n is < 0 or > 100) return false;

        // Do not confuse "not charging" with "charging".
        if (normal.Contains("sarj olmuyor") ||
            normal.Contains("not charging") ||
            normal.Contains("not plugged in"))
            charging = false;
        else if (normal.Contains("sarj oluyor") ||
            normal.Contains("charging") ||
            normal.Contains("plugged in"))
            charging = true;

        percent = n;
        return true;
    }

    private static string Normalize(string text)
    {
        var decomp = text.ToLowerInvariant().Replace('ı', 'i')
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomp.Length);
        foreach (var ch in decomp)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed class ProcessScope : IDisposable
    {
        private readonly Process[] processes;
        public int[] ProcessIds { get; }
        public ProcessScope(string name)
        {
            processes = Process.GetProcessesByName(name);
            ProcessIds = processes.Select(p => p.Id).ToArray();
        }
        public void Dispose()
        {
            foreach (var p in processes) p.Dispose();
        }
    }
}

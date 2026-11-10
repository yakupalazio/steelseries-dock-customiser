using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            if (pids.Length == 0) return null;

            var root = AutomationElement.RootElement;
            var windows = root.FindAll(TreeScope.Children, Condition.TrueCondition);
            var candidates = new List<BatteryReading>();

            foreach (AutomationElement window in windows)
            {
                try
                {
                    if (!pids.Contains(window.Current.ProcessId)) continue;
                    // Use owning process rather than a hard-coded WinUI class.
                    // Phone Link can change its native window class after updates.
                    if(window.Current.NativeWindowHandle==0) continue;

                    // Restrict reads to Group control names: the phone battery
                    // is a Group. This deliberately avoids messages/notifications.
                    var groups = window.FindAll(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Group));
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
            return candidates.Count>0 && candidates.Select(c=>c.Percent).Distinct().Count()==1
                ? candidates[0] : null;
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

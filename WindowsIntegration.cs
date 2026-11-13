using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Explicit, current-user Windows integration. Never touches the machine-wide
/// Run key, schedules tasks, enables Realtime or self-elevates silently.
/// </summary>
internal static class WindowsIntegration
{
    // Absence of a persisted preference or Run key must be OFF.
    internal const bool DefaultStartupEnabled=false;
    internal const bool DefaultHighPriorityEnabled=false;

    internal const string RunKeyPath=
        @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string RunValueName="SteelseriesDockCustomiser";
    internal static string PreferencePath=>Path.Combine(
        Beta5Sandbox.SettingsDirectory,"windows-integration-v1.json");

    private sealed class Preferences
    {
        public bool HighPriority {get;set;} // default false
    }

    internal static bool IsReleaseExecutable(string? path)=>
        !string.IsNullOrWhiteSpace(path) &&
        Path.IsPathFullyQualified(path) &&
        string.Equals(Path.GetFileName(path),
            "SteelseriesDockCustomiser.exe",
            StringComparison.OrdinalIgnoreCase) &&
        File.Exists(path);

    internal static bool CanRegisterAutostart()=>
        IsReleaseExecutable(Environment.ProcessPath);

    // Pure, testable and intentionally per-user: no admin and no task scheduler.
    internal static string BuildStartupCommand(string executablePath)
    {
        if(string.IsNullOrWhiteSpace(executablePath) ||
           !Path.IsPathFullyQualified(executablePath) ||
           !string.Equals(Path.GetFileName(executablePath),
               "SteelseriesDockCustomiser.exe",
               StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Geçerli bir yayımlanmış EXE gerekli.");
        if(executablePath.Contains('\"')||
           executablePath.Contains((char)13)||
           executablePath.Contains((char)10))
            throw new ArgumentException("Geçersiz Windows dosya yolu.");
        return "\""+executablePath+"\" --windows-startup";
    }

    internal static bool IsOurStartupCommand(string? value)
    {
        if(string.IsNullOrEmpty(value)||
           !value.EndsWith("\" --windows-startup",
               StringComparison.OrdinalIgnoreCase)||
           value[0]!='\"')return false;
        var path=value[1..^(("\" --windows-startup").Length)];
        return Path.IsPathFullyQualified(path)&&
            string.Equals(Path.GetFileName(path),
                "SteelseriesDockCustomiser.exe",
                StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsAutostartEnabled()
    {
        try
        {
            using var key=Registry.CurrentUser.OpenSubKey(RunKeyPath,false);
            string? value=key?.GetValue(RunValueName) as string;
            return IsOurStartupCommand(value);
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException
            or System.Security.SecurityException or ArgumentException){return false;}
    }

    internal static void SetAutostartEnabled(bool enabled)
    {
        if(!CanRegisterAutostart())
            throw new InvalidOperationException(
                "Otomatik başlangıç yalnızca yayımlanmış SteelseriesDockCustomiser.exe "+
                "içinden ayarlanabilir. Geliştirme DLL'si başlangıca eklenmez.");
        using var key=Registry.CurrentUser.CreateSubKey(
            RunKeyPath,writable:true)
            ??throw new IOException("Windows başlangıç kaydı açılamadı.");
        if(enabled)
            key.SetValue(RunValueName,BuildStartupCommand(
                Environment.ProcessPath!),RegistryValueKind.String);
        else
            key.DeleteValue(RunValueName,throwOnMissingValue:false);
    }

    internal static bool HighPriorityEnabled()
    {
        try
        {
            if(!File.Exists(PreferencePath) ||
               new FileInfo(PreferencePath).Length>4096)return false;
            return JsonSerializer.Deserialize<Preferences>(
                File.ReadAllText(PreferencePath))?.HighPriority==true;
        }
        catch{return false;}
    }

    internal static void SaveHighPriority(bool enabled)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
        string temporary=PreferencePath+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(new Preferences{
            HighPriority=enabled}));
        File.Move(temporary,PreferencePath,true);
    }

    internal static ProcessPriorityClass PriorityFor(bool high)=>
        high?ProcessPriorityClass.High:ProcessPriorityClass.Normal;

    internal static bool TrySetCurrentPriority(bool high,out string error)
    {
        try
        {
            using var current=Process.GetCurrentProcess();
            var goal=PriorityFor(high);
            current.PriorityClass=goal;
            error="";
            return current.PriorityClass==goal;
        }
        catch(Exception ex) when(ex is Win32Exception or UnauthorizedAccessException
            or InvalidOperationException or NotSupportedException)
        {
            error=ex.Message;
            return false;
        }
    }

    // Called ONLY on normal interactive app launch, never in diagnostics,
    // screenshots or self-test entrypoints. No UAC at startup.
    internal static void ApplySavedPriorityAtLaunch()
    {
        if(HighPriorityEnabled())
            _=TrySetCurrentPriority(true,out _);
    }

    internal static bool CanRequestElevatedHelper()=>
        CanRegisterAutostart();

    // If Windows refuses modifying our own process priority, the owner can
    // explicitly approve an isolated elevated helper. The main WinForms app
    // continues to run unelevated, avoiding elevated UI, HID and web code.
    internal static async Task<bool> RequestElevatedHighPriorityAsync()
    {
        if(!CanRequestElevatedHelper())return false;
        using var current=Process.GetCurrentProcess();
        int pid=current.Id;
        long started=current.StartTime.ToUniversalTime().Ticks;
        using var elevated=Process.Start(new ProcessStartInfo{
            FileName=Environment.ProcessPath!,
            Arguments=$"--priority-helper {pid} {started}",
            Verb="runas",UseShellExecute=true,
            WindowStyle=ProcessWindowStyle.Hidden
        });
        if(elevated is null)return false;
        // UAC appears only after an explicit user decision. The helper exits
        // immediately after changing this application's process priority.
        await elevated.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        if(elevated.ExitCode!=0)return false;
        try
        {
            using var refreshed=Process.GetCurrentProcess();
            refreshed.Refresh();
            return refreshed.PriorityClass==ProcessPriorityClass.High;
        }
        catch{return false;}
    }

    // Privileged mode executes no UI, profile read, network or HID code and
    // only touches the exact same EXE process, startup timestamp and session.
    internal static bool RunElevatedPriorityHelper(string[] args)
    {
        if(args.Length!=3 ||
           !int.TryParse(args[1],out int pid)||pid<=0||
           !long.TryParse(args[2],out long startTicks)||
           !IsReleaseExecutable(Environment.ProcessPath))return false;
        try
        {
            using var self=Process.GetCurrentProcess();
            using var target=Process.GetProcessById(pid);
            if(target.SessionId!=self.SessionId ||
               target.StartTime.ToUniversalTime().Ticks!=startTicks ||
               !string.Equals(target.MainModule?.FileName,
                   self.MainModule?.FileName,
                   StringComparison.OrdinalIgnoreCase))
                return false;
            target.PriorityClass=ProcessPriorityClass.High;
            return target.PriorityClass==ProcessPriorityClass.High;
        }
        catch{return false;}
    }
}

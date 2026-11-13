using System;
using System.Diagnostics;
using System.IO;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Windows integration invariants without registry modification,
/// priority mutations, UAC requests or persistent settings writes.
/// </summary>
internal static class WindowsIntegrationSelfTests
{
    internal static void Run()
    {
        if(WindowsIntegration.DefaultStartupEnabled ||
           WindowsIntegration.DefaultHighPriorityEnabled)
            throw new Exception("Windows integration default was not OFF.");

        const string example=
            @"C:\Program Files\Dock Studio\SteelseriesDockCustomiser.exe";
        string entry=WindowsIntegration.BuildStartupCommand(example);
        if(entry!="\""+example+"\" --windows-startup" ||
           !WindowsIntegration.IsOurStartupCommand(entry) ||
           WindowsIntegration.IsOurStartupCommand(null) ||
           WindowsIntegration.IsOurStartupCommand(
               "\"C:\\Other\\FakeApp.exe\" --windows-startup") ||
           WindowsIntegration.IsOurStartupCommand(
               "\""+example+"\" --priority-helper 42 10") ||
           WindowsIntegration.IsOurStartupCommand(
               "\""+example+"\" & malicious"))
            throw new Exception("Autostart entry validation failed.");

        try
        {
            _=WindowsIntegration.BuildStartupCommand("SteelseriesDockCustomiser.exe");
            throw new Exception("Accepted a relative autorun executable.");
        }
        catch(ArgumentException){}
        try
        {
            _=WindowsIntegration.BuildStartupCommand(
                "C:\\bad\" --unexpected\\SteelseriesDockCustomiser.exe");
            throw new Exception("Accepted an unsafe autorun executable.");
        }
        catch(ArgumentException){}

        if(WindowsIntegration.PriorityFor(true)!=ProcessPriorityClass.High ||
           WindowsIntegration.PriorityFor(false)!=ProcessPriorityClass.Normal ||
           WindowsIntegration.PriorityFor(true)==ProcessPriorityClass.RealTime)
            throw new Exception("Realtime priority unexpectedly allowed.");

        // The elevated entrypoint must reject malformed PID/start data before
        // it could obtain any process handle. No OS settings are modified.
        if(WindowsIntegration.RunElevatedPriorityHelper(
            ["--priority-helper","0","0"]) ||
           WindowsIntegration.RunElevatedPriorityHelper(
            ["--priority-helper","not-a-pid","0"]) ||
           WindowsIntegration.RunElevatedPriorityHelper(
            ["--priority-helper","42"]))
            throw new Exception("Invalid elevated-helper arguments accepted.");

        Console.WriteLine(
            "PASS: Windows startup OFF by default, safe per-user Run key "+
            "command, opt-in High/Normal only, UAC helper rejects invalid PID.");
    }
}

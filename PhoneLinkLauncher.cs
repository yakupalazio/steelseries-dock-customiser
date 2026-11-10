using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SteelseriesDockCustomiser;

/// <summary>
/// User-initiated workaround: opens the official Phone Link window and
/// minimizes it. Does not access or alter private Phone Link storage.
/// Closed-window battery data remains unavailable to third-party apps.
/// </summary>
internal static class PhoneLinkLauncher
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd,int nCmdShow);

    public static async Task<string> OpenMinimizedAsync()
    {
        try
        {
            var command=new ProcessStartInfo("explorer.exe",
                @"shell:AppsFolder\Microsoft.YourPhone_8wekyb3d8bbwe!App")
            {
                UseShellExecute=true
            };
            Process.Start(command)?.Dispose();
            for(int i=0;i<35;i++)
            {
                await Task.Delay(150);
                foreach(var p in Process.GetProcessesByName("PhoneExperienceHost"))
                {
                    using(p)
                    {
                        p.Refresh();
                        if(p.MainWindowHandle==IntPtr.Zero)continue;
                        ShowWindow(p.MainWindowHandle,6); // SW_MINIMIZE
                        return "Phone Link küçültülmüş açıldı; pil okuyucu yeniden denenecek.";
                    }
                }
            }
            return "Phone Link açıldı ancak pencere algılanamadı. Doğrudan uygulamada kontrol et.";
        }
        catch(Exception ex)
        {
            return "Phone Link açılamadı: "+ex.GetType().Name;
        }
    }
}

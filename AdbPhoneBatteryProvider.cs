using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Optional, opt-in Android ADB battery adapter. Only sends the following
/// read-only commands: "devices", "shell getprop ro.product.model"
/// and "shell dumpsys battery".
/// No apps, files, messages, screenshots or device settings are accessed.
/// Requires user-approved USB/wireless debugging and Google Platform-Tools.
/// </summary>
internal static class AdbPhoneBatteryProvider
{
    public static string PreferredPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","platform-tools","adb.exe");

    private static string? FindAdb()
    {
        if(File.Exists(PreferredPath))return PreferredPath;
        foreach(var entry in (Environment.GetEnvironmentVariable("PATH")??"").Split(';',
            StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string file=Path.Combine(entry.Trim('"'),"adb.exe");
                if(File.Exists(file))return file;
            }catch{}
        }
        return null;
    }

    public static bool Installed=>FindAdb()!=null;

    public static Task<BatteryReading?> ReadAsync() => Task.Run(()=>{
        var exe=FindAdb();
        if(exe is null)return null;
        try
        {
            string list=Run(exe,["devices"],4500);
            var devices=list.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries)
                .Select(s=>s.Split(['\t',' '],StringSplitOptions.RemoveEmptyEntries))
                .Where(parts=>parts.Length>=2 && parts[1]=="device")
                .Select(parts=>parts[0]).ToArray();
            if(devices.Length!=1)return null; // Never guess which phone.
            string model=Run(exe,["-s",devices[0],"shell","getprop","ro.product.model"],4500).Trim();
            // Avoid accidentally reading a different paired Android device.
            if(!model.StartsWith("SM-S926",StringComparison.OrdinalIgnoreCase) &&
               !model.Contains("S24+",StringComparison.OrdinalIgnoreCase))return null;
            string status=Run(exe,["-s",devices[0],"shell","dumpsys","battery"],4500);
            return TryParse(status,DateTimeOffset.Now,out var reading) ? reading : null;
        }
        catch(Exception){return null;}
    });

    internal static bool TryParse(string response,DateTimeOffset time,out BatteryReading reading)
    {
        reading=new BatteryReading("Phone",null,"Android ADB","No valid level",time);
        static int? Property(string data,string key)
        {
            var m=Regex.Match(data,@"(?im)^\s*"+Regex.Escape(key)+@"\s*:\s*(\d+)\s*$");
            return m.Success && int.TryParse(m.Groups[1].Value,out int n) ? n : null;
        }

        int? level=Property(response,"level"),scale=Property(response,"scale");
        if(level is null || scale is null || scale<=0 || scale>10000 ||
           level<0 || level>scale)return false;
        int percent=(int)Math.Round(level.Value*100.0/scale.Value);
        if(percent is <0 or >100)return false;

        int? chargingStatus=Property(response,"status");
        bool plugged=Property(response,"powered")==1 ||
            (Property(response,"AC powered")==1 ||
             Property(response,"USB powered")==1 ||
             Property(response,"Wireless powered")==1);
        string state=chargingStatus switch
        {
            2=>"Şarj oluyor",
            5=>"Tam dolu",
            3=>"Şarj olmuyor",
            4=>"Şarj olmuyor",
            _=>plugged?"Güç bağlı":"Pil"
        };
        reading=new BatteryReading("Phone",percent,"Android ADB (salt-okunur)",
            state+" · USB/kablosuz hata ayıklama yetkilendirildi",time);
        return true;
    }

    private static string Run(string exe,string[] arguments,int timeoutMilliseconds)
    {
        using var proc=new Process();
        proc.StartInfo=new ProcessStartInfo(exe){
            UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardOutput=true,RedirectStandardError=true
        };
        foreach(var arg in arguments)proc.StartInfo.ArgumentList.Add(arg);
        proc.Start();
        if(!proc.WaitForExit(timeoutMilliseconds))
        {
            proc.Kill(entireProcessTree:true);
            throw new TimeoutException("ADB command time-out.");
        }
        if(proc.ExitCode!=0)throw new IOException("ADB unavailable");
        return proc.StandardOutput.ReadToEnd();
    }
}

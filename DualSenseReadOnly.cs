using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Optional experimental DualSense Bluetooth HID *input* reader.
/// Never sends HID output/feature reports, controller effects or device settings.
/// It can fail when Steam or a game has the input stream open. Off by default.
/// </summary>
internal static class DualSenseReadOnly
{
    private const uint DIGCF_PRESENT=0x2,DIGCF_DEVICEINTERFACE=0x10;
    private const uint GENERIC_READ=0x80000000,FILE_SHARE_READ=0x1,FILE_SHARE_WRITE=0x2;
    private const uint OPEN_EXISTING=3,FILE_FLAG_OVERLAPPED=0x40000000;
    private static readonly Guid HidInterface=new("4d1e55b2-f16f-11cf-88cb-001111000030");

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterfaceData { public int cbSize;public Guid InterfaceClassGuid;public int Flags;public IntPtr Reserved; }

    [DllImport("setupapi.dll",EntryPoint="SetupDiGetClassDevsW",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid g,IntPtr enumerator,IntPtr hwnd,uint flags);
    [DllImport("setupapi.dll",SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr devinfo,ref Guid g,
        uint index,ref DeviceInterfaceData entry);
    [DllImport("setupapi.dll",EntryPoint="SetupDiGetDeviceInterfaceDetailW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref DeviceInterfaceData entry,
        IntPtr detail,uint size,out uint required,IntPtr info);
    [DllImport("setupapi.dll",SetLastError=true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern SafeFileHandle CreateFile(string path,uint access,uint share,
        IntPtr security,uint creation,uint flags,IntPtr template);

    private static List<string> EnumeratePaths()
    {
        var paths=new List<string>();
        var guid=HidInterface;
        IntPtr set=SetupDiGetClassDevs(ref guid,IntPtr.Zero,IntPtr.Zero,DIGCF_PRESENT|DIGCF_DEVICEINTERFACE);
        if(set==new IntPtr(-1))return paths;
        try
        {
            for(uint idx=0;idx<256;idx++)
            {
                var d=new DeviceInterfaceData{cbSize=Marshal.SizeOf<DeviceInterfaceData>()};
                if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,idx,ref d))break;
                SetupDiGetDeviceInterfaceDetail(set,ref d,IntPtr.Zero,0,out var needed,IntPtr.Zero);
                if(needed<16||needed>4096)continue;
                IntPtr buffer=Marshal.AllocHGlobal((int)needed);
                try
                {
                    Marshal.WriteInt32(buffer,IntPtr.Size==8?8:6);
                    if(!SetupDiGetDeviceInterfaceDetail(set,ref d,buffer,needed,out _,IntPtr.Zero))continue;
                    var path=Marshal.PtrToStringUni(IntPtr.Add(buffer,4));
                    if(path is not null && IsSupportedDualSensePath(path))
                        paths.Add(path);
                }
                finally {Marshal.FreeHGlobal(buffer);}
            }
        }
        finally {SetupDiDestroyDeviceInfoList(set);}
        return paths;
    }

    // Classic Bluetooth HID paths use VID&0002054C_PID&0CE6 rather than
    // USB's VID_054C&PID_0CE6. Both are Sony's same hardware, not aliases
    // for random Bluetooth devices.
    internal static bool IsSupportedDualSensePath(string path)
    {
        bool sony=path.Contains("vid_054c",StringComparison.OrdinalIgnoreCase) ||
            path.Contains("vid&0002054c",StringComparison.OrdinalIgnoreCase) ||
            path.Contains("vid&0001054c",StringComparison.OrdinalIgnoreCase);
        bool product=path.Contains("pid_0ce6",StringComparison.OrdinalIgnoreCase) ||
            path.Contains("pid&0ce6",StringComparison.OrdinalIgnoreCase) ||
            path.Contains("pid_0df2",StringComparison.OrdinalIgnoreCase) ||
            path.Contains("pid&0df2",StringComparison.OrdinalIgnoreCase);
        return sony&&product;
    }

    public static string LastDiagnostic {get;private set;}="Henüz sorgulanmadı.";
    public static async Task<int?> TryBatteryPercentAsync()
    {
        var paths=EnumeratePaths();
        LastDiagnostic=paths.Count==0?
            "DualSense HID yolu bulunamadı. Bluetooth HID aygıtını kontrol et.":
            $"DualSense HID yolu bulundu ({paths.Count}), pil raporu bekleniyor.";
        foreach(string path in paths)
        {
            try
            {
                using var handle=CreateFile(path,GENERIC_READ,FILE_SHARE_READ|FILE_SHARE_WRITE,
                    IntPtr.Zero,OPEN_EXISTING,FILE_FLAG_OVERLAPPED,IntPtr.Zero);
                if(handle.IsInvalid)
                {
                    LastDiagnostic="DualSense HID açma izni yok. Steam/oyun kontrolcüyü kullanıyor olabilir.";
                    continue;
                }
                using var stream=new FileStream(handle,FileAccess.Read,128,isAsync:true);
                using var timeout=new CancellationTokenSource(TimeSpan.FromMilliseconds(1300));
                var bytes=new byte[128];
                // Read an already generated input report. Does not request or send anything.
                int count=await stream.ReadAsync(bytes.AsMemory(),timeout.Token);
                if(count==0)continue;
                int? result=DecodeReport(bytes,count);
                if(result is int level)
                {
                    LastDiagnostic=$"DualSense pili: yaklaşık %{level} (HID raporu).";
                    return level;
                }
                LastDiagnostic=$"DualSense HID raporu geldi ({count} bayt); pil alanı doğrulanamadı.";
            }
            catch(OperationCanceledException)
            {
                LastDiagnostic="DualSense HID pil raporu zaman aşımına uğradı.";
            }
            catch(IOException)
            {
                LastDiagnostic="DualSense HID verisine erişilemedi (başka uygulama kullanıyor olabilir).";
            }
            catch(UnauthorizedAccessException)
            {
                LastDiagnostic="DualSense HID okuma izni yok.";
            }
            catch(ArgumentException)
            {
                LastDiagnostic="DualSense HID rapor biçimi desteklenmiyor.";
            }
        }
        return null;
    }

    internal static int? DecodeReport(byte[] data,int length)
    {
        if(data.Length==0||length<60)return null;
        // Bluetooth standard full report: id 0x31, status near byte 54.
        // USB full report id 0x01, status near byte 53.
        int statusIndex=data[0] switch {0x31=>54,0x01=>53,_=>-1};
        if(statusIndex<0||length<=statusIndex)return null;
        int tenths=data[statusIndex]&0x0F;
        if(tenths>10)return null;
        // DualSense reports battery in approx. 10% bands, not 1% precision.
        return tenths*10;
    }
}

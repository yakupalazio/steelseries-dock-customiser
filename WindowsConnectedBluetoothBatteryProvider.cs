using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Read-only Windows Bluetooth battery from PnP property
/// {104EA319-6EE2-4701-BD47-8DDBF425BBE5}, pid 2.
/// A cached Windows percentage is only accepted if the exact Bluetooth
/// address is independently reported as CONNECTED by WinRT at poll time.
/// No Bluetooth pairing, HID config, USB writes or extra vendor software.
/// </summary>
internal static class WindowsConnectedBluetoothBatteryProvider
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static readonly Guid BatteryGuid=
        new("104EA319-6EE2-4701-BD47-8DDBF425BBE5");
    private static readonly Guid NameGuid=
        new("A45C254E-DF1C-4EFD-8020-67D146A850E0");
    private static readonly Guid AddressGuid=
        new("2BD67D8B-8BEB-48D5-87E0-6CDA3428040A");
    private const uint DIGCF_PRESENT=2,DIGCF_ALLCLASSES=4;
    private const uint DN_DEVICE_DISCONNECTED=0x02000000;
    private const uint DEVPROP_TYPE_BYTE=0x00000003;
    private const uint DEVPROP_TYPE_STRING=0x00000012;
    private const int MaximumNodes=512;
    private const int MaximumAddresses=80;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVPROPKEY
    {
        public Guid fmtid;
        public uint pid;
        public DEVPROPKEY(Guid g,uint p){fmtid=g;pid=p;}
    }

    [DllImport("setupapi.dll",EntryPoint="SetupDiGetClassDevsW",
        CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern IntPtr SetupDiGetClassDevs(
        IntPtr classGuid,
        [MarshalAs(UnmanagedType.LPWStr)]string? enumerator,
        IntPtr hwnd,uint flags);

    [DllImport("setupapi.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(
        IntPtr set,uint index,ref SP_DEVINFO_DATA info);

    [DllImport("setupapi.dll",EntryPoint="SetupDiGetDevicePropertyW",
        CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceProperty(
        IntPtr set,ref SP_DEVINFO_DATA info,ref DEVPROPKEY key,
        out uint type,[Out]byte[] bytes,uint size,
        out uint required,uint flags);

    [DllImport("setupapi.dll",EntryPoint="SetupDiGetDeviceInstanceIdW",
        CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstanceId(
        IntPtr set,ref SP_DEVINFO_DATA info,
        StringBuilder deviceId,uint bufferSize,out uint required);

    [DllImport("setupapi.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    [DllImport("cfgmgr32.dll",EntryPoint="CM_Get_DevNode_Status")]
    private static extern uint CM_Get_DevNode_Status(
        out uint status,out uint problem,uint devInst,uint flags);

    internal sealed record WindowsBatteryFact(
        string Address,string Name,int Battery,bool PresentConnected);

    internal static string? NormalizeBluetoothAddress(string? raw)
    {
        if(string.IsNullOrWhiteSpace(raw))return null;
        // Only complete, 48-bit Bluetooth addresses are candidates.
        string normalized=new string(raw.Where(Uri.IsHexDigit)
            .Select(char.ToUpperInvariant).ToArray());
        return normalized.Length==12 &&
               normalized!="000000000000"?normalized:null;
    }

    internal static string? AddressFromDeviceInstance(string? id)
    {
        if(string.IsNullOrWhiteSpace(id))return null;
        var match=Regex.Match(id,@"DEV_([0-9a-f]{12})",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        return match.Success?NormalizeBluetoothAddress(match.Groups[1].Value):null;
    }

    private static byte[]? GetProperty(IntPtr set,ref SP_DEVINFO_DATA device,
        Guid keyGuid,uint keyId,out uint propertyType)
    {
        var key=new DEVPROPKEY(keyGuid,keyId);
        var buffer=new byte[768];
        bool ok=SetupDiGetDeviceProperty(set,ref device,ref key,
            out propertyType,buffer,(uint)buffer.Length,out uint required,0);
        if(!ok||required==0||required>buffer.Length)return null;
        return buffer[..(int)required];
    }

    private static string? GetStringProperty(IntPtr set,
        ref SP_DEVINFO_DATA device,Guid key,uint id)
    {
        byte[]? value=GetProperty(set,ref device,key,id,out uint type);
        if(value is null||type!=DEVPROP_TYPE_STRING||value.Length<2 ||
           value.Length%2!=0)return null;
        return Encoding.Unicode.GetString(value).TrimEnd('\0');
    }

    private static int? GetBattery(IntPtr set,ref SP_DEVINFO_DATA device)
    {
        var value=GetProperty(set,ref device,BatteryGuid,2,out uint type);
        if(type!=DEVPROP_TYPE_BYTE||value is not{Length:1}||
           value[0]>100)return null;
        return value[0];
    }

    private static IReadOnlyList<WindowsBatteryFact> ReadLocalPnpBattery()
    {
        var all=new List<WindowsBatteryFact>();
        foreach(string enumerator in new[]{"BTHENUM","BTHLE","BTHLEDEVICE"})
        {
            IntPtr devices=SetupDiGetClassDevs(IntPtr.Zero,enumerator,
                IntPtr.Zero,DIGCF_PRESENT|DIGCF_ALLCLASSES);
            if(devices==new IntPtr(-1))continue;
            try
            {
                for(uint n=0;n<MaximumNodes;n++)
                {
                    var info=new SP_DEVINFO_DATA{
                        cbSize=(uint)Marshal.SizeOf<SP_DEVINFO_DATA>()};
                    if(!SetupDiEnumDeviceInfo(devices,n,ref info))break;
                    int? percent=GetBattery(devices,ref info);
                    if(percent is null)continue;
                    if(CM_Get_DevNode_Status(out uint status,out _,
                        info.DevInst,0)!=0 ||
                       (status&DN_DEVICE_DISCONNECTED)!=0)
                        continue;
                    var name=GetStringProperty(devices,ref info,
                        NameGuid,14)??"Bluetooth device";
                    var address=NormalizeBluetoothAddress(
                        GetStringProperty(devices,ref info,AddressGuid,1));
                    if(address is null)
                    {
                        var id=new StringBuilder(512);
                        if(SetupDiGetDeviceInstanceId(devices,ref info,id,
                            (uint)id.Capacity,out _))
                            address=AddressFromDeviceInstance(id.ToString());
                    }
                    if(address is null)continue;
                    all.Add(new(address,name,percent.Value,true));
                }
            }
            catch{/* Windows device state can change during enumeration. */}
            finally{SetupDiDestroyDeviceInfoList(devices);}
        }
        return all;
    }

    private static async Task<HashSet<string>> ConnectedAddresses()
    {
        var all=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var classic=await DeviceInformation.FindAllAsync(
                BluetoothDevice.GetDeviceSelectorFromConnectionStatus(
                    BluetoothConnectionStatus.Connected));
            foreach(var info in classic.Take(MaximumAddresses))
            {
                try
                {
                    using var device=await BluetoothDevice.FromIdAsync(info.Id);
                    if(device is null ||
                       device.ConnectionStatus!=BluetoothConnectionStatus.Connected)
                        continue;
                    var address=NormalizeBluetoothAddress(
                        device.BluetoothAddress.ToString("X12"));
                    if(address is not null)all.Add(address);
                }
                catch{}
            }
        }
        catch{}
        try
        {
            var ble=await DeviceInformation.FindAllAsync(
                BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(
                    BluetoothConnectionStatus.Connected));
            foreach(var info in ble.Take(MaximumAddresses))
            {
                try
                {
                    using var device=await BluetoothLEDevice.FromIdAsync(info.Id);
                    if(device is null||
                       device.ConnectionStatus!=BluetoothConnectionStatus.Connected)
                        continue;
                    var address=NormalizeBluetoothAddress(
                        device.BluetoothAddress.ToString("X12"));
                    if(address is not null)all.Add(address);
                }
                catch{}
            }
        }
        catch{}
        return all;
    }

    internal static IReadOnlyList<BatteryReading> ConfirmFresh(
        IReadOnlyList<WindowsBatteryFact> pnp,
        ISet<string> connected,DateTimeOffset now)
    {
        var list=new List<BatteryReading>();
        foreach(var group in pnp.Where(f=>
            f.PresentConnected&&
            connected.Contains(f.Address)&&
            f.Battery is >=0 and <=100)
            .GroupBy(f=>f.Address,StringComparer.OrdinalIgnoreCase))
        {
            // If profile nodes for a single Bluetooth device disagree on
            // percent, there is no trustworthy one-to-one reading.
            int[] percentages=group.Select(f=>f.Battery).Distinct().ToArray();
            if(percentages.Length!=1)continue;
            string name=group.Select(f=>f.Name).FirstOrDefault(
                n=>!string.IsNullOrWhiteSpace(n))??"Bluetooth device";
            name=CleanName(name);
            string kind=StableKind(group.Key);
            string icon=BatteryDeviceClassifier.IconFor(name,"Bluetooth");
            BatteryDeviceCatalog.Register(kind,name,icon);
            list.Add(new BatteryReading(kind,percentages[0],
                "Windows Bluetooth (connected; OS-reported)",
                "Connection verified; Windows battery indicator may lag hardware",now)
            {FriendlyName=name,IconKind=icon});
        }
        return list;
    }

    private static string CleanName(string name)
    {
        string[] suffixes={
            " Hands-Free AG"," Hands-Free HF"," Hands-Free",
            " Handsfree AG"," Handsfree"," Avrcp Transport"," Stereo"};
        string value=name.Trim();
        foreach(string suffix in suffixes)
            if(value.EndsWith(suffix,StringComparison.OrdinalIgnoreCase))
                return value[..^suffix.Length].Trim();
        return value;
    }

    internal static string StableKind(string address)=>
        "WinBt:"+Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(address)),0,7);

    internal static async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        if(!OperatingSystem.IsWindows()||
           !await Gate.WaitAsync(0))return Array.Empty<BatteryReading>();
        try
        {
            var devices=await Task.Run(ReadLocalPnpBattery);
            if(devices.Count==0)return Array.Empty<BatteryReading>();
            var connected=await ConnectedAddresses();
            return ConfirmFresh(devices,connected,DateTimeOffset.Now);
        }
        catch{return Array.Empty<BatteryReading>();}
        finally{Gate.Release();}
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

// Do not replace a missing reading with an arbitrary 0%, 100%, or demo number.
internal sealed record BatteryReading(string Kind,int? Percent,string Source,
    string Detail, DateTimeOffset CheckedAt)
{
    public string Display => Percent is int p ? $"{p}%" : "--%";
    public bool Available => Percent is >=0 and <=100;
}
internal sealed class BatterySnapshot
{
    public DateTimeOffset CheckedAt {get; init;} = DateTimeOffset.MinValue;
    public bool DualSenseConnected {get;init;}=false;
    public Dictionary<string,BatteryReading> Values {get; init;} = new(StringComparer.OrdinalIgnoreCase);
    public BatteryReading this[string kind] => Values.TryGetValue(kind,out var b)
        ? b : new BatteryReading(kind,null,"No provider","Kaynak bulunamadı",CheckedAt);
}
internal static class BatterySources
{
    public static readonly string[] Kinds =
        ["Laptop","Mouse","Headset","SpareBattery","DualSense","Phone"];
    // Polling does not change GG, Bluetooth profiles, paired devices or HID.
    public static async Task<BatterySnapshot> PollAsync(bool readDualSenseHid=false,
        bool enableAdbPhone=false,bool autoMinimizePhoneLink=false)
    {
        // Live updates never enumerate all Windows PnP devices. That diagnostic
        // path is expensive and carries no trustworthy live percentages.
        // GG and laptop are read from their current sources instead.
        var snapshot=await PollRealtimeAsync(new BatterySnapshot());
        // Phone Link's accessibility status is available while minimized.
        // Do not display old Bluetooth PnP percentages when UIA is unavailable.
        BatteryReading? phone=null;
        if(enableAdbPhone) phone=await AdbPhoneBatteryProvider.ReadAsync();
        phone ??= await PhoneLinkBatteryProvider.TryReadAsync(autoMinimizePhoneLink);
        if(phone is not null)
            snapshot.Values["Phone"] = phone;
        else
            snapshot.Values["Phone"]=new BatteryReading("Phone",null,
                "Phone Link (erişilemiyor)",
                PhoneLinkBatteryProvider.LastDiagnostic,DateTimeOffset.Now);
        if(readDualSenseHid && !snapshot["DualSense"].Available)
        {
            int? estimated=await DualSenseReadOnly.TryBatteryPercentAsync();
            if(estimated is int value)
                snapshot.Values["DualSense"]=new BatteryReading("DualSense",value,
                    "Bluetooth HID (read-only; approximate 10% bands)",
                    "Deneysel: son alınan HID raporundan yaklaşık seviye.",
                    DateTimeOffset.Now);
        }
        // Query actual Bluetooth connection status only in the slow loop.
        bool connected=await DualSenseConnection.IsConnectedAsync();
        return new BatterySnapshot{
            CheckedAt=snapshot.CheckedAt,Values=snapshot.Values,
            DualSenseConnected=connected || snapshot["DualSense"].Available
        };
    }

    /// <summary>
    /// Per-second lightweight refresh: Windows PowerStatus and local GG only.
    /// The expensive Bluetooth device enumeration is never run in this loop.
    /// Phone Link UIA, ADB and optional DualSense input are sampled separately
    /// every five seconds. The OLED framebuffer may still refresh every second.
    /// </summary>
    public static async Task<BatterySnapshot> PollRealtimeAsync(BatterySnapshot previous)
    {
        var now=DateTimeOffset.Now;
        var values=new Dictionary<string,BatteryReading>(previous.Values,
            StringComparer.OrdinalIgnoreCase);
        try
        {
            var p=SystemInformation.PowerStatus;
            if((p.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery)==0 &&
               p.BatteryLifePercent is >=0 and <=1f)
                values["Laptop"]=new BatteryReading("Laptop",
                    (int)Math.Round(p.BatteryLifePercent*100),
                    "Windows PowerStatus",p.PowerLineStatus==PowerLineStatus.Online?
                    "Prize bağlı":"Batarya",now);
        }catch{}

        var gg=await GgBatteryProvider.ReadAsync();
        foreach(string kind in new[]{"Mouse","Headset","SpareBattery"})
        {
            if(gg.TryGetValue(kind,out var current)) values[kind]=current;
            else if(values.TryGetValue(kind,out var old) &&
                    old.Source.StartsWith("SteelSeries GG Engine",StringComparison.Ordinal) &&
                    (now-old.CheckedAt).TotalSeconds>10)
                values[kind]=new BatteryReading(kind,null,
                    "SteelSeries GG (bağlantı yok)","Son onaylı veri 10 saniyeyi aştı.",now);
        }
        return new BatterySnapshot{
            CheckedAt=now,Values=values,DualSenseConnected=previous.DualSenseConnected
        };
    }

    public static BatterySnapshot PollNow()
    {
        var now=DateTimeOffset.Now;
        var values=Kinds.ToDictionary(k=>k,k=>new BatteryReading(k,null,
            k switch {
                "Headset" or "Mouse"=>"SteelSeries / GG (doğrulanmamış)",
                "DualSense"=>"Bluetooth HID (bağlı olduğunda)",
                "Phone"=>"Phone Link (doğrulanmamış)",
                _=>"Windows battery"
            },
            k switch {
                "Headset" or "Mouse"=>"SteelSeries GG değeri için doğrulanmış canlı okuyucu henüz yok.",
                "DualSense"=>"Bağlı değil veya canlı pil raporu alınamadı.",
                "Phone"=>"Phone Link açık değil veya pil göstergesi erişilebilir değil.",
                _=>"Pil bilgisi yok."
            },now),StringComparer.OrdinalIgnoreCase);

        try {
            var p=SystemInformation.PowerStatus;
            if((p.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery)==0
                && p.BatteryLifePercent>=0 && p.BatteryLifePercent<=1.0f)
            {
                int pct=(int)Math.Round(p.BatteryLifePercent*100);
                values["Laptop"]=new("Laptop",pct,"Windows PowerStatus",
                    p.PowerLineStatus==PowerLineStatus.Online?"Prize bağlı":"Batarya",now);
            }
        } catch(Exception ex) {
            values["Laptop"]=new("Laptop",null,"Windows PowerStatus",ex.GetType().Name,now);
        }

        // Windows caches DEVPKEY_Bluetooth_Battery when devices disconnect.
        // This *must not* be presented as a current battery percentage.
        // In particular, on this development PC Bluetooth cached Aerox 30%
        // while GG independently showed 93%; phone cached 40% vs Phone Link 84%.
        // We show the cached value ONLY in diagnostic detail and never on OLED.
        // A real percentage requires a provider with verified connection and freshness.
        foreach(var device in PnpBatteryReader.Read())
        {
            string? kind=Classify(device.Name);
            if(kind is null || kind=="Laptop" || values[kind].Available)continue;
            if(device.Percent is not int cached)continue;
            var original=values[kind];
            string info=kind switch
            {
                "Mouse"=>"Bluetooth'ta eski kayıt: "+cached+
                    "%. Aerox 3 Gen 2 için GG/USB canlı verisi gerekli.",
                "Phone"=>"Bluetooth'ta eski kayıt: "+cached+
                    "%. Telefon Bağlantısı'nın canlı değeri ayrı.",
                "Headset"=>"Bluetooth'ta doğrulanmamış son değer: "+cached+
                    "%. Nova Pro baz istasyonu bağlantısı ayrı.",
                "DualSense"=>"Bluetooth'ta son kayıt: "+cached+
                    "%. Aktif kontrolcü bağlantısı doğrulanmadı.",
                _=>"Windows Bluetooth geçmişi: "+cached+"%."
            };
            values[kind]=new(kind,null,"Bluetooth önbelleği (CANLI DEĞİL)",info,now);
        }
        return new BatterySnapshot { CheckedAt=now,Values=values };
    }

    private static string? Classify(string name)
    {
        var s=name.ToLowerInvariant();
        if(s.Contains("dualsense") || s.Contains("wireless controller"))return "DualSense";
        if(s.Contains("aerox"))return "Mouse";
        if(s.Contains("arctis") || s.Contains("nova pro wireless"))return "Headset";
        if(s.Contains("galaxy")||s.Contains("s24")||s.Contains("samsung")||
           s.Contains("phone link"))return "Phone";
        return null;
    }
}

internal sealed record PnpBattery(string Name,int? Percent);

internal static class PnpBatteryReader
{
    private const uint DIGCF_PRESENT=2,DIGCF_ALLCLASSES=4;
    private static readonly Guid NameGuid=new("a45c254e-df1c-4efd-8020-67d146a850e0");
    private static readonly Guid BatteryGuid=new("104ea319-6ee2-4701-bd47-8ddbf425bbe5");

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA { public uint cbSize;public Guid ClassGuid; public uint DevInst;public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)]
    private struct DEVPROPKEY { public Guid fmtid;public uint pid; public DEVPROPKEY(Guid g,uint p){fmtid=g;pid=p;} }

    [DllImport("setupapi.dll",EntryPoint="SetupDiGetClassDevsW",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid,IntPtr enumerator,IntPtr hwnd,uint flags);
    [DllImport("setupapi.dll",SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr set,uint index,ref SP_DEVINFO_DATA info);
    [DllImport("setupapi.dll",EntryPoint="SetupDiGetDevicePropertyW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceProperty(IntPtr set,ref SP_DEVINFO_DATA info,
        ref DEVPROPKEY key,out uint propertyType,[Out] byte[] bytes,uint size,out uint needed,uint flags);
    [DllImport("setupapi.dll",SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    private static byte[]? ReadProperty(IntPtr set,ref SP_DEVINFO_DATA info,Guid guid,uint pid)
    {
        var key=new DEVPROPKEY(guid,pid);
        var bytes=new byte[512];
        return SetupDiGetDeviceProperty(set,ref info,ref key,out _,bytes,(uint)bytes.Length,out uint needed,0)
            ? bytes[..(int)Math.Min(needed, (uint)bytes.Length)] : null;
    }

    public static IReadOnlyList<PnpBattery> Read()
    {
        var list=new List<PnpBattery>();
        IntPtr set=SetupDiGetClassDevs(IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,DIGCF_PRESENT|DIGCF_ALLCLASSES);
        if(set==new IntPtr(-1))return list;
        try
        {
            for(uint index=0;index<4096;index++)
            {
                var info=new SP_DEVINFO_DATA{cbSize=(uint)Marshal.SizeOf<SP_DEVINFO_DATA>()};
                if(!SetupDiEnumDeviceInfo(set,index,ref info))break;
                var nameBytes=ReadProperty(set,ref info,NameGuid,14)
                    ?? ReadProperty(set,ref info,NameGuid,2);
                if(nameBytes is null||nameBytes.Length<2)continue;
                string name=System.Text.Encoding.Unicode.GetString(nameBytes).TrimEnd('\0');
                if(string.IsNullOrWhiteSpace(name))continue;
                var battery=ReadProperty(set,ref info,BatteryGuid,2);
                int? percent=battery is { Length:>0 } && battery[0]<=100
                    ? battery[0] : null;
                if(percent.HasValue)list.Add(new PnpBattery(name,percent));
            }
        }
        catch { /* Don't break UI when Windows declines to report a property. */ }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return list;
    }
}

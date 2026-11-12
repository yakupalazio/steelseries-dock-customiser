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
    string Detail, DateTimeOffset CheckedAt,bool? Charging=null)
{
    // Explicit source-provided charging state: never infer it from a paired
    // Bluetooth record or a spare-battery mention in another device's detail.
    public bool IsCharging => Charging==true;
    public string FriendlyName {get;init;}="";
    public string IconKind {get;init;}="";
    public string Display => Percent is int p ? $"{p}%" : "--%";
    public string DisplayWithCharge => Display+(IsCharging?" ⚡":"");
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
        // Discover display names on the current user's PC only. The
        // connected-device cache is never written into portable defaults.
        await LocalBatteryDeviceNames.RefreshAsync();
        // Phone Link's accessibility status is available while minimized.
        // Do not display old Bluetooth PnP percentages when UIA is unavailable.
        BatteryReading? phone=null;
        // Automatically prefer the low-overhead Phone Link source. Only
        // query an already-installed, previously authorized ADB connection
        // if the usual source is unavailable. Nothing is installed/prompted.
        // One bounded background recovery for Windows' built-in Phone Link
        // is attempted when the COM server has no accessible battery window.
        phone=await PhoneLinkBatteryProvider.TryReadAsync(true);
        if(phone is null && AdbPhoneBatteryProvider.Installed)
            phone=await AdbPhoneBatteryProvider.ReadAsync();
        if(phone is not null)
            snapshot.Values["Phone"] = phone;
        else
            snapshot.Values["Phone"]=new BatteryReading("Phone",null,
                "Phone Link (erişilemiyor)",
                PhoneLinkBatteryProvider.LastDiagnostic,DateTimeOffset.Now);
        if(!snapshot["DualSense"].Available)
        {
            int? estimated=await DualSenseReadOnly.TryBatteryPercentAsync();
            if(estimated is int value)
                snapshot.Values["DualSense"]=new BatteryReading("DualSense",value,
                    "Bluetooth HID (read-only; approximate 10% bands)",
                    "Deneysel: son alınan HID raporundan yaklaşık seviye.",
                    DateTimeOffset.Now)
                {
                    FriendlyName="DualSense Wireless Controller",
                    IconKind="DualSense"
                };
        }
        // Only live GATT 0x2A19 reads from already-paired devices may
        // populate additional, dynamically discovered battery slots.
        // Extend brand coverage through internal provider adapters.
        // A single failed provider never discards another source's reading.
        var genericReadings=await PeripheralBatteryProviderRegistry.ReadAsync();
        MergeVerifiedBleReadings(snapshot,genericReadings);
        MergeVerifiedHidReadings(snapshot,genericReadings);
        // SetupAPI is useful for unsupported Classic Bluetooth hardware,
        // including checking whether Windows has ever seen a phone battery.
        // Its percentages can be stale while the device is disconnected,
        // so never promote them to a verified number on the OLED.
        ApplySetupApiDiagnostics(snapshot,
            await SetupApiBatteryDiagnostics.ReadAsync(),DateTimeOffset.Now);
        // Query actual Bluetooth connection status only in the slow loop.
        bool connected=await DualSenseConnection.IsConnectedAsync();
        if(connected && !snapshot["DualSense"].Available)
            snapshot.Values["DualSense"]=new BatteryReading(
                "DualSense",null,"Bluetooth HID (bağlı)",
                DualSenseReadOnly.LastDiagnostic,DateTimeOffset.Now)
            {
                FriendlyName="DualSense Wireless Controller",
                IconKind="DualSense"
            };
        return new BatterySnapshot{
            CheckedAt=snapshot.CheckedAt,Values=snapshot.Values,
            DualSenseConnected=connected || snapshot["DualSense"].Available
        };
    }

    /// <summary>
    /// Merge exactly matched known models with their existing provider slot.
    /// Other BLE devices keep their stable identities, so a second generic
    /// mouse or phone is never mistaken for the user's Aerox or Galaxy S24+.
    /// </summary>
    internal static void MergeVerifiedBleReadings(BatterySnapshot snapshot,
        IReadOnlyList<BatteryReading> discovered)
    {
        foreach(var value in discovered)
        {
            if(!value.Available || !value.Kind.StartsWith("Ble:",
                StringComparison.OrdinalIgnoreCase))continue;
            string name=value.FriendlyName.ToLowerInvariant();
            string? known=name.Contains("aerox 3")?"Mouse":
                name.Contains("nova pro wireless")?"Headset":
                name.Contains("galaxy s24")?"Phone":
                name.Contains("dualsense")?"DualSense":null;
            if(known is not null)
            {
                if(!snapshot[known].Available)
                    snapshot.Values[known]=value with {Kind=known};
                // Never append another OLED slot for a known GG/Phone model.
                continue;
            }
            snapshot.Values[value.Kind]=value;
        }
    }

    /// <summary>
    /// Generic USB/Bluetooth HID usage 0x06/0x20 supports more brands
    /// without asking users to write a custom model-specific reader.
    /// The vendor-agnostic report is valid only during this actual read.
    /// </summary>
    internal static void MergeVerifiedHidReadings(BatterySnapshot snapshot,
        IReadOnlyList<BatteryReading> discovered)
    {
        foreach(var reading in discovered)
        {
            if(!reading.Available ||
               !reading.Kind.StartsWith("Hid:",StringComparison.OrdinalIgnoreCase))
                continue;
            string name=reading.FriendlyName.ToLowerInvariant();
            string? known=name.Contains("aerox 3")?"Mouse":
                name.Contains("arctis nova pro")?"Headset":
                name.Contains("galaxy s24")?"Phone":
                name.Contains("dualsense")?"DualSense":null;
            if(known is not null)
            {
                if(!snapshot[known].Available)
                    snapshot.Values[known]=reading with {Kind=known};
                continue;
            }
            // If the same exact model is already available over GATT,
            // prefer the existing one instead of duplicating OLED slots.
            if(snapshot.Values.Values.Any(old=>
                old.Available &&
                old.FriendlyName.Length>2 &&
                string.Equals(old.FriendlyName,reading.FriendlyName,
                    StringComparison.OrdinalIgnoreCase)))
                continue;
            snapshot.Values[reading.Kind]=reading;
        }
    }

    /// <summary>
    /// Retain cached Windows information as diagnostics only. An active GATT,
    /// GG, ADB, Phone Link or HID percentage always takes priority. Merely
    /// observing a Bluetooth pairing never proves the value is fresh.
    /// </summary>
    internal static void ApplySetupApiDiagnostics(BatterySnapshot snapshot,
        IReadOnlyList<PnpBattery> records,DateTimeOffset now)
    {
        foreach(var cached in records)
        {
            string? kind=Classify(cached.Name);
            if(kind is null || snapshot[kind].Available ||
               snapshot[kind].Source.StartsWith("Windows SetupAPI",
                   StringComparison.Ordinal))continue;
            if(cached.Percent is not int lastKnown || lastKnown is <0 or >100)
                continue;
            snapshot.Values[kind]=new BatteryReading(kind,null,
                "Windows SetupAPI (yalnızca tanılama)",
                cached.Name+": önbellekte %"+lastKnown+
                "; bağlantı/güncellik doğrulanamadı.",now);
        }
    }

    /// <summary>
    /// Per-second lightweight refresh: Windows PowerStatus and local GG only.
    /// The expensive Bluetooth device enumeration is never run in this loop.
    /// Phone Link UIA, ADB and optional DualSense input are sampled separately
    /// every ten seconds. The OLED framebuffer may still refresh every second.
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
                    "Prize bağlı":"Batarya",now,
                    p.PowerLineStatus==PowerLineStatus.Online &&
                    (p.BatteryChargeStatus&BatteryChargeStatus.Charging)!=0);
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
                    p.PowerLineStatus==PowerLineStatus.Online?"Prize bağlı":"Batarya",now,
                    p.PowerLineStatus==PowerLineStatus.Online &&
                    (p.BatteryChargeStatus&BatteryChargeStatus.Charging)!=0);
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
                    "%. Güncel fare pil seviyesi için canlı bir okuyucu gerekiyor.",
                "Phone"=>"Bluetooth'ta eski kayıt: "+cached+
                    "%. Telefon Bağlantısı'nın canlı değeri ayrı.",
                "Headset"=>"Bluetooth'ta doğrulanmamış son değer: "+cached+
                    "%. Güncel kulaklık pili için uygun canlı kaynak gerekiyor.",
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

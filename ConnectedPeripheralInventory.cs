using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HidSharp;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace SteelseriesDockCustomiser;

/// <summary>
/// One row per discovered peripheral, including devices whose manufacturer
/// does NOT offer a readable battery protocol. No pairing, HID report writes,
/// or drivers are installed. Discovery never invents a battery percentage.
/// </summary>
internal sealed record ConnectedPeripheral(
    string Id,string Name,string Connection,string Manufacturer,
    string IconKind,int? Percent,string BatterySource,string Detail,
    string? BatteryKind)
{
    public string BatteryDisplay=>Percent is int n?$"{n}%":"--%";
    // Optional, public USB vendor/product code only. No USB serial/path,
    // Bluetooth MAC/address or per-user instance ID is copied into reports.
    public string HardwareSummary {get;init;}="";
}

internal static class ConnectedPeripheralInventory
{
    private static readonly string[] BtProperties=[
        "System.Devices.Aep.ContainerId",
        "System.Devices.Aep.IsConnected",
        "System.Devices.Aep.IsPresent",
        "System.Devices.Aep.Manufacturer"
    ];
    private static readonly string[] UsbProperties=[
        "System.Devices.ContainerId",
        "System.Devices.DeviceManufacturer",
        "System.Devices.Present"
    ];

    private sealed record Candidate(string Identity,string Name,
        string Connection,string Manufacturer,string HardwareId="");

    public static async Task<IReadOnlyList<ConnectedPeripheral>> DiscoverAsync(
        BatterySnapshot battery)
    {
        var candidates=new List<Candidate>();
        try
        {
            var classic=await DeviceInformation.FindAllAsync(
                BluetoothDevice.GetDeviceSelectorFromConnectionStatus(
                    BluetoothConnectionStatus.Connected),BtProperties);
            AddBluetooth(candidates,classic,"Bluetooth");
        }
        catch{/* Unsupported adapter is not a disconnected device. */}
        try
        {
            var ble=await DeviceInformation.FindAllAsync(
                BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(
                    BluetoothConnectionStatus.Connected),BtProperties);
            AddBluetooth(candidates,ble,"Bluetooth LE");
        }
        catch{/* BLE adapter unavailable */}
        var descriptiveUsbNames=new Dictionary<string,string>(
            StringComparer.OrdinalIgnoreCase);
        try
        {
            // Many USB receivers identify their physical node merely as
            // "USB Composite Device". HID metadata can reveal the actual
            // Logitech/ASUS/ATK model without opening its interface.
            foreach(var hid in DeviceList.Local.GetHidDevices().Take(240))
            {
                try
                {
                    string name=hid.GetProductName();
                    if(string.IsNullOrWhiteSpace(name))continue;
                    string hardware=$"{hid.VendorID:X4}:{hid.ProductID:X4}";
                    if(!descriptiveUsbNames.TryGetValue(hardware,out var old)||
                       NameScore(name)>NameScore(old))
                        descriptiveUsbNames[hardware]=name;
                }
                catch{}
            }
        }
        catch{}
        try
        {
            // DeviceKind.Device represents physical PnP nodes, not all the
            // logical HID interfaces belonging to a composite USB dongle.
            var all=await DeviceInformation.FindAllAsync(
                "",UsbProperties,DeviceInformationKind.Device);
            foreach(var device in all)
            {
                if(!device.Id.StartsWith("USB\\VID_",StringComparison.OrdinalIgnoreCase))
                    continue;
                if(device.Properties.TryGetValue(
                    "System.Devices.Present",out var present) &&
                    present is bool available && !available)continue;
                string name=string.IsNullOrWhiteSpace(device.Name)
                    ?"USB peripheral":device.Name;
                // Recover a useful model name for composite receivers.
                if(NameScore(name)<0)
                {
                    var matched=System.Text.RegularExpressions.Regex.Match(
                        device.Id,@"VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if(matched.Success && descriptiveUsbNames.TryGetValue(
                        matched.Groups[1].Value+":"+matched.Groups[2].Value,
                        out var descriptive) && NameScore(descriptive)>NameScore(name))
                        name=descriptive;
                }
                string manufacturer=Property(device,"System.Devices.DeviceManufacturer");
                string identity=ContainerKey(device,"System.Devices.ContainerId");
                candidates.Add(new Candidate(identity,name,"USB / Dock",
                    manufacturer,device.Id));
            }
        }
        catch{/* The OS may restrict device-node enumeration. */}
        if(!candidates.Any(d=>d.Connection.Contains("USB",StringComparison.Ordinal)))
        {
            // Fallback for WinRT Device-kind permissions: locally enumerated
            // USB HID interfaces, without opening or reading any report.
            try
            {
                foreach(var hid in DeviceList.Local.GetHidDevices().Take(240))
                {
                    if(!hid.DevicePath.Contains("vid_",StringComparison.OrdinalIgnoreCase) ||
                       hid.DevicePath.Contains("bthenum",StringComparison.OrdinalIgnoreCase))
                        continue;
                    string name;
                    string maker;
                    try{name=hid.GetProductName();}
                    catch{name=$"HID {hid.VendorID:X4}:{hid.ProductID:X4}";}
                    // No driver IPC required: USB vendor ID is enough
                    // when the OS has no friendly manufacturer property.
                    maker=$"USB VID {hid.VendorID:X4}";
                    candidates.Add(new Candidate("usb-hid:"+
                        hid.VendorID.ToString("X4")+":"+
                        hid.ProductID.ToString("X4")+":"+
                        Hash(hid.DevicePath),name,"USB / Dock",maker,
                        hid.DevicePath));
                }
            }
            catch{/* Not all USB devices are HID devices. */}
        }
        return MergeAndAttachBatteries(candidates,battery);
    }

    private static void AddBluetooth(List<Candidate> output,
        IEnumerable<DeviceInformation> devices,string connection)
    {
        foreach(var device in devices.Take(100))
        {
            if(device.Properties.TryGetValue(
                "System.Devices.Aep.IsConnected",out var connected) &&
                connected is bool active && !active)continue;
            if(string.IsNullOrWhiteSpace(device.Name))continue;
            output.Add(new Candidate(ContainerKey(device,
                    "System.Devices.Aep.ContainerId"),
                device.Name,connection,
                Property(device,"System.Devices.Aep.Manufacturer"),device.Id));
        }
    }

    private static string ContainerKey(DeviceInformation info,string property)
    {
        if(info.Properties.TryGetValue(property,out var value) &&
           Guid.TryParse(value?.ToString(),out var guid) && guid!=Guid.Empty)
            return "container:"+guid.ToString("D");
        return "device:"+Hash(info.Id);
    }

    private static string Property(DeviceInformation device,string key)=>
        device.Properties.TryGetValue(key,out var value)
            ?value?.ToString()??"":"";

    private static string Hash(string text)=>Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(text.ToLowerInvariant())),0,7);

    private static int NameScore(string name)
    {
        string lower=name.ToLowerInvariant();
        int score=name.Length;
        if(lower.Contains("composite")||lower.Contains("generic")||
           lower.Contains("usb input")||lower.Contains("hid-compliant"))
            score-=80;
        return score;
    }

    private static string Normalize(string name)=>new string(name.ToLowerInvariant()
        .Where(char.IsLetterOrDigit).ToArray());

    private static string? RecognizedKind(string name)
    {
        string s=name.ToLowerInvariant();
        if(s.Contains("dualsense") ||
           s.Contains("sony interactive entertainment wireless controller"))
            return "DualSense";
        if(s.Contains("aerox 3"))return "Mouse";
        if(s.Contains("arctis nova pro"))return "Headset";
        if(s.Contains("galaxy s24")||s.Contains("s24+") ||
           s.Contains("s24 plus"))return "Phone";
        return null;
    }

    internal static IReadOnlyList<ConnectedPeripheral> MergeAndAttachBatteries(
        IEnumerable<(string Id,string Name,string Connection,
            string Manufacturer)> source,BatterySnapshot snapshot)
        => MergeAndAttachBatteries(source.Select(c=>
            new Candidate(c.Id,c.Name,c.Connection,c.Manufacturer)),snapshot);

    private static IReadOnlyList<ConnectedPeripheral> MergeAndAttachBatteries(
        IEnumerable<Candidate> candidates,BatterySnapshot snapshot)
    {
        var rows=new List<ConnectedPeripheral>();
        var candidatesArray=candidates.Take(250).ToArray();
        // Matching a phone's battery to a different phone is worse than
        // displaying unknown. Only one connected phone may receive the
        // Phone Link / ADB battery reading without explicit pairing.
        int phoneCount=candidatesArray
            .Where(c=>LocalBatteryDeviceNames.ClassifyConnectedBluetooth(c.Name)
                =="Phone")
            .Select(c=>c.Identity).Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var now=DateTimeOffset.Now;
        var usable=snapshot.Values.Values.Where(r=>BatteryPriority.IsCurrent(r,now))
            .ToArray();
        foreach(var group in candidatesArray.GroupBy(x=>x.Identity,
            StringComparer.OrdinalIgnoreCase))
        {
            var selected=group.OrderByDescending(x=>NameScore(x.Name)).First();
            string name=selected.Name;
            string connection=string.Join(" + ",group.Select(x=>x.Connection)
                .Distinct(StringComparer.OrdinalIgnoreCase));
            string maker=group.Select(x=>x.Manufacturer).FirstOrDefault(
                x=>!string.IsNullOrWhiteSpace(x))??"";
            var recognized=WirelessCompatibilityCatalog.Recognize(
                name,maker,connection);
            if(recognized is null)
            {
                var identity=group.Select(x=>
                    WirelessCompatibilityCatalog.ReadHardwareId(x.HardwareId))
                    .FirstOrDefault(x=>x.HasValue);
                if(identity.HasValue)
                    recognized=WirelessCompatibilityCatalog.RecognizeUsbIdentity(
                        identity.Value.Vid,identity.Value.Pid);
            }
            string? kind=RecognizedKind(name);
            if(LocalBatteryDeviceNames.ClassifyConnectedBluetooth(name)=="Phone")
                kind=phoneCount==1?"Phone":null;
            BatteryReading? reading=kind is not null
                ?usable.FirstOrDefault(r=>r.Kind==kind)
                :null;
            if(reading is null)
            {
                string normalized=Normalize(name);
                var same=usable.Where(r=>r.FriendlyName.Length>2 &&
                    Normalize(r.FriendlyName)==normalized).ToArray();
                if(same.Length==1)
                {
                    reading=same[0];
                    kind=reading.Kind;
                }
            }
            string icon=BatteryDeviceClassifier.IconFor(name,
                connection.Contains("USB",StringComparison.OrdinalIgnoreCase)
                    ?"USB":"Bluetooth");
            if(recognized is not null&&recognized.Category is
                "Mouse" or "Keyboard" or "Headset" or "Controller")
                icon=recognized.Category=="Controller"
                    ?"DualSense":recognized.Category;
            // Only a truly model-specific, documented receiver ID can
            // upgrade the generic "dongle" device name to a real model.
            // A shared ATK/VXE/Logitech dongle still stays brand-only.
            if(recognized is {ExactModel:true,
                Confidence:"documented-receiver"}&&
               new[]{"receiver","dongle","nearlink","nano alıcı"}
                   .Any(word=>name.Contains(word,
                       StringComparison.OrdinalIgnoreCase)))
                name=recognized.Name+(UiLanguage.English
                    ?" (receiver)":" (alıcı)");
            rows.Add(new ConnectedPeripheral(group.Key,name,connection,maker,
                reading?.IconKind?.Length>0?reading.IconKind:icon,
                reading?.Percent,
                reading?.Source??(recognized is null
                    ?"Pil bilgisi okunamadı":"Kablosuz cihaz kütüphanesi"),
                reading?.Detail??(recognized is null
                    ?"Bağlı; güncel pil yüzdesi sağlanmıyor. "+
                     "Cihaz pilli olmayabilir veya üreticiye özel protokol gerektirebilir."
                    :recognized.ExactModel
                        ?"Tanınan model: "+recognized.Name+
                         ". Canlı pil okuyucusu doğrulanmadı; yüzde gösterilmiyor."
                        :"Tanınan marka: "+recognized.Brand+
                         ". Model ve pil protokolü henüz doğrulanmadı."),
                kind)
            {
                HardwareSummary=group.Select(item=>
                        WirelessCompatibilityCatalog.ReadHardwareId(
                            item.HardwareId))
                    .Where(pair=>pair.HasValue)
                    .Select(pair=>$"VID_{pair!.Value.Vid:X4} PID_{pair.Value.Pid:X4}")
                    .FirstOrDefault()??""
            });
        }
        return rows.OrderByDescending(r=>r.Percent.HasValue)
            .ThenBy(r=>r.Connection,StringComparer.OrdinalIgnoreCase)
            .ThenBy(r=>r.Name,StringComparer.OrdinalIgnoreCase).ToArray();
    }
}

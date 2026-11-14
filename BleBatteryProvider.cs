using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Uncached, *read-only* Bluetooth LE Battery Service (180F) / Battery Level
/// (2A19) provider. No pairing, CCCD writes, device configuration or
/// firmware operations. Only paired devices with successful live reads are
/// eligible to appear in the battery widgets.
/// </summary>
internal static class BleBatteryProvider
{
    private static readonly Guid BatteryService=
        BluetoothUuidHelper.FromShortId(0x180F);
    private static readonly Guid BatteryLevel=
        BluetoothUuidHelper.FromShortId(0x2A19);
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static readonly object WatchLock=new();
    private static readonly List<DeviceWatcher> Watchers=[];
    public static event Action? DevicesChanged;

    public static string StableKind(string id)
    {
        byte[] hash=SHA256.HashData(Encoding.UTF8.GetBytes(id));
        return "Ble:"+Convert.ToHexString(hash,0,7);
    }

    public static void StartWatching()
    {
        lock(WatchLock)
        {
            if(Watchers.Count>0)return;
            // Observe both exposed GATT Battery services and connected LE
            // devices. Windows may not cache the BAS service until connection.
            string[] selectors=[
                GattDeviceService.GetDeviceSelectorFromUuid(BatteryService),
                BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(
                    BluetoothConnectionStatus.Connected)
            ];
            foreach(string selector in selectors)
            {
                try
                {
                    var watcher=DeviceInformation.CreateWatcher(selector);
                    watcher.Added+=(_,_)=>DevicesChanged?.Invoke();
                    watcher.Updated+=(_,_)=>DevicesChanged?.Invoke();
                    watcher.Removed+=(_,_)=>DevicesChanged?.Invoke();
                    watcher.Start();
                    Watchers.Add(watcher);
                }
                catch {/* Optional second watcher may be unavailable. */}
            }
        }
    }

    public static void StopWatching()
    {
        lock(WatchLock)
        {
            foreach(var watcher in Watchers)
                try{watcher.Stop();}catch{}
            Watchers.Clear();
        }
    }

    private static BatteryReading? ParseLiveLevel(string id,string? name,
        byte[] data)
    {
        if(data.Length!=1||data[0]>100||id.Length==0)return null;
        string title=string.IsNullOrWhiteSpace(name)
            ?"Bluetooth device":name.Trim();
        string kind=StableKind(id);
        string icon=BatteryDeviceClassifier.IconFor(title,"Bluetooth");
        BatteryDeviceCatalog.Register(kind,title,icon);
        return new BatteryReading(kind,data[0],
            "Bluetooth LE GATT BAS (uncached 0x180F/0x2A19)",
            "Live read, confirmed paired device",DateTimeOffset.Now)
        {
            FriendlyName=title,IconKind=icon
        };
    }

    internal static bool IsValidBatteryByte(byte[]? data)=>
        data is {Length:1}&&data[0]<=100;

    // Both WinRT access routes to a BAS instance must produce exactly
    // the same local key. Distinct identical-named devices (and two BAS
    // instances on one peripheral) must never be merged merely by name
    // and coincidentally equal battery percent.
    private static string ServiceIdentity(GattDeviceService service)=>
        service.DeviceId+"/battery/"+service.AttributeHandle;

    private static async Task<BatteryReading?> ReadCharacteristic(
        GattDeviceService service,string identity,string? deviceName)
    {
        try
        {
            var chars=await service.GetCharacteristicsForUuidAsync(
                BatteryLevel,BluetoothCacheMode.Uncached);
            if(chars.Status!=GattCommunicationStatus.Success)return null;
            foreach(var ch in chars.Characteristics.Take(1))
            {
                if(!ch.CharacteristicProperties.HasFlag(
                    GattCharacteristicProperties.Read))continue;
                var value=await ch.ReadValueAsync(BluetoothCacheMode.Uncached);
                if(value.Status!=GattCommunicationStatus.Success||
                   value.Value is null||value.Value.Length!=1)
                    return null;
                var reader=DataReader.FromBuffer(value.Value);
                byte percent=reader.ReadByte();
                return ParseLiveLevel(identity,deviceName,[percent]);
            }
        }
        catch{/* Pairing/connection failure: never use old cached percent. */}
        return null;
    }

    public static async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        if(!await Gate.WaitAsync(0).ConfigureAwait(true))
            return Array.Empty<BatteryReading>();
        try
        {
            var list=new List<BatteryReading>();

            // First pass: recognized, already-registered Battery Service(s).
            // Supports a device publishing multiple BAS instances.
            try
            {
                var registered=await DeviceInformation.FindAllAsync(
                    GattDeviceService.GetDeviceSelectorFromUuid(BatteryService));
                foreach(var info in registered.Take(32))
                {
                    if(!info.IsEnabled||!info.Pairing.IsPaired)continue;
                    try
                    {
                        using var service=await GattDeviceService.FromIdAsync(
                            info.Id);
                        if(service is null)continue;
                        string name=info.Name;
                        try
                        {
                            if(!string.IsNullOrWhiteSpace(service.Device?.Name))
                                name=service.Device.Name;
                        }
                        catch{}
                        var reading=await ReadCharacteristic(service,
                            ServiceIdentity(service),name);
                        if(reading is not null)list.Add(reading);
                    }
                    catch{/* Some services are inaccessible without user consent. */}
                }
            }
            catch{/* Service selector may be missing on this Windows build. */}

            // Second pass: already-connected, already-paired LE device.
            // On some Windows versions the OS does not expose BAS as a
            // service DeviceInformation node until the paired peripheral
            // is connected; ask that LE device directly for 0x180F.
            try
            {
                var connected=await DeviceInformation.FindAllAsync(
                    BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(
                        BluetoothConnectionStatus.Connected));
                foreach(var info in connected.Take(32))
                {
                    if(!info.IsEnabled||!info.Pairing.IsPaired)continue;
                    try
                    {
                        using var device=await BluetoothLEDevice.FromIdAsync(info.Id);
                        if(device is null ||
                           device.ConnectionStatus!=BluetoothConnectionStatus.Connected)
                            continue;
                        var services=await device.GetGattServicesForUuidAsync(
                            BatteryService,BluetoothCacheMode.Uncached);
                        if(services.Status!=GattCommunicationStatus.Success)
                            continue;
                        foreach(var service in services.Services.Take(5))
                        {
                            using(service)
                            {
                                var reading=await ReadCharacteristic(service,
                                    ServiceIdentity(service),device.Name);
                                if(reading is not null &&
                                   !list.Any(existing=>
                                       string.Equals(existing.Kind,reading.Kind,
                                           StringComparison.OrdinalIgnoreCase)))
                                    list.Add(reading);
                            }
                        }
                    }
                    catch{/* No paired or untrusted device is ever written to. */}
                }
            }
            catch{/* BLE adapter does not support this selector. */}
            return list;
        }
        catch{return Array.Empty<BatteryReading>();}
        finally{Gate.Release();}
    }
}

internal static class BatteryDeviceClassifier
{
    public static string IconFor(string deviceName,string transport)
    {
        string s=(deviceName??"").ToLowerInvariant();
        if(s.Contains("keyboard")||s.Contains("klavye")||s.Contains("keychron")||
           s.Contains("logitech mx keys"))return "Keyboard";
        if(s.Contains("speaker")||s.Contains("hoparl")||s.Contains("soundcore")||
           s.Contains("jbl flip")||s.Contains("jbl charge"))return "Speaker";
        if(s.Contains("buds")||s.Contains("tws")||s.Contains("airpod")||
           s.Contains("earbud")||s.Contains("kulak içi")||s.Contains("freebuds"))
            return "Earbuds";
        if(LocalBatteryDeviceNames.ClassifyConnectedBluetooth(deviceName??"")
               =="Phone" ||
           s.Contains("galaxy s")||s.Contains("galaxy a")||
           s.Contains("iphone")||s.Contains("pixel phone")||
           s.Contains("smartphone")||s.Contains("android phone")||
           s.Contains("telefon"))return "Phone";
        if(s.Contains("dualsense")||s.Contains("dualshock")||
           s.Contains("controller")||s.Contains("gamepad")||
           s.Contains("xbox wireless"))return "DualSense";
        if(s.Contains("mouse")||s.Contains("fare"))return "Mouse";
        if(s.Contains("headset")||s.Contains("headphone")||
           s.Contains("arctis")||s.Contains("kulaklık"))return "Headset";
        return transport.ToLowerInvariant() switch{
            "usb"=>"Usb",
            "wifi" or "wireless"=>"Wireless",
            _=>"Bluetooth"
        };
    }
}

internal static class BatteryDeviceCatalog
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,
        (string Name,string Icon)> Catalog=new(StringComparer.OrdinalIgnoreCase);
    public static void Register(string kind,string name,string icon)
        =>Catalog[kind]=(name,icon);
    public static string Name(string kind)
        =>Catalog.TryGetValue(kind,out var d)?d.Name:kind;
    public static string Icon(string kind)
        =>Catalog.TryGetValue(kind,out var d)?d.Icon:"Bluetooth";
}

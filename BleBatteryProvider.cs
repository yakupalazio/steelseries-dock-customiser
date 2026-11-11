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
    private static readonly Guid BatteryService=BluetoothUuidHelper.FromShortId(0x180F);
    private static readonly Guid BatteryLevel=BluetoothUuidHelper.FromShortId(0x2A19);
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static readonly object WatchLock=new();
    private static DeviceWatcher? watcher;
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
            if(watcher!=null)return;
            try
            {
                var created=DeviceInformation.CreateWatcher(
                    GattDeviceService.GetDeviceSelectorFromUuid(BatteryService));
                created.Added+=(_,_)=>DevicesChanged?.Invoke();
                created.Updated+=(_,_)=>DevicesChanged?.Invoke();
                created.Removed+=(_,_)=>DevicesChanged?.Invoke();
                created.Start();
                watcher=created;
            }
            catch { watcher?.Stop();watcher=null; }
        }
    }
    public static void StopWatching()
    {
        lock(WatchLock)
        {
            if(watcher is null)return;
            try{watcher.Stop();}catch{}
            watcher=null;
        }
    }

    public static async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        if(!await Gate.WaitAsync(0).ConfigureAwait(true))
            return Array.Empty<BatteryReading>();
        try
        {
            var list=new List<BatteryReading>();
            var infos=await DeviceInformation.FindAllAsync(
                GattDeviceService.GetDeviceSelectorFromUuid(BatteryService));
            foreach(var info in infos.Take(12))
            {
                if(!info.IsEnabled || !info.Pairing.IsPaired)continue;
                try
                {
                    // An OS-managed GATT read is initiated only after pairing
                    // and a prior device-service discovery result.
                    using var service=await GattDeviceService.FromIdAsync(info.Id);
                    if(service is null)continue;
                    var chars=await service.GetCharacteristicsForUuidAsync(
                        BatteryLevel,BluetoothCacheMode.Uncached);
                    if(chars.Status!=GattCommunicationStatus.Success)continue;
                    foreach(var ch in chars.Characteristics.Take(1))
                    {
                        if(!ch.CharacteristicProperties.HasFlag(
                            GattCharacteristicProperties.Read))continue;
                        var value=await ch.ReadValueAsync(BluetoothCacheMode.Uncached);
                        if(value.Status!=GattCommunicationStatus.Success ||
                           value.Value is null || value.Value.Length!=1)continue;
                        var reader=DataReader.FromBuffer(value.Value);
                        byte percentage=reader.ReadByte();
                        if(percentage>100)continue;
                        string name=info.Name;
                        try
                        {
                            // Preferred display name is the device, not the
                            // generic "Battery Service" service instance.
                            string? parent=service.Device?.Name;
                            if(!string.IsNullOrWhiteSpace(parent))name=parent;
                        }
                        catch { }
                        if(string.IsNullOrWhiteSpace(name))name="Bluetooth device";
                        string kind=StableKind(info.Id);
                        string icon=BatteryDeviceClassifier.IconFor(name,"Bluetooth");
                        BatteryDeviceCatalog.Register(kind,name,icon);
                        list.Add(new BatteryReading(kind,percentage,
                            "Bluetooth LE GATT (uncached; 0x180F/0x2A19)",
                            name,DateTimeOffset.Now)
                        {
                            FriendlyName=name,
                            IconKind=icon
                        });
                    }
                }
                catch { /* No stale/derived reading is eligible. */ }
            }
            return list;
        }
        catch { return Array.Empty<BatteryReading>(); }
        finally { Gate.Release(); }
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

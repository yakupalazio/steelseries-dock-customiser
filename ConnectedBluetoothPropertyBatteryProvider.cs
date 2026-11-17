using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Extra Windows battery source for connected Bluetooth accessories whose
/// percentage is exposed on their WinRT device properties rather than their
/// Bluetooth PnP node or GATT 0x180F service (some HFP earbuds and headsets).
/// Never treats an unpaired/disconnected device or a cached registry value
/// as a live level. The OS value may itself update with a small delay.
/// </summary>
internal sealed class ConnectedBluetoothPropertyBatteryProvider:
    IPeripheralBatteryProvider
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static readonly string[] Properties=[
        "System.Devices.BatteryLifePercent",
        "System.Devices.BatteryLife"
    ];
    public string Protocol=>"Connected Windows Bluetooth device battery properties";

    internal static int? ParsePercent(object? value) => value switch
    {
        byte n when n<=100 =>n,
        sbyte n when n is >=0 and <=100=>n,
        short n when n is >=0 and <=100=>n,
        ushort n when n<=100=>n,
        int n when n is >=0 and <=100=>n,
        uint n when n<=100=>checked((int)n),
        long n when n is >=0 and <=100=>checked((int)n),
        string n when int.TryParse(n,out int parsed)&&parsed is >=0 and <=100=>parsed,
        _=>null
    };

    internal static int? ReadProperty(IReadOnlyDictionary<string,object> values)
    {
        foreach(string key in Properties)
            if(values.TryGetValue(key,out object? value) &&
               ParsePercent(value) is int percent)return percent;
        return null;
    }

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        if(!await Gate.WaitAsync(0))return Array.Empty<BatteryReading>();
        try{return await ReadConnectedAsync();}
        catch{return Array.Empty<BatteryReading>();}
        finally{Gate.Release();}
    }

    private static async Task<IReadOnlyList<BatteryReading>> ReadConnectedAsync()
    {
        var rows=new List<BatteryReading>();
        async Task Scan(string selector,bool classic)
        {
            var all=await DeviceInformation.FindAllAsync(selector,Properties);
            foreach(var info in all.Take(36))
            {
                try
                {
                    if(!info.IsEnabled||!info.Pairing.IsPaired)continue;
                    int? level=ReadProperty(info.Properties);
                    if(level is null)continue;
                    ulong address;
                    string name;
                    if(classic)
                    {
                        using var device=await BluetoothDevice.FromIdAsync(info.Id);
                        if(device is null||device.ConnectionStatus!=
                           BluetoothConnectionStatus.Connected)continue;
                        address=device.BluetoothAddress;
                        name=device.Name;
                    }
                    else
                    {
                        using var device=await BluetoothLEDevice.FromIdAsync(info.Id);
                        if(device is null||device.ConnectionStatus!=
                           BluetoothConnectionStatus.Connected)continue;
                        address=device.BluetoothAddress;
                        name=device.Name;
                    }
                    string? normalized=
                        WindowsConnectedBluetoothBatteryProvider
                            .NormalizeBluetoothAddress(address.ToString("X12"));
                    if(normalized is null)continue;
                    string kind=WindowsConnectedBluetoothBatteryProvider
                        .StableKind(normalized);
                    string label=string.IsNullOrWhiteSpace(name)
                        ?info.Name:name;
                    string icon=BatteryDeviceClassifier.IconFor(label,"Bluetooth");
                    BatteryDeviceCatalog.Register(kind,label,icon);
                    rows.Add(new BatteryReading(kind,level.Value,
                        "Windows Bluetooth (connected device property)",
                        "Bağlı cihazın güncel Windows pil özelliği; işletim "+
                        "sistemi göstergesi gecikmeli güncellenebilir.",
                        DateTimeOffset.Now)
                    {FriendlyName=label,IconKind=icon});
                }
                catch{/* Device can disconnect while WinRT is enumerating. */}
            }
        }
        try
        {
            await Scan(BluetoothDevice
                .GetDeviceSelectorFromConnectionStatus(
                    BluetoothConnectionStatus.Connected),true);
        }catch{/* BR/EDR selector unavailable on this Windows build. */}
        try
        {
            await Scan(BluetoothLEDevice
                .GetDeviceSelectorFromConnectionStatus(
                    BluetoothConnectionStatus.Connected),false);
        }catch{/* LE selector unavailable on this Windows build. */}
        return rows.DistinctBy(x=>x.Kind,StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

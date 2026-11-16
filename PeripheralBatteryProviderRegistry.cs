using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Internal, zero-external-app battery reader contract. Adding a supported
/// vendor protocol in a later release requires one self-contained adapter,
/// not asking end users to write code for their devices.
/// </summary>
internal interface IPeripheralBatteryProvider
{
    string Protocol {get;}
    Task<IReadOnlyList<BatteryReading>> ReadAsync();
}

internal static class PeripheralBatteryProviderRegistry
{
    private sealed class BleGatt : IPeripheralBatteryProvider
    {
        public string Protocol=>"Bluetooth LE GATT 0x180F";
        public Task<IReadOnlyList<BatteryReading>> ReadAsync()
            =>BleBatteryProvider.ReadAsync();
    }
    private sealed class WindowsBluetooth : IPeripheralBatteryProvider
    {
        public string Protocol=>"Connected Windows Bluetooth HFP / PnP";
        public Task<IReadOnlyList<BatteryReading>> ReadAsync()=>
            WindowsConnectedBluetoothBatteryProvider.ReadAsync();
    }

    private sealed class StandardHid : IPeripheralBatteryProvider
    {
        public string Protocol=>"USB/Bluetooth HID Battery Strength";
        public Task<IReadOnlyList<BatteryReading>> ReadAsync()
            =>StandardHidBatteryProvider.ReadAsync();
    }

    // Standard readers need no vendor app. Official iCUE is optional:
    // a separately provided Corsair x64 SDK library is used only if present.
    // Neither a vendor application nor SDK is installed/started by us.
    private static readonly IPeripheralBatteryProvider[] Providers=[
        new BleGatt(),new WindowsBluetooth(),new StandardHid(),
        new WindowsGameControllerBatteryProvider(),
        new CorsairIcueBatteryProvider(),new CompxWirelessBatteryProvider(),
        new WlmouseBatteryProvider()
    ];

    private static async Task<IReadOnlyList<BatteryReading>> ReadBounded(
        IPeripheralBatteryProvider provider)
    {
        try
        {
            // WinRT BLE discovery can stall on a sleeping paired device.
            // Do not let that block independent USB HID and vendor SDK reads.
            var operation=provider.ReadAsync();
            if(await Task.WhenAny(operation,Task.Delay(5000))!=operation)
            {
                _=operation.ContinueWith(t=>{_ =t.Exception;},
                    TaskContinuationOptions.OnlyOnFaulted);
                return Array.Empty<BatteryReading>();
            }
            return await operation;
        }
        catch{return Array.Empty<BatteryReading>();}
    }

    public static async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        // Providers operate read-only and are isolated. BLE has its own
        // exclusive gate, so timeout does not create overlapping GATT scans.
        var tasks=Providers.Select(ReadBounded).ToArray();
        var batches=await Task.WhenAll(tasks);
        return batches.SelectMany(batch=>batch)
            .Where(reading=>reading.Available)
            .DistinctBy(reading=>reading.Kind,StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

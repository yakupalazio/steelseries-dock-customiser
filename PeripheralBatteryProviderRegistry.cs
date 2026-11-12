using System;
using System.Collections.Generic;
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
    private sealed class StandardHid : IPeripheralBatteryProvider
    {
        public string Protocol=>"USB/Bluetooth HID Battery Strength";
        public Task<IReadOnlyList<BatteryReading>> ReadAsync()
            =>StandardHidBatteryProvider.ReadAsync();
    }

    // Internal list; no third-party daemon, BQC, vendor program, installer
    // or downloaded executable is required by the common battery readers.
    private static readonly IPeripheralBatteryProvider[] Providers=[
        new BleGatt(),new StandardHid()
    ];

    public static async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        var combined=new List<BatteryReading>();
        foreach(var reader in Providers)
        {
            try
            {
                // A failure in one Windows device API must not hide an
                // already-verified reading from another independent API.
                combined.AddRange(await reader.ReadAsync());
            }
            catch{/* Other providers are still eligible. */}
        }
        return combined;
    }
}

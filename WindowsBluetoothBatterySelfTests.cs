using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Classic Bluetooth correctness and freshness tests without PnP/Bluetooth
/// hardware reads, registry writes or network requests.
/// </summary>
internal static class WindowsBluetoothBatterySelfTests
{
    internal static void Run()
    {
        const string address="001122AABBCC";
        var normalized=WindowsConnectedBluetoothBatteryProvider
            .NormalizeBluetoothAddress("00:11:22:AA:BB:CC");
        if(normalized!=address ||
           WindowsConnectedBluetoothBatteryProvider
               .AddressFromDeviceInstance(@"BTHENUM\DEV_001122AABBCC\X")
               !=address ||
           WindowsConnectedBluetoothBatteryProvider
               .NormalizeBluetoothAddress("MAC address")!=null ||
           WindowsConnectedBluetoothBatteryProvider
               .NormalizeBluetoothAddress("000000000000")!=null)
            throw new Exception("Bluetooth address matching is unsafe.");

        var at=DateTimeOffset.Now;
        var facts=new[]{
            new WindowsConnectedBluetoothBatteryProvider.WindowsBatteryFact(
                address,"1MORE SonoFlow Hands-Free AG",78,true)
        };
        var connected=new HashSet<string>(StringComparer.OrdinalIgnoreCase){
            address};
        var matches=WindowsConnectedBluetoothBatteryProvider
            .ConfirmFresh(facts,connected,at);
        if(matches.Count!=1||matches[0].Percent!=78||
           matches[0].FriendlyName!="1MORE SonoFlow"||
           !matches[0].Kind.StartsWith("WinBt:",StringComparison.Ordinal)||
           matches[0].Kind.Contains(address,StringComparison.OrdinalIgnoreCase))
            throw new Exception("Connected HFP level was not read securely.");

        if(WindowsConnectedBluetoothBatteryProvider.ConfirmFresh(
            facts,new HashSet<string>(),at).Count!=0 ||
           WindowsConnectedBluetoothBatteryProvider.ConfirmFresh(
            [facts[0] with{PresentConnected=false}],connected,at).Count!=0)
            throw new Exception("Disconnected HFP level was marked live.");

        var conflicts=WindowsConnectedBluetoothBatteryProvider.ConfirmFresh(
            [facts[0],facts[0] with{Battery=43}],connected,at);
        if(conflicts.Count!=0)
            throw new Exception("Conflicting Bluetooth profile readings accepted.");

        var snapshot=new BatterySnapshot();
        BatterySources.MergeVerifiedWindowsBluetoothReadings(snapshot,matches);
        if(snapshot.Values.Count!=1 ||
           !BatteryPriority.SelectThree(snapshot,new BatteryPreferences())
               .Contains(matches[0].Kind))
            throw new Exception("Connected BT reader not eligible on OLED.");

        var older=new BatterySnapshot{
            Values=new Dictionary<string,BatteryReading>{
                ["Ble:already"]=new("Ble:already",91,"BLE","",at)
                {FriendlyName="1MORE SonoFlow"}}
        };
        BatterySources.MergeVerifiedWindowsBluetoothReadings(older,matches);
        if(older.Values.Count!=1)
            throw new Exception("Duplicate LE and HFP Bluetooth battery shown.");

        if(BatteryPriority.SelectThreeAt(snapshot,new BatteryPreferences(),
            at.AddMinutes(1)).Any())
            throw new Exception("Stale Windows Bluetooth level kept on OLED.");

        Console.WriteLine(
            "PASS: connected Windows HFP / PnP battery reader, "+
            "private hashed address, no disconnected/cached values, "+
            "no conflicting profiles, BLE deduplication and freshness.");
    }
}

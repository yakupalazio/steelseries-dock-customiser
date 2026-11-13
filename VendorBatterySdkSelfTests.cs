using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// In-process invariants only; must not load Corsair's DLL, contact iCUE,
/// enumerate hardware, modify device settings or change user preferences.
/// </summary>
internal static class VendorBatterySdkSelfTests
{
    internal static void Run()
    {
        // Official C ABI: type int + three 128-byte char arrays + two int.
        if(CorsairIcueBatteryProvider.NativeDeviceInfoSize!=396)
            throw new Exception("CorsairDeviceInfo P/Invoke layout changed.");
        var at=DateTimeOffset.Now;
        var good=CorsairIcueBatteryProvider.CheckedReading(
            "synthetic-corsair-id","Corsair Wireless Mouse",1,1,67,at);
        if(good is null || good.Percent!=67 ||
           !good.Kind.StartsWith("Vendor:Corsair:",StringComparison.Ordinal) ||
           good.FriendlyName!="Corsair Wireless Mouse")
            throw new Exception("Verified SDK BatteryLevel was not recognized.");
        if(CorsairIcueBatteryProvider.CheckedReading("id","mouse",0,1,67,at)
            is not null ||
           CorsairIcueBatteryProvider.CheckedReading("id","mouse",1,2,67,at)
            is not null ||
           CorsairIcueBatteryProvider.CheckedReading("id","mouse",1,1,-1,at)
            is not null ||
           CorsairIcueBatteryProvider.CheckedReading("id","mouse",1,1,101,at)
            is not null ||
           CorsairIcueBatteryProvider.CheckedReading("","mouse",1,1,67,at)
            is not null)
            throw new Exception("Unverified Corsair SDK value passed validation.");

        var normal=new BatterySnapshot();
        BatterySources.MergeVerifiedVendorReadings(normal,[good]);
        if(normal[good.Kind].Percent!=67 ||
           !BatteryPriority.Sanitize([good.Kind]).Contains(good.Kind) ||
           !BatteryPriority.SelectThree(normal,new BatteryPreferences())
                .Contains(good.Kind))
            throw new Exception("Live vendor battery was not added to OLED.");
        if(BatteryPriority.SelectThreeAt(normal,new BatteryPreferences(),
            at.AddMinutes(1)).Contains(good.Kind))
            throw new Exception("Stale vendor SDK percentage shown on OLED.");

        var same=new BatterySnapshot{
            Values=new Dictionary<string,BatteryReading>{
                ["Ble:known"]=new("Ble:known",71,"GATT","",at){
                    FriendlyName="Corsair Wireless Mouse"}
            }
        };
        BatterySources.MergeVerifiedVendorReadings(same,[good]);
        if(same.Values.ContainsKey(good.Kind))
            throw new Exception("Vendor SDK duplicated verified BLE device.");
        var rejected=good with{Percent=null};
        BatterySources.MergeVerifiedVendorReadings(new BatterySnapshot(),
            [rejected]);
        if(VendorBatterySourcesDialog.Entries(false).Count<8 ||
           VendorBatterySourcesDialog.Entries(true).Count<8)
            throw new Exception("Vendor capability guide incomplete.");

        Console.WriteLine(
            "PASS: optional official Corsair iCUE BatteryLevel reader ABI, "+
            "validated 0-100% values, vendor priority + freshness, "+
            "BLE deduplication, truthful brand capability catalog.");
    }
}

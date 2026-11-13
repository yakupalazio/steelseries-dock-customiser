using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>Offline product facts and BLE safety test; no device access.</summary>
internal static class WirelessCatalogSelfTests
{
    internal static void Run()
    {
        if(WirelessCompatibilityCatalog.ModelCount<240||
           WirelessCompatibilityCatalog.BrandCount<25)
            throw new Exception("Wireless catalog unexpectedly small.");
        if(WirelessCompatibilityCatalog.All.Any(item=>
            string.IsNullOrWhiteSpace(item.Brand)||
            string.IsNullOrWhiteSpace(item.Model)))
            throw new Exception("Empty product catalog record.");
        if(WirelessCompatibilityCatalog.All.GroupBy(x=>
            WirelessCompatibilityCatalog.Normalize(x.Brand+x.Model)+
            x.Category).Any(group=>group.Count()>1))
            throw new Exception("Duplicated wireless product model.");

        var beast=WirelessCompatibilityCatalog.Recognize(
            "WLMOUSE Beast X Pro 8K Receiver","","USB / Dock");
        if(beast?.Model!="Beast X Pro")
            throw new Exception("WLMOUSE tester model not recognized.");
        var atk=WirelessCompatibilityCatalog.Recognize(
            "ATK X1 V2 Ultimate Gaming Mouse","ATK","USB / Dock");
        if(atk?.Model!="X1 V2 Ultimate")
            throw new Exception("ATK longest model recognition failed.");
        var unknownAtk=WirelessCompatibilityCatalog.Recognize(
            "ATK 2.4GHz Receiver","ATK","USB / Dock");
        if(unknownAtk is null||unknownAtk.ExactModel)
            throw new Exception("Blind ATK tester misidentified as known model.");
        if(WirelessCompatibilityCatalog.Recognize(
            "ROG STRIX LC III LCD","ASUSTeK","USB / Dock")!=null)
            throw new Exception("Nonbattery ASUS cooler misclassified.");
        if(WirelessCompatibilityCatalog.Recognize(
            "HyperX Cloud III Wireless","HP","Bluetooth")?.Category!="Headset")
            throw new Exception("Wireless headset not recognized.");
        if(WirelessCompatibilityCatalog.Recognize(
            "Wraith W1 Wireless Mouse","Wraith","USB / Dock")?.Model!="W1")
            throw new Exception("Wraith W1 not recognized.");
        if(WirelessCompatibilityCatalog.RecognizeUsbIdentity(
            0x373B,0x10C9)?.Model!="A9 Plus" ||
           WirelessCompatibilityCatalog.RecognizeUsbIdentity(
            0x3057,0x1005)?.Model!="AX Wireless" ||
           WirelessCompatibilityCatalog.RecognizeUsbIdentity(
            0x373B,0x0101)!=null)
            throw new Exception("VID/PID precise identity failed.");

        var disconnected=new BatterySnapshot();
        var noBattery=ConnectedPeripheralInventory.MergeAndAttachBatteries(
            [("tester","WLMOUSE Beast X Pro","USB / Dock","WLMOUSE"),
             ("cooler","ROG STRIX LC III LCD","USB / Dock","ASUSTeK")],
            disconnected);
        if(noBattery.Count!=2 ||
           noBattery.Any(row=>row.Percent.HasValue)||
           !noBattery.Any(row=>row.Detail.Contains("Tanınan model"))||
           noBattery.Any(row=>row.Name.Contains("ROG STRIX")&&
               row.BatterySource=="Kablosuz cihaz kütüphanesi"))
            throw new Exception("Catalog generated fictional battery data.");

        var now=DateTimeOffset.Now;
        var confirmedSnap=new BatterySnapshot{
            Values=new Dictionary<string,BatteryReading>{
                ["Ble:verified"]=new("Ble:verified",63,
                    "Bluetooth GATT","",now)
                {FriendlyName="WLMOUSE Beast X Pro"}}
        };
        var confirmed=ConnectedPeripheralInventory.MergeAndAttachBatteries(
            [("known","WLMOUSE Beast X Pro","Bluetooth","WLMOUSE")],
            confirmedSnap);
        if(confirmed.Count!=1||confirmed[0].Percent!=63||
           confirmed[0].BatteryKind!="Ble:verified")
            throw new Exception("Live BLE battery did not attach to device.");

        if(!BleBatteryProvider.IsValidBatteryByte([0])||
           !BleBatteryProvider.IsValidBatteryByte([100])||
           BleBatteryProvider.IsValidBatteryByte([101])||
           BleBatteryProvider.IsValidBatteryByte([])||
           BleBatteryProvider.IsValidBatteryByte([0,100])||
           BleBatteryProvider.IsValidBatteryByte(null))
            throw new Exception("Bluetooth battery range check failed.");

        var privateEntry=new ConnectedPeripheral(
            "PRIVATE_BLUETOOTH_ADDRESS_NOT_FOR_REPORT",
            "Tester WLMOUSE Beast X Pro","USB / Dock","WLmouse",
            "Mouse",null,"Unverified","No private protocol",null){
                HardwareSummary="VID_373B PID_10C9"};
        string report=ConnectedDevicesDialog.BuildSafeReport([privateEntry]);
        if(report.Contains("PRIVATE_BLUETOOTH_ADDRESS_NOT_FOR_REPORT") ||
           !report.Contains("VID_373B PID_10C9")||
           !report.Contains("Tester WLMOUSE Beast X Pro"))
            throw new Exception("Device report leaks hardware instance IDs.");

        Console.WriteLine(
            $"PASS: {WirelessCompatibilityCatalog.ModelCount} wireless models / "+
            $"{WirelessCompatibilityCatalog.BrandCount} brands, "+
            "ATK/WLMOUSE/Wraith, no invented battery, safe BLE percent.");
    }
}

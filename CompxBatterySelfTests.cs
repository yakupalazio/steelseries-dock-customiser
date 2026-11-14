using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Strict CompX query safety and integration tests. No USB enumeration,
/// mouse input, HID writes, configuration changes or PnP access.
/// </summary>
internal static class CompxBatterySelfTests
{
    internal static void Run()
    {
        var request=CompxWirelessBatteryProvider.MakePowerQuery();
        if(request.Length!=17||request[0]!=8||request[1]!=4||
           request[16]!=0x49||
           request.Skip(2).Take(14).Any(b=>b!=0)||
           request.Sum(b=>b)%256!=0x55)
            throw new Exception("CompX power request packet changed.");

        // Published USB capture: CompX mouse 80%, 4013mV, not charging.
        byte[] sample=[
            0x08,0x04,0,0,0,2,0x50,0,0x0F,0xAD,
            0,0,0,0,0,0,0x3B
        ];
        var parsed=CompxWirelessBatteryProvider.ParsePowerReply(sample);
        if(parsed?.Percent!=80||parsed.OnCable||
           parsed.MilliVolts!=4013)
            throw new Exception("Real CompX battery frame not decoded.");

        var charge=(byte[])sample.Clone();
        charge[6]=67;charge[7]=1;
        charge[16]=CompxWirelessBatteryProvider.ComputeChecksum(
            charge.AsSpan(0,16));
        if(CompxWirelessBatteryProvider.ParsePowerReply(charge) is not
           {Percent:67,OnCable:true})
            throw new Exception("CompX charging byte not decoded.");

        var damaged=(byte[])sample.Clone();
        damaged[6]=20;
        if(CompxWirelessBatteryProvider.ParsePowerReply(damaged)!=null)
            throw new Exception("CompX incorrect checksum accepted.");
        damaged=(byte[])sample.Clone();
        damaged[1]=0x0A;
        damaged[16]=CompxWirelessBatteryProvider.ComputeChecksum(
            damaged.AsSpan(0,16));
        if(CompxWirelessBatteryProvider.ParsePowerReply(damaged)!=null)
            throw new Exception("CompX unsolicited event accepted.");
        damaged=(byte[])sample.Clone();
        damaged[0]=0x09;
        damaged[16]=CompxWirelessBatteryProvider.ComputeChecksum(
            damaged.AsSpan(0,16));
        if(CompxWirelessBatteryProvider.ParsePowerReply(damaged)!=null)
            throw new Exception("Wrong HID report ID accepted.");
        damaged=(byte[])sample.Clone();
        damaged[6]=101;
        damaged[16]=CompxWirelessBatteryProvider.ComputeChecksum(
            damaged.AsSpan(0,16));
        if(CompxWirelessBatteryProvider.ParsePowerReply(damaged)!=null)
            throw new Exception("Invalid battery percent accepted.");
        if(CompxWirelessBatteryProvider.ParsePowerReply(
            sample.AsSpan(0,16))!=null)
            throw new Exception("Truncated CompX report accepted.");

        var known=CompxWirelessBatteryProvider.FindReceiver(0x373B,0x101A);
        if(known is not {ConfirmedOnReferenceHardware:true}||
           CompxWirelessBatteryProvider.FindReceiver(0x373B,0xDEAD)!=null||
           CompxWirelessBatteryProvider.FindReceiver(0x1038,0x12E0)!=null)
            throw new Exception("HID positive allowlist too broad.");
        var caps=new CompxWirelessBatteryProvider.CollectionCaps(
            0xFF02,0x0002,17,17);
        if(!CompxWirelessBatteryProvider.IsSafeCollection(known,caps)||
           CompxWirelessBatteryProvider.IsSafeCollection(known,
               caps with{UsagePage=0xFF04})||
           CompxWirelessBatteryProvider.IsSafeCollection(known,
               caps with{Usage=0x0001})||
           CompxWirelessBatteryProvider.IsSafeCollection(known,
               caps with{OutputLength=2})||
           CompxWirelessBatteryProvider.IsSafeCollection(known,
               caps with{OutputLength=64})||
           CompxWirelessBatteryProvider.IsSafeCollection(null,caps))
            throw new Exception("Wrong CompX interface eligible for HID I/O.");

        var id=WirelessCompatibilityCatalog.RecognizeUsbIdentity(
            0x373B,0x101A);
        if(id is null||id.Brand!="ATK"||id.ExactModel)
            throw new Exception("ATK 1K receiver falsely identified as mouse.");
        string path=@"\\?\hid#vid_373b&pid_101a#nonsecret";
        string hash=CompxWirelessBatteryProvider.StableKind(path);
        if(!hash.StartsWith("Vendor:Compx:",StringComparison.Ordinal)||
           hash.Contains("pid_101a",StringComparison.OrdinalIgnoreCase))
            throw new Exception("CompX UI identity exposes hardware path.");

        var current=DateTimeOffset.Now;
        var reading=new BatteryReading(hash,67,
            "CompX HID power status (live 0x04)","checksum passed",
            current,false)
        {FriendlyName="ATK Mouse 1K Dongle",IconKind="Mouse"};
        var snapshot=new BatterySnapshot();
        BatterySources.MergeVerifiedVendorReadings(snapshot,[reading]);
        if(snapshot[hash].Percent!=67||
           !BatteryPriority.SelectThree(snapshot,new BatteryPreferences())
               .Contains(hash))
            throw new Exception("CompX live percent did not reach OLED priority.");

        var devices=ConnectedPeripheralInventory.MergeAndAttachBatteries(
            [("atk-device","ATK Mouse 1K Dongle","USB / Dock",
                "USB VID 373B")],snapshot);
        if(devices.Count!=1||devices[0].Percent!=67||
           devices[0].BatteryKind!=hash||devices[0].IconKind!="Mouse")
            throw new Exception("CompX live percent did not attach to ATK dongle.");
        if(BatteryPriority.SelectThreeAt(snapshot,new BatteryPreferences(),
            current.AddMinutes(1)).Contains(hash))
            throw new Exception("Stale CompX percentage incorrectly displayed.");

        Console.WriteLine(
            $"PASS: CompX {CompxWirelessBatteryProvider.Receivers.Count} "+
            "precise HID IDs, read-only 0x04 status request, strict "+
            "FF02:0002 + checksum, 0..100%, ATK blind receiver, "+
            "OLED + connected-device integration and freshness.");
    }
}

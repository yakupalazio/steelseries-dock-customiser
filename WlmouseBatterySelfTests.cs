using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>Only offline WLmouse protocol tests; never opens a USB device.</summary>
internal static class WlmouseBatterySelfTests
{
    internal static void Run()
    {
        byte[] query=WlmouseBatteryProvider.MakePowerQuery();
        if(query.Length!=65||query[0]!=0||query[1]!=0||
           query[2]!=0||query[3]!=2||query[4]!=2||
           query[5]!=0||query[6]!=0x83||
           query.Skip(7).Any(b=>b!=0))
            throw new Exception("WLmouse power inquiry payload changed.");

        byte[] feature=new byte[65];
        byte[] body=[0xA1,0,2,2,0,0x83,0,73];
        body.CopyTo(feature,1);
        var reading=WlmouseBatteryProvider.ParseFeature(feature);
        if(reading is not {Percent:73,Charging:false})
            throw new Exception("WLmouse report 0x83 response not decoded.");

        feature[8]=101;
        if(WlmouseBatteryProvider.ParseFeature(feature)!=null)
            throw new Exception("Invalid WLmouse battery percent accepted.");
        feature[8]=73;
        feature[6]=0x82;
        if(WlmouseBatteryProvider.ParseFeature(feature)!=null)
            throw new Exception("Wrong WLmouse command accepted.");
        var heartbeat=WlmouseBatteryProvider.ParseHeartbeat([0,3,0,64,1]);
        if(heartbeat is not{Percent:64,Charging:true}||
           WlmouseBatteryProvider.ParseHeartbeat([0,3,0,101,0])!=null)
            throw new Exception("Passive mouse battery report validation failed.");

        var caps=new CompxWirelessBatteryProvider.CollectionCaps(
            0xFFFF,0,65,65,65);
        if(WlmouseBatteryProvider.Model(0x36A7,0xA880)!="WLMOUSE Beast X Max"||
           !WlmouseBatteryProvider.MayQueryBattery(0x36A7,0xA880,caps)||
           WlmouseBatteryProvider.MayQueryBattery(0x36A7,0xA870,caps)||
           !WlmouseBatteryProvider.MayObserveHeartbeat(
               0x36A7,0xA870,caps)||
           WlmouseBatteryProvider.MayQueryBattery(0x36A7,0xDEAD,caps)||
           WlmouseBatteryProvider.MayQueryBattery(
               0x1038,0x12E0,caps)||
           WlmouseBatteryProvider.MayQueryBattery(
               0x36A7,0xA880,caps with{UsagePage=0xFF1C})||
           WlmouseBatteryProvider.MayQueryBattery(
               0x36A7,0xA880,caps with{FeatureLength=16}))
            throw new Exception("WLmouse HID feature usage/PID guard regression.");

        string marker="36A7:A880";
        var now=DateTimeOffset.Now;
        var vendor=new BatterySnapshot{
            Values=new Dictionary<string,BatteryReading>{
                ["Vendor:Wlmouse:test"]=new("Vendor:Wlmouse:test",64,
                    "0x83","Live receiver "+marker,now,false)
                {FriendlyName="WLMOUSE Beast X Max",IconKind="Mouse"}
            }
        };
        var found=ConnectedPeripheralInventory.MergeAndAttachBatteries(
            [("test-one","WLMOUSE Beast X Max Receiver","USB / Dock",
                "WLMOUSE",@"USB\VID_36A7&PID_A880\TEMP")],vendor);
        if(found.Count!=1||found[0].Percent!=64||
           found[0].IconKind!="Mouse")
            throw new Exception(
                "WLmouse live reading could not attach to its 8K dongle.");

        var ambiguous=ConnectedPeripheralInventory.MergeAndAttachBatteries(
            [("receiver-A","WLMOUSE A880 Dongle","USB / Dock",
                "WLMOUSE",@"USB\VID_36A7&PID_A880\A"),
             ("receiver-B","WLMOUSE A880 Dongle","USB / Dock",
                "WLMOUSE",@"USB\VID_36A7&PID_A880\B")],
            vendor);
        if(ambiguous.Any(d=>d.Percent.HasValue))
            throw new Exception("One mouse level attached to two USB receivers.");

        Console.WriteLine("PASS: WLmouse strict 64/65-byte feature report 0x83, "+
            "passive heartbeat, receiver allowlist, no unsupported Pro writes, "+
            "unique public VID/PID matching and duplicate suppression.");
    }
}

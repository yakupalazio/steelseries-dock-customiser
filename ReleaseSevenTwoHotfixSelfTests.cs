using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// RC7.2 regressions are intentionally in-process and in-memory only.
/// They never create user icon overrides or change the profile directory.
/// </summary>
internal static class ReleaseSevenTwoHotfixSelfTests
{
    internal static void Run()
    {
        var track=new DockWidget{Kind="Track"};
        var artist=new DockWidget{Kind="Artist"};
        if(OledRenderer.DisplayText(track)!="No media playing" ||
           OledRenderer.DisplayText(artist)!="")
            throw new Exception("Old Draconian / PALE TORTURED demo fallback survived.");

        var source=new DockWidget{
            Kind="Lyrics",LyricLines=4,LyricTransitionStyle="Fade",
            LyricOffsetMs=-320,WidgetWidth=108,StrokeWidth=2};
        var target=new DockWidget{Kind="Lyrics"};
        PixelStudioForm.TransferOptionalMediaSettings(target,source);
        if(target.LyricTransitionStyle!="Fade"||
           target.LyricLines!=4||target.LyricOffsetMs!=-320)
            throw new Exception("Lyric settings are not copied from dialog.");
        var layout=new LayoutState{Widgets=[target]};
        StudioProject.Sanitize(layout,ScreenMode.Media,true);
        var clone=layout.DeepClone();
        string json=JsonSerializer.Serialize(clone);
        var roundtrip=JsonSerializer.Deserialize<LayoutState>(json);
        if(roundtrip?.Widgets.FirstOrDefault()?.LyricTransitionStyle!="Fade")
            throw new Exception("Lyric style reset on saved-layout roundtrip.");
        source.LyricTransitionStyle="Wipe";
        PixelStudioForm.TransferOptionalMediaSettings(target,source);
        if(target.LyricTransitionStyle!="Wipe")
            throw new Exception("Changing style twice reverts to Slide.");

        var at=DateTimeOffset.Now;
        var linked=new BatterySnapshot{
            Values=new Dictionary<string,BatteryReading>{
                ["Phone"]=new("Phone",61,"Microsoft Phone Link",
                    "live",at)
            }
        };
        var windows=new BatteryReading("WinBt:device1",75,
            "Windows BT","connected",at)
            {FriendlyName="Galaxy S24+",IconKind="Bluetooth"};
        BatterySources.MergeVerifiedWindowsBluetoothReadings(
            linked,[windows]);
        if(linked.Values.Count!=1||linked["Phone"].Percent!=61||
           linked["Phone"].IconKind!="Phone")
            throw new Exception("Connected phone duplicated PhoneLink OLED slot.");
        var onlyWindows=new BatterySnapshot();
        BatterySources.MergeVerifiedWindowsBluetoothReadings(
            onlyWindows,[windows]);
        if(onlyWindows.Values.Count!=1||
           onlyWindows["Phone"].Percent!=75||
           onlyWindows["Phone"].IconKind!="Phone")
            throw new Exception("Connected Windows phone was not classified.");
        var multiple=new BatterySnapshot();
        BatterySources.MergeVerifiedWindowsBluetoothReadings(
            multiple,[windows,windows with{
                Kind="WinBt:device2",FriendlyName="iPhone 16"}]);
        if(multiple.Values.ContainsKey("Phone"))
            throw new Exception("Two connected phones wrongly assigned to one.");

        var le=new BatteryReading("Ble:s24",72,"GATT","connected",at)
            {FriendlyName="Galaxy S24+",IconKind="Bluetooth"};
        var oneBle=new BatterySnapshot{
            Values=new Dictionary<string,BatteryReading>{
                ["Phone"]=new("Phone",50,"Phone Link","live",at)}
        };
        BatterySources.MergeVerifiedBleReadings(oneBle,[le]);
        if(oneBle.Values.Count!=1||oneBle["Phone"].Percent!=50||
           oneBle["Phone"].IconKind!="Phone")
            throw new Exception("BLE phone duplicated the existing Phone slot.");
        var freshBle=new BatterySnapshot();
        BatterySources.MergeVerifiedBleReadings(freshBle,[le]);
        if(freshBle.Values.Count!=1||
           freshBle["Phone"].Percent!=72 ||
           freshBle["Phone"].IconKind!="Phone")
            throw new Exception("Unique BLE phone could not use Phone slot.");

        var dualMode=ConnectedPeripheralInventory.MergeAndAttachBatteries(
            [("classic-id","Galaxy S24+","Bluetooth","Samsung"),
             ("le-id","Galaxy S24+","Bluetooth LE","Samsung")],
            linked);
        if(dualMode.Count!=1||dualMode[0].Percent!=61||
           dualMode[0].IconKind!="Phone"||
           !dualMode[0].Connection.Contains("Bluetooth LE"))
            throw new Exception("One dual-mode phone appears twice in inventory.");
        // Ambiguous repeated names must NOT incorrectly merge multiple
        // same-model physical phones into one battery identity.
        var ambiguous=ConnectedPeripheralInventory.MergeAndAttachBatteries(
            [("first-id","Galaxy S24+","Bluetooth","Samsung"),
             ("second-id","Galaxy S24+","Bluetooth","Samsung"),
             ("le-id","Galaxy S24+","Bluetooth LE","Samsung")],
            linked);
        if(ambiguous.Count!=3||ambiguous.Any(d=>d.Percent.HasValue))
            throw new Exception("Ambiguous phone models falsely merged.");

        if(BatteryDeviceClassifier.IconFor("Galaxy S24+","Bluetooth")!="Phone"||
           BatteryDeviceClassifier.IconFor("iPhone 16","Bluetooth")!="Phone"||
           BatteryDeviceClassifier.IconFor("Pixel 9 Pro","Bluetooth")!="Phone"||
           !BatteryPriority.DefaultKinds.SequenceEqual(
               new[]{"Headset","SpareBattery"}))
            throw new Exception("Phone icon or neutral two-slot defaults regressed.");

        using(var art=new Bitmap(11,11))
        {
            using(var g=Graphics.FromImage(art))g.Clear(Color.White);
            for(int x=3;x<8;x++)art.SetPixel(x,5,Color.Black);
            var pixels=BatteryDeviceIconOverrides.Rasterize(art);
            if(pixels.Length!=11||pixels.Any(line=>line.Length!=11)||
               pixels.All(line=>!line.Contains('1')))
                throw new Exception("Imported local icon failed 11x11 raster.");
        }
        using(var photograph=new Bitmap(9,9))
        using(var header=DockFactoryHeaderReference.Extract(
            photograph,[new PointF(0,0),new PointF(8,0),
                new PointF(8,8),new PointF(0,8)]))
        {
            if(header is null||header.Width!=128||header.Height!=10||
               header.GetPixel(46,2).R<220 ||
               header.GetPixel(52,3).R>30 ||
               header.GetPixel(4,0).R>30)
                throw new Exception("Clean OEM USB-1 guide pixel header regressed.");
        }

        using(var battery=new BatteryStatusDialog(
            new BatteryPreferences(),()=>new BatterySnapshot(),
            ()=>System.Threading.Tasks.Task.CompletedTask,()=>{}))
        {
            var root=battery.Controls.OfType<TableLayoutPanel>()
                .FirstOrDefault();
            var footer=root?.Controls.OfType<Panel>()
                .FirstOrDefault(x=>x.Controls.OfType<Button>()
                    .Any(b=>b.Text.Contains("kaydet",
                        StringComparison.OrdinalIgnoreCase)));
            var button=footer?.Controls.OfType<Button>().FirstOrDefault();
            if(root is null||footer is null||button is null||
               root.RowCount!=2 ||button.Bottom>58 ||
               !root.RowStyles.Cast<RowStyle>().Any(r=>
                   r.SizeType==SizeType.Absolute&&r.Height>=58))
                throw new Exception("Battery save button not in visible fixed footer.");
        }

        Console.WriteLine(
            "PASS: no hardcoded media demo, persisted lyric transitions, "+
            "no duplicate phone/PhoneLink slot, right phone icon, "+
            "neutral default batteries, local 11x11 icons, "+
            "clean USB-1 reference and unclipped Settings save button.");
    }
}

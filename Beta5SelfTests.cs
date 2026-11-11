using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace SteelseriesDockCustomiser;

internal static class Beta5SelfTests
{
    private static bool Lit(Bitmap image,int x,int y)
        =>image.GetPixel(x,y).R>150;

    private static Bitmap DrawDial(string style,int game,int chat,int width=102)
    {
        var state=new LayoutState{
            Widgets=[new DockWidget{
                Kind="VolumeChatMix",X=1,Y=12,DialStyle=style,
                MiniWidth=width,WheelShowGame=true,WheelShowChat=true,
                WheelRingThickness=3
            }]
        };
        var snap=new DialSnapshot(DialView.ChatMix,null,game,chat,
            DateTimeOffset.UtcNow,"Beta5 test");
        return OledRenderer.Render(state,false,false,false,
            protectHeader:true,layoutMode:ScreenMode.Idle,dial:snap);
    }

    public static void Run()
    {
        using(var neutral=DrawDial("Bar",100,100))
        using(var game=DrawDial("Bar",100,0))
        using(var chat=DrawDial("Bar",0,100))
        {
            int center=1+(102-2)/2, y=12+14+3;
            if(!Lit(neutral,center,y) || !Lit(game,center,y) ||
               !Lit(chat,center,y))
                throw new Exception("Flat ChatMix center divider disappeared");
            if(Lit(neutral,center-20,y)||Lit(neutral,center+20,y))
                throw new Exception("Flat neutral ChatMix should have empty halves");
            if(!Lit(game,center-20,y)||Lit(game,center+20,y))
                throw new Exception("Game dominance did not fill left half only");
            if(Lit(chat,center-20,y)||!Lit(chat,center+20,y))
                throw new Exception("Chat dominance did not fill right half only");
        }
        using(var mini=DrawDial("MiniWheel",100,0))
        using(var full=DrawDial("Wheel",100,0))
        {
            int miniCount=0, fullCount=0;
            for(int y=10;y<53;y++)for(int x=0;x<70;x++)
            {
                if(Lit(mini,x,y))miniCount++;
                if(Lit(full,x,y))fullCount++;
            }
            if(miniCount==0||fullCount==0||miniCount>=fullCount)
                throw new Exception("Mini wheel was not smaller than full ring");
        }
        string id=BleBatteryProvider.StableKind("test-device-id-1");
        if(!id.StartsWith("Ble:")||id!=BleBatteryProvider.StableKind("test-device-id-1")||
           id==BleBatteryProvider.StableKind("test-device-id-2"))
            throw new Exception("BLE stable ID derivation failed");
        if(BatteryDeviceClassifier.IconFor("Galaxy Buds 3","Bluetooth")!="Earbuds"||
           BatteryDeviceClassifier.IconFor("Wireless Controller","Bluetooth")!="DualSense"||
           BatteryDeviceClassifier.IconFor("Keychron Keyboard","Bluetooth")!="Keyboard"||
           BatteryDeviceClassifier.IconFor("JBL Flip Speaker","Bluetooth")!="Speaker"||
           BatteryDeviceClassifier.IconFor("Unknown","Bluetooth")!="Bluetooth"||
           BatteryDeviceClassifier.IconFor("Unknown","USB")!="Usb")
            throw new Exception("Automatic battery icon classification failed");
        var now=DateTimeOffset.Now;
        var detected=new BatterySnapshot{
            Values=new Dictionary<string,BatteryReading>{
                [id]=new(id,20,"GATT (uncached)","",now){
                    IconKind="Earbuds",FriendlyName="Galaxy Buds 3"}
            }
        };
        if(!BatteryPriority.SelectThree(detected,new BatteryPreferences()).Contains(id))
            throw new Exception("Verified new GATT device did not enter priority");
        var monitor=new BatteryWarningMonitor();
        var pref=new BatteryPreferences{
            EnableLowBatteryWarnings=true,LowBatteryWarningPercent=20,
            CriticalBatteryWarningPercent=10
        };
        if(monitor.Evaluate(detected,pref,now).Count!=1 ||
           monitor.Evaluate(detected,pref,now.AddSeconds(10)).Count!=0)
            throw new Exception("Low battery warning repeated too frequently");
        if(monitor.Evaluate(new BatterySnapshot{
            Values=new Dictionary<string,BatteryReading>{
                [id]=new(id,5,"GATT (uncached)","",now.AddSeconds(12))}
            },pref,now.AddSeconds(12)).Count!=1)
            throw new Exception("Critical low battery escalation not delivered");
        using(var boldClock=OledRenderer.RenderDateTimeFontSample("08:08",3,true))
        using(var thinClock=OledRenderer.RenderDateTimeFontSample("08:08",1,true))
        {
            // Eight must retain its two counters even with 3px seven segments.
            if(Lit(boldClock,8,16)||Lit(boldClock,8,23)||
               !Lit(boldClock,7,13)||Lit(thinClock,7,13))
                throw new Exception("Bold seven-segment clock lost readable counters");
        }
        Console.WriteLine("PASS: mini radial, split ChatMix, bold fonts, BLE priorities and alerts.");
    }

    public static void RenderExamples(string folder)
    {
        Directory.CreateDirectory(folder);
        using(var neutral=DrawDial("Bar",100,100))
            neutral.Save(Path.Combine(folder,"beta5-flat-neutral.png"),ImageFormat.Png);
        using(var game=DrawDial("Bar",100,0))
            game.Save(Path.Combine(folder,"beta5-flat-game.png"),ImageFormat.Png);
        using(var chat=DrawDial("Bar",0,100))
            chat.Save(Path.Combine(folder,"beta5-flat-chat.png"),ImageFormat.Png);
        using(var mini=DrawDial("MiniWheel",100,50))
            mini.Save(Path.Combine(folder,"beta5-mini-wheel.png"),ImageFormat.Png);
        foreach(var weight in new[]{1,2,3})
        {
            using var clock=OledRenderer.RenderDateTimeFontSample("08:08",weight,true);
            clock.Save(Path.Combine(folder,$"beta5-clock-{weight}.png"),ImageFormat.Png);
            using var date=OledRenderer.RenderDateTimeFontSample("08/11/2026",weight,false);
            date.Save(Path.Combine(folder,$"beta5-date-{weight}.png"),ImageFormat.Png);
        }
    }
}

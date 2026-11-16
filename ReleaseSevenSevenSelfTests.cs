using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace SteelseriesDockCustomiser;

internal static class ReleaseSevenSevenSelfTests
{
    internal static void Run()
    {
        LogoContrast();
        WindowsControllerBattery();
        Console.WriteLine("PASS: general color-contrast icon conversion "+
            "(transparent, dark/bright, textured), controller battery "+
            "capacity/identity, distinct devices and unknown values.");
    }

    private static void LogoContrast()
    {
        // Actual League icon contains gold strokes and dark navy fill on
        // alpha canvas. Alpha-only converters turn its whole shape white.
        using var icon=new Bitmap(64,64,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(icon))
        {
            g.Clear(Color.Transparent);
            using var dark=new SolidBrush(Color.FromArgb(255,11,23,43));
            using var gold=new Pen(Color.FromArgb(255,236,174,51),5);
            using var blue=new Pen(Color.FromArgb(255,81,152,194),3);
            g.FillEllipse(dark,8,6,47,51);
            g.DrawEllipse(gold,8,6,47,51);
            g.DrawLine(blue,20,10,20,49);
            g.DrawLine(gold,20,49,47,49);
        }
        bool[] pixels=GameLogoProvider.ConvertImage(icon);
        int on=pixels.Count(v=>v);
        if(on is <28 or >330||GameLogoProvider.IsFilledRectangle(pixels))
            throw new Exception("League-style colored artwork lost dark detail");
        // A dark interior region MUST remain off while the bright outline
        // near it remains on.
        if(pixels[16*32+18]||
           !Enumerable.Range(6,14).Any(x=>pixels[15*32+x]))
            throw new Exception("Dark colored icon interior became white");

        using var textured=new Bitmap(128,128,PixelFormat.Format32bppArgb);
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            textured.SetPixel(x,y,Color.FromArgb(255,
                50+x/9,80+(x+y)%68,35+y/8));
        using(var g=Graphics.FromImage(textured))
        using(var brush=new SolidBrush(Color.FromArgb(255,5,7,10)))
            g.FillRectangle(brush,40,40,48,48);
        bool[] tile=GameLogoProvider.ConvertImage(textured);
        if(tile.Count(v=>v) is <20 or >810 ||
           GameLogoProvider.IsFilledRectangle(tile))
            throw new Exception("Opaque multicolor game tile is blank/block");

        using var whiteArt=new Bitmap(32,32);
        using(var g=Graphics.FromImage(whiteArt))
        {
            g.Clear(Color.Black);
            using var pen=new Pen(Color.White,3);
            g.DrawRectangle(pen,4,4,24,24);
        }
        if(GameLogoProvider.ConvertImage(whiteArt).Count(v=>v)<30)
            throw new Exception("Bright monochrome icon regression");

        // Colors of an already drawn OLED sprite do not depend on the
        // target widget: renderer and editor use exactly these bits.
        var layout=new LayoutState{Widgets=[
            new DockWidget{Kind="GameLogo",X=40,Y=14,GameLogoSize=32}
        ]};
        using var output=OledRenderer.Render(layout,false,false,false,
            game:new GameSnapshot("Sample","sample",123),
            protectHeader:true,layoutMode:ScreenMode.Game,
            gameLogo:new GameLogoSnapshot("sample","Sample",32,pixels,"QA"));
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            if((output.GetPixel(x+40,y+14).R>127)!=pixels[y*32+x])
                throw new Exception("OLED rendering and saved bitmap diverged");
    }

    private static void WindowsControllerBattery()
    {
        static void Check(int? remaining,int? full,int? expected)
        {
            if(WindowsGameControllerBatteryProvider.FromCapacity(
                remaining,full)!=expected)
                throw new Exception("OS controller battery capacity invalid");
        }
        Check(50,100,50);
        Check(0,100,0);
        Check(999,1000,100);
        Check(null,100,null);
        Check(50,null,null);
        Check(100,0,null);
        Check(101,100,null);
        Check(-1,100,null);
        if(!WindowsGameControllerBatteryProvider.IsDualSense(
            "DualSense Wireless Controller",0x054c,0x0ce6)||
           WindowsGameControllerBatteryProvider.IsDualSense(
               "Wireless Controller",0x045e,0x0ce6))
            throw new Exception("Controller manufacturer identity not verified");

        var at=DateTimeOffset.Now;
        BatteryReading Report(string id,int pct)=>
            new("Vendor:WinGamepad:"+id,pct,
                "Windows.Gaming.Input live BatteryReport",
                "Verified",at)
            {FriendlyName="DualSense Wireless Controller",IconKind="DualSense"};
        var one=new BatterySnapshot();
        BatterySources.MergeVerifiedVendorReadings(one,[Report("first",63)]);
        if(one["DualSense"].Percent!=63||
           !BatteryPriority.SelectThree(one,new BatteryPreferences())
               .Contains("DualSense"))
            throw new Exception("Verified WinRT controller battery not displayed");
        var preexisting=new BatterySnapshot();
        preexisting.Values["DualSense"]=new("DualSense",47,
            "Read-only HID","",at);
        BatterySources.MergeVerifiedVendorReadings(preexisting,
            [Report("first",63)]);
        if(preexisting["DualSense"].Percent!=47)
            throw new Exception("Controller fallback overwrote verified HID");
        var twins=new BatterySnapshot();
        BatterySources.MergeVerifiedVendorReadings(twins,
            [Report("first",25),Report("second",78)]);
        if(twins["DualSense"].Available||
           !twins.Values.ContainsKey("Vendor:WinGamepad:first")||
           !twins.Values.ContainsKey("Vendor:WinGamepad:second"))
            throw new Exception("Two controllers were merged into wrong slot");
    }
}

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SteelseriesDockCustomiser;

/// <summary>
/// No hardware access: reliable long-idle wheel state, true zero vs unknown,
/// large rectangular black/white logo conversion and on-OLED parity,
/// source name, and collision-free battery toolbar layout.
/// Optional private fixture folder can contain the user's own uploaded art.
/// </summary>
internal static class ReleaseSevenFourHotfixSelfTests
{
    internal static void Run()
    {
        DialStaleness();
        LogoModes();
        BatteryLayout();
        Console.WriteLine(
            "PASS: RC7.4 preserved 40min Volume/ChatMix wheel values " +
            "including real 0, never-known remains unknown; " +
            "non-square/inverse PNG and EXE-style bitmap background removal, " +
            "OLED pixel parity, battery last-read toolbar collision.");
    }

    private static Bitmap DrawWheel(string style,DialSnapshot snapshot)
    {
        var layout=new LayoutState{Widgets=[
            new DockWidget{Kind="VolumeChatMix",X=7,Y=12,
                DialStyle=style,MiniWidth=98}
        ]};
        return OledRenderer.Render(layout,false,false,false,
            protectHeader:true,layoutMode:ScreenMode.Idle,dial:snapshot);
    }

    private static bool Same(Bitmap first,Bitmap second)
    {
        if(first.Size!=second.Size)return false;
        for(int y=0;y<first.Height;y++)
        for(int x=0;x<first.Width;x++)
            if(first.GetPixel(x,y).ToArgb()!=
               second.GetPixel(x,y).ToArgb())
                return false;
        return true;
    }

    private static void DialStaleness()
    {
        var now=DateTimeOffset.UtcNow;
        var oldReal=new DialSnapshot(DialView.Volume,67,null,null,
            now.AddMinutes(-40),"Live");
        var described=ArctisDialMonitor.DescribeIdleSnapshot(oldReal,now);
        if(described.Volume!=67||described.Status=="Live" ||
           ArctisDialMonitor.DescribeIdleSnapshot(
               oldReal with{UpdatedAt=now.AddSeconds(-5)},now).Status!="Live")
            throw new Exception("Idle dial status must say last-known");
        foreach(string style in new[]{"Bar","Wheel","MiniWheel"})
        foreach(DialView mode in new[]{DialView.Volume,DialView.ChatMix})
        {
            var recent=new DialSnapshot(mode,67,100,36,
                now.AddSeconds(-12),"real HID event")
            {VolumeAttenuationSteps=18};
            var stale=recent with{UpdatedAt=now.AddMinutes(-40)};
            using var freshImage=DrawWheel(style,recent);
            using var staleImage=DrawWheel(style,stale);
            if(!Same(freshImage,staleImage))
                throw new Exception(
                    $"Dial went blank after 40min of inactivity ({style}/{mode})");

            var unknown=new DialSnapshot(mode,null,null,null,
                DateTimeOffset.MinValue,"never seen");
            using var emptyImage=DrawWheel(style,unknown);
            if(Same(emptyImage,staleImage))
                throw new Exception(
                    $"A last known wheel value rendered as unknown ({style}/{mode})");

            var realZero=new DialSnapshot(mode,0,0,100,
                now.AddMinutes(-40),"real zero")
            {VolumeAttenuationSteps=56};
            var realZeroRecent=realZero with{
                UpdatedAt=now.AddSeconds(-12)};
            using var zeroImage=DrawWheel(style,realZero);
            using var zeroRecentImage=DrawWheel(style,realZeroRecent);
            if(!Same(zeroImage,zeroRecentImage))
                throw new Exception(
                    $"True zero was not preserved during idle ({style}/{mode})");
            if(style=="Bar"&&mode==DialView.Volume &&
               Same(zeroImage,emptyImage))
                throw new Exception("Real 0% confused with never-known volume");
        }
    }

    private static void LogoModes()
    {
        // User reports large rectangular art being letterboxed and the
        // original transparent margin incorrectly causing a solid square.
        foreach(bool light in new[]{true,false})
        {
            using var original=new Bitmap(520,384);
            using(var g=Graphics.FromImage(original))
            {
                g.Clear(light?Color.White:Color.Black);
                using var pen=new Pen(light?Color.Black:Color.White,13);
                g.DrawEllipse(pen,20,40,460,270);
                g.DrawBezier(pen,
                    new Point(100,280),new Point(170,30),
                    new Point(300,320),new Point(475,75));
            }
            VerifyLogo(original,light?"black-on-white":"white-on-black");
        }
        using(var transparent=new Bitmap(520,384,
            PixelFormat.Format32bppArgb))
        {
            using var g=Graphics.FromImage(transparent);
            g.Clear(Color.Transparent);
            using var brush=new SolidBrush(Color.Black);
            g.FillEllipse(brush,125,65,270,230);
            VerifyLogo(transparent,"transparent black art");
        }

        var fixtureFolder=Environment.GetEnvironmentVariable(
            "DOCK_LOGO_FIXTURES");
        if(!string.IsNullOrWhiteSpace(fixtureFolder))
        {
            foreach(string name in new[]{
                "lies-black-on-white.png","lies-white-on-black.png"})
            {
                string file=Path.Combine(fixtureFolder,name);
                if(!File.Exists(file))
                    throw new Exception("Missing private user fixture "+name);
                using var input=Image.FromFile(file);
                bool[] pixels=VerifyLogo(input,"user "+name);
                using var result=new Bitmap(32,32);
                for(int y=0;y<32;y++)
                for(int x=0;x<32;x++)
                    result.SetPixel(x,y,
                        pixels[y*32+x]?Color.White:Color.Black);
                // Private, opt-in QA image; not tracked/redistributed.
                result.Save(Path.Combine(fixtureFolder,
                    Path.GetFileNameWithoutExtension(name)+"-oled.png"),
                    ImageFormat.Png);
            }
        }

        // Real extraction path uses the exact same shared converter.
        try
        {
            string? exe=Environment.ProcessPath;
            if(exe is not null&&File.Exists(exe))
            {
                using var icon=Icon.ExtractAssociatedIcon(exe);
                if(icon is not null)
                {
                    using var bitmap=icon.ToBitmap();
                    bool[] bits=GameLogoProvider.ConvertImage(bitmap);
                    if(bits.Length!=1024 ||
                       bits.Count(b=>b)>900)
                        throw new Exception("EXE icon raster became a full block");
                }
            }
        }
        catch(ArgumentException) { /* Unbranded test-host EXE icon absent. */ }
    }

    private static bool[] VerifyLogo(Image source,string description)
    {
        bool[] pixels=GameLogoProvider.ConvertImage(source);
        int on=pixels.Count(v=>v);
        if(pixels.Length!=1024||on is <18 or >800)
            throw new Exception(
                $"Game logo becomes a block/blank: {description} ({on}/1024)");

        if(GameLogoProvider.IsFilledRectangle(pixels))
            throw new Exception($"Solid rectangle persisted: {description}");
        var layout=new LayoutState{Widgets=[
            new DockWidget{Kind="GameLogo",X=47,Y=12,GameLogoSize=32}
        ]};
        var game=new GameSnapshot("Lies of P","liesofp",12345);
        var snapshot=new GameLogoSnapshot("liesofp","Lies of P",
            32,pixels,"private test");
        using var output=OledRenderer.Render(
            layout,false,false,false,game:game,
            protectHeader:true,layoutMode:ScreenMode.Game,
            gameLogo:snapshot);
        for(int y=0;y<32;y++)
        for(int x=0;x<32;x++)
        {
            bool isOn=output.GetPixel(47+x,12+y).R>=128;
            if(isOn!=pixels[y*32+x])
                throw new Exception(
                    $"Preview/OLED logo mismatch: {description} at {x},{y}");
        }
        return pixels;
    }

    private static void BatteryLayout()
    {
        using var form=new BatteryStatusDialog(
            new BatteryPreferences(),()=>new BatterySnapshot(),
            ()=>Task.CompletedTask,()=>{});
        if(form.LastReadOverlapsButtonsForTest())
            throw new Exception("Last read timestamp overlaps a battery button");
        string translated=LocalBatteryDeviceNames.DefaultName(
            "Vendor:Compx:example");
        if(translated.Contains("Üretici pili",
            StringComparison.OrdinalIgnoreCase)||
           translated.Contains("Vendor battery",
            StringComparison.OrdinalIgnoreCase))
            throw new Exception("Confusing vendor battery label remains");
    }
}

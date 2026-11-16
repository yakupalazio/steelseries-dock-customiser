using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

internal static class StudioLaunchLogoSelfTests
{
    internal static void Run()
    {
        UniqueInstanceAndRestore();
        DarkIconAndAssetFallback();
        Console.WriteLine("PASS: single interactive studio, signal/restore, "+
            "and dark/opaque game icon fallback.");
    }

    private static void UniqueInstanceAndRestore()
    {
        string key="selftest"+Guid.NewGuid().ToString("N");
        using var primary=StudioSingleInstance.TryAcquire(key)
            ??throw new Exception("First Studio mutex acquisition failed");
        using var second=StudioSingleInstance.TryAcquire(key);
        if(second is not null)
            throw new Exception("Second Studio was allowed to own OLED mutex");
        using var form=new Form{
            StartPosition=FormStartPosition.Manual,
            Location=new Point(-5000,-5000),
            ShowInTaskbar=false
        };
        int calls=0;
        primary.Bind(form,()=>calls++);
        form.Show();
        Application.DoEvents();
        if(!StudioSingleInstance.NotifyExisting(key))
            throw new Exception("Second Studio failed to signal the primary");
        var clock=Stopwatch.StartNew();
        while(calls==0 && clock.ElapsedMilliseconds<1800)
        {
            Application.DoEvents();
            Thread.Sleep(15);
        }
        if(calls!=1)
            throw new Exception("Primary Studio did not receive exactly one restore");
        form.Close();
    }

    private static void DarkIconAndAssetFallback()
    {
        using var dark=new Bitmap(96,96,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(dark))
        {
            g.Clear(Color.FromArgb(13,14,17));
            using var green=new SolidBrush(Color.FromArgb(36,90,48));
            using var blue=new SolidBrush(Color.FromArgb(35,55,75));
            g.FillRectangle(green,20,20,55,55);
            g.FillEllipse(blue,30,32,30,30);
        }
        bool[] bits=GameLogoProvider.ConvertImage(dark);
        int lit=bits.Count(p=>p);
        if(lit is <50 or >800 || GameLogoProvider.IsFilledRectangle(bits))
            throw new Exception("Dark Minecraft-style emblem becomes blank/block");

        // Real candidate selection must not paint an entire opaque PNG as white.
        string? fixture=Environment.GetEnvironmentVariable("DOCK_GAME_EXE");
        if(!string.IsNullOrEmpty(fixture) && File.Exists(fixture))
        {
            var game=new GameSnapshot(
                Path.GetFileNameWithoutExtension(fixture),"synthetic-test",1);
            var resolved=GameLogoProvider.ResolveFromExecutable(game,fixture)
                ??throw new Exception("Actual game icon was not resolved: "+fixture);
            if(resolved.Pixels.Count(p=>p) is <12 or >820)
                throw new Exception("Actual game icon is blank/block: "+fixture);
            Console.WriteLine("GAME_ICON_QA="+Path.GetFileName(fixture)+
                "; source="+resolved.Source+
                "; onPixels="+resolved.Pixels.Count(p=>p));
            string? previewDir=Environment.GetEnvironmentVariable("DOCK_LOGO_PREVIEW_DIR");
            if(!string.IsNullOrWhiteSpace(previewDir))
            {
                Directory.CreateDirectory(previewDir);
                using var preview=new Bitmap(32,32);
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                    preview.SetPixel(x,y,resolved.Pixels[y*32+x]
                        ?Color.White:Color.Black);
                preview.Save(Path.Combine(previewDir,
                    Path.GetFileNameWithoutExtension(fixture)+"-oled.png"),
                    ImageFormat.Png);
            }
        }
    }
}

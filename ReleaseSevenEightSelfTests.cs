using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>RC7.8 offline NFS detection and Valorant icon regressions.
/// Never launches a game, probes memory or writes OLED/GG/user profiles.</summary>
internal static class ReleaseSevenEightSelfTests
{
    internal static void Run()
    {
        EaDirectoryRegistration();
        using(var current=Process.GetCurrentProcess())
        {
            string? ownImage=GgGameDetector.ReadOnlyProcessPath(current);
            if(string.IsNullOrWhiteSpace(ownImage)||!File.Exists(ownImage))
                throw new Exception("Read-only game process path query failed");
        }
        ArtworkPrecedence();
        Console.WriteLine("PASS: EA App installed-game directory fallback, "+
            "GG image asset preference for low-detail game EXE, "+
            "unchanged unrelated game and launcher exclusions.");
    }

    private static void EaDirectoryRegistration()
    {
        var entries=new (string Name,string Directory)[]
        {
            ("Need for Speed™ Unbound",
                "c:/programfiles/eagames/needforspeedunbound/"),
            ("Another EA Game","c:/programfiles/eagames/anothergame/")
        };
        string real=@"C:\Program Files\EA Games\Need for Speed Unbound\NeedForSpeedUnbound.exe";
        if(GgGameDetector.DirectoryRegisteredGameName(real,entries)!=
           "Need for Speed™ Unbound"||
           !GgGameDetector.IsInstalledGamePath(real))
            throw new Exception("NFS Unbound EA App directory is not recognized");

        if(GgGameDetector.DirectoryRegisteredGameName(
           @"C:\Program Files\EA Games\Need for Speed Unbound\NeedForSpeedUnboundTrial.exe",
           entries)!="Need for Speed™ Unbound")
            throw new Exception("Legitimate EA trial game executable not matched");
        foreach(string invalid in new[]{
            @"C:\Program Files\EA Games\Need for Speed Unbound\EADesktop.exe",
            @"C:\Program Files\EA Games\Other Game\NeedForSpeedUnbound.exe",
            @"C:\Windows\System32\NeedForSpeedUnbound.exe",
            @"C:\Program Files\EA Games\Need for Speed Unbound\NeedForSpeedUnbound.txt"
        })
            if(GgGameDetector.DirectoryRegisteredGameName(invalid,entries)!=null)
                throw new Exception("False positive EA game: "+invalid);
    }

    private static void ArtworkPrecedence()
    {
        using var symbol=new Bitmap(64,64);
        using(var g=Graphics.FromImage(symbol))
        {
            g.Clear(Color.FromArgb(222,54,62));
            using var bright=new SolidBrush(Color.White);
            g.FillPolygon(bright,[
                new Point(10,18),new Point(20,18),
                new Point(30,32),new Point(48,10),
                new Point(54,10),new Point(30,46)
            ]);
        }
        var bits=GameLogoProvider.ConvertImage(symbol);
        if(bits.Count(p=>p) is <12 or >800||
           GameLogoProvider.IsFilledRectangle(bits))
            throw new Exception("Bright symbol on red game badge disappeared");

        // Optional real installed game fixtures; tests only the app's own
        // read-only SQLite/path extraction (not anti-cheat or running games).
        var valorant=Environment.GetEnvironmentVariable("DOCK_VALORANT_EXE");
        if(!string.IsNullOrWhiteSpace(valorant)&&File.Exists(valorant))
        {
            var sample=new GameSnapshot("VALORANT",
                Path.GetFileNameWithoutExtension(valorant),int.MaxValue);
            var logo=GameLogoProvider.Resolve(sample);
            if(logo is null ||logo.Pixels.Count(p=>p)<12||
               GameLogoProvider.IsFilledRectangle(logo.Pixels)||
               !logo.Source.Contains("GG",StringComparison.Ordinal))
                throw new Exception("Valorant official local GG logo unavailable");
            Console.WriteLine("RC78_REAL_VALORANT="+logo.Source+
                "; on="+logo.Pixels.Count(p=>p));
            WritePreview("valorant",logo.Pixels);
        }
        var unbound=Environment.GetEnvironmentVariable("DOCK_NFS_UNBOUND_EXE");
        if(!string.IsNullOrWhiteSpace(unbound)&&File.Exists(unbound))
        {
            var registered=GgGameDetector.DirectoryRegisteredGameName(
                unbound,[
                    ("Need for Speed™ Unbound",
                        "c:/programfiles/eagames/needforspeedunbound/")
                ]);
            if(registered is null)
                throw new Exception("Actual NFS Unbound executable not mapped");
            var logo=GameLogoProvider.Resolve(
                new GameSnapshot(registered,
                    Path.GetFileNameWithoutExtension(unbound),int.MaxValue));
            if(logo is null||logo.Pixels.Count(p=>p)<12||
               GameLogoProvider.IsFilledRectangle(logo.Pixels))
                throw new Exception("NFS Unbound GG image fallback empty");
            Console.WriteLine("RC78_REAL_NFS="+logo.Source+
                "; on="+logo.Pixels.Count(p=>p));
            WritePreview("nfs-unbound",logo.Pixels);
        }
    }

    private static void WritePreview(string name,bool[] pixels)
    {
        string? folder=Environment.GetEnvironmentVariable(
            "DOCK_RC78_PREVIEW_DIR");
        if(string.IsNullOrWhiteSpace(folder))return;
        Directory.CreateDirectory(folder);
        using var frame=new Bitmap(32,32);
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            frame.SetPixel(x,y,pixels[y*32+x]?Color.White:Color.Black);
        frame.Save(Path.Combine(folder,name+"-oled.png"));
    }
}

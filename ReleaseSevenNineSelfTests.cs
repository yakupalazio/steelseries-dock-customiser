using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// RC7.9: a GG uninstalled flag is not sufficient evidence a game is absent.
/// Require live process identity and the original GG installation directory.
/// All tests are read-only; they never start games or alter player assets.
/// </summary>
internal static class ReleaseSevenNineSelfTests
{
    internal static void Run()
    {
        StaleRegistrationSafety();
        RealAmongUsFixture();
        Console.WriteLine("PASS: GG stale-installed Among Us recognition "+
            "with strict live EXE directory match, filename whitespace "+
            "normalization, and no false game/launcher detection.");
    }

    private static void StaleRegistrationSafety()
    {
        string current=Environment.ProcessPath??
            throw new Exception("No self-test process EXE");
        if(!File.Exists(current))
            throw new Exception("Self-test process EXE missing");
        string folder=Path.GetDirectoryName(current)!;
        string name=Path.GetFileNameWithoutExtension(current);
        string ext=Path.GetExtension(current);
        string spaced=name.Insert(Math.Max(1,name.Length/2)," ")+ext;
        string registered=Path.Combine(folder,spaced);
        if(!GgGameDetector.StaleRegistrationMatchesActual(
            current,registered))
            throw new Exception("Existing spaced GG basename was not recovered");
        foreach(string invalid in new[]{
            Path.Combine(folder,"UnrelatedApp.exe"),
            Path.Combine(folder,"Other",spaced),
            Path.Combine(folder+"2",spaced),
            Path.Combine(folder,"AnotherLauncher.exe")
        })
        {
            if(GgGameDetector.StaleRegistrationMatchesActual(
                current,invalid))
                throw new Exception("Stale GG false positive "+invalid);
        }
        if(GgGameDetector.StaleRegistrationMatchesActual(
               @"C:\this\does\not\exist\Among Us.exe",
               @"C:\this\does\not\exist\AmongUs.exe")||
           GgGameDetector.StaleRegistrationMatchesActual(null,registered)||
           GgGameDetector.StaleRegistrationMatchesActual(
               current,"")||
           GgGameDetector.StaleRegistrationMatchesActual(
               current,Path.Combine(folder,name+".dll")))
            throw new Exception("Missing or non-executable GG match accepted");
    }

    private static void RealAmongUsFixture()
    {
        string? actual=Environment.GetEnvironmentVariable(
            "DOCK_AMONG_US_EXE");
        if(string.IsNullOrWhiteSpace(actual))return;
        if(!File.Exists(actual))throw new Exception(
            "Opt-in Among Us EXE not found: "+actual);
        string? name=GgGameDetector.StaleRegisteredNameForQa(actual);
        if(name is null || !string.Equals(name,"Among Us",
            StringComparison.OrdinalIgnoreCase))
            throw new Exception("Live GG uninstalled=1 entry not recovered");
        var game=new GameSnapshot(name,
            Path.GetFileNameWithoutExtension(actual),int.MaxValue);
        GameLogoSnapshot? result=GameLogoProvider.Resolve(game);
        if(result is null||result.Pixels.Count(x=>x)<12||
           GameLogoProvider.IsFilledRectangle(result.Pixels))
            throw new Exception("Among Us recognized but logo is empty");
        Console.WriteLine("RC79_REAL_AMONG_US="+result.Source+
            "; pixels="+result.Pixels.Count(x=>x));

        string? output=Environment.GetEnvironmentVariable(
            "DOCK_RC79_PREVIEW_DIR");
        if(string.IsNullOrWhiteSpace(output))return;
        Directory.CreateDirectory(output);
        using var image=new Bitmap(32,32);
        for(int y=0;y<32;y++)
        for(int x=0;x<32;x++)
            image.SetPixel(x,y,result.Pixels[y*32+x]
                ?Color.White:Color.Black);
        image.Save(Path.Combine(output,"among-us-oled.png"),
            ImageFormat.Png);
    }
}

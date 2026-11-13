using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>Repeatable offscreen renderer + transition stress smoke.
/// No windows shown, no external service, hardware, GitHub or user settings.</summary>
internal static class PerformanceSmoke
{
    internal static int Run()
    {
        const int frames=400,transitionFrames=120;
        var modes=new[]{ScreenMode.Media,ScreenMode.Game,ScreenMode.Idle};
        var layouts=modes.Select(StudioProject.Defaults).ToArray();
        // Add visible text to measure the common OLED drawing path.
        layouts[0].Widgets.Add(new DockWidget{
            Kind="Track",Text="PREVIEW PERF",X=20,Y=20,TextWidth=75});
        var benchmark=Stopwatch.StartNew();
        Bitmap? previous=null;
        for(int i=0;i<frames;i++)
        {
            using var current=OledRenderer.Render(layouts[i%3],
                i%3==0,true,false,
                protectHeader:true,layoutMode:modes[i%3]);
            if(current.Width!=128||current.Height!=64)
                throw new Exception("Frame geometry changed during stress test");
            previous?.Dispose();previous=(Bitmap)current.Clone();
        }
        benchmark.Stop();
        using var first=previous??new Bitmap(128,64);
        using var second=new Bitmap(128,64);
        using(var g=Graphics.FromImage(second))g.Clear(Color.White);
        var clock=Stopwatch.StartNew();
        for(int i=0;i<transitionFrames;i++)
        {
            using var frame=OledModeTransition.Compose(
                first,second,(i%9)/8.0,
                OledModeTransition.Effects[i%
                    (OledModeTransition.Effects.Length-1)],true);
            if(frame.GetPixel(0,0).ToArgb()!=second.GetPixel(0,0).ToArgb())
                throw new Exception("OEM header changed during stress test");
        }
        clock.Stop();
        Console.WriteLine($"PASS: {frames} OLED raster frames "+
            $"({benchmark.Elapsed.TotalMilliseconds/frames:F2} ms avg), "+
            $"{transitionFrames} transition frames "+
            $"({clock.Elapsed.TotalMilliseconds/transitionFrames:F2} ms avg).");
        // This is a loose smoke limit, not a guarantee about actual HID FPS.
        if(benchmark.Elapsed.TotalSeconds>18 || clock.Elapsed.TotalSeconds>18)
            throw new Exception("Severe renderer regression detected");
        return 0;
    }
}

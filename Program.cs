using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        var args=Environment.GetCommandLineArgs().Skip(1).ToArray();
        if(args.Contains("--battery-scan") || args.Contains("--battery-scan-file"))
        {
            var snapshot=BatterySources.PollAsync().GetAwaiter().GetResult();
            var lines=BatterySources.Kinds.Select(kind =>
            {
                var b=snapshot[kind];
                return $"{kind}: {b.Display} | {b.Source} | {b.Detail}";
            }).ToArray();
            foreach(var line in lines) Console.WriteLine(line);
            var at=Array.IndexOf(args,"--battery-scan-file");
            if(at>=0 && at+1<args.Length)
                System.IO.File.WriteAllLines(args[at+1],lines);
            return;
        }
        if(args.Contains("--battery-preview"))
        {
            var at=Array.IndexOf(args,"--battery-preview");
            var filename=at>=0 && at+1<args.Length ? args[at+1] : "battery-live-preview.png";
            using var image=OledRenderer.Render(LayoutDefaults.Create(),true,true,true,BatterySources.PollAsync().GetAwaiter().GetResult());
            image.Save(filename,System.Drawing.Imaging.ImageFormat.Png);
            return;
        }
        if(args.Contains("--self-test"))
        {
            var at=Array.IndexOf(args,"--demo-output");
            var export=at>=0 && at+1<args.Length ? args[at+1] : null;
            Environment.ExitCode=SelfTests.Run(export);
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new PixelStudioForm());
    }
}

public sealed class DockWidget
{
    public string Kind { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; } = 17;
    public int FontSize { get; set; } = 10;
    public string Text { get; set; } = "HELLO";
    public bool Visible { get; set; } = true;
    public override string ToString() => $"{Kind}  ({X}, {Y})";
}

public sealed class LayoutState
{
    public bool[] Pixels { get; set; } = new bool[128 * 64];
    public List<DockWidget> Widgets { get; set; } = new();

    public LayoutState DeepClone() =>
        JsonSerializer.Deserialize<LayoutState>(JsonSerializer.Serialize(this)) ?? new LayoutState();
}

internal static class LayoutDefaults
{
    public static LayoutState Create() => new()
    {
        Widgets = new List<DockWidget>
        {
            new() { Kind="Source", X=3, Y=18 },
            new() { Kind="Track", X=22, Y=19, FontSize=9 },
            new() { Kind="Artist", X=22, Y=30, FontSize=9 },
            new() { Kind="Progress", X=22, Y=42 },
            new() { Kind="Clock", X=35, Y=19, FontSize=20 },
            new() { Kind="Date", X=33, Y=39, FontSize=9 },
            new() { Kind="Mouse", X=2, Y=53 },
            new() { Kind="Phone", X=45, Y=53 },
            new() { Kind="DualSense", X=88, Y=53 }
        }
    };
}

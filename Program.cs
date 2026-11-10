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
        if(args.Contains("--media-preview-file"))
        {
            int at=Array.IndexOf(args,"--media-preview-file");
            if(at+1>=args.Length)return;
            string output=args[at+1];
            var playback=MediaSessionProvider.PollAsync().GetAwaiter().GetResult();
            using var rendered=OledRenderer.Render(LayoutDefaults.Create(),playback.IsPlaying,true,
                playback.IsSpotify,BatterySources.PollAsync().GetAwaiter().GetResult(),playback);
            rendered.Save(output,System.Drawing.Imaging.ImageFormat.Png);
            System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(output,".txt"),
                $"Playing={playback.IsPlaying}; Spotify={playback.IsSpotify}; Title={playback.Title}; Artist={playback.Artist}; Progress={playback.ProgressPercent}%");
            return;
        }
        if(args.Contains("--game-scan-file"))
        {
            int pos=Array.IndexOf(args,"--game-scan-file");
            if(pos>=0 && pos+1<args.Length)
            {
                var catalog=GgGameDetector.LoadGgLibrary();
                var active=GgGameDetector.Poll();
                var lines=new[]{
                    "GG Library entries="+catalog.Count,
                    "Includes VALORANT="+catalog.Any(g=>g.Name.Equals("VALORANT",
                        StringComparison.OrdinalIgnoreCase)),
                    "Running game="+(active.IsRunning?active.Name:"None"),
                    "Detector source=read-only GG library_scan + Windows process list"
                };
                System.IO.File.WriteAllLines(args[pos+1],lines);
            }
            return;
        }
        if(args.Contains("--phone-recovery-test-file"))
        {
            int pos=Array.IndexOf(args,"--phone-recovery-test-file");
            if(pos>=0 && pos+1<args.Length)
            {
                var first=PhoneLinkBatteryProvider.TryRead();
                string prior=PhoneLinkBatteryProvider.LastDiagnostic;
                var second=PhoneLinkBatteryProvider.TryReadAsync(true)
                    .GetAwaiter().GetResult();
                var lines=new[]{
                    "Without opening: "+(first?.Display??"--%")+" | "+prior,
                    "After optional Phone Link minimize: "+(second?.Display??"--%")+
                        " | "+PhoneLinkBatteryProvider.LastDiagnostic,
                    "This method uses only Phone Link UI Automation, not a background API."
                };
                System.IO.File.WriteAllLines(args[pos+1],lines);
            }
            return;
        }
        if(args.Contains("--mini-player-preview-file"))
        {
            var at=Array.IndexOf(args,"--mini-player-preview-file");
            if(at>=0 && at+1<args.Length)
            {
                var location=args[at+1];
                var design=StudioProject.Defaults(ScreenMode.Game);
                design.Widgets.Add(new DockWidget{
                    Kind="GameMiniPlayer",X=4,Y=38,FontSize=7,
                    StrokeWidth=1,MiniWidth=88
                });
                var game=new GameSnapshot("VALORANT","valorant",1);
                var song=new MediaSnapshot(true,true,
                    "IN THE END","LINKIN PARK","Spotify",
                    TimeSpan.FromSeconds(48),TimeSpan.FromSeconds(210),
                    DateTimeOffset.UtcNow);
                using(var playing=OledRenderer.Render(design,false,true,
                    false,null,song,game,protectHeader:true,layoutMode:ScreenMode.Game))
                    playing.Save(location,System.Drawing.Imaging.ImageFormat.Png);
                using(var paused=OledRenderer.Render(design,false,true,
                    false,null,MediaSnapshot.Empty,game,protectHeader:true,
                    layoutMode:ScreenMode.Game))
                    paused.Save(System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(location)!,
                        System.IO.Path.GetFileNameWithoutExtension(location)+"-paused.png"),
                        System.Drawing.Imaging.ImageFormat.Png);
            }
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
    public int StrokeWidth { get; set; } = 1;
    public int MiniWidth { get; set; } = 88;
    public string Format { get; set; } = "";
    public string Text { get; set; } = "HELLO";
    public bool Visible { get; set; } = true;
    public override string ToString() => $"{(Kind=="GameMiniPlayer"?"Mini Player (Oyun)":Kind)}  ({X}, {Y})";
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

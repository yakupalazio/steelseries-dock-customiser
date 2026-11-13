using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Release-candidate invariants: 1x normal widget / mode, four repeatable
/// types, category/mode filtering and uploaded Dynamic Island-like lyric
/// motion. No media downloads, hardware writes or user preferences touched.
/// </summary>
internal static class ReleaseCandidateSelfTests
{
    internal static void Run()
    {
        var allKinds=WidgetCatalog.Categories
            .SelectMany(c=>WidgetCatalog.ForCategory(c.Key)).ToArray();
        if(allKinds.Length!=27||
           allKinds.Distinct(StringComparer.Ordinal).Count()!=27)
            throw new Exception("Widget catalog is incomplete or duplicated");
        var multiple=new[]{"Clock","AnimatedGif","AudioVisualizer",
            "Custom text"};
        foreach(var name in multiple)
            if(!WidgetCatalog.AllowsMultiple(name))
                throw new Exception("Repeatable widget blocked: "+name);
        foreach(var name in WidgetCatalog.Categories
            .SelectMany(c=>WidgetCatalog.ForCategory(c.Key)).Distinct())
            if(!multiple.Contains(name)&&WidgetCatalog.AllowsMultiple(name))
                throw new Exception("Unexpected repeatable widget: "+name);

        foreach(var name in new[]{"Lyrics","AudioVisualizer","Track",
            "Artist","Source","Progress","MediaClock"})
        {
            if(!StudioProject.IsAllowed(ScreenMode.Media,name) ||
               StudioProject.IsAllowed(ScreenMode.Game,name) ||
               StudioProject.IsAllowed(ScreenMode.Idle,name))
                throw new Exception("Media widget leaked into other mode: "+name);
        }
        if(!StudioProject.IsAllowed(ScreenMode.Game,"GameMiniPlayer") ||
           StudioProject.IsAllowed(ScreenMode.Media,"GameTitle"))
            throw new Exception("Dedicated game widget classification changed");

        var input=new LayoutState{Widgets=[
            new(){Kind="Headset",X=3,Y=12},
            new(){Kind="Headset",X=18,Y=25},
            new(){Kind="Lyrics",X=3,Y=35},
            new(){Kind="Lyrics",X=8,Y=43},
            new(){Kind="AnimatedGif"},
            new(){Kind="AnimatedGif"},
            new(){Kind="Custom text"},
            new(){Kind="Custom text"},
            new(){Kind="Clock"},
            new(){Kind="Clock"},
            new(){Kind="AudioVisualizer"},
            new(){Kind="AudioVisualizer"}
        ]};
        StudioProject.Sanitize(input,ScreenMode.Media,true);
        if(input.Widgets.Count(w=>w.Kind=="Headset")!=1 ||
           input.Widgets.First(w=>w.Kind=="Headset").X!=3 ||
           input.Widgets.Count(w=>w.Kind=="Lyrics")!=1 ||
           multiple.Any(m=>input.Widgets.Count(w=>w.Kind==m)!=2))
            throw new Exception("Singleton/opt-in multi-instance sanitation failed");

        var oldDuplicate=new LayoutState{Widgets=[
            new(){Kind="Phone",Visible=false,X=2,Y=14},
            new(){Kind="Phone",Visible=true,X=49,Y=42}
        ]};
        StudioProject.Sanitize(oldDuplicate,ScreenMode.Media,true);
        if(oldDuplicate.Widgets.Count!=1||
           !oldDuplicate.Widgets[0].Visible||
           oldDuplicate.Widgets[0].X!=49)
            throw new Exception(
                "Legacy duplicate migration hid user's visible widget");

        if(WidgetCatalog.CanAdd(ScreenMode.Media,"Headset",input.Widgets) ||
           WidgetCatalog.CanAdd(ScreenMode.Game,"Lyrics",input.Widgets) ||
           WidgetCatalog.CanAdd(ScreenMode.Idle,"AudioVisualizer",input.Widgets) ||
           !WidgetCatalog.CanAdd(ScreenMode.Media,"AnimatedGif",input.Widgets) ||
           !WidgetCatalog.CanAdd(ScreenMode.Media,"Clock",input.Widgets))
            throw new Exception("Picker duplicate/mode restrictions failed");

        if(WidgetCatalog.Available(ScreenMode.Media,"Battery",input.Widgets)
            .Contains("Headset") ||
           WidgetCatalog.Available(ScreenMode.Game,"Media",[])
            .Contains("Lyrics") ||
           WidgetCatalog.AvailableCategories(ScreenMode.Idle,[])
            .Any(c=>c.Key=="Media"))
            throw new Exception("Picker shows used or media-only widget");

        var lines=new List<SyncedLyricLine>{
            new(TimeSpan.FromMilliseconds(500),
                "GOT THESE QUESTIONS ALWAYS RUNNING THROUGH MY HEAD"),
            new(TimeSpan.FromMilliseconds(2500),
                "SO MANY THINGS THAT I WOULD LIKE TO UNDERSTAND"),
            new(TimeSpan.FromMilliseconds(6200),
                "WE ARE BORN TO DIE AND WE ALL DIE TO LIVE")
        };
        var a=IslandLyricsMotion.Evaluate(lines,
            TimeSpan.FromMilliseconds(850),74);
        var b=IslandLyricsMotion.Evaluate(lines,
            TimeSpan.FromMilliseconds(1900),74);
        if(a.CurrentIndex!=0||b.CurrentIndex!=0 ||
           b.CurrentScrollPx<=a.CurrentScrollPx)
            throw new Exception("Lyric text must scroll smoothly LEFT");
        var start=IslandLyricsMotion.Evaluate(lines,
            TimeSpan.FromMilliseconds(2500),74);
        var middle=IslandLyricsMotion.Evaluate(lines,
            TimeSpan.FromMilliseconds(2670),74);
        var finished=IslandLyricsMotion.Evaluate(lines,
            TimeSpan.FromMilliseconds(2880),74);
        if(start.CurrentIndex!=1||start.PreviousIndex!=0 ||
           start.TransitionPx!=0||!middle.Transitioning ||
           middle.TransitionPx<2||middle.TransitionPx>7 ||
           finished.CurrentIndex!=1||finished.Transitioning ||
           finished.TransitionPx!=IslandLyricsMotion.LineHeight)
            throw new Exception("Old lyric must go UP and next come from BELOW");

        string track="Synthetic";string artist="Synthetic artist";
        var baseline=new MediaSnapshot(true,true,track,artist,
            "SyntheticSpotify",TimeSpan.Zero,TimeSpan.FromSeconds(217),
            DateTimeOffset.Now);
        var snapshot=new SyncedLyricsSnapshot(
            SyncedLyricsProvider.Key(baseline),lines,"in-memory test");
        var lyricWidget=new DockWidget{
            Kind="Lyrics",X=4,Y=28,WidgetWidth=120,FontSize=7};
        var layout=new LayoutState{Widgets=[lyricWidget]};
        var samples=new List<bool[]>();
        foreach(int ms in new[]{1200,2000,2520,2650,2880})
        {
            var playing=baseline with{
                Position=TimeSpan.FromMilliseconds(ms),
                SampledAt=DateTimeOffset.Now
            };
            using var screen=OledRenderer.Render(layout,true,true,true,
                playback:playing,protectHeader:true,
                layoutMode:ScreenMode.Media,lyrics:snapshot);
            var pixels=new bool[128*64];
            for(int y=0;y<64;y++)
            for(int x=0;x<128;x++)
                pixels[y*128+x]=screen.GetPixel(x,y).R>150;
            samples.Add(pixels);
            if(!Enumerable.Range(lyricWidget.Y,8)
                .Any(y=>Enumerable.Range(lyricWidget.X,lyricWidget.WidgetWidth)
                    .Any(x=>pixels[y*128+x])))
                throw new Exception("Island lyric frame renders blank");
            if(Enumerable.Range(0,10)
                .Any(y=>Enumerable.Range(0,128).Any(x=>pixels[y*128+x])))
                throw new Exception("Lyric animation entered protected OEM rows");
        }
        // The compact media icon remains pinned; the old duration prefix
        // is gone and the lyric viewport now starts 12px from the left.
        for(int i=1;i<samples.Count;i++)
        for(int y=28;y<35;y++)
        for(int x=4;x<16;x++)
            if(samples[0][y*128+x]!=samples[i][y*128+x])
                throw new Exception("Media icon moved with lyrics");
        if(!Enumerable.Range(30,16).Any(x=>samples[0][32*128+x]))
            throw new Exception("Lyrics did not reclaim the duration area");
        if(samples[0].SequenceEqual(samples[1]) ||
           samples[2].SequenceEqual(samples[3]) ||
           samples[3].SequenceEqual(samples[4]))
            throw new Exception("Lyric scroll/vertical transition has no animation");

        Console.WriteLine(
            "PASS: categorized singletons/multi-widgets, media-only modes, "+
            "Dynamic Island horizontal + vertical lyric animation.");
    }

    internal static void RenderIslandExamples(string folder)
    {
        Directory.CreateDirectory(folder);
        var lines=SyncedLyricsProvider.Parse(
            "[00:00.50]GOT THESE QUESTIONS ALWAYS RUNNING THROUGH MY HEAD\n"+
            "[00:02.50]SO MANY THINGS THAT I WOULD LIKE TO UNDERSTAND\n"+
            "[00:06.20]WE ARE BORN TO DIE AND WE ALL DIE TO LIVE");
        var template=new MediaSnapshot(true,true,"Synthetic","Demo",
            "Spotify",TimeSpan.Zero,TimeSpan.FromSeconds(217),DateTimeOffset.Now);
        var synced=new SyncedLyricsSnapshot(SyncedLyricsProvider.Key(template),
            lines,"local");
        var design=new LayoutState{Widgets=[new DockWidget{
            Kind="Lyrics",X=4,Y=28,WidgetWidth=120}]};
        foreach(int ms in new[]{750,1500,2100,2500,2600,2700,2840,3600})
        {
            var song=template with{
                Position=TimeSpan.FromMilliseconds(ms),
                SampledAt=DateTimeOffset.Now
            };
            using var image=OledRenderer.Render(design,true,true,true,
                playback:song,protectHeader:true,
                layoutMode:ScreenMode.Media,lyrics:synced);
            image.Save(Path.Combine(folder,$"island-{ms:D4}.png"),
                ImageFormat.Png);
        }
    }
}

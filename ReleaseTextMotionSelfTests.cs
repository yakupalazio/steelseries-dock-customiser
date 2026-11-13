using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Deterministic OLED-only checks: no downloaded lyrics, audio capture,
/// physical dock output, Bluetooth access or user settings writes.
/// </summary>
internal static class ReleaseTextMotionSelfTests
{
    private static bool Lit(Bitmap frame,int x,int y)=>
        frame.GetPixel(x,y).R>160;
    private static bool AnyLit(Bitmap frame,int x0,int x1,int y0,int y1)
    {
        for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
            if(Lit(frame,x,y))return true;
        return false;
    }

    internal static void Run()
    {
        var text=new DockWidget{
            Kind="Custom text",Text="THE WIDEST LONGEST STRING",
            X=6,Y=25,FontSize=7,StrokeWidth=3,TextWidth=24
        };
        var stored=new LayoutState{Widgets=[text]}.DeepClone();
        StudioProject.Sanitize(stored,ScreenMode.Idle,true);
        if(stored.Widgets[0].TextWidth!=24||
           stored.Widgets[0].StrokeWidth!=3||
           OledRenderer.GetBounds(text).Width!=24||
           !OledRenderer.NeedsMarquee(text,text.Text))
            throw new Exception("Text width / weight persistence or bounds");
        using(var clipped=OledRenderer.Render(stored,false,true,false,
            protectHeader:true,layoutMode:ScreenMode.Idle))
        {
            if(!AnyLit(clipped,6,30,25,38)||
               AnyLit(clipped,30,128,25,42))
                throw new Exception("Wide text leaked outside 24px user box");
        }
        if(OledRenderer.MarqueeOffsetMs(220,70,1000)!=28||
           OledRenderer.MarqueeOffsetMs(220,70,1500)!=42||
           OledRenderer.MarqueeOffsetMs(220,70,1060)<=28||
           OledRenderer.MarqueeOffsetMs(30,70,5000)!=0)
            throw new Exception("Fast millisecond marquee is not smooth");

        var wrapped=IslandLyricsMotion.WrapRows(
            "FIRST WORDS SECOND WORDS THIRD WORDS LAST WORDS",78,3);
        if(wrapped.Length!=3||
           wrapped[0].Length==0||wrapped[1].Length==0||
           wrapped[2].Length==0||
           !wrapped[2].Contains("LAST"))
            throw new Exception("Multiline lyrics lost words");

        var song=new MediaSnapshot(true,true,"Synthetic track",
            "Synthetic artist","SyntheticSpotify",TimeSpan.FromMilliseconds(1750),
            TimeSpan.FromSeconds(30),DateTimeOffset.Now);
        var lines=SyncedLyricsProvider.Parse(
            "[00:00.100]FIRST VERY LONG LINE TO WRAP OVER MANY MULTIPLE LINES");
        var synced=new SyncedLyricsSnapshot(
            SyncedLyricsProvider.Key(song),lines,"synthetic");
        var lyric=new DockWidget{
            Kind="Lyrics",X=5,Y=18,WidgetWidth=116,LyricLines=3
        };
        StudioProject.Sanitize(new LayoutState{Widgets=[lyric]},
            ScreenMode.Media,true);
        using(var three=OledRenderer.Render(new LayoutState{
            Widgets=[lyric]},true,true,true,playback:song,
            protectHeader:true,layoutMode:ScreenMode.Media,lyrics:synced))
        {
            if(!AnyLit(three,18,121,38,46))
                throw new Exception("Third lyric row did not render");
        }
        lyric.LyricLines=2;
        using(var two=OledRenderer.Render(new LayoutState{
            Widgets=[lyric]},true,true,true,playback:song,
            protectHeader:true,layoutMode:ScreenMode.Media,lyrics:synced))
        {
            if(!AnyLit(two,18,121,30,38))
                throw new Exception("Second lyric row did not render");
        }

        var cues=SyncedLyricsProvider.Parse(
            "[00:01.000]EARLIER WORDS\n[00:02.000]LATER WORDS");
        var shifted=new SyncedLyricsSnapshot(
            SyncedLyricsProvider.Key(song),cues,"synthetic");
        var nearBoundary=song with{Position=TimeSpan.FromMilliseconds(1850),
            SampledAt=DateTimeOffset.Now};
        lyric.LyricLines=1;
        lyric.LyricOffsetMs=400;
        using var delayed=OledRenderer.Render(new LayoutState{
            Widgets=[lyric]},true,true,true,playback:nearBoundary,
            protectHeader:true,layoutMode:ScreenMode.Media,lyrics:shifted);
        lyric.LyricOffsetMs=-400;
        using var advanced=OledRenderer.Render(new LayoutState{
            Widgets=[lyric]},true,true,true,playback:nearBoundary,
            protectHeader:true,layoutMode:ScreenMode.Media,lyrics:shifted);
        bool differs=false;
        for(int y=18;y<34;y++)for(int x=18;x<121;x++)
            if(Lit(delayed,x,y)!=Lit(advanced,x,y))differs=true;
        if(!differs||SyncedLyricsProvider.AtTime(cues,
            TimeSpan.FromMilliseconds(1850),-400)!="LATER WORDS"||
            SyncedLyricsProvider.AtTime(cues,
            TimeSpan.FromMilliseconds(1850),400)!="EARLIER WORDS")
            throw new Exception("Positive/negative 400ms lyric offset ineffective");

        var instrumental=SyncedLyricsProvider.Parse(
            "[00:00.000]INTRO WORDS\n[00:03.000]\n[00:07.000]WORDS RETURN");
        if(instrumental.Count!=3||
           !SyncedLyricsProvider.InstrumentalAt(instrumental,
               TimeSpan.FromMilliseconds(4000))||
           SyncedLyricsProvider.AtTime(instrumental,
               TimeSpan.FromMilliseconds(4000),0)!=""||
           SyncedLyricsProvider.AtTime(instrumental,
               TimeSpan.FromMilliseconds(7200),0)!="WORDS RETURN")
            throw new Exception("Instrumental LRC segment shifted lyric timings");
        var instrumentalTrack=song with{
            Position=TimeSpan.FromMilliseconds(4000),
            SampledAt=DateTimeOffset.Now};
        var instrumentalSnapshot=new SyncedLyricsSnapshot(
            SyncedLyricsProvider.Key(song),instrumental,"synthetic");
        lyric.LyricOffsetMs=0;
        using(var note=OledRenderer.Render(new LayoutState{
            Widgets=[lyric]},true,true,true,playback:instrumentalTrack,
            protectHeader:true,layoutMode:ScreenMode.Media,
            lyrics:instrumentalSnapshot))
        {
            // Note's leading stem in the lyric strip: X+12+2+5, Y+4.
            if(!Lit(note,lyric.X+19,lyric.Y+4))
                throw new Exception("Missing instrumental music-note glyph");
        }
        Console.WriteLine(
            "PASS: text width/weight, smooth ms marquee, 2/3 lyrics rows, "+
            "real +/-400ms offsets and instrumental notes.");
    }
}

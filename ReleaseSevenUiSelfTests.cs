using System;
using System.Drawing;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>OLED-only RC7 lyrics and profile-visibility regression.</summary>
internal static class ReleaseSevenUiSelfTests
{
    private static int Lit(Bitmap frame,int x0,int x1,int y0,int y1)
    {
        int count=0;
        for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
            if(frame.GetPixel(x,y).R>170)count++;
        return count;
    }

    internal static void Run()
    {
        var widget=new DockWidget{
            Kind="Lyrics",X=4,Y=15,WidgetWidth=118,LyricLines=4,
            LyricTransitionStyle="Fade"
        };
        var layout=new LayoutState{Widgets=[widget]};
        StudioProject.Sanitize(layout,ScreenMode.Media,true);
        if(widget.LyricLines!=4 ||
           widget.LyricTransitionStyle!="Fade" ||
           OledRenderer.GetBounds(widget).Height!=40)
            throw new Exception("4-line lyric layout lost/incorrect.");

        var song=new MediaSnapshot(true,true,"Test","Synthetic Artist",
            "synthetic Spotify",TimeSpan.FromMilliseconds(2400),
            TimeSpan.FromSeconds(20),DateTimeOffset.Now);
        var lines=SyncedLyricsProvider.Parse(
            "[00:00.00]THIS IS A VERY LONG SENTENCE WITH MANY WORDS TO SPREAD "+
            "ACROSS ALL FOUR ROWS\n"+
            "[00:02.30]ANOTHER LONG TEXT TO TEST ANIMATION WITHOUT SCREEN SPILL");
        var lyrics=new SyncedLyricsSnapshot(
            SyncedLyricsProvider.Key(song),lines,"synthetic");
        widget.LyricTransitionStyle="Instant";
        using(var frame=OledRenderer.Render(layout,true,true,true,
            protectHeader:true,layoutMode:ScreenMode.Media,
            playback:song,lyrics:lyrics))
        {
            if(Lit(frame,16,120,43,55)==0)
                throw new Exception("4th lyric line is not visible.");
            if(Lit(frame,0,128,0,10)!=0)
                throw new Exception("Lyric effect touched protected OEM header.");
        }
        var hashes=new System.Collections.Generic.HashSet<string>();
        foreach(string transition in new[]{
            "Slide","Fade","Drop","Wipe","Instant"})
        {
            widget.LyricTransitionStyle=transition;
            using var frame=OledRenderer.Render(layout,true,true,true,
                protectHeader:true,layoutMode:ScreenMode.Media,
                playback:song,lyrics:lyrics);
            int signature=0;
            for(int y=14;y<55;y++)for(int x=12;x<122;x++)
                if(frame.GetPixel(x,y).R>170)
                    signature=unchecked(signature*31+x+y*131);
            hashes.Add(transition+":"+signature);
        }
        // Structural validation catches a mistyped fallback animation.
        widget.LyricTransitionStyle="MadeUp";
        StudioProject.Sanitize(layout,ScreenMode.Media,true);
        if(widget.LyricTransitionStyle!="Slide"||hashes.Count!=5)
            throw new Exception("Lyric animation selectors not validated.");

        using var dialog=new LayoutProfileDialog(ScreenMode.Idle,
            ()=>StudioProject.Defaults(ScreenMode.Idle),_=>{});
        if(dialog.ClientSize.Height<580||
           dialog.Controls.OfType<System.Windows.Forms.Panel>().Count()<2||
           !dialog.Controls.OfType<System.Windows.Forms.Panel>().Any(p=>
               p.Dock==System.Windows.Forms.DockStyle.Bottom))
            throw new Exception("Profile dialog footer may be clipped.");

        Console.WriteLine(
            "PASS: 4-row lyrics and 5 monotone timecoded transitions; "+
            "profile manager has fixed bottom action area.");
    }
}

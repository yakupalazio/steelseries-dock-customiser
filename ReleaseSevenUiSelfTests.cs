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
        var layoutGrid=dialog.Controls.OfType<System.Windows.Forms.TableLayoutPanel>()
            .FirstOrDefault();
        var body=layoutGrid?.Controls.OfType<System.Windows.Forms.Panel>()
            .FirstOrDefault(p=>p.Controls.OfType<System.Windows.Forms.ListBox>().Any());
        var footer=layoutGrid?.Controls.OfType<System.Windows.Forms.Panel>()
            .FirstOrDefault(p=>p.Controls.OfType<System.Windows.Forms.Button>().Any());
        var preview=body?.Controls.OfType<System.Windows.Forms.Panel>()
            .FirstOrDefault(p=>p.Name=="ProfileMiniPreview");
        var use=body?.Controls.OfType<System.Windows.Forms.Button>()
            .FirstOrDefault(b=>b.Text.Contains("Profili uygula",
                StringComparison.OrdinalIgnoreCase));
        var hint=body?.Controls.OfType<System.Windows.Forms.Label>()
            .FirstOrDefault(l=>l.Left==390&&l.Top==439);
        var close=footer?.Controls.OfType<System.Windows.Forms.Button>()
            .FirstOrDefault();
        if(dialog.ClientSize.Height!=555||body is null||!body.AutoScroll||
           footer is null||footer.Height<54||preview is null||
           use is null||hint is null||
           use.Bounds.IntersectsWith(hint.Bounds) ||
           close is null||close.Bottom>footer.Height)
            throw new Exception(
                "Compact profile preview/footer has clipped or overlapping controls.");

        Console.WriteLine(
            "PASS: 4-row lyrics and 5 monotone timecoded transitions; "+
            "profile manager has fixed bottom action area.");
    }
}

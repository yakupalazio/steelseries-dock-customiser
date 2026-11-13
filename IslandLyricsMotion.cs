using System;
using System.Collections.Generic;

namespace SteelseriesDockCustomiser;

/// <summary>
/// OLED-sized interpretation of the user's Windhawk Dynamic Island reference:
/// time/artwork region fixed at left, lyrics move smoothly to the left,
/// then each new line slides upward from below while the old line slides
/// upward and fades out. Pure computation from the real playback clock;
/// seeking and pauses never depend on global animation frame counters.
/// </summary>
internal static class IslandLyricsMotion
{
    internal const int TransitionMs=340;
    internal const int LineHeight=8;

    internal readonly record struct Frame(
        int CurrentIndex,int PreviousIndex,int TransitionPx,
        int CurrentScrollPx,int PreviousScrollPx,float Progress,
        string CurrentText,string PreviousText)
    {
        internal bool Transitioning=>PreviousIndex>=0 && Progress<1;
    }

    internal static Frame Evaluate(IReadOnlyList<SyncedLyricLine> lyrics,
        TimeSpan position,int viewportWidth)
    {
        if(lyrics.Count==0||viewportWidth<1)
            return new(-1,-1,LineHeight,0,0,1,"","");
        long at=Math.Max(0,(long)position.TotalMilliseconds);
        int left=0,right=lyrics.Count-1,index=-1;
        while(left<=right)
        {
            int middle=left+(right-left)/2;
            if(lyrics[middle].At.TotalMilliseconds<=at)
            {
                index=middle;left=middle+1;
            }
            else right=middle-1;
        }
        if(index<0)
            return new(-1,-1,LineHeight,0,0,1,"","");
        var line=lyrics[index];
        long elapsed=Math.Max(0,at-(long)line.At.TotalMilliseconds);
        long next=index+1<lyrics.Count
            ?(long)lyrics[index+1].At.TotalMilliseconds
            :Math.Max(at+700, (long)line.At.TotalMilliseconds+3500);
        long duration=Math.Max(500,next-(long)line.At.TotalMilliseconds);
        int scroll=Scroll(line.Text,viewportWidth,elapsed,duration);
        int previous=index>0&&elapsed<TransitionMs?index-1:-1;
        float progress=previous>=0
            ?Math.Clamp(elapsed/(float)TransitionMs,0f,1f):1f;
        // Smoothstep avoids a hard jump in the first/last animation frame.
        float eased=progress*progress*(3-2*progress);
        int transition=(int)Math.Round(LineHeight*eased);
        int priorScroll=0;
        string prior="";
        if(previous>=0)
        {
            prior=lyrics[previous].Text;
            long oldStart=(long)lyrics[previous].At.TotalMilliseconds;
            long oldDuration=Math.Max(500,(long)line.At.TotalMilliseconds-oldStart);
            priorScroll=Scroll(prior,viewportWidth,oldDuration,oldDuration);
        }
        return new(index,previous,transition,scroll,priorScroll,
            eased,line.Text,prior);
    }

    private static int Scroll(string text,int viewportWidth,
        long elapsed,long duration)
    {
        int width=Math.Max(0,text.Length*6-1);
        int travel=Math.Max(0,width-viewportWidth);
        if(travel==0)return 0;
        // Leave a brief stationary moment on each line (as in the clip),
        // then use fractional milliseconds for smooth 1px movements.
        long durationForMove=Math.Max(360,duration-420);
        double move=Math.Clamp((elapsed-180.0)/durationForMove,0.0,1.0);
        return (int)Math.Round(travel*move);
    }
}

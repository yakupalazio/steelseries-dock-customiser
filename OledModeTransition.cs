using System;
using System.Drawing;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Deterministic, allocation-bounded, 1-bit 128x64 OLED transition effects.
/// Never blends firmware-protected top rows. No timers, hardware I/O or
/// external image processing; caller supplies frame snapshots and phase.
/// </summary>
internal static class OledModeTransition
{
    internal static readonly string[] Effects=
        ["Slide","Fade","Wipe","Pixel Dissolve","Zoom","Instant"];
    internal static string Normalize(string? effect)=>
        Array.IndexOf(Effects,effect)>=0?effect!:"Slide";
    internal static int DurationMs(string? effect,int speed)
    {
        if(effect=="Instant")return 0;
        var baseMs=effect switch{
            "Fade"=>300,"Wipe"=>290,"Pixel Dissolve"=>420,
            "Zoom"=>340,_=>350
        };
        return Math.Clamp((int)Math.Round(baseMs*100.0/
            Math.Clamp(speed,25,200)),80,1800);
    }

    internal static Bitmap Compose(Bitmap previous,Bitmap next,
        double phase,string? effect,bool protectHeader)
    {
        if(previous.Width!=128||previous.Height!=64||
           next.Width!=128||next.Height!=64)
            throw new ArgumentException("OLED transition needs 128x64 frames");
        phase=Math.Clamp(phase,0,1);
        string kind=Normalize(effect);
        if(phase>=1||kind=="Instant")return (Bitmap)next.Clone();
        var result=new Bitmap(128,64);
        int top=protectHeader?10:0;
        // Use the actual target header unmodified, even during Slide.
        for(int y=0;y<top;y++)for(int x=0;x<128;x++)
            result.SetPixel(x,y,next.GetPixel(x,y));
        double ease=phase*phase*(3-2*phase);
        int slide=(int)Math.Round(128*ease);
        for(int y=top;y<64;y++)for(int x=0;x<128;x++)
        {
            bool a=previous.GetPixel(x,y).R>=128;
            bool b=next.GetPixel(x,y).R>=128;
            bool on=kind switch
            {
                "Slide"=>x<128-slide
                    ?previous.GetPixel(x+slide,y).R>=128
                    :next.GetPixel(x-(128-slide),y).R>=128,
                "Wipe"=>x<Math.Round(ease*128)?b:a,
                "Fade"=>Hash(x,y,11)<ease?b:a,
                "Pixel Dissolve"=>Hash(x,y,71)<ease?b:a,
                "Zoom"=>Zoom(previous,next,x,y,ease,top),
                _=>b
            };
            result.SetPixel(x,y,on?Color.White:Color.Black);
        }
        return result;
    }

    private static bool Zoom(Bitmap oldFrame,Bitmap next,int x,int y,
        double phase,int top)
    {
        // Centered outgoing old image narrows; fresh frame replaces its rim.
        double scale=1-.38*phase;
        double cx=63.5,cy=31.5;
        double px=(x-cx)/scale+cx,py=(y-cy)/scale+cy;
        if(px<0||px>=128||py<top||py>=64)
            return next.GetPixel(x,y).R>=128;
        // The old image dissolves progressively at the shrunken boundary.
        return Hash(x,y,37)<phase
            ?next.GetPixel(x,y).R>=128
            :oldFrame.GetPixel((int)px,(int)py).R>=128;
    }

    private static double Hash(int x,int y,int salt)
    {
        unchecked
        {
            uint h=(uint)(x*73856093)^(uint)(y*19349663)^(uint)salt;
            h^=h>>16;h*=0x7feb352d;h^=h>>15;h*=0x846ca68b;h^=h>>16;
            return (h&0xffff)/65535.0;
        }
    }
}

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>Local 128x64 test preview: no game/dock/network calls.</summary>
internal sealed class OledTransitionPreview:Control
{
    private readonly Bitmap first=Make("MEDIA", "♪  ▶  ●"),
        second=Make("GAME", "◈  >  ◈");
    private readonly Stopwatch clock=new();
    private readonly System.Windows.Forms.Timer timer=new(){Interval=16};
    private int duration=350;
    private string effect="Slide";
    internal OledTransitionPreview()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|
            ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        timer.Tick+=(_,_)=>
        {
            if(clock.ElapsedMilliseconds>=duration)timer.Stop();
            Invalidate();
        };
    }
    private static Bitmap Make(string heading,string subtitle)
    {
        var bmp=new Bitmap(128,64);
        using var g=Graphics.FromImage(bmp);
        g.Clear(Color.Black);
        using var title=new Font("Consolas",14,FontStyle.Bold);
        using var detail=new Font("Consolas",9);
        g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.DrawString(heading,title,Brushes.White,12,20);
        g.DrawString(subtitle,detail,Brushes.White,18,42);
        return bmp;
    }
    internal void Play(string selected,int speed)
    {
        effect=OledModeTransition.Normalize(selected);
        duration=Math.Max(1,OledModeTransition.DurationMs(effect,speed));
        clock.Restart();timer.Start();Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);
        double phase=timer.Enabled
            ?Math.Clamp(clock.ElapsedMilliseconds/(double)duration,0,1):1;
        using var image=OledModeTransition.Compose(first,second,
            phase,effect,protectHeader:true);
        e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;
        var dest=new Rectangle(8,5,Math.Max(1,Width-16),Math.Max(1,Height-10));
        e.Graphics.DrawImage(image,dest,
            new Rectangle(0,0,128,64),GraphicsUnit.Pixel);
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing){timer.Stop();timer.Dispose();first.Dispose();second.Dispose();}
        base.Dispose(disposing);
    }
}

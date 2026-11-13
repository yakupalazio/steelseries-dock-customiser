using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// A short, optional 140ms transition between already-existing editor and
/// Settings pages. Both images are local in-memory UI snapshots. No OLED
/// output, user documents, network calls or hardware changes are involved.
/// Keyboard and focus return to the real controls when the overlay ends.
/// </summary>
internal static class UiPageTransition
{
    internal static void Swap(Panel? host,Action change,bool animate=true)
    {
        if(!animate||host is null||host.IsDisposed||!host.IsHandleCreated||
           !host.Visible||host.ClientSize.Width<80||host.ClientSize.Height<80)
        {
            change();
            return;
        }

        // Cancel previous visual overlay when the user quickly switches
        // sections. Real controls have already been updated underneath.
        foreach(var existing in host.Controls.OfType<FadeOverlay>().ToArray())
        {
            host.Controls.Remove(existing);
            existing.Dispose();
        }
        Bitmap? before=null,after=null;
        try
        {
            before=new Bitmap(host.ClientSize.Width,host.ClientSize.Height);
            host.DrawToBitmap(before,new Rectangle(Point.Empty,before.Size));
        }
        catch
        {
            before?.Dispose();
            change();
            return;
        }
        try
        {
            change();
            after=new Bitmap(host.ClientSize.Width,host.ClientSize.Height);
            host.DrawToBitmap(after,new Rectangle(Point.Empty,after.Size));
            // New layout is fully active underneath; this overlay only
            // animates its image, never delaying data changes or hardware.
            var overlay=new FadeOverlay(before,after)
            {
                Dock=DockStyle.Fill,TabStop=false
            };
            before=null;after=null;
            host.Controls.Add(overlay);
            overlay.BringToFront();
            overlay.Begin();
        }
        finally
        {
            before?.Dispose();after?.Dispose();
        }
    }

    internal const int AnimationDurationMs=235;
    private sealed class FadeOverlay:Control
    {
        private readonly Bitmap oldFrame,newFrame;
        private readonly Stopwatch clock=new();
        private readonly System.Windows.Forms.Timer timer=new(){Interval=16};
        internal FadeOverlay(Bitmap oldFrame,Bitmap newFrame)
        {
            this.oldFrame=oldFrame;this.newFrame=newFrame;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|
                ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            timer.Tick+=(_,_)=>
            {
                if(clock.ElapsedMilliseconds>=AnimationDurationMs)
                {
                    timer.Stop();
                    Parent?.Controls.Remove(this);
                    Dispose();
                }
                else Invalidate();
            };
        }
        internal void Begin(){clock.Start();timer.Start();}
        protected override void OnPaint(PaintEventArgs e)
        {
            float phase=Math.Clamp(clock.ElapsedMilliseconds/(float)AnimationDurationMs,0,1);
            // Smoothstep: immediate feel without harsh beginning/end.
            phase=phase*phase*(3-2*phase);
            e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;
            e.Graphics.DrawImage(oldFrame,ClientRectangle);
            using var attr=new ImageAttributes();
            var matrix=new ColorMatrix{Matrix33=phase};
            attr.SetColorMatrix(matrix,ColorMatrixFlag.Default,ColorAdjustType.Bitmap);
            int offset=(int)Math.Round((1-phase)*11);
            var target=new Rectangle(0,offset,Width,Height);
            e.Graphics.DrawImage(newFrame,target,0,0,newFrame.Width,newFrame.Height,
                GraphicsUnit.Pixel,attr);
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                timer.Stop();timer.Dispose();
                oldFrame.Dispose();newFrame.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

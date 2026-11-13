using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>Startup overlay draws on the UI thread; optional provider polling
/// continues asynchronously. The status never blocks an offline device.</summary>
internal sealed class BootOverlay:Panel
{
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly System.Windows.Forms.Timer timer=new(){Interval=30};
    internal string Detail="Profiller ve cihaz servisleri başlatılıyor";
    internal BootOverlay()
    {
        Dock=DockStyle.Fill;
        BackColor=Color.FromArgb(12,18,27);
        SetStyle(ControlStyles.AllPaintingInWmPaint|
            ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);
        timer.Tick+=(_,_)=>Invalidate();
        timer.Start();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        int cx=Width/2,cy=Height/2-30;
        BrandMark.Draw(e.Graphics,new Rectangle(cx-32,cy-83,64,64));
        using var title=new Font("Segoe UI Semibold",21,FontStyle.Bold);
        using var sub=new Font("Segoe UI",10);
        var white=Color.FromArgb(231,242,248);
        TextRenderer.DrawText(e.Graphics,"DOCK CUSTOMISER",title,
            new Rectangle(0,cy-6,Width,44),white,
            TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics,UiLanguage.English
            ?"Loading profiles and device services":Detail,sub,
            new Rectangle(0,cy+43,Width,35),
            Color.FromArgb(146,166,185),
            TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        using var ring=new Pen(Color.FromArgb(40,65,85),4);
        using var accent=new Pen(Color.FromArgb(82,201,239),4)
        {StartCap=LineCap.Round,EndCap=LineCap.Round};
        var bounds=new Rectangle(cx-17,cy+94,34,34);
        e.Graphics.DrawArc(ring,bounds,0,360);
        e.Graphics.DrawArc(accent,bounds,
            (float)(clock.Elapsed.TotalSeconds*210%360),95);
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing){timer.Stop();timer.Dispose();}
        base.Dispose(disposing);
    }
}

/// <summary>Local first-run steps, no telemetry or account requirement.</summary>
internal static class FirstRunGuideState
{
    private sealed class State {public bool Completed {get;set;}}
    internal static string PathName=>Path.Combine(
        Beta5Sandbox.SettingsDirectory,"first-run-guide-v1.json");
    internal static bool Completed()
    {
        try
        {
            return File.Exists(PathName)&&
                JsonSerializer.Deserialize<State>(File.ReadAllText(PathName))
                    ?.Completed==true;
        }
        catch{return false;}
    }
    internal static void MarkCompleted()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
        string temp=PathName+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(new State{Completed=true}));
        File.Move(temp,PathName,true);
    }
}

internal sealed class FirstRunGuide:Control
{
    internal sealed record Step(string Title,string Description,Control? Focus);
    private readonly Step[] steps;
    private readonly Action finished;
    private Bitmap? snapshot;
    private int index;
    private readonly DarkButton next=new(){
        Text="İleri →",Width=110,Height=39,
        BackColor=Color.FromArgb(62,174,215),
        ForeColor=Color.FromArgb(12,18,27)};
    private readonly DarkButton skip=new(){
        Text="Atla",Width=102,Height=39,
        BackColor=Color.FromArgb(37,52,68),
        ForeColor=Color.FromArgb(221,235,247)};
    private readonly Label detail=new(){
        Font=new Font("Segoe UI",10),
        ForeColor=Color.FromArgb(183,206,223),
        BackColor=Color.FromArgb(21,31,43)};
    private readonly Label title=new(){
        Font=new Font("Segoe UI Semibold",17f,FontStyle.Bold),
        ForeColor=Color.White,
        BackColor=Color.FromArgb(21,31,43)};
    private readonly Label count=new(){
        Font=new Font("Segoe UI",9),
        ForeColor=Color.FromArgb(105,199,231),
        BackColor=Color.FromArgb(21,31,43)};
    private Rectangle card;
    internal FirstRunGuide(Form owner,Step[] steps,Action onFinish)
    {
        this.steps=steps;finished=onFinish;
        Dock=DockStyle.Fill;TabStop=true;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|
            ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        try
        {
            snapshot=new Bitmap(owner.ClientSize.Width,owner.ClientSize.Height);
            owner.DrawToBitmap(snapshot,new Rectangle(Point.Empty,snapshot.Size));
        }
        catch{snapshot?.Dispose();snapshot=null;}
        Controls.AddRange([title,detail,count,next,skip]);
        skip.Click+=(_,_)=>Finish();
        next.Click+=(_,_)=>
        {
            if(index==steps.Length-1){Finish();return;}
            index++;UpdateStep();
        };
        Resize+=(_,_)=>UpdateStep();
        UpdateStep();
    }
    private void UpdateStep()
    {
        int w=Math.Min(700,Math.Max(420,Width-50));
        card=new Rectangle(Math.Max(15,(Width-w)/2),
            Math.Max(95,Height-220),w,184);
        count.SetBounds(card.X+23,card.Y+16,140,22);
        title.SetBounds(card.X+23,card.Y+38,w-45,39);
        detail.SetBounds(card.X+23,card.Y+84,w-44,53);
        skip.Location=new Point(card.Right-239,card.Bottom-49);
        next.Location=new Point(card.Right-124,card.Bottom-49);
        count.Text=(UiLanguage.English?"STEP ":"ADIM ")+
            $"{index+1} / {steps.Length}";
        title.Text=UiLanguage.T(steps[index].Title);
        detail.Text=UiLanguage.T(steps[index].Description);
        next.Text=UiLanguage.English
            ?(index==steps.Length-1?"Finish":"Next →")
            :(index==steps.Length-1?"Tamamla":"İleri →");
        skip.Text=UiLanguage.English?"Skip":"Atla";
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        if(snapshot is not null)
            e.Graphics.DrawImage(snapshot,new Rectangle(0,0,Width,Height));
        else e.Graphics.Clear(Color.FromArgb(15,22,32));
        using(var dim=new SolidBrush(Color.FromArgb(165,5,10,18)))
            e.Graphics.FillRectangle(dim,ClientRectangle);
        var focus=steps[index].Focus;
        if(focus is not null&&focus.IsHandleCreated&&snapshot is not null)
        {
            try
            {
                var topLeft=PointToClient(focus.PointToScreen(Point.Empty));
                var box=new Rectangle(topLeft.X-5,topLeft.Y-5,
                    focus.Width+10,focus.Height+10);
                box.Intersect(ClientRectangle);
                if(box.Width>4&&box.Height>4)
                {
                    e.Graphics.DrawImage(snapshot,box,box,GraphicsUnit.Pixel);
                    using var outline=new Pen(Color.FromArgb(96,209,246),3);
                    e.Graphics.DrawRoundedRectangleIfSupported(box,outline);
                }
            }
            catch(InvalidOperationException){}
        }
        using var fill=new SolidBrush(Color.FromArgb(21,31,43));
        using var stroke=new Pen(Color.FromArgb(57,103,125),1);
        var inner=new Rectangle(card.X,card.Y,card.Width,card.Height);
        using var path=Rounded(inner,14);
        e.Graphics.FillPath(fill,path);
        e.Graphics.DrawPath(stroke,path);
    }
    private static GraphicsPath Rounded(Rectangle r,int radius)
    {
        int d=radius*2;
        var path=new GraphicsPath();
        path.AddArc(r.X,r.Y,d,d,180,90);
        path.AddArc(r.Right-d,r.Y,d,d,270,90);
        path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);
        path.AddArc(r.X,r.Bottom-d,d,d,90,90);
        path.CloseFigure();return path;
    }
    private void Finish()
    {
        try{FirstRunGuideState.MarkCompleted();}
        catch(IOException) {/* A denied save never blocks the Studio. */}
        finished();
        Parent?.Controls.Remove(this);Dispose();
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing){snapshot?.Dispose();snapshot=null;}
        base.Dispose(disposing);
    }
}
internal static class GuideGraphicExtensions
{
    internal static void DrawRoundedRectangleIfSupported(
        this Graphics g,Rectangle box,Pen pen)
    {
        g.DrawRectangle(pen,box);
    }
}

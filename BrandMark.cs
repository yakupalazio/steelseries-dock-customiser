using System.Drawing;
using System.Drawing.Drawing2D;

namespace SteelseriesDockCustomiser;

/// <summary>Original Dock Customiser branding, not the SteelSeries trademark.
/// Scalable at 16, 32, 48 and 64px; shared by toolbar and splash.</summary>
internal static class BrandMark
{
    internal static void Draw(Graphics graphics,Rectangle area)
    {
        var state=graphics.Save();
        graphics.SmoothingMode=SmoothingMode.AntiAlias;
        graphics.TranslateTransform(area.X,area.Y);
        graphics.ScaleTransform(area.Width/64f,area.Height/64f);
        using(var bg=new SolidBrush(Color.FromArgb(29,43,58)))
        using(var background=Round(new Rectangle(1,1,62,62),15))
            graphics.FillPath(bg,background);

        // A single continuous "D" surrounding an OLED pixel field.
        using(var pen=new Pen(Color.FromArgb(82,207,243),5.5f)
            {StartCap=LineCap.Round,EndCap=LineCap.Round})
        {
            graphics.DrawLine(pen,19,14,19,50);
            graphics.DrawArc(pen,11,14,42,36,-90,180);
        }
        using var pixel=new SolidBrush(Color.FromArgb(224,246,252));
        foreach(var (x,y) in new (int,int)[]{
            (29,24),(36,24),(29,31),(36,31),(29,38)})
        using(var sprite=Round(new Rectangle(x,y,5,5),1))
            graphics.FillPath(pixel,sprite);
        graphics.Restore(state);
    }
    private static GraphicsPath Round(Rectangle r,int radius)
    {
        int diameter=radius*2;
        var path=new GraphicsPath();
        path.AddArc(r.Left,r.Top,diameter,diameter,180,90);
        path.AddArc(r.Right-diameter,r.Top,diameter,diameter,270,90);
        path.AddArc(r.Right-diameter,r.Bottom-diameter,diameter,diameter,0,90);
        path.AddArc(r.Left,r.Bottom-diameter,diameter,diameter,90,90);
        path.CloseFigure();
        return path;
    }
}

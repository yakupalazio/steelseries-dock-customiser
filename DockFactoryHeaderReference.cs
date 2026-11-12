using System;
using System.Collections.Generic;
using System.Drawing;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Extracts the visible, 1-bit native 128x10 OEM indicator strip from
/// precisely the same calibrated reference photograph as Dock Preview.
/// This is a display-only guide; no physical OLED header is captured,
/// edited, serialized, exported or transmitted.
/// </summary>
internal static class DockFactoryHeaderReference
{
    internal const int Width=128;
    internal const int Height=10;
    internal const int NativeOledHeight=64;

    // Grey/off pixels on the original photograph are below this level.
    // The luminous white pixels of the actual indicators are above it.
    private const int LitThreshold=105;

    internal static Bitmap? Extract(Image? photo,IReadOnlyList<PointF> corners)
    {
        if(photo is not Bitmap source || corners.Count!=4)return null;
        using var output=new Bitmap(Width,Height);
        var a=corners[0]; var b=corners[1];
        var c=corners[2]; var d=corners[3];
        for(int y=0;y<Height;y++)
        for(int x=0;x<Width;x++)
        {
            // Same four corners and native OLED coordinate system used by
            // the photo compositor. A bilinear mapping is sufficient for
            // the reference photo's nearly rectangular 128x64 glass.
            double u=(x+.5)/Width;
            double v=(y+.5)/NativeOledHeight;
            double upperX=a.X+(b.X-a.X)*u;
            double upperY=a.Y+(b.Y-a.Y)*u;
            double lowerX=d.X+(c.X-d.X)*u;
            double lowerY=d.Y+(c.Y-d.Y)*u;
            int srcX=(int)Math.Round(upperX+(lowerX-upperX)*v);
            int srcY=(int)Math.Round(upperY+(lowerY-upperY)*v);
            if(srcX<0||srcX>=source.Width||srcY<0||srcY>=source.Height)
                return null;
            var photoPixel=source.GetPixel(srcX,srcY);
            bool lit=(photoPixel.R+photoPixel.G+photoPixel.B)/3
                >=LitThreshold;
            output.SetPixel(x,y,lit?Color.White:Color.Black);
        }
        // Return a stand-alone copy. No references to the source
        // photograph, user project or any hardware image are retained.
        return (Bitmap)output.Clone();
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Pixel-perfect, display-only reconstruction of the 128x10 OEM guide.
///
/// The photograph's downsampled indicators produced holes, fused outlines
/// and unreadable reverse-color status text on the 128x64 Studio preview.
/// This tiny native monochrome overlay is deliberately redrawn as exact
/// pixels (rather than thresholding a photographed LCD at 1.9 samples/px).
/// It NEVER modifies firmware, a physical dock, saved user widgets or
/// transmitted HID frames. The genuine dock continues to own rows 0..9.
/// </summary>
internal static class DockFactoryHeaderReference
{
    internal const int Width=128;
    internal const int Height=10;
    internal const int NativeOledHeight=64;

    internal static Bitmap? Extract(Image? photo,IReadOnlyList<PointF> corners)
    {
        if(photo is not Bitmap||corners.Count!=4)return null;
        var result=new Bitmap(Width,Height);
        using(var graphics=Graphics.FromImage(result))
            graphics.Clear(Color.Black);

        void Dot(int x,int y,bool white=true)
        {
            if(x>=0&&x<Width&&y>=0&&y<Height)
                result.SetPixel(x,y,white?Color.White:Color.Black);
        }
        void Pattern(int x,int y,params string[] lines)
        {
            for(int yy=0;yy<lines.Length;yy++)
            for(int xx=0;xx<lines[yy].Length;xx++)
                if(lines[yy][xx]=='1')Dot(x+xx,y+yy);
        }
        void Outline(int x,int y,int width,int height)
        {
            for(int col=0;col<width;col++)
                {Dot(x+col,y);Dot(x+col,y+height-1);}
            for(int row=0;row<height;row++)
                {Dot(x,y+row);Dot(x+width-1,y+row);}
        }
        // Headset silhouette and two-battery row, matching the native
        // 10-pixel firmware header proportions without photo-derived noise.
        Pattern(4,2,
            "0011111100",
            "0100000010",
            "1100000011",
            "1100000011",
            "1100000011",
            "1100000011",
            "0110000110",
            "0011111100");
        Outline(16,2,18,8);
        for(int bx=19;bx<=28;bx+=4)
            for(int yy=4;yy<=7;yy++)
                {Dot(bx,yy);Dot(bx+1,yy);}
        for(int yy=4;yy<=7;yy++)Dot(34,yy);

        // The original glass contains an inverse-video central input
        // plate. The requested label is USB-1, not the photograph's
        // unreadable anti-aliased marker. Font is drawn natively at 4x5.
        for(int yy=2;yy<=9;yy++)
            for(int xx=46;xx<=81;xx++)Dot(xx,yy);
        var letters=new Dictionary<char,string[]>{
            ['U']=["1001","1001","1001","1001","0110"],
            ['S']=["0111","1000","0110","0001","1110"],
            ['B']=["1110","1001","1110","1001","1110"],
            ['-']=["0000","0000","1111","0000","0000"],
            ['1']=["0010","0110","0010","0010","0111"]
        };
        int xCursor=52;
        foreach(char character in "USB-1")
        {
            var glyph=letters[character];
            for(int yy=0;yy<glyph.Length;yy++)
            for(int xx=0;xx<glyph[yy].Length;xx++)
                if(glyph[yy][xx]=='1')
                    Dot(xCursor+xx,3+yy,false);
            xCursor+=5;
        }
        // Right-hand power/charging symbol and battery outline.
        Pattern(93,2,
            "0001000000",
            "0011100000",
            "0011110000",
            "1111111000",
            "0001111100",
            "0000111000",
            "0000110000",
            "0000100000");
        Outline(104,2,18,8);
        for(int bx=107;bx<=115;bx+=4)
            for(int yy=4;yy<=7;yy++)
                {Dot(bx,yy);Dot(bx+1,yy);}
        for(int yy=4;yy<=7;yy++)Dot(122,yy);
        return result;
    }
}

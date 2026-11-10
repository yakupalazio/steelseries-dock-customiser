using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Globalization;
using System.Text;

namespace SteelseriesDockCustomiser;

/// <summary>
/// A deterministic monochrome 128x64 pixel renderer.
/// No GDI text hinting or fractional anti-aliasing: a native pixel is either lit or unlit.
/// All glyphs and icons below are original, hand-edited one-bit shapes.
/// No HID, GameSense, device enumeration or network access.
/// </summary>
internal static class OledRenderer
{
    public const int Width=128, Height=64, ReservedTop=10;
    private static readonly IReadOnlyDictionary<char,string[]> Glyphs = MakeGlyphs();

    private static readonly Dictionary<string,string[]> IconSprites = new()
    {
        ["Mouse"] = [
            "00011111000",
            "00100000100",
            "01000100010",
            "01000100010",
            "01000100010",
            "01000000010",
            "01000000010",
            "01000000010",
            "00100000100",
            "00100000100",
            "00011111000"
        ],
        ["Phone"] = [
            "001111100",
            "010000010",
            "010111010",
            "010000010",
            "010000010",
            "010000010",
            "010000010",
            "010000010",
            "010000010",
            "010010010",
            "001111100"
        ],
        ["DualSense"] = [
            "000011111110000",
            "001110000011100",
            "011000000000110",
            "110011000110011",
            "100011000110001",
            "100000000000001",
            "100100000001001",
            "100000000000001",
            "010100000001010",
            "001110000011100",
            "000011000110000"
        ],
        ["Headset"] = [
            "000011110000",
            "001100001100",
            "011000000110",
            "010000000010",
            "110000000011",
            "100000000001",
            "110000000011",
            "110000000011",
            "110000000011",
            "011000000110",
            "001000000100"
        ],
        ["Laptop"] = [
            "0011111111100",
            "0010000000100",
            "0010000000100",
            "0010000000100",
            "0010000000100",
            "0010000000100",
            "0011111111100",
            "0111111111110",
            "1110000000111",
            "0111111111110",
            "0000000000000"
        ],
        ["Media"] = [
            "000000011100",
            "000001110100",
            "000011000100",
            "000010000100",
            "000010000100",
            "000010000100",
            "000010000100",
            "001110001100",
            "011110011100",
            "011100011000",
            "001000010000"
        ]
    };

    public static Bitmap Render(LayoutState layout,bool media,bool showDate,bool spotify,BatterySnapshot? batteries=null,MediaSnapshot? playback=null)
    {
        var frame=new bool[Width*Height];

        // Physical OEM indicator rows are managed by the dock firmware, not by us.
        // Leave the protected region black in the framebuffer; never transmit it.

        // Draw every editable pixel, clipped below simulated factory bar.
        for(int y=ReservedTop;y<Height;y++)
        for(int x=0;x<Width;x++)
            if(layout.Pixels[y*Width+x])frame[y*Width+x]=true;

        foreach(var w in layout.Widgets.Where(w=>w.Visible && ShouldShow(w,media,showDate)))
            DrawWidget(frame,w,spotify,batteries,playback);

        var bmp=new Bitmap(Width,Height,PixelFormat.Format24bppRgb);
        for(int y=0;y<Height;y++)
        for(int x=0;x<Width;x++)
            bmp.SetPixel(x,y,frame[y*Width+x]?Color.White:Color.Black);
        return bmp;
    }

    private static bool ShouldShow(DockWidget w,bool media,bool date)=>w.Kind switch
    {
        "Clock" or "Date" => !media && (w.Kind!="Date" || date),
        "Source" or "Track" or "Artist" or "Progress" => media,
        _=>true
    };

    public static string DisplayText(DockWidget w,MediaSnapshot? playback=null)=>w.Kind switch
    {
        "Clock"=>DateTime.Now.ToString(ClockDateFormats.ClockFormat(w.Format),CultureInfo.InvariantCulture),
        "Date"=>DateTime.Now.ToString(ClockDateFormats.DateFormat(w.Format),CultureInfo.InvariantCulture),
        "Track"=>playback?.IsPlaying==true ? playback.Title : "PALE TORTURED",
        "Artist"=>playback?.IsPlaying==true ? playback.Artist : "DRACONIAN",
        "Custom text"=>w.Text,
        _=>""
    };

    public static Rectangle GetBounds(DockWidget w)=>w.Kind switch
    {
        "Progress" => new(w.X,w.Y,98,5),
        "Source" => new(w.X,w.Y,14,14),
        "Mouse" or "Phone" or "Headset" or "Laptop" or "DualSense" =>
            new(w.X,w.Y,38,11),
        "Clock" => new(w.X,w.Y,Math.Min(128-w.X,
            DisplayText(w).Length*13),13),
        _ => new(w.X,w.Y,Math.Max(5,TextWidth(DisplayText(w),w.FontSize)),TextHeight(w.FontSize))
    };

    public static bool IsDevice(string name)=>name is "Mouse" or "Phone" or "Headset" or "Laptop" or "DualSense";

    public static int TextWidth(string text,int fontSize)
    {
        int scale=Scale(fontSize);
        return text.Length==0?0:text.Length*6*scale-1;
    }

    public static int TextHeight(int fontSize)=>7*Scale(fontSize);

    private static int Scale(int fontsize)=>fontsize>=15?2:1;

    private static void DrawFactoryPlaceholder(bool[] px)
    {
        // Native factory indicators never follow user icon overrides.
        DrawOriginalSprite(px,"Headset",2,0,clipFactory:false);
        DrawBattery(px,16,3,12,6,8);
        DrawBattery(px,94,3,12,6,10);
        for(int x=0;x<Width;x++)Dot(px,x,11,false);
    }

    private static void DrawWidget(bool[] px,DockWidget w,bool spotify,BatterySnapshot? batteries,MediaSnapshot? playback)
    {
        int x=w.X,y=w.Y;
        switch(w.Kind)
        {
            case "Source":
                DrawIcon(px,(playback?.IsPlaying==true ? playback.IsSpotify : spotify)?"Spotify":"Media",x,y);
                break;
            case "Mouse":
            case "Phone":
            case "Headset":
            case "Laptop":
            case "DualSense":
                DrawIcon(px,w.Kind,x,y);
                string pct=batteries?[w.Kind].Display ?? "--%";
                DrawText(px,pct,x+15,y+2,1);
                break;
            case "Progress":
                DrawProgress(px,x,y,Math.Min(98,Width-x),
                    playback?.IsPlaying==true ? playback.ProgressPercent : 46);
                break;
            case "Clock":
                DrawClock(px,DisplayText(w),x,y,Math.Clamp(w.StrokeWidth,1,3));
                break;
            case "Track":
            case "Artist":
                DrawMarquee(px,DisplayText(w,playback),x,y,Scale(w.FontSize));
                break;
            case "Date":
                DrawText(px,DisplayText(w),x,y,Scale(w.FontSize),
                    Math.Clamp(w.StrokeWidth,1,3));
                break;
            default:
                DrawText(px,DisplayText(w),x,y,Scale(w.FontSize));
                break;
        }
    }

    private static void DrawProgress(bool[] px,int x,int y,int width,int percentage)
    {
        if(width<3)return;
        int height=5;
        for(int i=0;i<width;i++){Dot(px,x+i,y,false);Dot(px,x+i,y+height-1,false);}
        for(int j=0;j<height;j++){Dot(px,x,y+j);Dot(px,x+width-1,y+j);}
        int fill=(width-2)*Math.Clamp(percentage,0,100)/100;
        for(int i=0;i<fill;i++)for(int j=1;j<height-1;j++)Dot(px,x+i+1,y+j);
    }

    private static void DrawBattery(bool[] px,int x,int y,int width,int height,int filled)
    {
        for(int i=0;i<width;i++){Dot(px,x+i,y,false);Dot(px,x+i,y+height-1,false);}
        for(int i=0;i<height;i++){Dot(px,x,y+i,false);Dot(px,x+width-1,y+i,false);}
        Dot(px,x+width,y+height/2,false);
        int total=width-2;
        for(int a=0;a<Math.Clamp(filled,0,total);a++)
            for(int b=1;b<height-1;b++)Dot(px,x+1+a,y+b,false);
    }

    public static string[] GetOriginalIconRows(string type)
    {
        if(IconSprites.TryGetValue(type,out var rows))return rows.ToArray();
        if(type!="Spotify")return ["0000","0000","0000","0000"];
        var tmp=new bool[Width*Height];
        DrawSpotify(tmp,0,ReservedTop,clipFactory:true);
        return Enumerable.Range(0,14).Select(y=>
            new string(Enumerable.Range(0,14).Select(x=>
                tmp[(y+ReservedTop)*Width+x]?'1':'0').ToArray())).ToArray();
    }

    private static void DrawOriginalSprite(bool[] px,string type,int x,int y,bool clipFactory=true)
    {
        if(!IconSprites.TryGetValue(type,out var original))return;
        for(int row=0;row<original.Length;row++)
        for(int col=0;col<original[row].Length;col++)
            if(original[row][col]=='1')Dot(px,x+col,y+row,clipFactory);
    }

    private static void DrawIcon(bool[] px,string type,int x,int y,bool clipFactory=true)
    {
        if(IconLibrary.TryCustom(type,out var custom))
        {
            for(int row=0;row<custom.Length;row++)
            for(int col=0;col<custom[row].Length;col++)
                if(custom[row][col]=='1')Dot(px,x+col,y+row,clipFactory);
            return;
        }
        if(type=="Spotify")
        {
            DrawSpotify(px,x,y,clipFactory);
            return;
        }
        if(!IconSprites.TryGetValue(type,out var data))return;
        for(int row=0;row<data.Length;row++)
        for(int col=0;col<data[row].Length;col++)
            if(data[row][col]=='1')Dot(px,x+col,y+row,clipFactory);
    }

    private static void DrawSpotify(bool[] px,int x,int y,bool clipFactory)
    {
        // 14x14 disc with negative-space three curved strokes.
        string[] disc = [
            "00001111110000",
            "00111111111100",
            "01111111111110",
            "01111111111110",
            "11111111111111",
            "11111111111111",
            "11111111111111",
            "11111111111111",
            "11111111111111",
            "11111111111111",
            "01111111111110",
            "01111111111110",
            "00111111111100",
            "00001111110000"
        ];
        for(int yy=0;yy<14;yy++)
        for(int xx=0;xx<14;xx++)
            if(disc[yy][xx]=='1')Dot(px,x+xx,y+yy,clipFactory);

        // Widths and curves tuned for the real monochrome 14-pixel silhouette.
        int[][] carve=[
            [4,5,6,7,8,9,10,11],
            [4,5,6,7,8,9,10],
            [5,6,7,8,9]
        ];
        for(int i=0;i<carve.Length;i++)
        {
            int baseY=5+2*i;
            foreach(int dx in carve[i])
            {
                int offset=(dx>=10?1:0);
                Undot(px,x+dx,y+baseY+offset,clipFactory);
            }
        }
    }

    // A physical OLED is 128 pixels wide: text moves by actual PIXELS,
    // rather than jumping one entire character at each update.
    public const int MarqueePixelsPerSecond=3;
    internal static int MarqueeOffset(int textWidth,int displayWidth,long seconds)
    {
        if(textWidth<=displayWidth)return 0;
        int cycle=textWidth+24; // Space between repeated labels.
        return (int)((seconds*MarqueePixelsPerSecond)%cycle);
    }

    private static void DrawMarquee(bool[] framebuffer,string text,int left,int top,int scale)
    {
        string label=ToPixelAlphabet(text);
        int advance=6*scale;
        int displayWidth=Width-left;
        if(label.Length*advance<=displayWidth)
        {
            DrawText(framebuffer,label,left,top,scale);
            return;
        }

        int labelWidth=label.Length*advance;
        int stride=labelWidth+24;
        int offset=MarqueeOffset(labelWidth,displayWidth,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        // Tile twice so the next copy enters after a short empty interval.
        for(int copy=0;copy<2;copy++)
        {
            int baseX=left-offset+copy*stride;
            for(int i=0;i<label.Length;i++)
            {
                int glyphX=baseX+i*advance;
                if(glyphX+5*scale<left || glyphX>=Width)continue;
                if(!Glyphs.TryGetValue(label[i],out var glyph))continue;
                for(int gy=0;gy<7;gy++)
                for(int gx=0;gx<5;gx++)
                    if(glyph[gy][gx]=='1')
                        for(int yy=0;yy<scale;yy++)
                        for(int xx=0;xx<scale;xx++)
                        {
                            int pixelX=glyphX+gx*scale+xx;
                            if(pixelX>=left && pixelX<Width)
                                Dot(framebuffer,pixelX,top+gy*scale+yy);
                        }
            }
        }
    }

    private static string ToPixelAlphabet(string input)
    {
        var normalized=input.Replace('ı','i').Normalize(NormalizationForm.FormD);
        var sb=new StringBuilder(normalized.Length);
        foreach(char c in normalized)
        {
            if(CharUnicodeInfo.GetUnicodeCategory(c)==UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(c);
        }
        return sb.ToString().ToUpperInvariant();
    }

    private static void DrawText(bool[] px,string? text,int x,int y,int scale,int stroke=1)
    {
        if(string.IsNullOrWhiteSpace(text))return;
        int advance=6*scale+Math.Clamp(stroke,1,3)-1;
        foreach(char c in ToPixelAlphabet(text))
        {
            if(Glyphs.TryGetValue(c,out var glyph))
            {
                for(int gy=0;gy<7;gy++)
                for(int gx=0;gx<5;gx++)
                    if(glyph[gy][gx]=='1')
                        for(int sy=0;sy<scale;sy++)
                        for(int sx=0;sx<scale;sx++)
                            StrokeDot(px,x+gx*scale+sx,y+gy*scale+sy,stroke);
            }
            x+=advance;
        }
    }

    private static void StrokeDot(bool[] px,int x,int y,int thickness)
    {
        for(int yy=0;yy<thickness;yy++)
        for(int xx=0;xx<thickness;xx++)
            Dot(px,x+xx,y+yy);
    }

    private static void DrawClock(bool[] px,string value,int x,int y,int thickness)
    {
        const int SegT=1,SegUL=2,SegUR=4,SegM=8,SegLL=16,SegLR=32,SegB=64;
        int[] map=[119,36,93,109,46,107,123,37,127,111];
        foreach(char c in value)
        {
            if(c==':')
            {
                StrokeDot(px,x+2,y+4,thickness);
                StrokeDot(px,x+2,y+9,thickness);
                x+=7;continue;
            }
            if(!char.IsDigit(c)){x+=10;continue;}
            int flags=map[c-'0'];
            if((flags&SegT)!=0)Line(px,x+2,y,x+6,y,thickness);
            if((flags&SegUL)!=0)Line(px,x+1,y+2,x+1,y+5,thickness);
            if((flags&SegUR)!=0)Line(px,x+7,y+2,x+7,y+5,thickness);
            if((flags&SegM)!=0)Line(px,x+2,y+6,x+6,y+6,thickness);
            if((flags&SegLL)!=0)Line(px,x+1,y+8,x+1,y+11,thickness);
            if((flags&SegLR)!=0)Line(px,x+7,y+8,x+7,y+11,thickness);
            if((flags&SegB)!=0)Line(px,x+2,y+12,x+6,y+12,thickness);
            x+=11+thickness-1;
        }
    }

    private static void Line(bool[] px,int x1,int y1,int x2,int y2,int thickness=1)
    {
        int dx=Math.Abs(x2-x1),sx=x1<x2?1:-1;
        int dy=-Math.Abs(y2-y1),sy=y1<y2?1:-1,err=dx+dy;
        while(true)
        {
            StrokeDot(px,x1,y1,thickness);
            if(x1==x2&&y1==y2)break;
            int e=2*err;
            if(e>=dy){err+=dy;x1+=sx;}
            if(e<=dx){err+=dx;y1+=sy;}
        }
    }

    private static void Dot(bool[] pixels,int x,int y,bool clip=true)
    {
        if(x>=0&&x<Width&&y>=0&&y<Height&&(!clip||y>=ReservedTop))
            pixels[y*Width+x]=true;
    }

    private static void Undot(bool[] pixels,int x,int y,bool clip=true)
    {
        if(x>=0&&x<Width&&y>=0&&y<Height&&(!clip||y>=ReservedTop))
            pixels[y*Width+x]=false;
    }

    private static IReadOnlyDictionary<char,string[]> MakeGlyphs()
    {
        string[] specs=[
            "A:01110/10001/10001/11111/10001/10001/10001",
            "B:11110/10001/10001/11110/10001/10001/11110",
            "C:01111/10000/10000/10000/10000/10000/01111",
            "D:11110/10001/10001/10001/10001/10001/11110",
            "E:11111/10000/10000/11110/10000/10000/11111",
            "F:11111/10000/10000/11110/10000/10000/10000",
            "G:01111/10000/10000/10111/10001/10001/01111",
            "H:10001/10001/10001/11111/10001/10001/10001",
            "I:11111/00100/00100/00100/00100/00100/11111",
            "J:00111/00010/00010/00010/10010/10010/01100",
            "K:10001/10010/10100/11000/10100/10010/10001",
            "L:10000/10000/10000/10000/10000/10000/11111",
            "M:10001/11011/10101/10101/10001/10001/10001",
            "N:10001/11001/10101/10011/10001/10001/10001",
            "O:01110/10001/10001/10001/10001/10001/01110",
            "P:11110/10001/10001/11110/10000/10000/10000",
            "Q:01110/10001/10001/10001/10101/10010/01101",
            "R:11110/10001/10001/11110/10100/10010/10001",
            "S:01111/10000/10000/01110/00001/00001/11110",
            "T:11111/00100/00100/00100/00100/00100/00100",
            "U:10001/10001/10001/10001/10001/10001/01110",
            "V:10001/10001/10001/10001/10001/01010/00100",
            "W:10001/10001/10001/10101/10101/10101/01010",
            "X:10001/10001/01010/00100/01010/10001/10001",
            "Y:10001/10001/01010/00100/00100/00100/00100",
            "Z:11111/00001/00010/00100/01000/10000/11111",
            "0:01110/10001/10011/10101/11001/10001/01110",
            "1:00100/01100/00100/00100/00100/00100/01110",
            "2:01110/10001/00001/00010/00100/01000/11111",
            "3:11110/00001/00001/01110/00001/00001/11110",
            "4:00010/00110/01010/10010/11111/00010/00010",
            "5:11111/10000/10000/11110/00001/00001/11110",
            "6:01111/10000/10000/11110/10001/10001/01110",
            "7:11111/00001/00010/00100/01000/01000/01000",
            "8:01110/10001/10001/01110/10001/10001/01110",
            "9:01110/10001/10001/01111/00001/00001/11110",
            "%:11001/11010/00100/00100/01011/10011/00000",
            "/:00001/00001/00010/00100/01000/10000/10000",
            "-:00000/00000/00000/11111/00000/00000/00000",
            ".:00000/00000/00000/00000/00000/01100/01100",
            " :00000/00000/00000/00000/00000/00000/00000",
            "?:01110/10001/00001/00010/00100/00000/00100"
        ];
        var d=new Dictionary<char,string[]>();
        foreach(string raw in specs)
        {
            int i=raw.IndexOf(':');
            if(i!=1)throw new InvalidOperationException("Glyph key syntax");
            var rows=raw[(i+1)..].Split('/');
            if(rows.Length!=7 || rows.Any(s=>s.Length!=5))
                throw new InvalidOperationException("Glyph rows invalid: "+raw);
            d[raw[0]]=rows;
        }
        return d;
    }
}

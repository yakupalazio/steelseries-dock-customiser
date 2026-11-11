using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Threading;

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
    private static readonly AsyncLocal<bool> EditableHeader = new();

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
        ["SpareBattery"] = [
            "11111111100",
            "10000000110",
            "10111110111",
            "10111110111",
            "10111110111",
            "10111110111",
            "10111110111",
            "10111110111",
            "10111110111",
            "10000000110",
            "11111111100"
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

    public static Bitmap Render(LayoutState layout,bool media,bool showDate,bool spotify,
        BatterySnapshot? batteries=null,MediaSnapshot? playback=null,
        GameSnapshot? game=null,BatteryPreferences? preferences=null,
        bool protectHeader=true,ScreenMode? layoutMode=null,DialSnapshot? dial=null,
        SystemTelemetrySnapshot? telemetry=null)
    {
        bool wasEditable=EditableHeader.Value;
        EditableHeader.Value=!protectHeader;
        try {
        var frame=new bool[Width*Height];

        // Physical OEM indicator rows are managed by the dock firmware, not by us.
        // Leave the protected region black in the framebuffer; never transmit it.

        // Draw every editable pixel, clipped below simulated factory bar.
        for(int y=protectHeader?ReservedTop:0;y<Height;y++)
        for(int x=0;x<Width;x++)
            if(layout.Pixels[y*Width+x])frame[y*Width+x]=true;

        var mode=layoutMode??(media?ScreenMode.Media:ScreenMode.Idle);
        var kinds=BatteryPriority.SelectThree(batteries??new BatterySnapshot(),
            preferences??new BatteryPreferences());
        // Draw optional Mini Player last. Its reserved rectangle becomes black
        // whenever music is not playing, even if unrelated free-drawn pixels
        // or another widget previously occupied that rectangle.
        foreach(var w in layout.Widgets.Where(w=>w.Visible &&
            (layoutMode is null?ShouldShow(w,media,showDate):
                StudioProject.IsAllowed(mode,w.Kind) &&
                    (w.Kind!="Date"||showDate)))
            .OrderBy(w=>w.Kind is "GameMiniPlayer" or "VolumeChatMix"?1:0))
            DrawWidget(frame,w,spotify,batteries,playback,game,kinds,dial,telemetry);

        var bmp=new Bitmap(Width,Height,PixelFormat.Format24bppRgb);
        for(int y=0;y<Height;y++)
        for(int x=0;x<Width;x++)
            bmp.SetPixel(x,y,frame[y*Width+x]?Color.White:Color.Black);
        return bmp;
        } finally { EditableHeader.Value=wasEditable; }
    }

    private static bool ShouldShow(DockWidget w,bool media,bool date)=>w.Kind switch
    {
        "Clock" or "Date" => !media && (w.Kind!="Date" || date),
        "Source" or "Track" or "Artist" or "Progress" => media,
        _=>true
    };

    public static string DisplayText(DockWidget w,MediaSnapshot? playback=null)=>w.Kind switch
    {
        "Clock" or "MediaClock"=>
            DateTime.Now.ToString(ClockDateFormats.ClockFormat(w.Format),CultureInfo.InvariantCulture),
        "Date"=>DateTime.Now.ToString(ClockDateFormats.DateFormat(w.Format),CultureInfo.InvariantCulture),
        "Track"=>playback?.IsPlaying==true ? playback.Title : "PALE TORTURED",
        "Artist"=>playback?.IsPlaying==true ? playback.Artist : "DRACONIAN",
        "Custom text" or "GameLabel"=>w.Text,
        "GameTitle"=>w.Text,
        _=>""
    };

    public static Rectangle GetBounds(DockWidget w)=>w.Kind switch
    {
        "Progress" => new(w.X,w.Y,98,5),
        "Source" => new(w.X,w.Y,14,14),
        "Mouse" or "Phone" or "Headset" or "SpareBattery" or "Laptop" or
        "DualSense" or "BatterySlot1" or "BatterySlot2" or "BatterySlot3" =>
            new(w.X,w.Y,41,11),
        "Clock" => w.FontSize>=15
            ? new(w.X,w.Y,Math.Min(128-w.X,DisplayText(w).Length*13),15)
            : new(w.X,w.Y,Math.Max(10,TextWidth(DisplayText(w),w.FontSize)+w.StrokeWidth),9),
        "GameMiniPlayer" => new(w.X,w.Y,Math.Min(Width-w.X,
            Math.Clamp(w.MiniWidth,35,128)),Math.Max(14,
            7*Scale(w.FontSize)+Math.Clamp(w.StrokeWidth,1,3)+2)),
        "SystemCpu" or "SystemRam" or "NetworkPing" =>
            new(w.X,w.Y,Math.Min(Width-w.X,83),8),
        "VolumeChatMix" when w.DialStyle=="Wheel" => new(w.X,w.Y,
            Math.Min(Width-w.X,WheelArtwork.Width),WheelArtwork.Height),
        "VolumeChatMix" => new(w.X,w.Y,Math.Min(Width-w.X,
            Math.Clamp(w.MiniWidth,60,128)),25),
        _ => new(w.X,w.Y,Math.Max(5,TextWidth(DisplayText(w),w.FontSize)),TextHeight(w.FontSize))
    };

    public static bool IsDevice(string name)=>name is "Mouse" or "Phone" or
        "Headset" or "SpareBattery" or "Laptop" or "DualSense" or
        "BatterySlot1" or "BatterySlot2" or "BatterySlot3";

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

    private static void DrawWidget(bool[] px,DockWidget w,bool spotify,
        BatterySnapshot? batteries,MediaSnapshot? playback,
        GameSnapshot? game,string[] currentDevices,DialSnapshot? dial,
        SystemTelemetrySnapshot? telemetry)
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
            case "SpareBattery":
            case "Laptop":
            case "DualSense":
            case "BatterySlot1":
            case "BatterySlot2":
            case "BatterySlot3":
                string device=w.Kind;
                if(device.StartsWith("BatterySlot",StringComparison.Ordinal))
                {
                    if(!int.TryParse(device[^1..],out var slot))break;
                    if(slot<1||slot>currentDevices.Length)break;
                    device=currentDevices[slot-1];
                }
                DrawIcon(px,device,x,y);
                var reading=batteries?[device];
                string pct=reading?.Display??"--%";
                int labelX=x+13;
                DrawText(px,pct,labelX,y+2,1);
                int boltX=labelX+TextWidth(pct,7)+2;
                if(reading?.IsCharging==true &&
                    boltX+3<=Math.Min(Width,x+42))
                    DrawChargeBolt(px,boltX,y+2);
                break;
            case "Progress":
                DrawProgress(px,x,y,Math.Min(98,Width-x),
                    playback?.IsPlaying==true ? playback.ProgressPercent : 46);
                break;
            case "Clock":
                if(w.FontSize>=15)
                    DrawClock(px,DisplayText(w),x,y,Math.Clamp(w.StrokeWidth,1,3));
                else
                    DrawText(px,DisplayText(w),x,y,Scale(w.FontSize),
                        Math.Clamp(w.StrokeWidth,1,3));
                break;
            case "MediaClock":
                DrawText(px,DisplayText(w),x,y,Scale(w.FontSize),
                    Math.Clamp(w.StrokeWidth,1,3));
                break;
            case "GameTitle":
                DrawMarquee(px,game?.IsRunning==true?game.Name:
                    "GAME NOT RUNNING",x,y,Scale(w.FontSize));
                break;
            case "GameLabel":
                DrawText(px,string.IsNullOrWhiteSpace(w.Text)?"NOW PLAYING:":w.Text,
                    x,y,Scale(w.FontSize),Math.Clamp(w.StrokeWidth,1,3));
                break;
            case "GameMiniPlayer":
                DrawGameMiniPlayer(px,w,playback);
                break;
            case "VolumeChatMix":
                DrawDialWidget(px,w,dial);
                break;
            case "SystemCpu":
                DrawText(px,$"CPU {FormatPercent(telemetry?.Cpu)}",x,y,1);
                break;
            case "SystemRam":
                DrawText(px,$"RAM {FormatPercent(telemetry?.Memory)}",x,y,1);
                break;
            case "NetworkPing":
                DrawText(px,$"PING {FormatPing(telemetry?.Ping)}",x,y,1);
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
                DrawText(px,DisplayText(w),x,y,Scale(w.FontSize),
                    Math.Clamp(w.StrokeWidth,1,3));
                break;
        }
    }

    private static string FormatPercent(int? value)=>value.HasValue
        ?$"{Math.Clamp(value.Value,0,100)}%":"--%";
    private static string FormatPing(int? value)=>value.HasValue
        ?$"{Math.Clamp(value.Value,0,9999)}MS":"--MS";

    /// <summary>
    /// Optional game-only media widget. No fake title or playback indicator:
    /// the entire reserved rectangle is black if no media session is playing.
    /// The active song is confined to a configurable box, so it cannot
    /// overwrite the game title or another widget outside that box.
    /// </summary>
    private static void DrawGameMiniPlayer(bool[] px,DockWidget widget,
        MediaSnapshot? playback)
    {
        var area=GetBounds(widget);
        for(int yy=Math.Max(0,area.Top);yy<Math.Min(Height,area.Bottom);yy++)
        for(int xx=Math.Max(0,area.Left);xx<Math.Min(Width,area.Right);xx++)
            px[yy*Width+xx]=false;

        if(playback?.IsPlaying!=true)return;

        int x=widget.X,y=widget.Y;
        // The icon editor supports custom 4..20px sprites. Render the chosen
        // icon in isolation then fit it into the mini player's 14x14 icon box,
        // never leaking into other game screen elements.
        string iconKind=playback.IsSpotify?"Spotify":"Media";
        if(IconLibrary.TryCustom(iconKind,out var custom) &&
           custom.Length>0 && custom[0].Length>0)
        {
            for(int yy=0;yy<14 && y+yy<area.Bottom;yy++)
            for(int xx=0;xx<14 && x+xx<area.Right;xx++)
            {
                int sy=yy*custom.Length/14;
                int sx=xx*custom[0].Length/14;
                if(custom[sy][sx]=='1')
                    Dot(px,x+xx,y+yy);
            }
        }
        else
        {
            var badge=new bool[Width*Height];
            DrawIcon(badge,iconKind,x,y);
            for(int yy=y;yy<Math.Min(area.Bottom,y+14);yy++)
            for(int xx=x;xx<Math.Min(area.Right,x+14);xx++)
                if(xx>=0 && xx<Width && yy>=0 && yy<Height &&
                   badge[yy*Width+xx])
                    Dot(px,xx,yy);
        }
        int textX=x+16;
        int width=area.Right-textX;
        if(width<=0)return;
        string name=string.IsNullOrWhiteSpace(playback.Title)
            ?"PLAYING":playback.Title;
        int scale=Scale(widget.FontSize);
        int textY=y+(scale>1?1:3);
        DrawMarquee(px,name,textX,textY,scale,width,
            Math.Clamp(widget.StrokeWidth,1,3),8);
    }

    /// <summary>
    /// Optional OLED widget driven by the read-only dock wheel notifications.
    /// Values stay unknown until a real event arrives; it never changes audio.
    /// Draw into an isolated tile and clip to the widget bounds.
    /// </summary>
    private static void DrawDialWidget(bool[] px,DockWidget widget,DialSnapshot? current)
    {
        var bounds=GetBounds(widget);
        var tile=new bool[Width*Height];
        var mask=new bool[Width*Height];
        int x=widget.X,y=widget.Y;
        var snapshot=current??new DialSnapshot(DialView.Volume,null,null,null,
            DateTimeOffset.MinValue,"Veri yok");
        bool fresh=snapshot.UpdatedAt!=DateTimeOffset.MinValue &&
            DateTimeOffset.UtcNow-snapshot.UpdatedAt<TimeSpan.FromMinutes(10);
        int width=Math.Max(3,bounds.Width-2);

        if(widget.DialStyle=="Wheel")
        {
            WheelArtwork.Draw(tile,mask,x,y,widget.WheelPixels,
                WheelArtwork.Value(snapshot,fresh),snapshot.View,widget.WheelAutoLabel,
                widget.WheelGapAngle,
                fresh&&DateTimeOffset.UtcNow-snapshot.UpdatedAt<TimeSpan.FromSeconds(2)
                    ?snapshot.VolumeDelta:0);
        }
        else
        {

        if(snapshot.View==DialView.ChatMix)
        {
            DrawText(tile,"CHATMIX",x+1,y+1,1);
            var left=fresh?snapshot.Game:null;
            var right=fresh?snapshot.Chat:null;
            DrawText(tile,left.HasValue&&right.HasValue
                ?$"G{left.Value} C{right.Value}":"G-- C--",x+1,y+10,1);
            int sum=(left??0)+(right??0);
            DrawProgress(tile,x+1,y+19,width,
                sum>0?(int)Math.Round((left??0)*100d/sum):0);
        }
        else
        {
            DrawText(tile,"VOLUME",x+1,y+1,1);
            int? volume=fresh?snapshot.Volume:null;
            DrawText(tile,volume.HasValue?$"{volume.Value}%":"--%",
                x+49,y+1,1);
            DrawProgress(tile,x+1,y+14,width,volume??0);
        }
        }
        for(int yy=Math.Max(0,bounds.Top);yy<Math.Min(Height,bounds.Bottom);yy++)
        for(int xx=Math.Max(0,bounds.Left);xx<Math.Min(Width,bounds.Right);xx++)
            if(EditableHeader.Value||yy>=ReservedTop)
            {
                // A wheel is a transparent 1-bit sprite except where it
                // explicitly draws lit or black pixels. Other widgets can
                // occupy its curved corners without being wiped away.
                if(widget.DialStyle=="Wheel")
                {
                    if(mask[yy*Width+xx])px[yy*Width+xx]=tile[yy*Width+xx];
                }
                else px[yy*Width+xx]=tile[yy*Width+xx];
            }
    }

    // Hand-drawn 3x7 native OLED lightning glyph shown only for confirmed charging.
    private static void DrawChargeBolt(bool[] frame,int x,int y)
    {
        string[] rows=[".11","11.","111",".11",".1.","11.","1.."];
        for(int row=0;row<rows.Length;row++)
        for(int col=0;col<3;col++)
            if(rows[row][col]=='1')Dot(frame,x+col,y+row);
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
    internal static int MarqueeOffset(int textWidth,int displayWidth,long seconds,int gap=24)
    {
        if(textWidth<=displayWidth)return 0;
        int cycle=textWidth+gap; // Shorter seam for compact game mini titles.
        return (int)((seconds*MarqueePixelsPerSecond)%cycle);
    }

    /// <summary>
    /// Per-second pixel marquee, optionally confined to a mini-player box.
    /// Text weight 1–3 is supported for the game mini player without leaking
    /// pixels outside its horizontal layout bounds.
    /// </summary>
    private static void DrawMarquee(bool[] framebuffer,string text,int left,int top,
        int scale,int maxWidth=128,int stroke=1,int gap=24)
    {
        if(string.IsNullOrWhiteSpace(text) || left<0 || left>=Width ||
           top<0 || top>=Height)return;
        string label=ToPixelAlphabet(text);
        if(label.Length==0)return;
        int weight=Math.Clamp(stroke,1,3);
        int right=Math.Min(Width,left+Math.Max(0,maxWidth));
        int displayWidth=right-left;
        if(displayWidth<=0)return;

        int advance=6*scale+weight-1;
        int labelWidth=label.Length*advance;
        int stride=labelWidth+gap;
        bool longText=labelWidth>displayWidth;
        int offset=longText
            ?MarqueeOffset(labelWidth,displayWidth,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),gap)
            :0;
        int copies=longText?2:1;

        for(int copy=0;copy<copies;copy++)
        {
            int baseX=left-offset+copy*stride;
            for(int i=0;i<label.Length;i++)
            {
                int glyphX=baseX+i*advance;
                if(glyphX+5*scale+weight<left || glyphX>=right)continue;
                if(!Glyphs.TryGetValue(label[i],out var glyph))continue;
                for(int gy=0;gy<7;gy++)
                for(int gx=0;gx<5;gx++)
                    if(glyph[gy][gx]=='1')
                        for(int sy=0;sy<scale;sy++)
                        for(int sx=0;sx<scale;sx++)
                        for(int yy=0;yy<weight;yy++)
                        for(int xx=0;xx<weight;xx++)
                        {
                            int px=glyphX+gx*scale+sx+xx;
                            int py=top+gy*scale+sy+yy;
                            if(px>=left && px<right)
                                Dot(framebuffer,px,py);
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
        if(x>=0&&x<Width&&y>=0&&y<Height&&(!clip||EditableHeader.Value||y>=ReservedTop))
            pixels[y*Width+x]=true;
    }

    private static void Undot(bool[] pixels,int x,int y,bool clip=true)
    {
        if(x>=0&&x<Width&&y>=0&&y<Height&&(!clip||EditableHeader.Value||y>=ReservedTop))
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
        // ':' cannot use the "key:rows" serializer because it is itself
        // the separator. A compact clock must retain its visible colon.
        d[':']=["00000","00100","00100","00000","00100","00100","00000"];
        return d;
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

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
        ],
        // Purpose-built pixel sprites, shared by the ChatMix wheel and Icon Workshop.
        ["Game"] = [
            "00011111000",
            "00111111100",
            "01111111110",
            "11000100011",
            "10011111001",
            "10100100101",
            "11000000011",
            "01100000110",
            "00111111100"
        ],
        ["Chat"] = [
            "00111111100",
            "01100000110",
            "11000000011",
            "10001000001",
            "10000100001",
            "11000000011",
            "01111111110",
            "00110000000",
            "00010000000"
        ],
        ["Earbuds"] = [
            "000000000000",
            "001110001110",
            "011110011110",
            "011010010110",
            "011010010110",
            "001010001010",
            "001010001010",
            "001010001010",
            "000110000110",
            "000010000100",
            "000000000000"
        ],
        ["Keyboard"] = [
            "1111111111111",
            "1000000000001",
            "1010101010101",
            "1000000000001",
            "1010101010101",
            "1000000000001",
            "1000111110001",
            "1111111111111"
        ],
        ["Speaker"] = [
            "00111111100",
            "01100000110",
            "01001110010",
            "01010101010",
            "01010101010",
            "01001110010",
            "01100000110",
            "00111111100"
        ],
        ["Bluetooth"] = [
            "000010000",
            "000011000",
            "010010100",
            "001011000",
            "000110000",
            "001011000",
            "010010100",
            "000011000",
            "000010000"
        ],
        ["Usb"] = [
            "000010000",
            "000111000",
            "001010100",
            "001010100",
            "001010100",
            "001010100",
            "010010010",
            "011111110",
            "000010000"
        ],
        ["Wireless"] = [
            "11111111111",
            "00000000000",
            "01111111110",
            "00000000000",
            "00011111000",
            "00000000000",
            "00000100000"
        ],
        ["Charging"] = [
            "0011",
            "0110",
            "1110",
            "0111",
            "0011",
            "0110",
            "1100"
        ]
    };

    public static Bitmap Render(LayoutState layout,bool media,bool showDate,bool spotify,
        BatterySnapshot? batteries=null,MediaSnapshot? playback=null,
        GameSnapshot? game=null,BatteryPreferences? preferences=null,
        bool protectHeader=true,ScreenMode? layoutMode=null,DialSnapshot? dial=null,
        SystemTelemetrySnapshot? telemetry=null,
        SyncedLyricsSnapshot? lyrics=null,
        AudioVisualizationSnapshot? audio=null,
        GameLogoSnapshot? gameLogo=null)
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
            DrawWidget(frame,w,spotify,batteries,playback,game,kinds,dial,
                telemetry,lyrics,audio,gameLogo);

        // The finished frame is already a 1-bit array. Bulk rasterization
        // avoids 8,192 GDI+ SetPixel calls on every live OLED frame.
        return Rasterize(frame);
        } finally { EditableHeader.Value=wasEditable; }
    }

    private static Bitmap Rasterize(bool[] mono)
    {
        var bmp=new Bitmap(Width,Height,PixelFormat.Format24bppRgb);
        var rect=new Rectangle(0,0,Width,Height);
        var data=bmp.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format24bppRgb);
        try
        {
            int pitch=Math.Abs(data.Stride);
            var bytes=new byte[pitch*Height];
            for(int y=0;y<Height;y++)
            for(int x=0;x<Width;x++)
            {
                byte color=mono[y*Width+x]?(byte)255:(byte)0;
                int offset=y*pitch+x*3;
                bytes[offset]=color;
                bytes[offset+1]=color;
                bytes[offset+2]=color;
            }
            if(data.Stride>0)
                Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
            else for(int y=0;y<Height;y++)
                Marshal.Copy(bytes,y*pitch,
                    IntPtr.Add(data.Scan0,y*data.Stride),pitch);
        }
        finally {bmp.UnlockBits(data);}
        return bmp;
    }

    private static bool ShouldShow(DockWidget w,bool media,bool date)=>w.Kind switch
    {
        "Clock" or "Date" => !media && (w.Kind!="Date" || date),
        "Source" or "Track" or "Artist" or "Progress" or "Lyrics" => media,
        _=>true
    };

    public static string DisplayText(DockWidget w,MediaSnapshot? playback=null)=>w.Kind switch
    {
        "Clock" or "MediaClock"=>
            DateTime.Now.ToString(ClockDateFormats.ClockFormat(w.Format),CultureInfo.InvariantCulture),
        "Date"=>DateTime.Now.ToString(ClockDateFormats.DateFormat(w.Format),CultureInfo.InvariantCulture),
        "Track"=>playback?.IsPlaying==true && !string.IsNullOrWhiteSpace(playback.Title)
            ?playback.Title:"No media playing",
        "Artist"=>playback?.IsPlaying==true ? playback.Artist : "",
        "Custom text" or "GameLabel"=>w.Text,
        "GameTitle"=>w.Text,
        _=>""
    };

    internal static bool IsTextWidget(string kind)=>kind is
        "Track" or "Artist" or "GameTitle" or "GameLabel" or
        "Custom text" or "Clock" or "Date" or "MediaClock" or
        "SystemCpu" or "SystemRam" or "NetworkPing" or "GameMiniPlayer";

    private static int TextAreaWidth(DockWidget w)=>
        Math.Max(1,Math.Min(Width-w.X,w.TextWidth>0
            ?Math.Clamp(w.TextWidth,16,128):Width-w.X));

    internal static bool NeedsMarquee(DockWidget w,string text)
    {
        if(!IsTextWidget(w.Kind)||w.Kind is "Clock" or "Date" or "MediaClock")
            return false;
        int scale=Scale(w.FontSize);
        int advance=6*scale+Math.Clamp(w.StrokeWidth,1,3)-1;
        int available=w.Kind=="GameMiniPlayer"
            ?Math.Max(0,Math.Min(w.MiniWidth,Width-w.X)-16)
            :TextAreaWidth(w);
        return ToPixelAlphabet(text).Length*advance>available;
    }

    private static void DrawTextWithOptionalClip(bool[] px,DockWidget w,
        Action<bool[]> paint)
    {
        if(w.TextWidth<=0){paint(px);return;}
        var clipped=new bool[Width*Height];
        paint(clipped);
        int x1=Math.Clamp(w.X,0,Width),x2=Math.Min(Width,w.X+w.TextWidth);
        int y1=Math.Clamp(w.Y,0,Height);
        int y2=Math.Min(Height,y1+Math.Max(16,TextHeight(w.FontSize)+4));
        for(int yy=y1;yy<y2;yy++)for(int xx=x1;xx<x2;xx++)
            if(clipped[yy*Width+xx])px[yy*Width+xx]=true;
    }

    public static Rectangle GetBounds(DockWidget w)
    {
        if(IsTextWidget(w.Kind)&&w.Kind!="GameMiniPlayer"&&w.TextWidth>0)
            return new(w.X,w.Y,TextAreaWidth(w),Math.Min(Height-w.Y,
                w.Kind=="Clock"&&w.FontSize>=15?16:TextHeight(w.FontSize)+4));
        return BoundsLegacy(w);
    }
    private static Rectangle BoundsLegacy(DockWidget w)=>w.Kind switch
    {
        "Progress" => new(w.X,w.Y,Math.Min(Width-w.X,
            Math.Clamp(w.ProgressWidth,12,128)),
            Math.Min(Height-w.Y,Math.Clamp(w.ProgressThickness,3,14))),
        "GameLogo" => new(w.X,w.Y,Math.Min(Width-w.X,
            Math.Clamp(w.GameLogoSize,12,54)),
            Math.Min(Height-w.Y,Math.Clamp(w.GameLogoSize,12,54))),
        "Lyrics" => new(w.X,w.Y,Math.Min(Width-w.X,w.WidgetWidth),
            Math.Min(Height-w.Y,Math.Clamp(w.LyricLines,1,4)*8+8)),
        "AudioVisualizer" or "AnimatedGif" => new(w.X,w.Y,
            Math.Min(Width-w.X,w.WidgetWidth),
            Math.Min(Height-w.Y,w.VisualizerHeight)),
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
        "VolumeChatMix" when w.DialStyle=="Wheel" => WheelBounds(w),
        "VolumeChatMix" when w.DialStyle=="MiniWheel" =>
            new(Math.Max(0,w.X-26),Math.Max(10,w.Y-14),
                Math.Min(128,MiniWheelArtwork.Width+75),
                Math.Min(64-Math.Max(10,w.Y-14),MiniWheelArtwork.Height+27)),
        "VolumeChatMix" => new(w.X,w.Y,Math.Min(Width-w.X,
            Math.Clamp(w.MiniWidth,60,128)),25),
        _ => new(w.X,w.Y,Math.Max(5,TextWidth(DisplayText(w),w.FontSize)),TextHeight(w.FontSize))
    };

    private static Rectangle WheelBounds(DockWidget w)
    {
        // Custom endpoint icon offsets may extend beyond the 45×42 ring.
        // Expand only the transparent wheel tile clipping area, preserving
        // the rest of the OLED and other widgets.
        // Placement solver keeps entire icon sprites visible. Reserve
        // additional surrounding OLED pixels without changing 45x42 source.
        int margin=35+new[]{w.WheelGameOffsetX,w.WheelGameOffsetY,
            w.WheelChatOffsetX,w.WheelChatOffsetY}.Max(v=>Math.Abs(v));
        int left=Math.Max(0,w.X-margin),top=Math.Max(10,w.Y-margin);
        int right=Math.Min(Width,w.X+WheelArtwork.Width+margin);
        // Never let optional icon offsets invade the separately reserved
        // battery area at the bottom of the default Game/Idle OLED layout.
        int bottom=Math.Min(53,w.Y+WheelArtwork.Height+margin);
        return Rectangle.FromLTRB(left,top,right,bottom);
    }

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
        SystemTelemetrySnapshot? telemetry,SyncedLyricsSnapshot? lyrics,
        AudioVisualizationSnapshot? audio,GameLogoSnapshot? gameLogo)
    {
        int x=w.X,y=w.Y;
        switch(w.Kind)
        {
            case "Lyrics":
                if(playback?.IsPlaying==true && lyrics?.TrackKey==
                   SyncedLyricsProvider.Key(playback))
                    DrawIslandLyrics(px,w,playback,lyrics);
                break;
            case "AudioVisualizer":
                DrawAudioVisualization(px,w,audio);
                break;
            case "AnimatedGif":
                var mono=AnimatedGifWidgetStore.Frame(w,DateTimeOffset.UtcNow);
                if(mono is not null)
                    for(int gy=0;gy<mono.Height;gy++)
                    for(int gx=0;gx<mono.Width;gx++)
                        if(mono.Pixels[gy*mono.Width+gx])
                            Dot(px,x+gx,y+gy);
                break;
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
                var reading=batteries?[device];
                string defaultIcon=reading?.IconKind?.Length>0
                    ?reading.IconKind
                    :device.StartsWith("Ble:",StringComparison.OrdinalIgnoreCase) ||
                     device.StartsWith("Hid:",StringComparison.OrdinalIgnoreCase) ||
                     device.StartsWith("WinBt:",StringComparison.OrdinalIgnoreCase) ||
                     device.StartsWith("Vendor:",StringComparison.OrdinalIgnoreCase)
                        ?BatteryDeviceCatalog.Icon(device):device;
                if(device=="Phone" &&
                   defaultIcon is "Bluetooth" or "Wireless" or "Usb")
                    defaultIcon="Phone";
                string iconKind=BatteryDeviceIconOverrides.Resolve(
                    device,reading?.FriendlyName,defaultIcon);
                string pct=reading?.Display??"--%";
                var layout=PlanBatteryRow(x,y,iconKind,pct);
                DrawBatterySlotIcon(px,iconKind,x,layout.IconY,
                    layout.IconWidth,layout.IconHeight);
                if(layout.Compact)
                    DrawCompactBatteryText(px,pct,layout.TextX,y+3);
                else DrawText(px,pct,layout.TextX,y+2,1);
                int boltX=layout.TextX+layout.TextWidth+2;
                if(reading?.IsCharging==true)
                {
                    var chargingIcon=IconLibrary.GetCurrent("Charging");
                    if(boltX+chargingIcon[0].Length<=Math.Min(Width,x+42))
                        DrawChargeBolt(px,boltX,y+2,chargingIcon);
                }
                break;
            case "Progress":
                DrawProgress(px,x,y,Math.Min(
                    Math.Clamp(w.ProgressWidth,12,128),Width-x),
                    Math.Clamp(w.ProgressThickness,3,14),
                    playback?.IsPlaying==true ? playback.ProgressPercent : 46);
                break;
            case "GameLogo":
                if(game?.IsRunning==true&&gameLogo is not null&&
                   string.Equals(game.Executable,gameLogo.Executable,
                       StringComparison.OrdinalIgnoreCase))
                    DrawGameLogo(px,w,gameLogo);
                break;
            case "Clock":
                DrawTextWithOptionalClip(px,w,target=>
                {
                    if(w.FontSize>=15)
                        DrawClock(target,DisplayText(w),x,y,
                            Math.Clamp(w.StrokeWidth,1,3));
                    else DrawReadableDateTime(target,DisplayText(w),x,y,
                        Scale(w.FontSize),Math.Clamp(w.StrokeWidth,1,3));
                });
                break;
            case "MediaClock":
                DrawTextWithOptionalClip(px,w,target=>
                    DrawReadableDateTime(target,DisplayText(w),x,y,
                        Scale(w.FontSize),Math.Clamp(w.StrokeWidth,1,3)));
                break;
            case "GameTitle":
                DrawMarquee(px,game?.IsRunning==true?game.Name:
                    "GAME NOT RUNNING",x,y,Scale(w.FontSize),
                    TextAreaWidth(w),w.StrokeWidth);
                break;
            case "GameLabel":
                string label=w.Text?.Trim()??"";
                if(label.Length==0 || label.Equals("GAME:",StringComparison.OrdinalIgnoreCase) ||
                   label.Equals("NOW PLAYING:",StringComparison.OrdinalIgnoreCase) ||
                   label.Equals("OYUN:",StringComparison.OrdinalIgnoreCase))
                    label=UiLanguage.English?"Game:":"Oyun:";
                DrawMarquee(px,label,x,y,Scale(w.FontSize),
                    TextAreaWidth(w),w.StrokeWidth);
                break;
            case "GameMiniPlayer":
                DrawGameMiniPlayer(px,w,playback);
                break;
            case "VolumeChatMix":
                DrawDialWidget(px,w,dial);
                break;
            case "SystemCpu":
                DrawMarquee(px,$"CPU {FormatPercent(telemetry?.Cpu)}",
                    x,y,Scale(w.FontSize),TextAreaWidth(w),w.StrokeWidth);
                break;
            case "SystemRam":
                DrawMarquee(px,$"RAM {FormatPercent(telemetry?.Memory)}",
                    x,y,Scale(w.FontSize),TextAreaWidth(w),w.StrokeWidth);
                break;
            case "NetworkPing":
                DrawMarquee(px,$"PING {FormatPing(telemetry?.Ping)}",
                    x,y,Scale(w.FontSize),TextAreaWidth(w),w.StrokeWidth);
                break;
            case "Track":
            case "Artist":
                DrawMarquee(px,DisplayText(w,playback),x,y,Scale(w.FontSize),
                    TextAreaWidth(w),w.StrokeWidth);
                break;
            case "Date":
                DrawTextWithOptionalClip(px,w,target=>
                    DrawReadableDateTime(target,DisplayText(w),x,y,
                        Scale(w.FontSize),Math.Clamp(w.StrokeWidth,1,3)));
                break;
            default:
                DrawMarquee(px,DisplayText(w),x,y,Scale(w.FontSize),
                    TextAreaWidth(w),w.StrokeWidth);
                break;
        }
    }

    /// <summary>
    /// OLED interpretation of the uploaded Dynamic Island example:
    /// a compact media glyph at the LEFT; current lyric glides
    /// LEFT through a wide, clipped single-line viewport. On a timecoded LRC
    /// line the old sentence rises/dims while the next rises from BELOW.
    /// All phase values depend on real song position, never wall-clock
    /// counters; a seek immediately picks the corresponding line.
    /// </summary>
    private static void DrawIslandLyrics(bool[] pixels,DockWidget widget,
        MediaSnapshot playback,SyncedLyricsSnapshot lyrics)
    {
        int left=Math.Clamp(widget.X,0,127);
        int top=Math.Clamp(widget.Y,0,63);
        int right=Math.Min(Width,left+Math.Clamp(widget.WidgetWidth,16,128));
        int rows=Math.Clamp(widget.LyricLines,1,4);
        int bottom=Math.Min(Height,top+rows*8+8);
        int baseline=top+4;
        if(right-left<16||bottom-baseline<7||lyrics.Lines.Count==0)return;

        bool hasIcon=right-left>=25;
        int textLeft=left+(hasIcon?12:0);
        int available=right-textLeft;
        if(available<10)return;
        if(hasIcon)
        {
            int iconX=left+1,iconY=baseline;
            for(int dy=0;dy<7;dy++)for(int dx=0;dx<7;dx++)
                if(dx==0||dx==6||dy==0||dy==6||
                   (dx==3&&(dy==2||dy==4))||(dx==4&&dy==3))
                    Dot(pixels,iconX+dx,iconY+dy);
        }

        // The offset applies to the ACTUAL interpolated media position
        // before timecode lookup, wrapping, notes and transition animation.
        // -500ms advances the lyrics; +500ms delays by half a second.
        TimeSpan at=SyncedLyricsProvider.PositionNow(playback)-
            TimeSpan.FromMilliseconds(widget.LyricOffsetMs);
        if(SyncedLyricsProvider.InstrumentalAt(lyrics.Lines,at))
        {
            DrawInstrumentalNote(pixels,textLeft+2,baseline,right,bottom);
            return;
        }
        var motion=IslandLyricsMotion.Evaluate(lyrics.Lines,at,available);
        if(motion.CurrentIndex<0)return;
        int movement=(int)Math.Round(rows*IslandLyricsMotion.LineHeight*
            motion.Progress);
        string transition=widget.LyricTransitionStyle;
        if(motion.Transitioning&&transition!="Instant")
        {
            int oldY=baseline-movement;
            int newY=baseline+rows*IslandLyricsMotion.LineHeight-movement;
            float oldAlpha=1f-motion.Progress;
            float newAlpha=Math.Max(.3f,motion.Progress);
            int clipRight=right;
            switch(transition)
            {
                case "Fade":
                    oldY=baseline;
                    newY=baseline;
                    newAlpha=motion.Progress;
                    break;
                case "Drop":
                    oldY=baseline+movement;
                    newY=baseline-rows*IslandLyricsMotion.LineHeight+movement;
                    break;
                case "Wipe":
                    oldY=baseline;
                    newY=baseline;
                    clipRight=Math.Clamp(textLeft+
                        (int)Math.Round(available*motion.Progress),
                        textLeft,right);
                    newAlpha=1f;
                    break;
            }
            DrawLyricRows(pixels,motion.PreviousText,textLeft,oldY,
                textLeft,right,top,bottom,oldAlpha,rows,available,
                motion.PreviousScrollPx,1,1,widget.StrokeWidth);
            DrawLyricRows(pixels,motion.CurrentText,textLeft,newY,
                textLeft,clipRight,top,bottom,newAlpha,rows,available,
                motion.CurrentScrollPx,1,1,widget.StrokeWidth);
        }
        else
        {
            long now=Math.Max(0,(long)at.TotalMilliseconds);
            long start=(long)lyrics.Lines[motion.CurrentIndex].At.TotalMilliseconds;
            long next=motion.CurrentIndex+1<lyrics.Lines.Count
                ?(long)lyrics.Lines[motion.CurrentIndex+1].At.TotalMilliseconds
                :start+5000;
            DrawLyricRows(pixels,motion.CurrentText,textLeft,baseline,
                textLeft,right,top,bottom,1f,rows,available,
                motion.CurrentScrollPx,Math.Max(0,now-start),
                Math.Max(500,next-start),widget.StrokeWidth);
        }
    }

    private static void DrawInstrumentalNote(bool[] pixels,int x,int y,
        int clipRight,int clipBottom)
    {
        // Compact 7x7 note drawn ONLY when no lyric is scheduled.
        // This is a visual placeholder, not an extra timecoded LRC entry.
        for(int dy=0;dy<5;dy++)
            if(x+5<clipRight && y+dy<clipBottom)Dot(pixels,x+5,y+dy);
        for(int dx=2;dx<=5;dx++)
            if(x+dx<clipRight&&y<clipBottom)Dot(pixels,x+dx,y);
        for(int dy=0;dy<2;dy++)for(int dx=0;dx<3;dx++)
            if(dx+dy<3&&x+dx<clipRight&&y+5+dy<clipBottom)
                Dot(pixels,x+dx,y+5+dy);
    }

    private static void DrawLyricRows(bool[] pixels,string text,
        int x,int startY,int clipLeft,int clipRight,int clipTop,
        int clipBottom,float opacity,int rows,int width,int singleScroll,
        long elapsed,long duration,int stroke)
    {
        if(string.IsNullOrWhiteSpace(text))return;
        int weight=Math.Clamp(stroke,1,3);
        var segments=IslandLyricsMotion.WrapRows(text,
            Math.Max(6,(width*6)/(6+weight-1)),rows);
        for(int row=0;row<segments.Length;row++)
        {
            string segment=segments[row];
            int scroll=rows==1&&weight==1?singleScroll:
                IslandLyricsMotion.ScrollRow(segment,width,elapsed,duration,
                    weight);
            DrawIslandGlyphs(pixels,segment,x-scroll,startY+row*8,
                clipLeft,clipRight,clipTop,clipBottom,opacity,weight);
        }
    }

    private static void DrawIslandGlyphs(bool[] pixels,string text,
        int startX,int startY,int clipLeft,int clipRight,
        int clipTop,int clipBottom,float opacity,int stroke)
    {
        if(string.IsNullOrWhiteSpace(text)||opacity<=0)return;
        string label=ToPixelAlphabet(text);
        int weight=Math.Clamp(stroke,1,3);
        int advance=6+weight-1;
        int keep=Math.Clamp((int)Math.Round(opacity*16),0,16);
        // Keep the raster entirely inside the widget; never spill glyphs
        // onto neighboring media widgets or the protected OEM header.
        for(int i=0;i<label.Length;i++)
        {
            int gx0=startX+i*advance;
            if(gx0>=clipRight)break;
            if(gx0+5<=clipLeft)continue;
            if(!Glyphs.TryGetValue(label[i],out var glyph))continue;
            for(int gy=0;gy<7;gy++)
            for(int gx=0;gx<5;gx++)
            {
                int px=gx0+gx,py=startY+gy;
                if(px<clipLeft||px>=clipRight||
                   py<clipTop||py>=clipBottom||
                   glyph[gy][gx]!='1')continue;
                // 4x4 distributed opacity mask for monochrome fade.
                if(((px&3)*5+(py&3)*3)%16<keep)
                    for(int weightX=0;weightX<weight;weightX++)
                        if(px+weightX<clipRight)
                            Dot(pixels,px+weightX,py);
            }
        }
    }

    private sealed class ParticleMotion
    {
        internal long LastTick=Environment.TickCount64;
        internal double Phase;
    }
    private static readonly ConditionalWeakTable<DockWidget,ParticleMotion>
        ParticleStates=new();

    private static double EasedParticlePhase(DockWidget widget,
        float[] left,float[] right,float gain)
    {
        float bass=0;
        for(int i=0;i<3;i++)
            bass+=Math.Clamp((left[i]+right[i])*.5f*gain,0,1)/3f;
        var motion=ParticleStates.GetValue(widget,_=>new ParticleMotion());
        lock(motion)
        {
            long now=Environment.TickCount64;
            double elapsed=Math.Clamp((now-motion.LastTick)/1000.0,0,.25);
            motion.LastTick=now;
            // Integrate speed, rather than multiply elapsed wall time by
            // instantaneous bass (which would teleport the bubbles on beats).
            motion.Phase+=elapsed*(.16+Math.Sqrt(bass)*2.3);
            return motion.Phase;
        }
    }

    private static void DrawAudioVisualization(bool[] pixels,DockWidget widget,
        AudioVisualizationSnapshot? audio)
    {
        // An unconnected/silent endpoint still shows a 1-pixel baseline.
        // Previously all three styles vanished completely without PCM.
        int x=widget.X,y=widget.Y;
        int width=Math.Min(Math.Clamp(widget.WidgetWidth,16,128),128-x);
        int height=Math.Min(Math.Clamp(widget.VisualizerHeight,8,54),64-y);
        if(width<2||height<2)return;
        float gain=Math.Clamp(widget.VisualizerGain,10,400)/100f;
        var left=audio?.Fresh==true?audio.Left:AudioVisualizationSnapshot.Silent.Left;
        var right=audio?.Fresh==true?audio.Right:AudioVisualizationSnapshot.Silent.Right;
        if(left.Length<12||right.Length<12)return;
        if(widget.VisualizerStyle is "Bubbles" or "Pulse")
        {
            DrawReactiveParticles(pixels,widget.VisualizerStyle,x,y,
                width,height,left,right,gain,
                EasedParticlePhase(widget,left,right,gain));
            return;
        }
        if(widget.VisualizerStyle=="Circular")
        {
            double cx=x+(width-1)/2.0,cy=y+(height-1)/2.0;
            int radius=Math.Max(2,Math.Min(width,height)/2-2);
            // Music-responsive centre ring: only the actual low-frequency
            // energy changes its radius, so silence stays completely still.
            float bass=0;
            for(int band=0;band<3;band++)
                bass+=(left[band]+right[band])/6f;
            double beat=Math.Sqrt(Math.Clamp(bass*gain,0f,1f));
            int inner=Math.Clamp(Math.Max(2,radius/4)+
                (int)Math.Round(beat*Math.Max(1,radius/7.0)),
                2,Math.Max(2,radius-3));
            for(int step=0;step<96;step++)
            {
                double angle=step*Math.PI*2/96;
                Dot(pixels,(int)Math.Round(cx+Math.Cos(angle)*inner),
                    (int)Math.Round(cy+Math.Sin(angle)*inner));
            }

            // Wide, two-pixel stereo rays communicate the beat much more
            // clearly on a 1-bit OLED than the old thin/short spokes.
            // Square-root compression emphasizes quiet REAL PCM energy
            // without generating fictional animation when the audio is off.
            int travel=Math.Max(0,radius-inner-2);
            for(int index=0;index<24;index++)
            {
                double angle=-Math.PI/2+index*Math.PI*2/24;
                float level=index<12?left[index]:right[index-12];
                double reactive=Math.Sqrt(Math.Clamp(level*gain*2.2f,0f,1f));
                if(reactive<.055||travel==0)continue;
                int length=Math.Max(1,(int)Math.Round(reactive*travel));
                double outwardX=Math.Cos(angle),outwardY=Math.Sin(angle);
                double tangentX=-outwardY,tangentY=outwardX;
                for(int distance=inner+2;distance<=Math.Min(radius,inner+2+length);
                    distance++)
                {
                    double centerX=cx+outwardX*distance;
                    double centerY=cy+outwardY*distance;
                    Dot(pixels,(int)Math.Round(centerX+tangentX*.65),
                        (int)Math.Round(centerY+tangentY*.65));
                    Dot(pixels,(int)Math.Round(centerX-tangentX*.65),
                        (int)Math.Round(centerY-tangentY*.65));
                }
            }
            return;
        }
        int count=widget.VisualizerStyle=="Stereo"?24:12;
        for(int index=0;index<count;index++)
        {
            float value=widget.VisualizerStyle=="Stereo"
                ?index<12?left[11-index]:right[index-12]
                :(left[index]+right[index])*.5f;
            int barHeight=Math.Max(1,(int)Math.Round(
                Math.Clamp(value*gain,0,1)*height));
            int start=x+(int)Math.Floor(index*width/(double)count);
            int cellRight=x+(int)Math.Floor((index+1)*width/(double)count);
            int end=cellRight-1-(cellRight-start>=4?1:0);
            for(int bx=start;bx<=end;bx++)
            for(int by=y+height-barHeight;by<y+height;by++)
                Dot(pixels,bx,by);
        }
    }

    /// <summary>
    /// No frame accumulation or particle allocations. All positions derive
    /// from wall-clock phase and verified live WASAPI energy. Slower when
    /// quiet, faster when bass peaks; no invented activity in silence.
    /// Draws only within the assigned widget rectangle on the 1-bit OLED.
    /// </summary>
    internal static void DrawReactiveParticles(bool[] pixels,string style,
        int x,int y,int width,int height,float[] left,float[] right,
        float gain,double motionPhase)
    {
        float bass=0,energy=0;
        for(int i=0;i<12;i++)
        {
            float level=Math.Clamp((left[i]+right[i])*.5f*gain,0,1);
            energy+=level/12f;
            if(i<3)bass+=level/3f;
        }
        if(energy<.025f)return;
        if(style=="Pulse")
        {
            int cx=x+width/2,cy=y+height/2;
            int radius=Math.Max(1,Math.Min(width,height)/2-2);
            int ringCount=Math.Clamp(1+(int)(Math.Sqrt(bass)*4),1,5);
            double beat=Math.Clamp(bass*2,0,1);
            for(int ring=0;ring<ringCount;ring++)
            {
                double r=Math.Max(1,radius*(ring+1.0)/(ringCount+1)*
                    (0.7+0.3*beat));
                for(int i=0;i<60;i++)
                {
                    double theta=i*Math.PI*2/60;
                    int px=(int)Math.Round(cx+Math.Cos(theta)*r);
                    int py=(int)Math.Round(cy+Math.Sin(theta)*r);
                    if(px>=x&&px<x+width&&py>=y&&py<y+height)
                        Dot(pixels,px,py);
                }
            }
            return;
        }
        // Each bubble has a deterministic speed that rises with the beat.
        // The song audio is never buffered or retained by the widget.
        double steps=motionPhase;
        int count=Math.Clamp(4+(int)(energy*12),4,15);
        for(int index=0;index<count;index++)
        {
            double phase=(steps*(.6+(index%3)*.2)+index*.241)%1;
            int cx=x+2+(index*37+index*index*11)%
                Math.Max(1,width-4);
            int cy=y+height-1-(int)Math.Round(phase*(height-2));
            int radius=1+(index%3==0&&energy>.12f?1:0)+
                (bass>.40f&&index%4==0?1:0);
            for(int angle=0;angle<24;angle++)
            {
                double theta=angle*Math.PI*2/24;
                int px=(int)Math.Round(cx+Math.Cos(theta)*radius);
                int py=(int)Math.Round(cy+Math.Sin(theta)*radius);
                if(px>=x&&px<x+width&&py>=y&&py<y+height)
                    Dot(pixels,px,py);
            }
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

        if(widget.DialStyle=="MiniWheel")
        {
            MiniWheelArtwork.Draw(tile,mask,x,y,WheelArtwork.Value(snapshot,fresh),
                snapshot.View,widget.WheelGapAngle,
                widget.WheelRingThickness,widget.WheelShowGame,widget.WheelShowChat,
                widget.WheelGameOffsetX,widget.WheelGameOffsetY,
                widget.WheelChatOffsetX,widget.WheelChatOffsetY,
                widget.MiniWheelSize,widget.MiniWheelPixels,
                widget.WheelAutoLabel,widget.WheelCloseEnds);
        }
        else if(widget.DialStyle=="Wheel")
        {
            WheelArtwork.Draw(tile,mask,x,y,widget.WheelPixels,
                WheelArtwork.Value(snapshot,fresh),snapshot.View,widget.WheelAutoLabel,
                widget.WheelGapAngle,
                fresh&&DateTimeOffset.UtcNow-snapshot.UpdatedAt<TimeSpan.FromSeconds(2)
                    ?snapshot.VolumeAttenuationSteps:null,
                fresh&&DateTimeOffset.UtcNow-snapshot.UpdatedAt<TimeSpan.FromSeconds(2),
                widget.WheelIconsFollowRing,widget.WheelGameOffsetX,
                widget.WheelGameOffsetY,widget.WheelChatOffsetX,
                widget.WheelChatOffsetY,widget.WheelShowGame,widget.WheelShowChat,
                widget.WheelCloseEnds,widget.WheelRingThickness);
        }
        else
        {

        if(snapshot.View==DialView.ChatMix)
        {
            DrawSplitChatMix(tile,x,y,width,
                WheelArtwork.Value(snapshot,fresh),
                widget.WheelShowGame,widget.WheelShowChat,
                widget.BarFrameThickness,widget.BarFillThickness);
        }
        else
        {
            DrawText(tile,"VOLUME",x+1,y+1,1);
            int? volume=fresh?snapshot.Volume:null;
            DrawText(tile,volume.HasValue?$"{volume.Value}%":"--%",
                x+49,y+1,1);
            DrawBarWithThickness(tile,x+1,y+14,width,volume??0,
                widget.BarFrameThickness,widget.BarFillThickness);
        }
        }
        for(int yy=Math.Max(0,bounds.Top);yy<Math.Min(Height,bounds.Bottom);yy++)
        for(int xx=Math.Max(0,bounds.Left);xx<Math.Min(Width,bounds.Right);xx++)
            if(EditableHeader.Value||yy>=ReservedTop)
            {
                // A wheel is a transparent 1-bit sprite except where it
                // explicitly draws lit or black pixels. Other widgets can
                // occupy its curved corners without being wiped away.
                if(widget.DialStyle is "Wheel" or "MiniWheel")
                {
                    if(mask[yy*Width+xx])px[yy*Width+xx]=tile[yy*Width+xx];
                }
                else px[yy*Width+xx]=tile[yy*Width+xx];
            }
    }

    // Two independent halves: neutral balance is completely empty, while
    // the center divider is always visible. The left side is Game, the right
    // side is Chat; both use user-customizable pixel Icon Workshop sprites.
    internal static void DrawSplitChatMix(bool[] px,int x,int y,int width,
        int? mix,bool showGame,bool showChat,int borderThickness,int fillThickness)
    {
        width=Math.Clamp(width,26,128-Math.Max(0,x));
        int center=x+width/2;
        int barY=y+14,barHeight=8;
        int left=x+1,right=x+width;
        int frame=Math.Clamp(borderThickness,1,2);
        int fillH=Math.Clamp(fillThickness,1,5);
        if(showGame)
        {
            var game=IconLibrary.GetCurrent("Game");
            DrawChargeBolt(px,x+1,y+1,game);
        }
        if(showChat)
        {
            var chat=IconLibrary.GetCurrent("Chat");
            DrawChargeBolt(px,right-chat[0].Length,y+1,chat);
        }
        for(int dx=0;dx<width;dx++)for(int dy=0;dy<barHeight;dy++)
        {
            bool edge=dx<frame || dx>=width-frame ||
                dy<frame || dy>=barHeight-frame || Math.Abs(x+dx-center)<frame;
            if(edge)Dot(px,x+dx,barY+dy);
        }
        // For the optical 0%, no fill; the middle is balance 50%.
        int position=mix??50;
        if(mix.HasValue && position!=50)
        {
            int innerHalf=Math.Max(0,(width/2)-frame-1);
            int fill=(int)Math.Round(Math.Abs(position-50)*2d*innerHalf/100d);
            int fy=barY+(barHeight-fillH)/2;
            for(int i=0;i<fill;i++)
            {
                int at=position>50?center-frame-1-i:center+frame+i;
                for(int yy=0;yy<fillH;yy++)
                    if(at>x+frame-1&&at<right-frame)
                        Dot(px,at,fy+yy);
            }
        }
    }

    private static void DrawBarWithThickness(bool[] pixels,int left,int top,
        int width,int value,int frameThickness,int fillThickness)
    {
        width=Math.Max(3,Math.Min(width,128-left));
        const int height=8;
        int frame=Math.Clamp(frameThickness,1,2),
            fillH=Math.Clamp(fillThickness,1,5);
        for(int dx=0;dx<width;dx++)for(int dy=0;dy<height;dy++)
            if(dx<frame||dx>=width-frame||dy<frame||dy>=height-frame)
                Dot(pixels,left+dx,top+dy);
        int amount=(int)Math.Round((width-2*frame)*
            Math.Clamp(value,0,100)/100d);
        int insideY=top+(height-fillH)/2;
        for(int dx=0;dx<amount;dx++)for(int dy=0;dy<fillH;dy++)
            Dot(pixels,left+frame+dx,insideY+dy);
    }

    // The charging badge is an editable Icon Workshop sprite, not the OEM header icon.
    private static void DrawChargeBolt(bool[] frame,int x,int y,string[] rows)
    {
        for(int row=0;row<rows.Length;row++)
        for(int col=0;col<rows[row].Length;col++)
            if(rows[row][col]=='1')Dot(frame,x+col,y+row);
    }

    private static void DrawProgress(bool[] px,int x,int y,int width,
        int height,int percentage)
    {
        if(width<3||height<3)return;
        height=Math.Min(height,Height-y);
        for(int i=0;i<width;i++){Dot(px,x+i,y,false);Dot(px,x+i,y+height-1,false);}
        for(int j=0;j<height;j++){Dot(px,x,y+j);Dot(px,x+width-1,y+j);}
        int fill=(width-2)*Math.Clamp(percentage,0,100)/100;
        for(int i=0;i<fill;i++)for(int j=1;j<height-1;j++)Dot(px,x+i+1,y+j);
    }

    private static void DrawGameLogo(bool[] pixels,DockWidget widget,
        GameLogoSnapshot snapshot)
    {
        int size=Math.Clamp(widget.GameLogoSize,12,54);
        var image=snapshot.Pixels;
        int src=snapshot.Size;
        if(src<=0||image.Length!=src*src)return;
        for(int row=0;row<size&&widget.Y+row<Height;row++)
        for(int col=0;col<size&&widget.X+col<Width;col++)
        {
            int sx=Math.Min(src-1,col*src/size);
            int sy=Math.Min(src-1,row*src/size);
            if(image[sy*src+sx])Dot(pixels,widget.X+col,widget.Y+row);
        }
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

    internal static Bitmap RenderDateTimeFontSample(string text,int weight,bool clock)
    {
        var pixels=new bool[Width*Height];
        if(clock)DrawClock(pixels,text,4,12,weight);
        else DrawReadableDateTime(pixels,text,4,12,1,weight);
        var bmp=new Bitmap(Width,Height);
        for(int yy=0;yy<Height;yy++)for(int xx=0;xx<Width;xx++)
            bmp.SetPixel(xx,yy,pixels[yy*Width+xx]?Color.White:Color.Black);
        return bmp;
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

    // A saved icon can be 20px wide. Battery labels used to begin at x+13,
    // so the user's 16px Controller sprite overlapped their "70%" reading.
    // Derive label placement from the ACTUAL saved 1-bit icon dimensions.
    internal readonly record struct BatteryRowLayout(
        int IconWidth,int IconHeight,int IconY,
        int TextX,int TextWidth,bool Compact);

    internal static BatteryRowLayout PlanBatteryRow(int x,int y,
        string iconKind,string pct)
    {
        var rows=IconLibrary.GetCurrent(iconKind);
        int originalWidth=rows.Length>0?rows[0].Length:0;
        int originalHeight=rows.Length;
        int slotRight=Math.Min(Width,x+42);
        int normalTextWidth=TextWidth(pct,7);
        int compactTextWidth=Math.Max(0,pct.Length*4-1);
        int iconWidth=originalWidth;
        int normalX=x+Math.Max(13,iconWidth+3);
        bool compact=normalX+normalTextWidth>slotRight;
        int gap=3;
        int textWidth=compact?compactTextWidth:normalTextWidth;
        int textX=normalX;
        if(compact && textX+textWidth>slotRight)
        {
            gap=1;
            textX=x+iconWidth+gap;
            if(textX+textWidth>slotRight)
            {
                // Scale only the on-screen rendering when a large icon
                // otherwise cannot coexist with four digit characters.
                // Source art and user widget coordinates remain unchanged.
                iconWidth=Math.Max(1,slotRight-x-gap-textWidth);
                textX=x+iconWidth+gap;
            }
        }
        int height=originalHeight;
        if(originalWidth>0 && iconWidth<originalWidth)
            height=(int)Math.Round(originalHeight*
                iconWidth/(double)originalWidth);
        height=Math.Clamp(height,0,11);
        int iconY=Math.Min(y,Height-height);
        return new BatteryRowLayout(iconWidth,height,iconY,
            textX,textWidth,compact);
    }

    private static readonly Dictionary<char,string[]> CompactBatteryGlyphs=new()
    {
        ['0']=["111","101","101","101","111"],
        ['1']=["010","110","010","010","111"],
        ['2']=["111","001","111","100","111"],
        ['3']=["111","001","111","001","111"],
        ['4']=["101","101","111","001","001"],
        ['5']=["111","100","111","001","111"],
        ['6']=["111","100","111","101","111"],
        ['7']=["111","001","010","010","010"],
        ['8']=["111","101","111","101","111"],
        ['9']=["111","101","111","001","111"],
        ['%']=["101","001","010","100","101"],
        ['-']=["000","000","111","000","000"]
    };

    private static void DrawCompactBatteryText(bool[] frame,string text,int x,int y)
    {
        foreach(char c in text)
        {
            if(CompactBatteryGlyphs.TryGetValue(c,out var rows))
                for(int yy=0;yy<rows.Length;yy++)
                for(int xx=0;xx<3;xx++)
                    if(rows[yy][xx]=='1')Dot(frame,x+xx,y+yy);
            x+=4;
        }
    }

    private static void DrawBatterySlotIcon(bool[] frame,string kind,int x,int y,
        int targetWidth,int targetHeight)
    {
        if(targetWidth<=0||targetHeight<=0)return;
        var rows=IconLibrary.GetCurrent(kind);
        if(rows.Length==0)return;
        int sourceWidth=rows[0].Length,sourceHeight=rows.Length;
        // All battery icons, including per-device imported 11x11 artwork,
        // render from the same saved one-bit rows. Never bypass imported
        // artwork via the global icon-name-only drawing path.
        // Nearest-neighbor 1-bit projection with no modified source pixels.
        for(int yy=0;yy<targetHeight;yy++)
        for(int xx=0;xx<targetWidth;xx++)
        {
            int sy=Math.Clamp((int)((yy+.5)*sourceHeight/targetHeight),
                0,sourceHeight-1);
            int sx=Math.Clamp((int)((xx+.5)*sourceWidth/targetWidth),
                0,sourceWidth-1);
            if(rows[sy][sx]=='1')Dot(frame,x+xx,y+yy);
        }
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
    public const int MarqueePixelsPerSecond=28;
    internal static int MarqueeOffset(int textWidth,int displayWidth,long seconds,int gap=24)
    {
        if(textWidth<=displayWidth)return 0;
        int cycle=textWidth+gap; // Shorter seam for compact game mini titles.
        return (int)((seconds*MarqueePixelsPerSecond)%cycle);
    }

    internal static int MarqueeOffsetMs(int textWidth,int displayWidth,
        long milliseconds,int gap=24)
    {
        if(textWidth<=displayWidth)return 0;
        long cycle=textWidth+gap;
        long pixels=(milliseconds*MarqueePixelsPerSecond/1000)%cycle;
        return (int)((pixels+cycle)%cycle);
    }

    /// <summary>
    /// 60 ms pixel marquee, optionally confined to a mini-player box.
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
            ?MarqueeOffsetMs(labelWidth,displayWidth,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),gap)
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

    // Thick time/date fonts must not simply stamp 3x3 squares over adjacent
    // 5x7 characters. Each glyph gets its own width and its enclosed holes
    // are preserved. If a long date does not fit, reduce stroke before losing
    // digits at the OLED's right edge.
    internal static void DrawReadableDateTime(bool[] output,string text,
        int x,int y,int scale,int requested)
    {
        string label=ToPixelAlphabet(text);
        if(label.Length==0)return;
        int weight=Math.Clamp(requested,1,3);
        scale=Math.Clamp(scale,1,2);
        while(scale>1 && label.Length*(5*scale+weight+1)>Width-x)scale--;
        while(weight>1 && label.Length*(5*scale+weight)>Width-x)weight--;
        int advance=5*scale+weight;
        foreach(var ch in label)
        {
            if(Glyphs.TryGetValue(ch,out var rows))
            {
                int tileW=5*scale+weight-1,tileH=7*scale+weight-1;
                bool[,] tile=new bool[tileH,tileW];
                for(int yy=0;yy<7;yy++)
                for(int xx=0;xx<5;xx++)
                    if(rows[yy][xx]=='1')
                        for(int ty=0;ty<scale+weight-1;ty++)
                        for(int tx=0;tx<scale+weight-1;tx++)
                        {
                            int xx0=xx*scale+tx,yy0=yy*scale+ty;
                            if(xx0<tileW && yy0<tileH)tile[yy0,xx0]=true;
                        }
                // Protect enclosed zero-counters, even when glyph is bold.
                for(int yy=0;yy<7;yy++)for(int xx=0;xx<5;xx++)
                    if(rows[yy][xx]=='0' && IsEnclosedGlyphCell(rows,xx,yy))
                        for(int ty=0;ty<scale;ty++)for(int tx=0;tx<scale;tx++)
                            tile[yy*scale+ty,xx*scale+tx]=false;
                for(int gy=0;gy<tileH;gy++)
                for(int gx=0;gx<tileW;gx++)
                    if(tile[gy,gx])Dot(output,x+gx,y+gy);
            }
            x+=advance;
            if(x>=Width)break;
        }
    }

    private static bool IsEnclosedGlyphCell(string[] rows,int gx,int gy)
    {
        if(gx<0||gy<0||gx>=5||gy>=7||rows[gy][gx]!='0')
            return false;
        var visited=new bool[7,5];
        var pending=new Stack<(int X,int Y)>();
        pending.Push((gx,gy));
        while(pending.Count>0)
        {
            var (x,y)=pending.Pop();
            if(x<0||x>=5||y<0||y>=7)return false;
            if(visited[y,x]||rows[y][x]=='1')continue;
            visited[y,x]=true;
            pending.Push((x+1,y));pending.Push((x-1,y));
            pending.Push((x,y+1));pending.Push((x,y-1));
        }
        return true;
    }

    // Fixed 9×15 seven-segment digits: 1-3px strokes grow toward the
    // *inside* of each segment. Both counters stay open even for 8 at 3px.
    internal static void DrawClock(bool[] px,string value,int x,int y,int thickness)
    {
        const int SegT=1,SegUL=2,SegUR=4,SegM=8,SegLL=16,SegLR=32,SegB=64;
        int[] map=[119,36,93,109,46,107,123,37,127,111];
        int t=Math.Clamp(thickness,1,3);
        void H(int dx,int dy)
        {
            for(int yy=0;yy<t;yy++)for(int xx=2;xx<=6;xx++)
                Dot(px,x+dx+xx,y+dy+yy);
        }
        void V(int dx,int dy)
        {
            for(int yy=0;yy<=3;yy++)for(int xx=0;xx<t;xx++)
                Dot(px,x+dx+xx,y+dy+yy);
        }
        foreach(char ch in value)
        {
            if(ch==':')
            {
                for(int a=0;a<t;a++)for(int b=0;b<t;b++)
                {
                    Dot(px,x+2+a,y+4+b);
                    Dot(px,x+2+a,y+10+b);
                }
                x+=6;continue;
            }
            if(!char.IsDigit(ch)){x+=11;continue;}
            int flags=map[ch-'0'];
            if((flags&SegT)!=0)H(0,0);
            if((flags&SegUL)!=0)V(0,2);
            if((flags&SegUR)!=0)V(9-t,2);
            if((flags&SegM)!=0)H(0,6);
            if((flags&SegLL)!=0)V(0,10);
            if((flags&SegLR)!=0)V(9-t,10);
            if((flags&SegB)!=0)H(0,15-t);
            x+=11;
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

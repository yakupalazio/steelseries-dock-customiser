using System;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Monochrome 45×42 source sprite for the hardware volume/ChatMix widget.
/// 0: transparent, 1: permanently lit, 2: percentage-controlled ring pixel,
/// 3: opaque black. Every cell represents exactly one physical OLED pixel.
/// The native-style centre badge is always VOL/MIX according to the wheel.
/// A user's custom sprite is saved inside the selected DockWidget layout.
/// </summary>
internal static class WheelArtwork
{
    public const int Width=45, Height=42, StartAngle=210, Sweep=300;
    public const int CenterX=22, CenterY=21;
    private static readonly string[] ThicknessTemplates=Enumerable.Range(1,7)
        .Select(CreateDefault).ToArray();
    private static readonly string Default=ThicknessTemplates[2];
    public static string Factory=>Default;

    public static bool IsValid(string? pixels)
    {
        if(pixels?.Length!=Width*Height)return false;
        foreach(char value in pixels)
            if(value is not ('0' or '1' or '2' or '3'))return false;
        return true;
    }

    public static string Resolve(string? pixels)=>IsValid(pixels)?pixels!:Default;
    private static string ResolveWithThickness(string? pixels,int thickness)
        => IsValid(pixels)?pixels!:ThicknessTemplates[Math.Clamp(thickness,1,7)-1];
    internal static string PreviewSource(string? pixels,int thickness)
        => ResolveWithThickness(pixels,thickness);

    private static string CreateDefault(int thickness)
    {
        var pixels=new char[Width*Height];
        Array.Fill(pixels,'0');
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
        {
            int dx=x-CenterX,dy=y-CenterY;
            double distance=Math.Sqrt(dx*dx+dy*dy);
            double angle=Angle(x,y);
            bool onSweep=angle<=Sweep;
            if(distance<=9.1)
                pixels[y*Width+x]='1';
            else if(onSweep)
            {
                double inner=19.25-thickness;
                if(distance>=19.25 && distance<=20.5 ||
                   distance>=inner-1.1 && distance<inner)
                    pixels[y*Width+x]='1';
                else if(distance>=inner && distance<19.25)
                    pixels[y*Width+x]='2';
            }
        }
        return new string(pixels);
    }

    private static double Angle(int x,int y)
    {
        double degrees=Math.Atan2(y-CenterY,x-CenterX)*180d/Math.PI;
        return (degrees-StartAngle+720d)%360d;
    }

    /// <summary>
    /// Source-space pixel at the displayed position. Both the empty track and
    /// its illuminated fill are rotated together; the badge is not rotated.
    /// Also used by the editor to avoid a stationary, misleading blue guide.
    /// </summary>
    public static (int X,int Y) SourceAt(int x,int y,int gapAngle)
    {
        int dx=x-CenterX,dy=y-CenterY;
        if(dx*dx+dy*dy<100)return (x,y);
        double radians=-Math.Clamp(gapAngle,-180,180)*Math.PI/180d;
        double cos=Math.Cos(radians),sin=Math.Sin(radians);
        return (CenterX+(int)Math.Round(dx*cos-dy*sin),
                CenterY+(int)Math.Round(dx*sin+dy*cos));
    }

    public static char Sample(string source,int x,int y,int gapAngle)
    {
        var (sx,sy)=SourceAt(x,y,gapAngle);
        return sx>=0&&sy>=0&&sx<Width&&sy<Height
            ?source[sy*Width+sx]:'0';
    }

    public static bool IsOn(char value,int x,int y,int? percent,
        DialView view=DialView.Volume)
        => value switch
        {
            '1'=>true,
            '2'=>percent.HasValue && DynamicSegment(Angle(x,y),percent.Value,view),
            _=>false
        };

    // Volume grows clockwise from the ring's first endpoint. ChatMix is
    // different: the middle is neutral and the illuminated segment grows
    // away from the center toward Game or Chat, as on the original GameDAC.
    private static bool DynamicSegment(double angle,int percentage,DialView view)
    {
        double stop=Sweep*Math.Clamp(percentage,0,100)/100d;
        if(view==DialView.Volume)return angle<=stop;
        return angle>=Math.Min(Sweep/2d,stop) &&
            angle<=Math.Max(Sweep/2d,stop);
    }

    public static int? Value(DialSnapshot snapshot,bool fresh)
    {
        if(!fresh)return null;
        if(snapshot.View==DialView.Volume)return snapshot.Volume;
        if(!snapshot.Game.HasValue||!snapshot.Chat.HasValue)return null;
        // Native ChatMix attenuates one channel while the other remains at
        // 100. The physical midpoint is G=100/C=100 (not a 50/50 gain split).
        // G=100/C=0 maps to the Game endpoint; G=0/C=100 to Chat.
        return Math.Clamp((int)Math.Round(50d+
            (snapshot.Game.Value-snapshot.Chat.Value)/2d),0,100);
    }

    public static void Draw(bool[] output,bool[] coverage,int x,int y,
        string? artwork,int? percent,DialView view,bool autoLabel=true,
        int gapAngle=0,int? attenuationSteps=null,bool transientAttenuation=false,
        bool followModeIcons=true,int gameOffsetX=0,int gameOffsetY=0,
        int chatOffsetX=0,int chatOffsetY=0,bool showGame=true,
        bool showChat=true,bool closeEnds=true,int ringThickness=3)
    {
        ringThickness=Math.Clamp(ringThickness,1,7);
        string src=ResolveWithThickness(artwork,ringThickness);
        for(int sy=0;sy<Height;sy++)for(int sx=0;sx<Width;sx++)
        {
            int px=x+sx,py=y+sy;
            if(px<0||py<0||px>=128||py>=64)continue;
            var (fromX,fromY)=SourceAt(sx,sy,gapAngle);
            if(fromX<0||fromY<0||fromX>=Width||fromY>=Height)continue;
            char value=src[fromY*Width+fromX];
            if(value=='0')continue;
            int dest=py*128+px;
            coverage[dest]=true;
            output[dest]=IsOn(value,fromX,fromY,percent,view);
        }
        // Draw short radial caps only at the two arc endpoints. These
        // are independently controllable and never alter saved user pixels.
        if(closeEnds)
        {
            DrawCap(output,coverage,x,y,StartAngle+gapAngle,ringThickness);
            DrawCap(output,coverage,x,y,StartAngle+Sweep+gapAngle,ringThickness);
        }
        // The original Nova Pro badge is a *filled* white disc with black
        // lettering, unlike the previous hollow badge with text to its right.
        if(autoLabel)
        {
            string label=view==DialView.ChatMix?"MIX":
                transientAttenuation&&attenuationSteps.HasValue
                    ?attenuationSteps.Value==0?"":$"-{Math.Clamp(attenuationSteps.Value,0,56)}"
                    :"VOL";
            DrawBadge(output,coverage,x,y,label);
        }
        if(view==DialView.ChatMix)
        {
            // Native 07 45 Game dominance fills the SECOND half of the
            // source ring (150..300°), Chat dominance the FIRST (0..150°).
            // The old code incorrectly labelled the opposite endpoints.
            // Manual offsets are constrained to each channel's own arc sector.
            if(showGame)
            {
                if(followModeIcons)
                    DrawEndpointIcon(output,coverage,x,y,"Game",
                        StartAngle+Sweep*.75+gapAngle,gameOffsetX,gameOffsetY,
                        StartAngle+gapAngle,gameSide:true);
                else DrawEndpointIcon(output,coverage,x,y,"Game",
                    StartAngle+Sweep*.75,gameOffsetX,gameOffsetY,
                    StartAngle+gapAngle,gameSide:true);
            }
            if(showChat)
            {
                if(followModeIcons)
                    DrawEndpointIcon(output,coverage,x,y,"Chat",
                        StartAngle+Sweep*.25+gapAngle,chatOffsetX,chatOffsetY,
                        StartAngle+gapAngle,gameSide:false);
                else DrawEndpointIcon(output,coverage,x,y,"Chat",
                    StartAngle+Sweep*.25,chatOffsetX,chatOffsetY,
                    StartAngle+gapAngle,gameSide:false);
            }
        }
    }

    private static void DrawCap(bool[] output,bool[] coverage,
        int x,int y,double angle,int thickness)
    {
        double rad=angle*Math.PI/180d;
        double inner=19.25-thickness-1.1;
        for(double r=inner;r<=20.6;r+=0.25)
        {
            int px=x+CenterX+(int)Math.Round(r*Math.Cos(rad));
            int py=y+CenterY+(int)Math.Round(r*Math.Sin(rad));
            Put(output,coverage,px,py,true);
        }
    }

    /// <summary>
    /// Find the nearest completely external icon placement, with one OLED
    /// pixel clearance around the actual ring bitmap. An endpoint close to
    /// the display edge slides along the free area rather than clipping.
    /// Icon pixels remain upright. Bounded computation: 37x37 positions.
    /// </summary>
    internal static (int X,int Y) EndpointIconPosition(
        int wheelX,int wheelY,string name,double angle,
        bool[] coverage,int offsetX=0,int offsetY=0,
        double? startAngle=null,bool? gameSide=null)
    {
        var rows=IconLibrary.GetCurrent(name);
        int width=rows[0].Length,height=rows.Length;
        double rad=angle*Math.PI/180d;
        double cx=wheelX+CenterX,cy=wheelY+CenterY;
        const double radius=24; // Keep icons close to their own audio arc.
        double wantX=cx+radius*Math.Cos(rad)-width/2d+offsetX;
        double wantY=cy+radius*Math.Sin(rad)-height/2d+offsetY;
        // Build a compact obstacle mask ONCE. Bitmap collision checks in
        // candidate loops were previously O(candidates × pixels × 9).
        var obstacles=new byte[128*64];
        for(int py=10;py<64;py++)for(int px=0;px<128;px++)
        {
            byte flags=0;
            double dx0=px-cx,dy0=py-cy;
            if(dx0*dx0+dy0*dy0<21.5*21.5)flags|=2;
            for(int by=Math.Max(0,py-1);by<=Math.Min(63,py+1);by++)
            for(int bx=Math.Max(0,px-1);bx<=Math.Min(127,px+1);bx++)
                if(coverage[by*128+bx])flags|=1;
            obstacles[py*128+px]=flags;
        }
        double best=double.PositiveInfinity;
        int bestX=-1,bestY=-1;
        int aroundX=Math.Clamp((int)Math.Round(wantX),0,128-width);
        int aroundY=Math.Clamp((int)Math.Round(wantY),10,64-height);
        // Search the free strip surrounding the ring, not just a small
        // square around the ideal anchor: at an OLED edge the ideal radial
        // position may be physically unavailable at some rotation angles.
        for(int top=10;top<=64-height;top++)
        for(int left=0;left<=128-width;left++)
        {
            int conflict=0,interior=0,wrongChannel=0;
            for(int yy=0;yy<height;yy++)for(int xx=0;xx<width;xx++)
            {
                if(rows[yy][xx]!='1')continue;
                int px=left+xx,py=top+yy;
                byte flags=obstacles[py*128+px];
                if((flags&2)!=0)interior++;
                if((flags&1)!=0)conflict++;
                if(startAngle.HasValue && gameSide.HasValue)
                {
                    double azimuth=Math.Atan2(py-cy,px-cx)*180d/Math.PI;
                    double progress=(azimuth-startAngle.Value+7200d)%360d;
                    bool own=gameSide.Value
                        ?progress>=Sweep/2d && progress<=Sweep
                        :progress>=0 && progress<=Sweep/2d;
                    if(!own)wrongChannel++;
                }
            }
            // If there's no perfect place near a screen edge, prefer a
            // fully visible icon with the fewest collisions.
            // Hard constraints: never show an icon on the wrong audio
            // channel, or overlap the ring/other icon. If a rotation leaves
            // no legal pixels, prefer not to show a misleading icon.
            if(conflict>0||interior>0||wrongChannel>0)continue;
            double displacement=(left-wantX)*(left-wantX)+
                (top-wantY)*(top-wantY);
            double score=displacement;
            if(score<best)
            {
                best=score;bestX=left;bestY=top;
            }
        }
        return (bestX,bestY);
    }

    private static void DrawEndpointIcon(bool[] output,bool[] coverage,
        int wheelX,int wheelY,string name,double angle,int offsetX,int offsetY,
        double startAngle,bool gameSide)
    {
        var rows=IconLibrary.GetCurrent(name);
        var (left,top)=EndpointIconPosition(wheelX,wheelY,name,angle,
            coverage,offsetX,offsetY,startAngle,gameSide);
        if(left<0||top<0)return; // no valid non-overlapping position
        for(int yy=0;yy<rows.Length;yy++)
        for(int xx=0;xx<rows[yy].Length;xx++)
            if(rows[yy][xx]=='1')Put(output,coverage,left+xx,top+yy,true);
    }

    private static void DrawModeIcon(bool[] output,bool[] coverage,
        int edgeX,int y,string name,bool alignRight)
    {
        var rows=IconLibrary.GetCurrent(name);
        int x=alignRight?edgeX-rows[0].Length:edgeX;
        for(int yy=0;yy<rows.Length;yy++)
        for(int xx=0;xx<rows[yy].Length;xx++)
            if(rows[yy][xx]=='1')Put(output,coverage,x+xx,y+yy,true);
    }

    private static void Put(bool[] output,bool[] coverage,int x,int y,bool on)
    {
        if(x<0||x>=128||y<0||y>=64)return;
        int pos=y*128+x;
        coverage[pos]=true;
        output[pos]=on;
    }

    private static void DrawBadge(bool[] output,bool[] coverage,
        int x,int y,string label)
    {
        static string[] Glyph(char ch)=>ch switch
        {
            'V'=>["101","101","101","101","010"],
            'O'=>["010","101","101","101","010"],
            'L'=>["100","100","100","100","111"],
            'M'=>["101","111","111","101","101"],
            'I'=>["111","010","010","010","111"],
            'X'=>["101","101","010","101","101"],
            '-'=>["000","000","111","000","000"],
            '0'=>["111","101","101","101","111"],
            '1'=>["010","110","010","010","111"],
            '2'=>["111","001","111","100","111"],
            '3'=>["111","001","111","001","111"],
            '4'=>["101","101","111","001","001"],
            '5'=>["111","100","111","001","111"],
            '6'=>["111","100","111","101","111"],
            '7'=>["111","001","010","010","010"],
            '8'=>["111","101","111","101","111"],
            '9'=>["111","101","111","001","111"],
            _=>["000","000","000","000","000"]
        };
        int letterX=CenterX-(label.Length*4-1)/2;
        int letterY=CenterY-2;
        for(int i=0;i<label.Length;i++)for(int yy=0;yy<5;yy++)for(int xx=0;xx<3;xx++)
        {
            if(Glyph(label[i])[yy][xx]!='1')continue;
            int px=x+letterX+i*4+xx,py=y+letterY+yy;
            if(px<0||py<0||px>=128||py>=64)continue;
            int pos=py*128+px;
            coverage[pos]=true;
            output[pos]=false;
        }
    }
}

using System;

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
    private static readonly string Default=CreateDefault();
    public static string Factory=>Default;

    public static bool IsValid(string? pixels)
    {
        if(pixels?.Length!=Width*Height)return false;
        foreach(char value in pixels)
            if(value is not ('0' or '1' or '2' or '3'))return false;
        return true;
    }

    public static string Resolve(string? pixels)=>IsValid(pixels)?pixels!:Default;

    private static string CreateDefault()
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
            else if(onSweep &&
                (distance>=19.25 && distance<=20.5 ||
                 distance>=15.1 && distance<16.2))
                pixels[y*Width+x]='1';
            else if(onSweep && distance>=16.2 && distance<19.25)
                pixels[y*Width+x]='2';
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
        int gapAngle=0,int? attenuationSteps=null,bool transientAttenuation=false)
    {
        string src=Resolve(artwork);
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
            // Independent icons share the same source-of-truth as Icon Workshop.
            // They stay upright when the ring's gap is rotated.
            DrawModeIcon(output,coverage,x+1,y+1,"Game",false);
            DrawModeIcon(output,coverage,x+Width-1,y+1,"Chat",true);
        }
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

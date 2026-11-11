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
            else if(onSweep && distance>=19.25 && distance<=20.5)
                pixels[y*Width+x]='1';
            else if(onSweep && distance>=15.8 && distance<19.25)
                pixels[y*Width+x]='2';
        }
        return new string(pixels);
    }

    private static double Angle(int x,int y)
    {
        double degrees=Math.Atan2(y-CenterY,x-CenterX)*180d/Math.PI;
        return (degrees-StartAngle+720d)%360d;
    }

    public static bool IsOn(char value,int x,int y,int? percent)
        => value switch
        {
            '1'=>true,
            '2'=>percent.HasValue && Angle(x,y)<=Sweep*
                Math.Clamp(percent.Value,0,100)/100d,
            _=>false
        };

    public static int? Value(DialSnapshot snapshot,bool fresh)
    {
        if(!fresh)return null;
        if(snapshot.View==DialView.Volume)return snapshot.Volume;
        if(!snapshot.Game.HasValue||!snapshot.Chat.HasValue)return null;
        int total=snapshot.Game.Value+snapshot.Chat.Value;
        return total>0?(int)Math.Round(snapshot.Game.Value*100d/total):50;
    }

    public static void Draw(bool[] output,bool[] coverage,int x,int y,
        string? artwork,int? percent,DialView view,bool autoLabel=true)
    {
        string src=Resolve(artwork);
        for(int sy=0;sy<Height;sy++)for(int sx=0;sx<Width;sx++)
        {
            int px=x+sx,py=y+sy;
            if(px<0||py<0||px>=128||py>=64)continue;
            char value=src[sy*Width+sx];
            if(value=='0')continue;
            int dest=py*128+px;
            coverage[dest]=true;
            output[dest]=IsOn(value,sx,sy,percent);
        }
        // The original Nova Pro badge is a *filled* white disc with black
        // lettering, unlike the previous hollow badge with text to its right.
        if(autoLabel)
            DrawBadge(output,coverage,x,y,view==DialView.Volume?"VOL":"MIX");
    }

    private static void DrawBadge(bool[] output,bool[] coverage,
        int x,int y,string label)
    {
        string[][] glyphs=label switch
        {
            "MIX" => [
                ["101","111","111","101","101"],
                ["111","010","010","010","111"],
                ["101","101","010","101","101"]],
            _ => [
                ["101","101","101","101","010"],
                ["010","101","101","101","010"],
                ["110","101","110","100","100"]]
        };
        const int letterX=CenterX-5,letterY=CenterY-2;
        for(int i=0;i<3;i++)for(int yy=0;yy<5;yy++)for(int xx=0;xx<3;xx++)
        {
            if(glyphs[i][yy][xx]!='1')continue;
            int px=x+letterX+i*4+xx,py=y+letterY+yy;
            if(px<0||py<0||px>=128||py>=64)continue;
            int pos=py*128+px;
            coverage[pos]=true;
            output[pos]=false;
        }
    }
}

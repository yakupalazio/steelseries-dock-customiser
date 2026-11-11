using System;
using System.Collections.Generic;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Compact 31×29 OLED ring, with a fixed outer diameter and inward thickness.
/// Volume uses V; ChatMix uses X. It never changes the 45×42 custom source.
/// </summary>
internal static class MiniWheelArtwork
{
    public const int Width=31,Height=29;
    public const int CenterX=15,CenterY=14;
    private static readonly string[] V=["101","101","101","101","010"];
    private static readonly string[] X=["101","101","010","101","101"];

    public static void Draw(bool[] output,bool[] mask,int left,int top,
        int? value,DialView view,int rotation=0,int thickness=3,
        bool showGame=true,bool showChat=true,int gameDX=0,int gameDY=0,
        int chatDX=0,int chatDY=0)
    {
        thickness=Math.Clamp(thickness,1,5);
        for(int sy=0;sy<Height;sy++)
        for(int sx=0;sx<Width;sx++)
        {
            double dx=sx-CenterX,dy=sy-CenterY;
            double r=Math.Sqrt(dx*dx+dy*dy);
            double angle=(Math.Atan2(dy,dx)*180d/Math.PI-
                WheelArtwork.StartAngle-rotation+1440d)%360d;
            bool onArc=angle<=WheelArtwork.Sweep;
            bool badge=r<6.5;
            bool ring=onArc&&r>=13.3-thickness&&r<=13.4;
            bool endcap=onArc &&
                (angle<3.5||angle>WheelArtwork.Sweep-3.5)&&
                r>=12.5-thickness&&r<=13.6;
            if(!badge&&!ring&&!endcap)continue;
            bool lit=true;
            if(ring&&r<13.3 && r>=13.3-thickness+0.8)
            {
                double target=WheelArtwork.Sweep*
                    Math.Clamp(value??0,0,100)/100d;
                lit=value.HasValue&&(view==DialView.Volume
                    ?angle<=target : angle>=Math.Min(target,150d)&&
                        angle<=Math.Max(target,150d));
            }
            Set(output,mask,left+sx,top+sy,lit);
        }
        string[] glyph=view==DialView.Volume?V:X;
        for(int y=0;y<5;y++)for(int x=0;x<3;x++)
            if(glyph[y][x]=='1')
                Set(output,mask,left+CenterX-1+x,top+CenterY-2+y,false);
        if(view!=DialView.ChatMix)return;
        // Original icon pixels retained; positions follow the two channel
        // sectors, Game = second half, Chat = first half.
        if(showGame)
            PlaceIcon(output,mask,"Game",left+CenterX,top+CenterY,
                WheelArtwork.StartAngle+rotation+WheelArtwork.Sweep*.75,
                gameDX,gameDY,WheelArtwork.StartAngle+rotation,true);
        if(showChat)
            PlaceIcon(output,mask,"Chat",left+CenterX,top+CenterY,
                WheelArtwork.StartAngle+rotation+WheelArtwork.Sweep*.25,
                chatDX,chatDY,WheelArtwork.StartAngle+rotation,false);
    }

    private static void PlaceIcon(bool[] output,bool[] mask,string kind,
        int centerX,int centerY,double angle,int dx,int dy,
        double startAngle,bool gameSide)
    {
        var sprite=IconLibrary.GetCurrent(kind);
        double rad=angle*Math.PI/180d;
        int intendedX=(int)Math.Round(centerX+18*Math.Cos(rad)-
            sprite[0].Length/2d+dx);
        int intendedY=(int)Math.Round(centerY+18*Math.Sin(rad)-
            sprite.Length/2d+dy);
        int bestX=-1,bestY=-1;double best=double.PositiveInfinity;
        // Prefer available locations within the display. Do not overlay
        // the ring, reserved header, battery widgets or the other icon.
        for(int y=10;y<=64-sprite.Length;y++)
        for(int x=0;x<=128-sprite[0].Length;x++)
        {
            int errors=0;
            for(int sy=0;sy<sprite.Length;sy++)
            for(int sx=0;sx<sprite[sy].Length;sx++)
            {
                if(sprite[sy][sx]!='1')continue;
                int px=x+sx,py=y+sy;
                double r=Math.Sqrt((px-centerX)*(px-centerX)+
                    (py-centerY)*(py-centerY));
                double azimuth=Math.Atan2(py-centerY,px-centerX)*180d/Math.PI;
                double progress=(azimuth-startAngle+7200d)%360d;
                bool ownChannel=gameSide
                    ?progress>=WheelArtwork.Sweep/2d && progress<=WheelArtwork.Sweep
                    :progress>=0 && progress<=WheelArtwork.Sweep/2d;
                if(r<15.5 || mask[py*128+px] || !ownChannel)errors++;
            }
            if(errors>0)continue;
            double score=(x-intendedX)*(x-intendedX)+
                (y-intendedY)*(y-intendedY);
            if(score<best){best=score;bestX=x;bestY=y;}
        }
        if(bestX<0)return;
        for(int sy=0;sy<sprite.Length;sy++)
        for(int sx=0;sx<sprite[sy].Length;sx++)
            if(sprite[sy][sx]=='1')
                Set(output,mask,bestX+sx,bestY+sy,true);
    }

    private static void Set(bool[] pixels,bool[] coverage,int x,int y,bool value)
    {
        if(x<0||x>=128||y<10||y>=64)return;
        pixels[y*128+x]=value;
        coverage[y*128+x]=true;
    }
}

using System;
using System.Collections.Generic;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Compact 31x29 OLED projection of an independently saved 45x42 source.
/// It shares the full-size wheel's editable 0/1/2/3 pixel grammar, rotation,
/// ring caps and two constrained channel sprites. A miniature does not
/// overwrite the user's full-size wheel artwork.
/// </summary>
internal static class MiniWheelArtwork
{
    public const int Width=31,Height=29,CenterX=15,CenterY=14;
    private static readonly string[] V=["101","101","101","101","010"];
    private static readonly string[] X=["101","101","010","101","101"];

    public static void Draw(bool[] output,bool[] mask,int left,int top,
        int? value,DialView view,int rotation=0,int thickness=3,
        bool showGame=true,bool showChat=true,int gameDX=0,int gameDY=0,
        int chatDX=0,int chatDY=0,int sizePercent=100,
        string? artwork=null,bool autoLabel=true,bool closeEnds=true)
    {
        thickness=Math.Clamp(thickness,1,7);
        double scale=Math.Clamp(sizePercent,70,100)/100d;
        bool custom=WheelArtwork.IsValid(artwork);
        string src=WheelArtwork.PreviewSource(artwork,thickness);

        for(int sy=0;sy<Height;sy++)for(int sx=0;sx<Width;sx++)
        {
            double dx=sx-CenterX,dy=sy-CenterY;
            double r=Math.Sqrt(dx*dx+dy*dy);
            double angle=(Math.Atan2(dy,dx)*180d/Math.PI-
                WheelArtwork.StartAngle-rotation+1440d)%360d;
            bool onArc=angle<=WheelArtwork.Sweep;
            bool badge=r<6.5*scale;
            bool ring=onArc&&r>=(13.3-thickness)*scale&&r<=13.4*scale;
            bool endcap=closeEnds && onArc &&
                (angle<3.5||angle>WheelArtwork.Sweep-3.5)&&
                r>=(12.5-thickness)*scale&&r<=13.6*scale;

            if(custom)
            {
                // Pixel art is drawn in the same normalized 45x42 coordinate
                // system as the large wheel. Nearest-neighbour scaling
                // retains a literal 1-bit output and custom dynamic layers.
                int ix=(int)Math.Round(WheelArtwork.CenterX+
                    dx*(WheelArtwork.Width/(double)Width)/scale);
                int iy=(int)Math.Round(WheelArtwork.CenterY+
                    dy*(WheelArtwork.Height/(double)Height)/scale);
                if(ix>=0&&ix<WheelArtwork.Width&&iy>=0&&iy<WheelArtwork.Height)
                {
                    var (sx0,sy0)=WheelArtwork.SourceAt(ix,iy,rotation);
                    if(sx0>=0&&sx0<WheelArtwork.Width&&
                       sy0>=0&&sy0<WheelArtwork.Height)
                    {
                        char pixel=src[sy0*WheelArtwork.Width+sx0];
                        if(pixel!='0')
                            Set(output,mask,left+sx,top+sy,
                                WheelArtwork.IsOn(pixel,sx0,sy0,value,view));
                    }
                }
            }
            else
            {
                if(!badge&&!ring&&!endcap)continue;
                bool lit=true;
                if(ring&&r<13.3*scale &&
                    r>=(13.3-thickness+0.8)*scale)
                {
                    double target=WheelArtwork.Sweep*
                        Math.Clamp(value??0,0,100)/100d;
                    lit=value.HasValue&&(view==DialView.Volume
                        ?angle<=target
                        :angle>=Math.Min(target,150d)&&
                            angle<=Math.Max(target,150d));
                }
                Set(output,mask,left+sx,top+sy,lit);
            }
            if(endcap)
                Set(output,mask,left+sx,top+sy,true);
        }

        if(autoLabel)
        {
            string[] glyph=view==DialView.Volume?V:X;
            for(int yy=0;yy<5;yy++)for(int xx=0;xx<3;xx++)
                if(glyph[yy][xx]=='1')
                    Set(output,mask,left+CenterX-1+xx,top+CenterY-2+yy,false);
        }
        if(view!=DialView.ChatMix)return;
        if(showGame)
            PlaceIcon(output,mask,"Game",left+CenterX,top+CenterY,
                WheelArtwork.StartAngle+rotation+WheelArtwork.Sweep*.75,
                gameDX,gameDY,WheelArtwork.StartAngle+rotation,true,scale);
        if(showChat)
            PlaceIcon(output,mask,"Chat",left+CenterX,top+CenterY,
                WheelArtwork.StartAngle+rotation+WheelArtwork.Sweep*.25,
                chatDX,chatDY,WheelArtwork.StartAngle+rotation,false,scale);
    }

    private static void PlaceIcon(bool[] output,bool[] mask,string kind,
        int centerX,int centerY,double angle,int dx,int dy,
        double startAngle,bool gameSide,double scale)
    {
        var sprite=IconLibrary.GetCurrent(kind);
        double rad=angle*Math.PI/180d;
        int intendedX=(int)Math.Round(centerX+18*scale*Math.Cos(rad)-
            sprite[0].Length/2d+dx);
        int intendedY=(int)Math.Round(centerY+18*scale*Math.Sin(rad)-
            sprite.Length/2d+dy);
        int bestX=-1,bestY=-1;double best=double.PositiveInfinity;
        // Position must remain beside its own section of the wheel.
        // The other sprite's pixels are already part of mask.
        for(int y=10;y<=64-sprite.Length;y++)
        for(int x=0;x<=128-sprite[0].Length;x++)
        {
            bool legal=true;
            for(int sy=0;sy<sprite.Length&&legal;sy++)
            for(int sx=0;sx<sprite[sy].Length;sx++)
            {
                if(sprite[sy][sx]!='1')continue;
                int px=x+sx,py=y+sy;
                double r=Math.Sqrt((px-centerX)*(px-centerX)+
                    (py-centerY)*(py-centerY));
                double azimuth=Math.Atan2(py-centerY,px-centerX)*180d/Math.PI;
                double progress=(azimuth-startAngle+7200d)%360d;
                bool own=gameSide
                    ?progress>=WheelArtwork.Sweep/2d&&progress<=WheelArtwork.Sweep
                    :progress>=0&&progress<=WheelArtwork.Sweep/2d;
                if(r<15.5*scale||r>31*scale||mask[py*128+px]||!own)
                { legal=false;break; }
            }
            if(!legal)continue;
            double displacement=(x-intendedX)*(x-intendedX)+
                (y-intendedY)*(y-intendedY);
            if(displacement<best){best=displacement;bestX=x;bestY=y;}
        }
        if(bestX<0||bestY<0)return;
        for(int yy=0;yy<sprite.Length;yy++)
        for(int xx=0;xx<sprite[yy].Length;xx++)
            if(sprite[yy][xx]=='1')
                Set(output,mask,bestX+xx,bestY+yy,true);
    }

    private static void Set(bool[] output,bool[] mask,int x,int y,bool lit)
    {
        if(x<0||x>=128||y<0||y>=64)return;
        int i=y*128+x;
        mask[i]=true;
        output[i]=lit;
    }
}

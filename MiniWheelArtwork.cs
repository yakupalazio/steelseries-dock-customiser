using System;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Native editable 31x29 pixel dial. Unlike the old 45x42 downsampler,
/// every editor cell now maps to one physical mini-wheel source pixel.
/// </summary>
internal static class MiniWheelArtwork
{
    public const int Width=31,Height=29,CenterX=15,CenterY=14;
    private static readonly string[] V=["101","101","101","101","010"];
    private static readonly string[] X=["101","101","010","101","101"];
    private static readonly string[] Templates=Enumerable.Range(1,7)
        .Select(Generate).ToArray();
    public static string Factory=>Templates[2];

    public static bool IsValid(string? source)=>
        source is {Length:Width*Height} &&
        source.All(c=>c is '0' or '1' or '2' or '3');

    // Previously saved mini art used the large dial's 45x42 grid. Convert
    // without ever touching the separately stored full-size wheel artwork.
    public static string Normalize(string? source)
    {
        if(IsValid(source))return source!;
        if(!WheelArtwork.IsValid(source))return "";
        var result=new char[Width*Height];
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
        {
            int oldX=Math.Clamp((int)Math.Round(
                WheelArtwork.CenterX+(x-CenterX)*WheelArtwork.Width/(double)Width),
                0,WheelArtwork.Width-1);
            int oldY=Math.Clamp((int)Math.Round(
                WheelArtwork.CenterY+(y-CenterY)*WheelArtwork.Height/(double)Height),
                0,WheelArtwork.Height-1);
            result[y*Width+x]=source![oldY*WheelArtwork.Width+oldX];
        }
        return new string(result);
    }

    public static string Resolve(string? source)=>
        IsValid(source)?source!:IsValid(Normalize(source))?Normalize(source):Factory;
    public static string PreviewSource(string? source,int thickness)=>
        IsValid(source)?source!:
        IsValid(Normalize(source))?Normalize(source):
        Templates[Math.Clamp(thickness,1,7)-1];

    private static string Generate(int thickness)
    {
        var art=Enumerable.Repeat('0',Width*Height).ToArray();
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
        {
            double dx=x-CenterX,dy=y-CenterY;
            double radius=Math.Sqrt(dx*dx+dy*dy);
            double sweep=Angle(x,y);
            char pixel='0';
            if(radius<=6.2)pixel='1';
            else if(sweep<=WheelArtwork.Sweep)
            {
                double inner=Math.Max(7.4,12.3-thickness);
                if(radius>=12.3&&radius<13.45 ||
                   radius>=inner-.6 && radius<inner+.35)
                    pixel='1';
                else if(radius>=inner && radius<12.3)
                    pixel='2';
            }
            art[y*Width+x]=pixel;
        }
        // Draw the two concentric boundaries as connected raster arcs.
        // Sampling every fraction of a degree guarantees no missing
        // neighbor in the 1-bit 31x29 outline at diagonal angles.
        for(double progress=0;progress<=WheelArtwork.Sweep;progress+=.25)
        {
            double theta=(WheelArtwork.StartAngle+progress)*Math.PI/180d;
            foreach(double radius in new[]{13.0,Math.Max(7.4,12.3-thickness)})
            {
                int px=CenterX+(int)Math.Round(radius*Math.Cos(theta));
                int py=CenterY+(int)Math.Round(radius*Math.Sin(theta));
                if(px>=0&&px<Width&&py>=0&&py<Height)
                    art[py*Width+px]='1';
            }
        }
        return new string(art);
    }

    private static double Angle(int x,int y)
    {
        double deg=Math.Atan2(y-CenterY,x-CenterX)*180d/Math.PI;
        return (deg-WheelArtwork.StartAngle+7200d)%360d;
    }

    public static (int X,int Y) SourceAt(int x,int y,int rotation)
    {
        double dx=x-CenterX,dy=y-CenterY;
        if(dx*dx+dy*dy<49)return (x,y); // badge letters don't rotate
        double rad=-Math.Clamp(rotation,-180,180)*Math.PI/180d;
        return (CenterX+(int)Math.Round(dx*Math.Cos(rad)-dy*Math.Sin(rad)),
            CenterY+(int)Math.Round(dx*Math.Sin(rad)+dy*Math.Cos(rad)));
    }

    public static char Sample(string source,int x,int y,int rotation)
    {
        var (sx,sy)=SourceAt(x,y,rotation);
        return sx>=0&&sx<Width&&sy>=0&&sy<Height
            ?source[sy*Width+sx]:'0';
    }

    public static bool IsOn(char pixel,int sx,int sy,int? value,DialView view)
    {
        if(pixel=='1')return true;
        if(pixel!='2'||!value.HasValue)return false;
        double progress=Angle(sx,sy);
        double point=WheelArtwork.Sweep*Math.Clamp(value.Value,0,100)/100d;
        return view==DialView.Volume
            ? progress<=point
            : progress>=Math.Min(WheelArtwork.Sweep/2d,point) &&
              progress<=Math.Max(WheelArtwork.Sweep/2d,point);
    }

    public static void Draw(bool[] output,bool[] mask,int left,int top,
        int? value,DialView view,int rotation=0,int thickness=3,
        bool showGame=true,bool showChat=true,int gameDX=0,int gameDY=0,
        int chatDX=0,int chatDY=0,int sizePercent=100,
        string? artwork=null,bool autoLabel=true,bool closeEnds=true)
    {
        double scale=Math.Clamp(sizePercent,70,100)/100d;
        string src=PreviewSource(artwork,thickness);
        for(int sy=0;sy<Height;sy++)for(int sx=0;sx<Width;sx++)
        {
            // Inverse mapping fills each OLED destination pixel exactly once:
            // no fragmenting of the outer ring during resizing or rotation.
            int tx=CenterX+(int)Math.Round((sx-CenterX)/scale);
            int ty=CenterY+(int)Math.Round((sy-CenterY)/scale);
            if(tx<0||tx>=Width||ty<0||ty>=Height)continue;
            var (fromX,fromY)=SourceAt(tx,ty,rotation);
            if(fromX<0||fromX>=Width||fromY<0||fromY>=Height)continue;
            char pixel=src[fromY*Width+fromX];
            if(pixel=='0')continue;
            Set(output,mask,left+sx,top+sy,
                IsOn(pixel,fromX,fromY,value,view));
        }
        if(closeEnds)
        {
            DrawCap(output,mask,left,top,
                WheelArtwork.StartAngle+rotation,thickness,scale);
            DrawCap(output,mask,left,top,
                WheelArtwork.StartAngle+WheelArtwork.Sweep+rotation,
                thickness,scale);
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
                WheelArtwork.StartAngle+rotation+WheelArtwork.Sweep*.75d,
                gameDX,gameDY,WheelArtwork.StartAngle+rotation,true,scale);
        if(showChat)
            PlaceIcon(output,mask,"Chat",left+CenterX,top+CenterY,
                WheelArtwork.StartAngle+rotation+WheelArtwork.Sweep*.25d,
                chatDX,chatDY,WheelArtwork.StartAngle+rotation,false,scale);
    }

    private static void DrawCap(bool[] output,bool[] mask,int left,int top,
        double angle,int thickness,double scale)
    {
        double rad=angle*Math.PI/180d;
        for(double r=Math.Max(7.4,12.3-thickness)-.7;r<=13.3;r+=.25)
            Set(output,mask,
                left+CenterX+(int)Math.Round(r*scale*Math.Cos(rad)),
                top+CenterY+(int)Math.Round(r*scale*Math.Sin(rad)),true);
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
                if(r<14.2*scale||r>30*scale||mask[py*128+px]||!own)
                { legal=false;break; }
            }
            if(!legal)continue;
            double delta=(x-intendedX)*(x-intendedX)+
                (y-intendedY)*(y-intendedY);
            if(delta<best){best=delta;bestX=x;bestY=y;}
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
        mask[i]=true;output[i]=lit;
    }
}

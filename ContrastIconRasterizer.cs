using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Shared 1-bit OLED game artwork rasterizer. Opaque does not mean white:
/// transparent colored symbols must retain their dark interior details.
/// Applies identically to installed EXE/Store game assets and user imports.
/// </summary>
internal static class ContrastIconRasterizer
{
    private const int Size=32;
    internal enum Polarity { Auto, Bright, Dark, Inverted }

    internal static bool[] Convert(Image source,Polarity polarity=Polarity.Auto)
    {
        if(source.Width<1||source.Height<1||source.Width>2048||
           source.Height>2048) return new bool[Size*Size];

        // Render to a well-defined RGBA surface, keeping original alpha.
        // Preserve exactly-sized icons to avoid destroying their fine outlines.
        using var sampled=new Bitmap(Size,Size,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(sampled))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            g.CompositingMode=CompositingMode.SourceCopy;
            float ratio=Math.Min((Size-2f)/source.Width,
                                 (Size-2f)/source.Height);
            if(source.Width<=Size && source.Height<=Size)ratio=1f;
            int w=Math.Clamp((int)Math.Round(source.Width*ratio),1,Size);
            int h=Math.Clamp((int)Math.Round(source.Height*ratio),1,Size);
            int x=(Size-w)/2,y=(Size-h)/2;
            using var attributes=new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(source,new Rectangle(x,y,w,h),
                0,0,source.Width,source.Height,GraphicsUnit.Pixel,attributes);
        }
        var rgb=new Color[Size*Size];
        var luminance=new int[Size*Size];
        var present=new bool[Size*Size];
        var histogram=new int[256];
        int visible=0,transparent=0;
        for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
        {
            int i=y*Size+x;
            Color c=sampled.GetPixel(x,y);
            rgb[i]=c;
            // Perceptual brightness, not 1-bit alpha occupancy.
            int brightness=(int)Math.Round(0.2126*c.R+
                0.7152*c.G+0.0722*c.B);
            luminance[i]=Math.Clamp(brightness,0,255);
            present[i]=c.A>=65;
            if(!present[i]){transparent++;continue;}
            histogram[luminance[i]]++;
            visible++;
        }
        var blank=new bool[Size*Size];
        if(visible<4)return blank;
        bool alphaArt=transparent>Size*Size/30;
        // Uniform opaque backgrounds are reliably identifiable by their
        // four corners; a heavily textured game tile is NOT uniform.
        Color[] corners=[rgb[0],rgb[Size-1],rgb[(Size-1)*Size],
            rgb[Size*Size-1]];
        int meanCorner=(int)Math.Round(corners.Average(c=>
            0.2126*c.R+0.7152*c.G+0.0722*c.B));
        bool flatBorder=!alphaArt && corners.All(c=>
            Math.Abs(.2126*c.R+.7152*c.G+.0722*c.B-meanCorner)<18 &&
            c.A>=220);
        int[] occupied=histogram;
        int Quantile(double fraction)
        {
            int cursor=0,target=(int)(visible*fraction);
            for(int i=0;i<256;i++)
            {
                cursor+=occupied[i];
                if(cursor>target)return i;
            }
            return 128;
        }
        int q10=Quantile(.10),q90=Quantile(.90);
        int range=q90-q10;
        int otsu=ComputeOtsu(histogram,visible);
        // Avoid a threshold below near-black: dark blue and shadows stay
        // black, while orange/gold/white strokes remain visible on OLED.
        int threshold=Math.Clamp((otsu+((q10+q90)/2))/2,55,208);
        bool darkOnLight=flatBorder&&meanCorner>=185;
        bool monochromeSilhouette=alphaArt&&range<17;

        bool[] Render(int cutoff,bool invert)
        {
            var px=new bool[Size*Size];
            for(int i=0;i<px.Length;i++)
            {
                if(!present[i])continue;
                int l=luminance[i];
                bool white=polarity switch
                {
                    Polarity.Bright=>l>=cutoff,
                    Polarity.Dark=>l<cutoff,
                    Polarity.Inverted=>l<cutoff,
                    _=>invert?l<=cutoff:
                         (l>=cutoff && l>=42)
                };
                // Opaque *uniform* dark/white silhouettes on transparent
                // canvases have no internal color detail. Retain their shape
                // rather than making an entirely black frame.
                if(monochromeSilhouette && polarity==Polarity.Auto)
                    white=true;
                if(flatBorder)
                {
                    int edge=Math.Abs(l-meanCorner);
                    // Known flat backing color is not part of the logo.
                    if(edge<19)white=false;
                    if(darkOnLight && polarity==Polarity.Auto)
                        white=edge>=28&&l<meanCorner-20;
                    if(!darkOnLight && polarity==Polarity.Auto)
                        white=edge>=25&&l>=cutoff;
                }
                px[i]=white;
            }
            return px;
        }

        bool[] best=Render(threshold,darkOnLight);
        double score=Score(best);
        if(polarity!=Polarity.Auto)return best;
        // If contrast split creates a solid block or a blank image, choose
        // a different threshold rather than persisting unreadable white art.
        if(score<0 || range>25)
        {
            foreach(int test in new[]{
                Math.Clamp(threshold+24,60,235),
                Math.Clamp(threshold-18,42,220),
                Math.Clamp(q90,60,235)
            }.Distinct())
            {
                bool[] alternative=Render(test,darkOnLight);
                double rank=Score(alternative);
                if(rank>score){best=alternative;score=rank;}
            }
        }
        return best;
    }

    private static int ComputeOtsu(int[] histogram,int total)
    {
        double sum=0;
        for(int i=0;i<256;i++)sum+=histogram[i]*i;
        int weightB=0,threshold=128;
        double sumB=0,best=-1;
        for(int i=0;i<256;i++)
        {
            weightB+=histogram[i];
            if(weightB==0)continue;
            int weightF=total-weightB;
            if(weightF==0)break;
            sumB+=i*histogram[i];
            double meanB=sumB/weightB;
            double meanF=(sum-sumB)/weightF;
            double between=(double)weightB*weightF*
                (meanB-meanF)*(meanB-meanF);
            if(between>best){best=between;threshold=i;}
        }
        return threshold;
    }

    // Prefer coherent bright details, not 31x25 white rectangles.
    // Edge transitions help recognize logos over indiscriminate white blobs.
    private static double Score(bool[] pixels)
    {
        int lit=pixels.Count(v=>v);
        if(lit<14||lit>810||GameLogoProvider.IsFilledRectangle(pixels))
            return -100;
        int edges=0,isolated=0;
        for(int y=1;y<Size-1;y++)
        for(int x=1;x<Size-1;x++)
        {
            int i=y*Size+x;
            bool now=pixels[i];
            if(now!=pixels[i+1])edges++;
            if(now!=pixels[i+Size])edges++;
            if(now && !pixels[i-1]&&!pixels[i+1]&&
               !pixels[i-Size]&&!pixels[i+Size])isolated++;
        }
        double density=lit/(double)(Size*Size);
        return edges*0.9-isolated*2.5
            -Math.Max(0,density-0.58)*460
            -Math.Max(0,0.045-density)*800;
    }
}

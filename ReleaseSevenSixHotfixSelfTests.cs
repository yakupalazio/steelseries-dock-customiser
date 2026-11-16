using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// RC7.6 offline regressions: do not touch real OLED/game files, launch the
/// browser, delete legacy assets or mutate the user's saved profiles.
/// </summary>
internal static class ReleaseSevenSixHotfixSelfTests
{
    internal static void Run()
    {
        LegacyWhiteRectangle();
        InstrumentalGlyph();
        AboutSupportAndExport();
        Console.WriteLine("PASS: legacy Sekiro 31x25 solid art fallback, "+
            "readable instrumental note, working PNG export label, "+
            "GitHub support link and About layout.");
    }

    private static void LegacyWhiteRectangle()
    {
        // Recreate the user's obsolete 775/1024 white rectangle (not
        // their actual private JSON). The old >900 gate missed it.
        var corrupted=new bool[32*32];
        for(int y=4;y<=28;y++)
        for(int x=1;x<=31;x++)
            corrupted[y*32+x]=true;
        if(corrupted.Count(x=>x)!=775 ||
           !GameLogoProvider.IsFilledRectangle(corrupted))
            throw new Exception("Legacy Sekiro white block not rejected");

        var detailed=(bool[])corrupted.Clone();
        for(int y=7;y<=27;y+=3)
        for(int x=5;x<=28;x++)
            detailed[y*32+x]=false;
        if(GameLogoProvider.IsFilledRectangle(detailed))
            throw new Exception("Detailed logo incorrectly rejected as block");
    }

    private static void InstrumentalGlyph()
    {
        var frame=new bool[128*64];
        OledRenderer.DrawInstrumentalNote(frame,40,18,55,30);
        var lit=frame.Count(b=>b);
        int left=0,right=0,minX=128,minY=64,maxX=-1,maxY=-1;
        for(int y=0;y<64;y++)for(int x=0;x<128;x++)
        {
            if(!frame[y*128+x])continue;
            minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);
            minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
            if(y>=26 && x<48)left++;
            if(y>=26 && x>=48)right++;
        }
        if(lit is <35 or >110||minX>43||maxX<53||minY!=18||
           maxY!=29||left<8||right<8)
            throw new Exception("Instrumental icon is too small, malformed or clipped");

        var clipped=new bool[128*64];
        OledRenderer.DrawInstrumentalNote(clipped,122,56,128,64);
        if(clipped.Count(b=>b)==0||clipped.Count(b=>b)>lit)
            throw new Exception("Instrumental note clipping failed");

        string? preview=Environment.GetEnvironmentVariable("DOCK_RC76_NOTE_PREVIEW");
        if(!string.IsNullOrWhiteSpace(preview))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(preview)!);
            using var bitmap=new Bitmap(128,64);
            for(int y=0;y<64;y++)for(int x=0;x<128;x++)
                bitmap.SetPixel(x,y,frame[y*128+x]?Color.White:Color.Black);
            bitmap.Save(preview,ImageFormat.Png);
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach(Control child in root.Controls)
        {
            yield return child;
            foreach(var nested in Descendants(child))yield return nested;
        }
    }

    private static void AboutSupportAndExport()
    {
        if(SettingsDialog.SupportGithubUrl!="https://github.com/yakupalazio")
            throw new Exception("Support URL is not the confirmed GitHub profile");
        using(var settings=new SettingsDialog())
        {
            var controls=Descendants(settings).ToArray();
            var github=controls.OfType<Button>().SingleOrDefault(
                b=>b.Name=="SupportGithubButton")??
                throw new Exception("About Support Me button missing");
            if(github.Width<150||github.Height<34||
               github.Parent is not Panel panel ||
               panel.Bottom>settings.ClientSize.Height-30)
                throw new Exception("Support link not reachable in About layout");
            var close=controls.OfType<Button>().FirstOrDefault(
                b=>b.Text=="Kapat"||b.Text=="Close");
            if(close is null ||close.Top<=panel.Bottom)
                throw new Exception("About close button overlaps Support Me");
            // No click: automated tests never open the actual GitHub page.
        }
        using(var studio=new PixelStudioForm())
        {
            var export=Descendants(studio).OfType<Button>()
                .FirstOrDefault(b=>b.Text=="PNG dışa aktar"||
                    b.Text=="Export PNG");
            if(export is null||export.Width<110)
                throw new Exception("PNG export was removed or mislabeled");
        }
    }
}

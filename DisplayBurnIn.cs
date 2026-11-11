using System;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

internal sealed class DisplayPreferences
{
    public bool BurnInDrift { get; set; }
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","display-v1.json");
    public static DisplayPreferences Load()
    {
        try{return File.Exists(FilePath)
            ?JsonSerializer.Deserialize<DisplayPreferences>(File.ReadAllText(FilePath))??new()
            :new();}
        catch{return new();}
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath,JsonSerializer.Serialize(this));
    }
}

/// <summary>
/// Optional 1-pixel orbital drift of the *live physical output only*. Editor
/// coordinates and exported designs stay exact. The original top ten rows
/// never move, and motion is suppressed at any occupied clipping boundary.
/// </summary>
internal static class DisplayBurnIn
{
    private static readonly (int X,int Y)[] Steps=
    [ (0,0), (1,0), (1,1), (0,1), (-1,1), (-1,0), (-1,-1), (0,-1) ];

    public static Bitmap Apply(Bitmap source,bool protectHeader,bool enabled,
        DateTimeOffset time)
    {
        if(!enabled)return source;
        int step=(int)((time.ToUnixTimeSeconds()/45)%Steps.Length);
        if(step<0)step+=Steps.Length;
        var (dx,dy)=Steps[step];
        int top=protectHeader?10:0;
        // Avoid cutting off edge pixels. A full-screen original design is
        // guaranteed to render with every pixel intact.
        for(int y=top;y<64;y++)for(int x=0;x<128;x++)
        {
            if(source.GetPixel(x,y).R<128)continue;
            if(x+dx<0||x+dx>=128)dx=0;
            if(y+dy<top||y+dy>=64)dy=0;
        }
        if(dx==0&&dy==0)return source;
        var shifted=new Bitmap(128,64);
        try
        {
            using(var g=Graphics.FromImage(shifted))g.Clear(Color.Black);
            // OEM-reserved upper strip must be bit-identical to the original.
            if(protectHeader)
                for(int y=0;y<10;y++)for(int x=0;x<128;x++)
                    shifted.SetPixel(x,y,source.GetPixel(x,y));
            for(int y=top;y<64;y++)for(int x=0;x<128;x++)
                if(source.GetPixel(x,y).R>=128)
                    shifted.SetPixel(x+dx,y+dy,Color.White);
            source.Dispose();
            return shifted;
        }
        catch{shifted.Dispose();throw;}
    }
}

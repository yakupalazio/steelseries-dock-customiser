using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

/// <summary>
/// A read-only OLED icon snapshot. The real game ID comes from the GG
/// library/process detector, not a web search or guessed game name.
/// </summary>
internal sealed record GameLogoSnapshot(
    string Executable,string Name,int Size,bool[] Pixels,string Source);

/// <summary>
/// Resolves the running game's registered EXE icon, or a user-provided local
/// image associated with the game's exact GG executable + name. Private
/// images are converted to 1-bit 32x32 data; nothing is uploaded and the
/// renderer itself never opens files or performs expensive decoding.
/// </summary>
internal static class GameLogoProvider
{
    private const int Canvas=32;
    private sealed class Asset
    {
        public string Executable {get;set;}="";
        public string Name {get;set;}="";
        public string[] Rows {get;set;}=[];
    }
    private static string DirectoryPath=>Path.Combine(
        Beta5Sandbox.SettingsDirectory,"game-logo-art-v1");
    private static string Key(GameSnapshot game)
    {
        // The executable remains stable across GG display-name changes,
        // localized titles and next session. Generic shared launchers are
        // rejected by GG detection separately.
        string identity=game.Executable.Trim().ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(identity)));
    }
    private static string PrivateFile(GameSnapshot game)=>
        Path.Combine(DirectoryPath,Key(game)+".json");

    internal static bool HasOverride(GameSnapshot game)=>
        game.IsRunning&&File.Exists(PrivateFile(game));

    internal static GameLogoSnapshot? Resolve(GameSnapshot game)
    {
        if(!game.IsRunning)return null;
        try
        {
            string file=PrivateFile(game);
            if(!File.Exists(file))
            {
                // Old files include the GG display name in their filename.
                // Keep existing player assets functional after upgrading.
                string legacy=game.Executable.Trim().ToLowerInvariant()+"\n"+
                    game.Name.Trim().ToLowerInvariant();
                string oldKey=Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(legacy)));
                string oldFile=Path.Combine(DirectoryPath,oldKey+".json");
                if(File.Exists(oldFile))file=oldFile;
            }
            if(File.Exists(file)&&new FileInfo(file).Length<12000)
            {
                var asset=JsonSerializer.Deserialize<Asset>(File.ReadAllText(file));
                if(asset is not null&&
                   string.Equals(asset.Executable,game.Executable,
                       StringComparison.OrdinalIgnoreCase)&&
                   TryReadRows(asset.Rows,out var pixels) &&
                   !IsFilledRectangle(pixels))
                    return new(game.Executable,game.Name,Canvas,pixels,
                        "Kullanıcı logosu");
            }
        }
        catch(IOException){}
        catch(JsonException){}
        catch(UnauthorizedAccessException){}

        string? registered=GgGameDetector.RegisteredExecutable(game);
        return registered is null?null:ResolveFromExecutable(game,registered);
    }

    // Shared by the normal GG workflow and read-only regression diagnostics.
    internal static GameLogoSnapshot? ResolveFromExecutable(
        GameSnapshot game,string executable)
    {
        GameLogoSnapshot? TryBitmap(Bitmap? bitmap,string source)
        {
            if(bitmap is null)return null;
            using(bitmap)
            {
                bool[] pixels=ConvertImage(bitmap);
                int lit=pixels.Count(v=>v);
                // A completely blank icon or near-solid image is never a
                // usable OLED game logo; try the next real local source.
                if(lit<12||lit>820||IsFilledRectangle(pixels))return null;
                return new(game.Executable,game.Name,Canvas,pixels,source);
            }
        }

        // MSIX/UWP games (notably Minecraft) commonly keep useful logo art
        // alongside a low-contrast or generic executable icon.
        foreach(string asset in GameIconSources.NearbyArtwork(executable))
        {
            var candidate=TryBitmap(GameIconSources.ReadImage(asset),
                "Yerel oyun görseli");
            if(candidate is not null)return candidate;
        }
        return TryBitmap(GameIconSources.ReadEmbedded(executable),
                   "Oyun EXE simgesi")
            ?? TryBitmap(GameIconSources.ReadShell(executable),
                   "Windows Shell simgesi");
    }

    internal static void ImportOverride(GameSnapshot game,string file)
    {
        if(!game.IsRunning)throw new InvalidOperationException(
            "Özel logo için önce GG tarafından tanınan bir oyun aç.");
        if(!File.Exists(file)||new FileInfo(file).Length>5_000_000)
            throw new InvalidDataException("Logo bulunamadı veya 5 MB sınırını aşıyor.");
        string extension=Path.GetExtension(file).ToLowerInvariant();
        if(extension is not (".png" or ".jpg" or ".jpeg" or ".ico" or ".bmp"))
            throw new InvalidDataException("Yalnızca PNG, JPG, BMP veya ICO kullan.");
        using var original=Image.FromFile(file);
        if(original.Width<1||original.Height<1||
           original.Width>2048||original.Height>2048)
            throw new InvalidDataException("Görsel boyutları en fazla 2048×2048 olmalı.");
        var bits=ConvertImage(original);
        if(!bits.Any(x=>x))
            throw new InvalidDataException("Görselde OLED'e aktarılacak piksel bulunamadı.");
        if(IsFilledRectangle(bits))
            throw new InvalidDataException(
                "Bu görsel düz beyaz kare olarak çevriliyor. "+
                "Logonun arka planını temizle veya farklı görsel seç.");
        var asset=new Asset{
            Executable=game.Executable,Name=game.Name,
            Rows=Enumerable.Range(0,Canvas).Select(y=>
                new string(Enumerable.Range(0,Canvas)
                    .Select(x=>bits[y*Canvas+x]?'1':'0').ToArray())).ToArray()
        };
        Directory.CreateDirectory(DirectoryPath);
        string destination=PrivateFile(game),temp=destination+".tmp";
        try
        {
            File.WriteAllText(temp,JsonSerializer.Serialize(asset));
            File.Move(temp,destination,true);
        }
        finally {try{if(File.Exists(temp))File.Delete(temp);}catch{}}
    }

    internal static void RemoveOverride(GameSnapshot game)
    {
        string file=PrivateFile(game);
        if(File.Exists(file))File.Delete(file);
    }

    /// <summary>
    /// The same monochrome conversion is used for user PNG/JPEG/ICO
    /// images and extracted game EXE icons. Analyze the ORIGINAL image
    /// background before letterboxing: a transparent letterbox is not
    /// evidence that the entire opaque source rectangle is foreground.
    /// Then crop the content bounding box and resample with antialiasing.
    /// </summary>
    internal static bool IsFilledRectangle(bool[] pixels)
        =>pixels.Length==Canvas*Canvas &&
          pixels.Count(x=>x)>900;

    internal static bool[] ConvertImage(Image source)
    {
        using var original=new Bitmap(source.Width,source.Height,
            PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(original))
        {
            g.Clear(Color.Transparent);
            g.DrawImage(source,0,0,source.Width,source.Height);
        }

        Color[] corners=[
            original.GetPixel(0,0),
            original.GetPixel(original.Width-1,0),
            original.GetPixel(0,original.Height-1),
            original.GetPixel(original.Width-1,original.Height-1)
        ];
        int alphaCorners=corners.Count(c=>c.A<48);
        bool alphaBackground=alphaCorners>=3;
        // Near-uniform, opaque corners are a solid canvas, regardless
        // of whether that canvas is white, black or another color.
        var solid=corners.Where(c=>c.A>=220).ToArray();
        Color backdrop=solid.Length>0
            ?Color.FromArgb(
                (int)solid.Average(c=>c.R),
                (int)solid.Average(c=>c.G),
                (int)solid.Average(c=>c.B))
            :Color.Black;
        // Four opaque corners alone DO NOT imply a solid background:
        // Minecraft's grass/dirt texture is opaque at every corner.
        int alikeCorners=solid.Count(c=>Math.Max(Math.Abs(c.R-backdrop.R),
            Math.Max(Math.Abs(c.G-backdrop.G),
                     Math.Abs(c.B-backdrop.B)))<=25);
        bool solidBackground=!alphaBackground&&alikeCorners>=3;

        // Non-uniform full-color icons require an adaptive threshold.
        // Choosing the less common luminance class retains both dark-on-light
        // and light-on-dark artwork without a hard-coded 135 cutoff.
        int[] histogram=new int[256];
        int totalSamples=0;
        int stepX=Math.Max(1,original.Width/128);
        int stepY=Math.Max(1,original.Height/128);
        for(int yy=0;yy<original.Height;yy+=stepY)
        for(int xx=0;xx<original.Width;xx+=stepX)
        {
            Color sample=original.GetPixel(xx,yy);
            if(sample.A<70)continue;
            int lum=(sample.R*3+sample.G*6+sample.B)/10;
            histogram[lum]++;
            totalSamples++;
        }
        int Quantile(double proportion)
        {
            int target=(int)(totalSamples*proportion),accumulated=0;
            for(int lum=0;lum<256;lum++)
            {
                accumulated+=histogram[lum];
                if(accumulated>target)return lum;
            }
            return 128;
        }
        int low=Quantile(0.12),high=Quantile(0.88);
        int adaptiveThreshold=(low+high)/2;
        int brightSamples=histogram.Skip(adaptiveThreshold).Sum();
        bool preferBright=brightSamples<=totalSamples-brightSamples;
        bool hasContrast=high-low>=12;

        bool[] foreground=new bool[original.Width*original.Height];
        int minX=original.Width,minY=original.Height,maxX=-1,maxY=-1;
        for(int y=0;y<original.Height;y++)
        for(int x=0;x<original.Width;x++)
        {
            Color c=original.GetPixel(x,y);
            bool lit;
            if(alphaBackground)
                lit=c.A>=70;
            else if(solidBackground)
            {
                int distance=Math.Max(Math.Abs(c.R-backdrop.R),
                    Math.Max(Math.Abs(c.G-backdrop.G),
                        Math.Abs(c.B-backdrop.B)));
                lit=c.A>=70&&distance>=38;
            }
            else
            {
                // Textured game icons (e.g. Minecraft) have opaque, differing
                // corners. The minority brightness class shows the texture
                // instead of deleting the entire low-luminance image.
                int luminance=(c.R*3+c.G*6+c.B)/10;
                lit=hasContrast && c.A>=70 &&
                    (preferBright ? luminance>=adaptiveThreshold
                                  : luminance<adaptiveThreshold);
            }
            if(!lit)continue;
            foreground[y*original.Width+x]=true;
            minX=Math.Min(x,minX);maxX=Math.Max(x,maxX);
            minY=Math.Min(y,minY);maxY=Math.Max(y,maxY);
        }
        var output=new bool[Canvas*Canvas];
        if(maxX<minX||maxY<minY)return output;
        // Dense square emblems need their surrounding margin preserved.
        // Cropping a filled Minecraft-style tile to its own bounding box
        // scales it into a solid white square on the 1-bit OLED.
        int boxArea=(maxX-minX+1)*(maxY-minY+1);
        bool denseRectangle=boxArea>0 &&
            foreground.Count(v=>v)>=boxArea*0.70;
        // Hand-crafted 32x32 icon art is already composed for the OLED.
        // Preserve its intentional margins instead of re-centering every
        // stroke and invalidating existing user's Icon Workshop layouts.
        if((original.Width<=Canvas&&original.Height<=Canvas)||denseRectangle)
        {
            minX=0;minY=0;
            maxX=original.Width-1;maxY=original.Height-1;
        }

        // Leave a one-pixel OLED border and preserve the logo aspect.
        int srcW=maxX-minX+1,srcH=maxY-minY+1;
        int target=Canvas-2;
        float ratio=Math.Min((float)target/srcW,(float)target/srcH);
        int targetW=Math.Max(1,(int)Math.Round(srcW*ratio));
        int targetH=Math.Max(1,(int)Math.Round(srcH*ratio));
        int left=(Canvas-targetW)/2,top=(Canvas-targetH)/2;

        // Coverage resampling retains thin curls and letter strokes when
        // reduced from a high-resolution raster to 32 monochrome pixels.
        // A pixel is on if sufficient source foreground area overlaps it.
        double srcPerX=(double)srcW/targetW;
        double srcPerY=(double)srcH/targetH;
        for(int yy=0;yy<targetH;yy++)
        for(int xx=0;xx<targetW;xx++)
        {
            double x0=minX+xx*srcPerX,x1=minX+(xx+1)*srcPerX;
            double y0=minY+yy*srcPerY,y1=minY+(yy+1)*srcPerY;
            int firstX=Math.Max(minX,(int)Math.Floor(x0));
            int lastX=Math.Min(maxX,(int)Math.Ceiling(x1)-1);
            int firstY=Math.Max(minY,(int)Math.Floor(y0));
            int lastY=Math.Min(maxY,(int)Math.Ceiling(y1)-1);
            double area=0;
            for(int sy=firstY;sy<=lastY;sy++)
            for(int sx=firstX;sx<=lastX;sx++)
            {
                if(!foreground[sy*original.Width+sx])continue;
                area+=Math.Max(0,Math.Min(x1,sx+1)-Math.Max(x0,sx))*
                    Math.Max(0,Math.Min(y1,sy+1)-Math.Max(y0,sy));
            }
            // At least ~20% coverage. This retains hairline blackletter
            // strokes without turning the background into a white square.
            output[(top+yy)*Canvas+left+xx]=
                area/Math.Max(0.01,(x1-x0)*(y1-y0))>=0.19;
        }
        return output;
    }

    private static bool TryReadRows(string[]? rows,out bool[] pixels)
    {
        pixels=new bool[Canvas*Canvas];
        if(rows is null||rows.Length!=Canvas||
           rows.Any(row=>row is null||row.Length!=Canvas||
               row.Any(c=>c!='0'&&c!='1')))
            return false;
        for(int y=0;y<Canvas;y++)for(int x=0;x<Canvas;x++)
            pixels[y*Canvas+x]=rows[y][x]=='1';
        return true;
    }
}

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
    {
        if(pixels.Length!=Canvas*Canvas)return false;
        int on=0,minX=Canvas,minY=Canvas,maxX=-1,maxY=-1;
        for(int y=0;y<Canvas;y++)for(int x=0;x<Canvas;x++)
        {
            if(!pixels[y*Canvas+x])continue;
            on++;
            minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);
            minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
        }
        if(on>900)return true;
        if(on==0)return false;
        int width=maxX-minX+1,height=maxY-minY+1;
        int area=width*height;
        // Legacy converters could serialize a perfectly filled 31x25
        // rectangle (775/1024 lit pixels). Pixel density alone missed it.
        // Preserve detailed high-density logos: reject only near-solid,
        // large axis-aligned blocks, not arbitrary bright artwork.
        return width>=16&&height>=16&&
            area>=Canvas*Canvas*0.42&&on>=area*0.97;
    }

    internal static bool[] ConvertImage(Image source)=>
        ContrastIconRasterizer.Convert(source);

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

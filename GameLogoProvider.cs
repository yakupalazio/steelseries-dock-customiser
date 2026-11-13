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
        string identity=game.Executable.Trim().ToLowerInvariant()+"\n"+
            game.Name.Trim().ToLowerInvariant();
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
            if(File.Exists(file)&&new FileInfo(file).Length<12000)
            {
                var asset=JsonSerializer.Deserialize<Asset>(File.ReadAllText(file));
                if(asset is not null&&
                   string.Equals(asset.Executable,game.Executable,
                       StringComparison.OrdinalIgnoreCase)&&
                   string.Equals(asset.Name,game.Name,
                       StringComparison.OrdinalIgnoreCase)&&
                   TryReadRows(asset.Rows,out var pixels))
                    return new(game.Executable,game.Name,Canvas,pixels,
                        "Kullanıcı logosu");
            }
        }
        catch(IOException){}
        catch(JsonException){}
        catch(UnauthorizedAccessException){}

        string? registered=GgGameDetector.RegisteredExecutable(game);
        if(registered is null)return null;
        try
        {
            using var icon=Icon.ExtractAssociatedIcon(registered);
            if(icon is null)return null;
            using var image=icon.ToBitmap();
            var data=ConvertImage(image);
            return data.Any(x=>x)
                ?new(game.Executable,game.Name,Canvas,data,"Oyun EXE simgesi")
                :null;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or System.Runtime.InteropServices.ExternalException)
        {
            return null;
        }
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

    internal static bool[] ConvertImage(Image source)
    {
        using var bitmap=new Bitmap(Canvas,Canvas,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.SmoothingMode=SmoothingMode.AntiAlias;
            float scale=Math.Min((float)Canvas/source.Width,
                (float)Canvas/source.Height);
            int w=Math.Max(1,(int)Math.Round(source.Width*scale));
            int h=Math.Max(1,(int)Math.Round(source.Height*scale));
            g.DrawImage(source,(Canvas-w)/2,(Canvas-h)/2,w,h);
        }
        int fullyTransparent=0;
        for(int y=0;y<Canvas;y++)for(int x=0;x<Canvas;x++)
            if(bitmap.GetPixel(x,y).A<32)fullyTransparent++;
        bool silhouette=fullyTransparent>Canvas*Canvas/10;
        var pixels=new bool[Canvas*Canvas];
        for(int y=0;y<Canvas;y++)for(int x=0;x<Canvas;x++)
        {
            var c=bitmap.GetPixel(x,y);
            if(c.A<88)continue;
            int luminosity=(c.R*3+c.G*6+c.B)/10;
            pixels[y*Canvas+x]=silhouette
                ?c.A>110:luminosity>=112;
        }
        return pixels;
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

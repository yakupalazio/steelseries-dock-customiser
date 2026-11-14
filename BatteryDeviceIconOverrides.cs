using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Explicit per-device OLED icons. No USB/Bluetooth identifiers, source
/// image paths or linked account data are persisted; an imported image
/// is converted to an 11x11 monochrome bitmap and stored locally.
/// </summary>
internal static class BatteryDeviceIconOverrides
{
    private sealed class IconOption
    {
        public string Style {get;set;}="";
        public string[] Pixels {get;set;}=[];
    }

    private static readonly object Gate=new();
    private static Dictionary<string,IconOption>? cache;
    private static string FilePath=>Path.Combine(
        Beta5Sandbox.SettingsDirectory,"battery-device-icons-v1.json");

    internal static readonly string[] Choices=[
        "Phone","Mouse","Keyboard","Headset","Earbuds",
        "DualSense","Laptop","Speaker","Bluetooth","Usb","Wireless"
    ];

    private static string NameKey(string? name)
    {
        string label=WirelessCompatibilityCatalog.Normalize(name??"");
        return "name:"+label[..Math.Min(label.Length,120)];
    }

    private static string KindKey(string? kind)=>
        "kind:"+((kind??"").Length>150 ? kind![..150] : kind??"");

    private static Dictionary<string,IconOption> Load()
    {
        if(cache is not null)return cache;
        try
        {
            if(File.Exists(FilePath)&&new FileInfo(FilePath).Length<120_000)
            {
                var saved=JsonSerializer.Deserialize<
                    Dictionary<string,IconOption>>(File.ReadAllText(FilePath));
                if(saved is {Count:<=250})
                {
                    var clean=new Dictionary<string,IconOption>(
                        StringComparer.OrdinalIgnoreCase);
                    foreach(var (key,value) in saved)
                    {
                        if(key.Length>165||value is null)continue;
                        if(Choices.Contains(value.Style) ||
                           value.Style=="Custom"&&ValidPixels(value.Pixels))
                            clean[key]=value;
                    }
                    return cache=clean;
                }
            }
        }
        catch{}
        return cache=new(StringComparer.OrdinalIgnoreCase);
    }

    private static bool ValidPixels(string[]? rows)=>
        rows is {Length:11}&&rows.All(r=>r.Length==11&&
            r.All(c=>c is '0' or '1'));

    internal static string Resolve(
        string? kind,string? name,string defaultIcon)
    {
        lock(Gate)
        {
            var saved=Load();
            string namedKey=NameKey(name);
            string exactKey=KindKey(kind);
            string? selectedKey=null;
            if(!string.IsNullOrWhiteSpace(kind)&&
               saved.ContainsKey(exactKey))selectedKey=exactKey;
            else if(saved.ContainsKey(namedKey))selectedKey=namedKey;
            if(selectedKey is null)return defaultIcon;
            var selected=saved[selectedKey];
            if(selected.Style=="Custom")
                return CustomKey(selectedKey);
            return selected.Style;
        }
    }

    private static string CustomKey(string persistedKey)=>
        "DeviceCustom:"+Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(persistedKey)),0,8);

    internal static string[]? CustomRows(string iconKind)
    {
        if(!iconKind.StartsWith("DeviceCustom:",StringComparison.Ordinal))
            return null;
        lock(Gate)
        {
            foreach(var (key,entry) in Load())
            {
                if(entry.Style!="Custom")continue;
                string candidate="DeviceCustom:"+
                    Convert.ToHexString(SHA256.HashData(
                        Encoding.UTF8.GetBytes(key)),0,8);
                if(candidate==iconKind)return entry.Pixels.ToArray();
            }
            return null;
        }
    }

    private static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string target=FilePath+".tmp";
        File.WriteAllText(target,JsonSerializer.Serialize(Load(),
            new JsonSerializerOptions{WriteIndented=true}));
        File.Move(target,FilePath,true);
    }

    internal static void Assign(string? kind,string name,string icon)
    {
        if(!Choices.Contains(icon))throw new ArgumentException("Unknown icon.");
        if(!LocalBatteryDeviceNames.IsUsableName(name))
            throw new ArgumentException("Device name is unavailable.");
        lock(Gate)
        {
            var entry=new IconOption{Style=icon};
            Load()[NameKey(name)]=entry;
            if(!string.IsNullOrWhiteSpace(kind))
                Load()[KindKey(kind)]=entry;
            Save();
        }
    }

    internal static void Reset(string? kind,string name)
    {
        lock(Gate)
        {
            Load().Remove(NameKey(name));
            if(!string.IsNullOrWhiteSpace(kind))
                Load().Remove(KindKey(kind));
            Save();
        }
    }

    internal static string[] Rasterize(Bitmap source)
    {
        using var small=new Bitmap(11,11);
        using(var g=Graphics.FromImage(small))
        {
            g.Clear(Color.Transparent);
            g.CompositingMode=CompositingMode.SourceCopy;
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.DrawImage(source,new Rectangle(0,0,11,11));
        }
        bool hasAlpha=false;
        double borderLuminance=0;
        int samples=0;
        for(int y=0;y<11;y++)for(int x=0;x<11;x++)
        {
            var c=small.GetPixel(x,y);
            if(c.A<245)hasAlpha=true;
            if(x is 0 or 10||y is 0 or 10)
            {
                borderLuminance+=(c.R+c.G+c.B)/3.0;
                samples++;
            }
        }
        bool darkInk=!hasAlpha&&borderLuminance/samples>=145;
        var rows=new string[11];
        for(int y=0;y<11;y++)
        {
            var builder=new StringBuilder(11);
            for(int x=0;x<11;x++)
            {
                var color=small.GetPixel(x,y);
                int brightness=(color.R+color.G+color.B)/3;
                bool lit=hasAlpha?color.A>=115 :
                    darkInk?brightness<135:brightness>=145;
                builder.Append(lit?'1':'0');
            }
            rows[y]=builder.ToString();
        }
        return rows;
    }

    internal static void Import(string? kind,string name,string imagePath)
    {
        using var original=new Bitmap(imagePath);
        if(original.Width>8192||original.Height>8192)
            throw new InvalidDataException("Image exceeds size limit.");
        var pixels=Rasterize(original);
        lock(Gate)
        {
            var entry=new IconOption{Style="Custom",Pixels=pixels};
            Load()[NameKey(name)]=entry;
            if(!string.IsNullOrWhiteSpace(kind))
                Load()[KindKey(kind)]=entry;
            Save();
        }
    }
}

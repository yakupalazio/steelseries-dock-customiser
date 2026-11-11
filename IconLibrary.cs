using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

internal static class IconLibrary
{
    public static readonly string[] Names=["Mouse","Phone","DualSense","Headset","Laptop","Spotify","Media","Game","Chat","Charging",
        "Earbuds","Keyboard","Speaker","Bluetooth","Usb","Wireless"];
    private static readonly string FileName=Path.Combine(
        Beta5Sandbox.SettingsDirectory,"icons-v09.json");
    private static readonly string PersonalDefaultsFile=Path.Combine(
        Path.GetDirectoryName(FileName)!,"personal-default-icons-v1.json");
    private static readonly Dictionary<string,string[]> Custom=new(StringComparer.OrdinalIgnoreCase);

    static IconLibrary() { Load(); }

    public static bool TryCustom(string name,out string[] rows)
    {
        if(Custom.TryGetValue(name,out var s)){rows=s;return true;}
        rows=[];return false;
    }

    public static string[] GetCurrent(string name)
    {
        return Custom.TryGetValue(name,out var s)
            ? s.ToArray() : OledRenderer.GetOriginalIconRows(name);
    }

    public static bool IsModified(string name)=>Custom.ContainsKey(name);

    public static void Set(string name,string[] rows)
    {
        if(!Names.Contains(name))throw new ArgumentException("Unknown icon");
        Validate(rows,name);
        Custom[name]=rows.ToArray();
        Save();
    }

    public static void Reset(string name)
    {
        Custom.Remove(name);Save();
    }

    public static void Load()
    {
        Custom.Clear();
        try
        {
            string source=File.Exists(FileName)?FileName:PersonalDefaultsFile;
            if(!File.Exists(source))return;
            var icons=JsonSerializer.Deserialize<Dictionary<string,string[]>>(File.ReadAllText(source));
            if(icons is null || icons.Count>Names.Length) return;
            foreach(var (name,rows) in icons)
            {
                if(!Names.Contains(name))continue;
                try{Validate(rows,name);Custom[name]=rows.ToArray();}catch{}
            }
        }
        catch{}
    }
    private static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
        string json=JsonSerializer.Serialize(Custom,new JsonSerializerOptions{WriteIndented=true});
        File.WriteAllText(FileName,json);
        try
        {
            string temporary=PersonalDefaultsFile+".tmp";
            File.WriteAllText(temporary,json);
            File.Move(temporary,PersonalDefaultsFile,true);
        }
        catch { /* A backup failure must never discard the primary icon edit. */ }
    }
    private static void Validate(string[] rows,string? name=null)
    {
        if(rows is null||rows.Length<4||rows.Length>20)throw new ArgumentException("Height 4-20");
        int width=rows[0].Length;
        if(width<4||width>20||rows.Any(r=>r.Length!=width||r.Any(c=>c!='0'&&c!='1')))
            throw new ArgumentException("Every icon must have equal-sized 0/1 rows, 4-20 px.");
        // Tiny fixed OLED slots cannot safely accommodate arbitrary 20px badges.
        int maxWidth=name is "Game" or "Chat"?12:name=="Charging"?4:20;
        int maxHeight=name is "Game" or "Chat"?10:name=="Charging"?7:20;
        if(width>maxWidth||rows.Length>maxHeight)
            throw new ArgumentException($"{name} icon limit: {maxWidth}x{maxHeight} pixels.");
    }
}

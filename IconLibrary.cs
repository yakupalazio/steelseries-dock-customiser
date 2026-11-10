using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

internal static class IconLibrary
{
    public static readonly string[] Names=["Mouse","Phone","DualSense","Headset","SpareBattery","Laptop","Spotify","Media"];
    private static readonly string FileName=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","icons-v09.json");
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
        Validate(rows);
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
            if(!File.Exists(FileName))return;
            var icons=JsonSerializer.Deserialize<Dictionary<string,string[]>>(File.ReadAllText(FileName));
            if(icons is null || icons.Count>Names.Length) return;
            foreach(var (name,rows) in icons)
            {
                if(!Names.Contains(name))continue;
                try{Validate(rows);Custom[name]=rows.ToArray();}catch{}
            }
        }
        catch{}
    }
    private static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
        File.WriteAllText(FileName,JsonSerializer.Serialize(Custom,new JsonSerializerOptions{WriteIndented=true}));
    }
    private static void Validate(string[] rows)
    {
        if(rows is null||rows.Length<4||rows.Length>20)throw new ArgumentException("Height 4-20");
        int width=rows[0].Length;
        if(width<4||width>20||rows.Any(r=>r.Length!=width||r.Any(c=>c!='0'&&c!='1')))
            throw new ArgumentException("Every icon must have equal-sized 0/1 rows, 4-20 px.");
    }
}

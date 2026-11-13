using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

/// <summary>Local, private nicknames for verified battery provider keys.
/// No physical device identifiers, source paths or cloud dependency.</summary>
internal static class BatteryDeviceAliases
{
    private static string FilePath=>Path.Combine(
        Beta5Sandbox.SettingsDirectory,"battery-aliases-v1.json");
    private static readonly object Sync=new();
    private static Dictionary<string,string>? cached;

    private static Dictionary<string,string> Load()
    {
        if(cached is not null)return cached;
        try
        {
            if(File.Exists(FilePath)&&new FileInfo(FilePath).Length<16384)
                return cached=JsonSerializer.Deserialize<
                    Dictionary<string,string>>(File.ReadAllText(FilePath))
                    ??new(StringComparer.OrdinalIgnoreCase);
        }
        catch{}
        return cached=new(StringComparer.OrdinalIgnoreCase);
    }
    internal static string? Name(string kind)
    {
        lock(Sync)
        {
            var names=Load();
            return names.TryGetValue(kind,out var name)&&
                LocalBatteryDeviceNames.IsUsableName(name)?name:null;
        }
    }

    internal static void Set(string kind,string alias)
    {
        if(string.IsNullOrWhiteSpace(kind)||kind.Length>120 ||
           (!kind.StartsWith("Ble:",StringComparison.OrdinalIgnoreCase)&&
            !kind.StartsWith("Hid:",StringComparison.OrdinalIgnoreCase)&&
            !kind.StartsWith("Vendor:",StringComparison.OrdinalIgnoreCase)&&
            !kind.StartsWith("WinBt:",StringComparison.OrdinalIgnoreCase)&&
            !BatteryPriority.AllKinds.Contains(kind)))
            throw new ArgumentException("Unsupported battery identity.");
        if(alias.Length>48 || !LocalBatteryDeviceNames.IsUsableName(alias))
            throw new ArgumentException("Use a valid device nickname (2–48 characters).");
        lock(Sync)
        {
            var names=Load().ToDictionary(x=>x.Key,x=>x.Value,
                StringComparer.OrdinalIgnoreCase);
            names[kind]=alias.Trim();
            if(names.Count>100)
                names=names.OrderBy(x=>x.Key).Take(100)
                    .ToDictionary(x=>x.Key,x=>x.Value,
                        StringComparer.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temporary=FilePath+".tmp";
            File.WriteAllText(temporary,JsonSerializer.Serialize(names));
            File.Move(temporary,FilePath,true);
            cached=names;
        }
    }
}

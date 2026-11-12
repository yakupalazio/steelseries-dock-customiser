using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

/// <summary>
/// The designer-approved, saved RC2.1 layout and icon sprites are bundled
/// read-only into the portable EXE. Existing user profiles take precedence.
/// There are no environment-specific paths or account identifiers in presets.
/// </summary>
internal static class FactoryPresetDefaults
{
    private const string StudioResource=
        "SteelseriesDockCustomiser.FactoryPresets.Studio";
    private const string IconResource=
        "SteelseriesDockCustomiser.FactoryPresets.Icons";

    private static string? Read(string name)
    {
        try
        {
            using var source=Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(name);
            if(source is null)return null;
            using var reader=new StreamReader(source);
            return reader.ReadToEnd();
        }
        catch{return null;}
    }

    public static StudioProject? LoadStudio()
    {
        try
        {
            string? json=Read(StudioResource);
            if(json is null)return null;
            return JsonSerializer.Deserialize<StudioProject>(json);
        }
        catch{return null;}
    }

    public static Dictionary<string,string[]> LoadIcons()
    {
        try
        {
            string? json=Read(IconResource);
            return json is null
                ?new Dictionary<string,string[]>()
                :JsonSerializer.Deserialize<Dictionary<string,string[]>>(json)
                  ??new Dictionary<string,string[]>();
        }
        catch{return new Dictionary<string,string[]>();}
    }
}

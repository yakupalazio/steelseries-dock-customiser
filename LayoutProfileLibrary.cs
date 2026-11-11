using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

internal sealed class SavedLayoutProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ScreenMode Mode { get; set; }
    public LayoutState Layout { get; set; } = new();
    public override string ToString() => Name;
}

/// <summary>
/// Local data-only profiles, kept separate from the current three live
/// layouts. Import does not load code, executables, network URLs or scripts.
/// </summary>
internal sealed class LayoutProfileLibrary
{
    public List<SavedLayoutProfile> Profiles { get; set; } = [];
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","profiles-v1.json");

    public static LayoutProfileLibrary Load()
    {
        try
        {
            if(!File.Exists(FilePath) || new FileInfo(FilePath).Length>1_000_000)
                return new();
            var library=JsonSerializer.Deserialize<LayoutProfileLibrary>(
                File.ReadAllText(FilePath))??new();
            library.Profiles=(library.Profiles??[]).Take(64)
                .Where(x=>x!=null && x.Id!=Guid.Empty &&
                    x.Name is {Length:>0 and <=64} &&
                    Enum.IsDefined(x.Mode) && x.Layout!=null)
                .ToList();
            foreach(var profile in library.Profiles)
                StudioProject.Sanitize(profile.Layout,profile.Mode,true);
            return library;
        }
        catch{return new();}
    }

    public void Save()
    {
        if(Profiles.Count>64)throw new InvalidOperationException("Too many profiles.");
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temporary=FilePath+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(this,
            new JsonSerializerOptions{WriteIndented=true}));
        File.Move(temporary,FilePath,true);
    }

    public SavedLayoutProfile Add(string name,ScreenMode mode,LayoutState layout)
    {
        if(Profiles.Count>=64)throw new InvalidOperationException("Profile limit reached.");
        if(string.IsNullOrWhiteSpace(name)||name.Length>64)
            throw new ArgumentException("Profile name must contain 1–64 characters.");
        var copy=layout.DeepClone();
        StudioProject.Sanitize(copy,mode,true);
        var profile=new SavedLayoutProfile{
            Id=Guid.NewGuid(),Name=name.Trim(),Mode=mode,Layout=copy};
        Profiles.Add(profile);
        return profile;
    }

    public static SavedLayoutProfile ReadImport(string file)
    {
        if(new FileInfo(file).Length>1_000_000)
            throw new InvalidDataException("Profile file is too large.");
        var profile=JsonSerializer.Deserialize<SavedLayoutProfile>(
            File.ReadAllText(file))??throw new InvalidDataException("Empty profile.");
        if(!Enum.IsDefined(profile.Mode)||profile.Layout is null ||
           string.IsNullOrWhiteSpace(profile.Name)||profile.Name.Length>64)
            throw new InvalidDataException("Invalid profile structure.");
        StudioProject.Sanitize(profile.Layout,profile.Mode,true);
        profile.Id=Guid.NewGuid();
        return profile;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Offline, versioned data-only contract for future community gallery.
/// Never uploads, fetches, authenticates or publishes anything. Stable
/// on-disk profile format remains untouched and personal PC identifiers
/// are stripped from any future shareable draft.
/// </summary>
internal static class WorkshopProfileContract
{
    internal const int SchemaVersion=1;
    internal sealed class Draft
    {
        public int Version {get;set;}=SchemaVersion;
        public string Title {get;set;}="";
        public string Description {get;set;}="";
        public ScreenMode Mode {get;set;}
        public LayoutState Layout {get;set;}=new();
        public List<AssetPlaceholder> Assets {get;set;}=[];
    }
    internal sealed record AssetPlaceholder(
        string Kind,string WidgetKind,string? ContentHash);
    internal static Draft Prepare(SavedLayoutProfile profile,
        string description="")
    {
        if(profile.Name.Length<1||profile.Name.Length>64 ||
           description.Length>500)
            throw new ArgumentException("Invalid workshop draft metadata.");
        var sourceGifs=profile.Layout.Widgets.Count(w=>
            w.Kind=="AnimatedGif"&&!string.IsNullOrEmpty(w.GifAssetId));
        LayoutState safe=profile.Layout.DeepClone();
        StudioProject.Sanitize(safe,profile.Mode,true);
        var assets=new List<AssetPlaceholder>();
        foreach(var widget in safe.Widgets)
        {
            // Source device IDs/paths are local-only and never transferable.
            widget.AudioEndpointId="";
            if(widget.Kind=="AnimatedGif")
            {
                widget.GifAssetId="";
            }
            // Game icons are private per GG executable name and must be
            // explicitly approved/imported separately in a future gallery.
        }
        // Preserve a future opt-in asset placeholder even if local GIF
        // validation strips the machine-specific asset path.
        for(int i=0;i<sourceGifs;i++)
            assets.Add(new("image/gif","AnimatedGif",null));
        return new Draft{
            Title=profile.Name,
            Description=description.Trim(),
            Mode=profile.Mode,Layout=safe,Assets=assets};
    }
    internal static string Serialize(Draft draft)=>
        JsonSerializer.Serialize(draft,new JsonSerializerOptions{WriteIndented=true});
    internal static Draft Parse(string json)
    {
        if(json.Length>1_000_000)
            throw new ArgumentException("Workshop draft too large.");
        var result=JsonSerializer.Deserialize<Draft>(json)
            ??throw new ArgumentException("Empty draft.");
        if(result.Version!=SchemaVersion ||
           !Enum.IsDefined(result.Mode) ||
           result.Title.Length is <1 or >64 ||
           result.Description.Length>500 ||
           result.Layout is null)
            throw new ArgumentException("Unsupported workshop draft.");
        StudioProject.Sanitize(result.Layout,result.Mode,true);
        // Never trust imported source device IDs.
        foreach(var widget in result.Layout.Widgets)
        {
            widget.AudioEndpointId="";
            widget.GifAssetId="";
        }
        return result;
    }
}

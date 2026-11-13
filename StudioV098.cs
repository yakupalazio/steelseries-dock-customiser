using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

internal enum ScreenMode { Media=0, Game=1, Idle=2 }

/// <summary>Three fully independent design surfaces with a shared hardware-header policy.</summary>
internal sealed class StudioProject
{
    public LayoutState Media { get; set; } = Defaults(ScreenMode.Media);
    public LayoutState Game { get; set; } = Defaults(ScreenMode.Game);
    public LayoutState Idle { get; set; } = Defaults(ScreenMode.Idle);
    public bool ProtectHeader { get; set; } = true;
    // Live output may own all 128x64 pixels to reclaim the screen after wheel input.
    // Static Apply still follows ProtectHeader, and Reset always restores the OEM.
    public bool LiveOwnsDisplay { get; set; } = false;
    // A one-time migration keeps pre-1.1 layouts intact and, crucially,
    // remembers when a user deliberately removes either default dial.
    public bool RadialDefaultsMigrated { get; set; }
    // One-time startup alignment only: the two wheels remain independently
    // editable afterwards. Never synchronize user edits between modes.
    public bool WheelDefaultsAligned { get; set; }
    public static string PersonalDefaultPath => Path.Combine(
        Path.GetDirectoryName(FilePath)!,"personal-default-studio-v1.json");

    public LayoutState For(ScreenMode mode) => mode switch
    {
        ScreenMode.Media=>Media,
        ScreenMode.Game=>Game,
        _=>Idle
    };

    public void Set(ScreenMode mode,LayoutState layout)
    {
        switch(mode)
        {
            case ScreenMode.Media: Media=layout;break;
            case ScreenMode.Game: Game=layout;break;
            default: Idle=layout;break;
        }
    }

    public static string FilePath => Path.Combine(
        Beta5Sandbox.SettingsDirectory,"studio-v100.json");

    public static StudioProject Load()
    {
        try
        {
            // Copy-on-first-load. Prefer the v1.0 layout, fall back to
            // v0.9.9 and v0.9.8. Never overwrite the old file versions.
            string folder=Path.GetDirectoryName(FilePath)!;
            string? source=new[]{
                FilePath,
                PersonalDefaultPath,
                Path.Combine(folder,"studio-v099.json"),
                Path.Combine(folder,"studio-v098.json")
            }.FirstOrDefault(File.Exists);
            if(source!=null)
            {
                var result=JsonSerializer.Deserialize<StudioProject>(File.ReadAllText(source));
                if(result!=null)
                {
                    foreach(var mode in Enum.GetValues<ScreenMode>())
                        Sanitize(result.For(mode),mode,result.ProtectHeader);
                    result.MigrateRadialDefaults();
                    result.AlignWheelDefaultsOnce();
                    return result;
                }
            }
        }
        catch{}
        // First launch on another PC: use the designer-approved saved
        // layout embedded in the EXE. Existing profiles always win.
        var preset=FactoryPresetDefaults.LoadStudio();
        if(preset is not null)
        {
            foreach(var mode in Enum.GetValues<ScreenMode>())
                Sanitize(preset.For(mode),mode,preset.ProtectHeader);
            preset.MigrateRadialDefaults();
            preset.AlignWheelDefaultsOnce();
            return preset;
        }
        return new StudioProject { RadialDefaultsMigrated=true, WheelDefaultsAligned=true };
    }

    // The user's last confirmed design is a *personal* reset baseline,
    // backed up outside the repository. Factory defaults remain separate.
    public static LayoutState UserDefault(ScreenMode mode)
    {
        try
        {
            if(File.Exists(PersonalDefaultPath))
            {
                var project=JsonSerializer.Deserialize<StudioProject>(
                    File.ReadAllText(PersonalDefaultPath));
                if(project is not null)
                {
                    project.AlignWheelDefaultsOnce();
                    var layout=project.For(mode).DeepClone();
                    Sanitize(layout,mode,project.ProtectHeader);
                    return layout;
                }
            }
        }
        catch { }
        var preset=FactoryPresetDefaults.LoadStudio();
        if(preset is not null)
        {
            var layout=preset.For(mode).DeepClone();
            Sanitize(layout,mode,preset.ProtectHeader);
            return layout;
        }
        return Defaults(mode);
    }

    private void AlignWheelDefaultsOnce()
    {
        if(WheelDefaultsAligned)return;
        var idle=Idle.Widgets.FirstOrDefault(w=>w.Kind=="VolumeChatMix");
        var game=Game.Widgets.FirstOrDefault(w=>w.Kind=="VolumeChatMix");
        if(idle is { DialStyle:"Wheel" } && game is { DialStyle:"Wheel" })
        {
            // Align only presentation and position. Saved artwork, mode
            // layouts and user-drawn pixels remain untouched.
            game.X=idle.X;
            game.Y=idle.Y;
            game.WheelGapAngle=idle.WheelGapAngle;
            game.WheelAutoLabel=idle.WheelAutoLabel;
            game.WheelIconsFollowRing=idle.WheelIconsFollowRing;
            game.WheelGameOffsetX=idle.WheelGameOffsetX;
            game.WheelGameOffsetY=idle.WheelGameOffsetY;
            game.WheelChatOffsetX=idle.WheelChatOffsetX;
            game.WheelChatOffsetY=idle.WheelChatOffsetY;
            game.WheelShowGame=idle.WheelShowGame;
            game.WheelShowChat=idle.WheelShowChat;
            game.WheelCloseEnds=idle.WheelCloseEnds;
            game.WheelRingThickness=idle.WheelRingThickness;
            if(string.IsNullOrEmpty(game.WheelPixels))
                game.WheelPixels=idle.WheelPixels;
        }
        WheelDefaultsAligned=true;
    }

    public void Save()
    {
        RadialDefaultsMigrated=true;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        foreach(var mode in Enum.GetValues<ScreenMode>())
            Sanitize(For(mode),mode,ProtectHeader);
        string json=JsonSerializer.Serialize(this,new JsonSerializerOptions{
            WriteIndented=true
        });
        File.WriteAllText(FilePath,json);
        // Last explicitly saved artwork is this user's personal default.
        // A second atomic copy survives accidental deletion or file damage.
        try
        {
            string temporary=PersonalDefaultPath+".tmp";
            File.WriteAllText(temporary,json);
            File.Move(temporary,PersonalDefaultPath,true);
        }
        catch { /* Primary save has already succeeded. */ }
    }

    public static LayoutState Defaults(ScreenMode mode)
    {
        List<DockWidget> common=[
            new(){Kind="BatterySlot1",X=1,Y=53},
            new(){Kind="BatterySlot2",X=85,Y=53}
        ];
        List<DockWidget> primary=mode switch
        {
            ScreenMode.Media=>[
                new(){Kind="Source",X=2,Y=21},
                new(){Kind="Track",X=20,Y=21,FontSize=9},
                new(){Kind="Artist",X=20,Y=32,FontSize=9},
                new(){Kind="Progress",X=20,Y=43},
                new(){Kind="MediaClock",X=92,Y=11,FontSize=7,StrokeWidth=1}
            ],
            ScreenMode.Game=>[
                new(){Kind="GameLabel",X=78,Y=15,FontSize=7,Text="GAME:"},
                new(){Kind="GameTitle",X=78,Y=29,FontSize=7,Text="VALORANT"},
                new(){Kind="VolumeChatMix",X=2,Y=11,DialStyle="Wheel"}
            ],
            _=>[
                new(){Kind="Clock",X=85,Y=18,FontSize=10},
                new(){Kind="Date",X=67,Y=45,FontSize=7},
                new(){Kind="VolumeChatMix",X=2,Y=11,DialStyle="Wheel"}
            ]
        };
        return new LayoutState{Widgets=primary.Concat(common).ToList()};
    }

    internal void MigrateRadialDefaults()
    {
        if(RadialDefaultsMigrated)return;
        // Older media presets placed the tiny clock a few rows too low,
        // where its last pixels touched long scrolling song titles.
        var mediaClock=Media.Widgets.FirstOrDefault(w=>w.Kind=="MediaClock" &&
            w.X>=85 && w.Y>=15 && w.Y<=18);
        if(mediaClock!=null && Media.Widgets.Any(w=>w.Kind=="Track" && w.Y<=23))
            mediaClock.Y=11;
        foreach(var mode in new[]{ScreenMode.Idle,ScreenMode.Game})
        {
            var layout=For(mode);
            var oldDial=layout.Widgets.FirstOrDefault(w=>w.Kind=="VolumeChatMix");
            // A legacy bar in its old default/left-top slot is migrated once.
            // A bar deliberately positioned elsewhere keeps its custom style.
            bool legacyBar=oldDial is {DialStyle:"Bar"} &&
                oldDial.X<=12 && oldDial.Y>=19 && oldDial.Y<=30;
            if(oldDial==null || legacyBar)
            {
                if(oldDial==null)
                    layout.Widgets.Insert(0,Defaults(mode).Widgets.First(
                        w=>w.Kind=="VolumeChatMix"));
                else
                {
                    oldDial.DialStyle="Wheel";
                    (oldDial.X,oldDial.Y)=(2,11);
                }
                // Only adjust familiar legacy defaults, leaving custom
                // placement untouched. Users can remove and save the wheel.
                if(mode==ScreenMode.Idle)
                {
                    var clock=layout.Widgets.FirstOrDefault(w=>w.Kind=="Clock");
                    var date=layout.Widgets.FirstOrDefault(w=>w.Kind=="Date");
                    if(clock is {X:31,Y:17,FontSize:20})
                        (clock.X,clock.Y,clock.FontSize)=(85,18,10);
                    if(clock is {X:72,Y:20})
                        (clock.X,clock.Y,clock.FontSize)=(85,18,10);
                    if(date is {X:37,Y:35})
                        (date.X,date.Y,date.FontSize)=(67,45,7);
                    if(date is {X:62,Y:38})
                        (date.X,date.Y,date.FontSize)=(67,45,7);
                }
                else
                {
                    var title=layout.Widgets.FirstOrDefault(w=>w.Kind=="GameTitle");
                    var label=layout.Widgets.FirstOrDefault(w=>w.Kind=="GameLabel");
                    var legacyClock=layout.Widgets.FirstOrDefault(w=>w.Kind=="Clock" &&
                        w.X==92 && w.Y==11 && w.FontSize==7);
                    if(legacyClock!=null)layout.Widgets.Remove(legacyClock);
                    if(title is {X:3,Y:30})
                        (title.X,title.Y,title.FontSize)=(78,29,7);
                    if(label is {X:3,Y:15})
                        (label.X,label.Y,label.FontSize,label.Text)=(78,15,7,"GAME:");
                }
            }
        }
        RadialDefaultsMigrated=true;
    }

    // Starter layouts remain useful defaults, but no visual widget is
    // mandatory. This allows a genuinely blank custom design in every mode.
    public static bool IsLocked(ScreenMode mode,string kind) => false;

    public static bool IsAllowed(ScreenMode mode,string kind) => kind switch
    {
        "Source" or "Track" or "Artist" or "Progress" or "MediaClock" or
        "Lyrics" or "AudioVisualizer" =>mode==ScreenMode.Media,
        "GameLabel" or "GameTitle" or "GameMiniPlayer" or "GameLogo"=>
            mode==ScreenMode.Game,
        "Clock" or "Date" or "Custom text" or
        "SystemCpu" or "SystemRam" or "NetworkPing" or
        "AnimatedGif" or
        "Mouse" or "Phone" or "DualSense" or "Headset" or "SpareBattery" or "Laptop" or
        "BatterySlot1" or "BatterySlot2" or "BatterySlot3" or "VolumeChatMix"=>true,
        _=>false
    };

    public static void Sanitize(LayoutState layout,ScreenMode mode,bool protectHeader)
    {
        if(layout.Pixels?.Length!=8192)layout.Pixels=new bool[8192];
        layout.Widgets??=[];
        // One instance per normal widget, only four explicit exceptions.
        // If old profiles contain duplicates, retain a VISIBLE instance
        // rather than hiding the user's design by keeping an older invisible
        // entry. The original on-disk profile is never overwritten by Load.
        var candidates=layout.Widgets.Take(80)
            .Where(w=>w!=null && IsAllowed(mode,w.Kind)).ToList();
        var preferred=candidates.Where(w=>!WidgetCatalog.AllowsMultiple(w.Kind))
            .GroupBy(w=>w.Kind,StringComparer.Ordinal)
            .ToDictionary(g=>g.Key,g=>g.FirstOrDefault(w=>w.Visible)??g.First(),
                StringComparer.Ordinal);
        layout.Widgets=candidates
            .Where(w=>WidgetCatalog.AllowsMultiple(w.Kind) ||
                ReferenceEquals(w,preferred[w.Kind]))
            .ToList();
        foreach(var w in layout.Widgets)
        {
            w.X=Math.Clamp(w.X,0,127);
            w.Y=Math.Clamp(w.Y,0,63);
            w.StrokeWidth=Math.Clamp(w.StrokeWidth,1,3);
            w.MiniWidth=Math.Clamp(w.MiniWidth,35,128);
            w.MiniWheelSize=Math.Clamp(w.MiniWheelSize,70,100);
            w.DialStyle=w.DialStyle is "Wheel" or "MiniWheel" ? w.DialStyle : "Bar";
            if(!WheelArtwork.IsValid(w.WheelPixels))w.WheelPixels="";
            // Upgrade previously saved 45x42 mini art to its native 31x29
            // canvas; never mutate the separate full-size WheelPixels.
            w.MiniWheelPixels=MiniWheelArtwork.Normalize(w.MiniWheelPixels);
            w.WheelGapAngle=Math.Clamp(w.WheelGapAngle,-180,180);
            w.WheelGameOffsetX=Math.Clamp(w.WheelGameOffsetX,-64,64);
            w.WheelGameOffsetY=Math.Clamp(w.WheelGameOffsetY,-64,64);
            w.WheelChatOffsetX=Math.Clamp(w.WheelChatOffsetX,-64,64);
            w.WheelChatOffsetY=Math.Clamp(w.WheelChatOffsetY,-64,64);
            w.WheelRingThickness=Math.Clamp(w.WheelRingThickness,1,7);
            w.BarFrameThickness=Math.Clamp(w.BarFrameThickness,1,2);
            w.BarFillThickness=Math.Clamp(w.BarFillThickness,1,5);
            w.FontSize=Math.Clamp(w.FontSize,7,24);
            w.Text=(w.Text??"")[..Math.Min(w.Text?.Length??0,160)];
            w.LyricOffsetMs=Math.Clamp(w.LyricOffsetMs,-10000,10000);
            w.LyricLines=Math.Clamp(w.LyricLines,1,3);
            w.TextWidth=Math.Clamp(w.TextWidth,0,128);
            w.VisualizerStyle=w.VisualizerStyle is "Bars" or "Stereo" or "Circular" or "Bubbles" or "Pulse"
                ?w.VisualizerStyle:"Bars";
            w.AudioEndpointId=(w.AudioEndpointId??"")[..
                Math.Min(w.AudioEndpointId?.Length??0,512)];
            w.VisualizerGain=Math.Clamp(w.VisualizerGain,10,400);
            w.VisualizerHeight=Math.Clamp(w.VisualizerHeight,8,54);
            w.WidgetWidth=Math.Clamp(w.WidgetWidth,16,128);
            w.ProgressWidth=Math.Clamp(w.ProgressWidth,12,128);
            w.ProgressThickness=Math.Clamp(w.ProgressThickness,3,14);
            w.GameLogoSize=Math.Clamp(w.GameLogoSize,12,54);
            w.GifFit=w.GifFit is "Contain" or "Cover" or "Stretch"
                ?w.GifFit:"Contain";
            w.GifDither=w.GifDither is "FloydSteinberg" or "Bayer" or "Threshold"
                ?w.GifDither:"FloydSteinberg";
            w.GifSpeedPercent=Math.Clamp(w.GifSpeedPercent,25,400);
            w.GifThreshold=Math.Clamp(w.GifThreshold,20,235);
            if(!System.Text.RegularExpressions.Regex.IsMatch(
                w.GifAssetId??"",@"\A[a-fA-F0-9]{32}\.gif\z"))
                w.GifAssetId="";
        }
        // Never silently re-create widgets the user has removed. Defaults()
        // supplies the initial Media/Game/Idle UI instead.
        // Do not delete header artwork when protection is enabled:
        // the renderer simply masks it, so disabling protection restores it.
        foreach(var w in layout.Widgets)
            if(IsLocked(mode,w.Kind))w.Visible=true;
    }

    public static ScreenMode Choose(bool gameRunning,bool mediaPlaying) =>
        gameRunning?ScreenMode.Game:mediaPlaying?ScreenMode.Media:ScreenMode.Idle;
}

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
    // A one-time migration keeps pre-1.1 layouts intact and, crucially,
    // remembers when a user deliberately removes either default dial.
    public bool RadialDefaultsMigrated { get; set; }

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
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","studio-v100.json");

    public static StudioProject Load()
    {
        try
        {
            // Copy-on-first-load. Prefer the v1.0 layout, fall back to
            // v0.9.9 and v0.9.8. Never overwrite the old file versions.
            string folder=Path.GetDirectoryName(FilePath)!;
            string? source=new[]{
                FilePath,
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
                    return result;
                }
            }
        }
        catch{}
        return new StudioProject { RadialDefaultsMigrated=true };
    }

    public void Save()
    {
        RadialDefaultsMigrated=true;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        foreach(var mode in Enum.GetValues<ScreenMode>())
            Sanitize(For(mode),mode,ProtectHeader);
        File.WriteAllText(FilePath,JsonSerializer.Serialize(this,new JsonSerializerOptions{
            WriteIndented=true
        }));
    }

    public static LayoutState Defaults(ScreenMode mode)
    {
        List<DockWidget> common=[
            new(){Kind="BatterySlot1",X=1,Y=53},
            new(){Kind="BatterySlot2",X=43,Y=53},
            new(){Kind="BatterySlot3",X=85,Y=53}
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
        "Source" or "Track" or "Artist" or "Progress" or "MediaClock" =>
            mode==ScreenMode.Media,
        "GameLabel" or "GameTitle" or "GameMiniPlayer"=>mode==ScreenMode.Game,
        "Clock" or "Date" or "Custom text" or
        "SystemCpu" or "SystemRam" or "NetworkPing" or
        "Mouse" or "Phone" or "DualSense" or "Headset" or "SpareBattery" or "Laptop" or
        "BatterySlot1" or "BatterySlot2" or "BatterySlot3" or "VolumeChatMix"=>true,
        _=>false
    };

    public static void Sanitize(LayoutState layout,ScreenMode mode,bool protectHeader)
    {
        if(layout.Pixels?.Length!=8192)layout.Pixels=new bool[8192];
        layout.Widgets??=[];
        var mandatorySeen=new HashSet<string>();
        layout.Widgets=layout.Widgets.Take(80)
            .Where(w=>w!=null && IsAllowed(mode,w.Kind) &&
                (!IsLocked(mode,w.Kind)||mandatorySeen.Add(w.Kind)) &&
                (w.Kind!="GameMiniPlayer"||mandatorySeen.Add(w.Kind)))
            .ToList();
        foreach(var w in layout.Widgets)
        {
            w.X=Math.Clamp(w.X,0,127);
            w.Y=Math.Clamp(w.Y,0,63);
            w.StrokeWidth=Math.Clamp(w.StrokeWidth,1,3);
            w.MiniWidth=Math.Clamp(w.MiniWidth,35,128);
            w.DialStyle=w.DialStyle=="Wheel"?"Wheel":"Bar";
            if(!WheelArtwork.IsValid(w.WheelPixels))w.WheelPixels="";
            w.WheelGapAngle=Math.Clamp(w.WheelGapAngle,-180,180);
            w.FontSize=Math.Clamp(w.FontSize,7,24);
            w.Text=(w.Text??"")[..Math.Min(w.Text?.Length??0,160)];
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

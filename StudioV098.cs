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
        "SteelseriesDockCustomiser","studio-v098.json");

    public static StudioProject Load()
    {
        try
        {
            if(File.Exists(FilePath))
            {
                var result=JsonSerializer.Deserialize<StudioProject>(File.ReadAllText(FilePath));
                if(result!=null)
                {
                    foreach(var mode in Enum.GetValues<ScreenMode>())
                        Sanitize(result.For(mode),mode,result.ProtectHeader);
                    return result;
                }
            }
        }
        catch{}
        return new StudioProject();
    }

    public void Save()
    {
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
                new(){Kind="GameLabel",X=3,Y=15,FontSize=10,Text="NOW PLAYING:"},
                new(){Kind="GameTitle",X=3,Y=30,FontSize=10,Text="VALORANT"},
                new(){Kind="Clock",X=92,Y=11,FontSize=7}
            ],
            _=>[
                new(){Kind="Clock",X=31,Y=17,FontSize=20},
                new(){Kind="Date",X=37,Y=35,FontSize=9}
            ]
        };
        return new LayoutState{Widgets=primary.Concat(common).ToList()};
    }

    public static bool IsLocked(ScreenMode mode,string kind) => mode switch
    {
        ScreenMode.Media=>kind is "Source" or "Track" or "Artist" or "Progress",
        ScreenMode.Game=>kind is "GameLabel" or "GameTitle",
        _=>false
    };

    public static bool IsAllowed(ScreenMode mode,string kind) => kind switch
    {
        "Source" or "Track" or "Artist" or "Progress" or "MediaClock" =>
            mode==ScreenMode.Media,
        "GameLabel" or "GameTitle"=>mode==ScreenMode.Game,
        "Clock" or "Date" or "Custom text" or
        "Mouse" or "Phone" or "DualSense" or "Headset" or "SpareBattery" or "Laptop" or
        "BatterySlot1" or "BatterySlot2" or "BatterySlot3"=>true,
        _=>false
    };

    public static void Sanitize(LayoutState layout,ScreenMode mode,bool protectHeader)
    {
        if(layout.Pixels?.Length!=8192)layout.Pixels=new bool[8192];
        layout.Widgets??=[];
        var mandatorySeen=new HashSet<string>();
        layout.Widgets=layout.Widgets.Take(80)
            .Where(w=>w!=null && IsAllowed(mode,w.Kind) &&
                (!IsLocked(mode,w.Kind)||mandatorySeen.Add(w.Kind)))
            .ToList();
        foreach(var w in layout.Widgets)
        {
            w.X=Math.Clamp(w.X,0,127);
            w.Y=Math.Clamp(w.Y,0,63);
            w.StrokeWidth=Math.Clamp(w.StrokeWidth,1,3);
            w.FontSize=Math.Clamp(w.FontSize,7,24);
            w.Text=(w.Text??"")[..Math.Min(w.Text?.Length??0,160)];
        }
        foreach(string required in mode switch
            {
                ScreenMode.Media=>new[]{"Source","Track","Artist","Progress"},
                ScreenMode.Game=>new[]{"GameLabel","GameTitle"},
                _=>Array.Empty<string>()
            })
        {
            if(layout.Widgets.All(w=>w.Kind!=required))
            {
                var example=Defaults(mode).Widgets.First(w=>w.Kind==required);
                layout.Widgets.Add(example);
            }
        }
        // Do not delete header artwork when protection is enabled:
        // the renderer simply masks it, so disabling protection restores it.
        foreach(var w in layout.Widgets)
            if(IsLocked(mode,w.Kind))w.Visible=true;
    }

    public static ScreenMode Choose(bool gameRunning,bool mediaPlaying) =>
        gameRunning?ScreenMode.Game:mediaPlaying?ScreenMode.Media:ScreenMode.Idle;
}

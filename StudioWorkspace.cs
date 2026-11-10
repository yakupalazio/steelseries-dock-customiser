using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

internal enum DockMode { Media, Game, Idle }

internal sealed class StudioWorkspace
{
    public LayoutState Media {get;set;}=MakeMedia();
    public LayoutState Game {get;set;}=MakeGame();
    public LayoutState Idle {get;set;}=MakeIdle();
    public bool PreserveFactoryHeader {get;set;}=true;

    public LayoutState For(DockMode m)=>m switch {
        DockMode.Media=>Media,DockMode.Game=>Game,_=>Idle
    };
    public void Set(DockMode m,LayoutState state)
    {
        switch(m) { case DockMode.Media:Media=state;break;
            case DockMode.Game:Game=state;break;
            default:Idle=state;break; }
    }

    public static string FilePath=>Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","studio-v098.json");

    public static StudioWorkspace Create()=>new();

    public static StudioWorkspace Load()
    {
        try
        {
            var ws=JsonSerializer.Deserialize<StudioWorkspace>(File.ReadAllText(FilePath));
            if(ws is not null) {ws.Validate();return ws;}
        }catch{}
        // Non-destructive import of older v0.9.7 single-layout JSON.
        var created=Create();
        string old=Path.Combine(Path.GetDirectoryName(FilePath)!,"studio-v05.json");
        try {
            var legacy=JsonSerializer.Deserialize<LayoutState>(File.ReadAllText(old));
            if(legacy is { Pixels.Length:8192 } && legacy.Widgets.Count<=100)
            {
                foreach(var widget in legacy.Widgets)
                {
                    var dst=widget.Kind switch {
                        "Source" or "Track" or "Artist" or "Progress"=>created.Media,
                        "Clock" or "Date" or "Custom text"=>created.Idle,
                        _=>null
                    };
                    if(dst is null)continue;
                    if(dst.Widgets.Any(x=>x.Kind==widget.Kind)) {
                        var pos=dst.Widgets.FindIndex(x=>x.Kind==widget.Kind);
                        dst.Widgets[pos]=widget;
                    }
                    else dst.Widgets.Add(widget);
                }
                created.Idle.Pixels=legacy.Pixels.ToArray();
            }
        }catch{}
        return created;
    }

    public void Save()
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath,JsonSerializer.Serialize(this,
            new JsonSerializerOptions{WriteIndented=true}));
    }

    public void Validate()
    {
        foreach(var mode in Enum.GetValues<DockMode>())
        {
            var layout=For(mode);
            if(layout==null || layout.Pixels?.Length!=8192 || layout.Widgets is null
                || layout.Widgets.Count>160)
                throw new InvalidDataException("Invalid OLED layout "+mode);
            foreach(var widget in layout.Widgets)
            {
                widget.X=Math.Clamp(widget.X,0,127);
                widget.Y=Math.Clamp(widget.Y,PreserveFactoryHeader?10:0,63);
                widget.FontSize=Math.Clamp(widget.FontSize,7,24);
                widget.StrokeWidth=Math.Clamp(widget.StrokeWidth,1,3);
                widget.Text=(widget.Text??"")[..Math.Min(widget.Text?.Length??0,150)];
            }
            EnsureMandatory(mode,layout);
        }
    }

    public static bool Mandatory(DockMode mode,string kind) =>
        mode==DockMode.Media && kind is "Source" or "Track" or "Artist" or "Progress" ||
        mode==DockMode.Game && kind is "GameLabel" or "GameTitle";

    public static void EnsureMandatory(DockMode mode,LayoutState state)
    {
        var canonical=mode switch {
            DockMode.Media=>MakeMedia(),
            DockMode.Game=>MakeGame(),
            _=>MakeIdle()
        };
        foreach(var w in canonical.Widgets.Where(x=>Mandatory(mode,x.Kind)))
        {
            var matches=state.Widgets.Where(x=>x.Kind==w.Kind).ToList();
            if(matches.Count==0)state.Widgets.Insert(0,w);
            else {
                matches[0].Visible=true;
                foreach(var dup in matches.Skip(1))state.Widgets.Remove(dup);
            }
        }
    }

    public static LayoutState MakeMedia()=>new() {Widgets=[
        new(){Kind="Source",X=2,Y=18},
        new(){Kind="Track",X=20,Y=23,FontSize=9},
        new(){Kind="Artist",X=20,Y=34,FontSize=9},
        new(){Kind="Progress",X=20,Y=45},
        new(){Kind="SmallClock",X=85,Y=12,FontSize=9,StrokeWidth=1},
        new(){Kind="BatterySlot1",X=2,Y=53},
        new(){Kind="BatterySlot2",X=45,Y=53},
        new(){Kind="BatterySlot3",X=88,Y=53}
    ]};
    public static LayoutState MakeGame()=>new() {Widgets=[
        new(){Kind="GameLabel",X=5,Y=13,FontSize=9,Text="NOW PLAYING:"},
        new(){Kind="GameTitle",X=5,Y=27,FontSize=11},
        new(){Kind="BatterySlot1",X=2,Y=53},
        new(){Kind="BatterySlot2",X=45,Y=53},
        new(){Kind="BatterySlot3",X=88,Y=53}
    ]};
    public static LayoutState MakeIdle()=>new() {Widgets=[
        new(){Kind="Clock",X=35,Y=18,FontSize=20,StrokeWidth=1},
        new(){Kind="Date",X=33,Y=37,FontSize=9,StrokeWidth=1},
        new(){Kind="BatterySlot1",X=2,Y=53},
        new(){Kind="BatterySlot2",X=45,Y=53},
        new(){Kind="BatterySlot3",X=88,Y=53}
    ]};
}

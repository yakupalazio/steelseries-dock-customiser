using System;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// RC8.1 — both game and active music can coexist. Preference changes which
/// active mode wins, not the media/game layouts, user widgets or OEM control.
/// Strictly offline acceptance: no user preference files or hardware writes.
/// </summary>
internal static class ReleaseEightOneSelfTests
{
    internal static void Run()
    {
        PriorityMatrix();
        LegacyPreferenceMigration();
        SettingsUserInterface();
        Console.WriteLine("PASS: RC8.1 optional game/media priority, " +
            "legacy game-first migration, saved preference JSON, " +
            "and Turkish/English OLED settings selector.");
    }

    private static void PriorityMatrix()
    {
        foreach(string pref in new[]{"Game","Media", "", "INVALID", "media"})
        {
            bool musicWins=pref.Equals("Media",
                StringComparison.OrdinalIgnoreCase);
            if(StudioProject.Choose(true,true,pref)!=(musicWins
                   ?ScreenMode.Media:ScreenMode.Game)||
               StudioProject.Choose(true,false,pref)!=ScreenMode.Game||
               StudioProject.Choose(false,true,pref)!=ScreenMode.Media||
               StudioProject.Choose(false,false,pref)!=ScreenMode.Idle)
                throw new Exception("Simultaneous activity priority regression: "+
                    pref);
        }
        if(StudioProject.Choose(true,true)!=ScreenMode.Game)
            throw new Exception("RC8 game's default priority changed");
        var project=StudioProject.Load();
        // Choosing modes must never mutate persisted widget geometry.
        string media=JsonSerializer.Serialize(project.Media);
        string game=JsonSerializer.Serialize(project.Game);
        _=StudioProject.Choose(true,true,"Media");
        _=StudioProject.Choose(true,true,"Game");
        if(JsonSerializer.Serialize(project.Media)!=media||
           JsonSerializer.Serialize(project.Game)!=game)
            throw new Exception("Preference modified a saved screen design");
    }

    private static void LegacyPreferenceMigration()
    {
        var old=JsonSerializer.Deserialize<DisplayPreferences>(
            "{\"BurnInDrift\":true,\"ModeTransitionEnabled\":false,"+
            "\"ModeTransitionEffect\":\"Fade\",\"ModeTransitionSpeed\":75}")!;
        if(old.ConcurrentModePriority!="Game" || !old.BurnInDrift ||
           old.ModeTransitionEnabled || old.ModeTransitionEffect!="Fade"||
           old.ModeTransitionSpeed!=75)
            throw new Exception("Older display settings migrated incorrectly");
        var media=new DisplayPreferences{ConcurrentModePriority="Media",
            ModeTransitionEnabled=true,ModeTransitionSpeed=150};
        string json=JsonSerializer.Serialize(media);
        var roundtrip=JsonSerializer.Deserialize<DisplayPreferences>(json)!;
        if(roundtrip.ConcurrentModePriority!="Media"||
           roundtrip.ModeTransitionSpeed!=150)
            throw new Exception("Media priority not stored in display JSON");
        if(DisplayPreferences.NormalizeConcurrentModePriority(null)!="Game"||
           DisplayPreferences.NormalizeConcurrentModePriority("bogus")!="Game"||
           DisplayPreferences.NormalizeConcurrentModePriority("MEDIA")!="Media")
            throw new Exception("Invalid preference must fail safe");
    }

    private static void SettingsUserInterface()
    {
        bool oldLanguage=UiLanguage.English;
        try
        {
            foreach(bool english in new[]{false,true})
            {
                UiLanguage.PreviewOnly(english);
                foreach(string pref in new[]{"Game","Media"})
                {
                    using var dialog=new SettingsDialog(displayPreferences:
                        new DisplayPreferences{
                            ConcurrentModePriority=pref});
                    var controls=AllControls(dialog).ToArray();
                    var selector=controls.OfType<ComboBox>()
                        .SingleOrDefault(x=>x.Name==
                            "ConcurrentModePrioritySelector");
                    var heading=controls.OfType<Label>()
                        .SingleOrDefault(x=>x.Name==
                            "ConcurrentModePriorityTitle");
                    var hint=controls.OfType<Label>()
                        .SingleOrDefault(x=>x.Name==
                            "ConcurrentModePriorityHint");
                    if(selector is null||heading is null||hint is null||
                       selector.Items.Count!=2||
                       selector.SelectedIndex!=(pref=="Media"?1:0)||
                       heading.Text.Length<10||hint.Text.Length<15||
                       selector.DropDownStyle!=ComboBoxStyle.DropDownList)
                        throw new Exception("Priority picker missing or misconfigured");
                    if(selector.Bounds.Right>dialog.ClientSize.Width-205||
                       hint.Bounds.Bottom>=427)
                        throw new Exception("Priority selection overlaps transitions");
                    if(english&&!selector.Items[0]!.ToString()!
                        .Contains("game",StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Priority picker is not English");
                    if(!english&&!selector.Items[0]!.ToString()!
                        .Contains("Oyun",StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Priority picker is not Turkish");
                }
            }
        }
        finally{UiLanguage.PreviewOnly(oldLanguage);}
    }

    private static System.Collections.Generic.IEnumerable<Control>
        AllControls(Control parent)
    {
        foreach(Control child in parent.Controls)
        {
            yield return child;
            foreach(var nested in AllControls(child))yield return nested;
        }
    }
}

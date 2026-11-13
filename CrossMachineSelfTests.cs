using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Second-computer failure regression: no personal settings, internet, GG
/// calls, Bluetooth pairing, HID writes or hardware side-effects.
/// </summary>
internal static class CrossMachineSelfTests
{
    internal static void Run()
    {
        var brandNew=new BatteryPreferences();
        if(brandNew.AutoMinimizePhoneLink ||
           brandNew.UseAdbPhoneBattery ||
           !brandNew.PriorityKinds.SequenceEqual(["Headset","SpareBattery"]))
            throw new Exception("Clean installation defaults are not private/neutral.");
        var preset=FactoryPresetDefaults.LoadStudio()
            ??throw new Exception("Missing neutral embedded layout.");
        foreach(var mode in Enum.GetValues<ScreenMode>())
        {
            var all=preset.For(mode).Widgets;
            if(all.Count(w=>w.Kind.StartsWith("BatterySlot"))!=2||
               !all.Any(w=>w.Kind=="BatterySlot1")||
               !all.Any(w=>w.Kind=="BatterySlot2")||
               all.Any(w=>w.Kind=="BatterySlot3"))
                throw new Exception("Portable starter layout has personal devices.");
        }
        // The new role preset is headset + spare only, but a verified third
        // brand's GATT/HID reader can fill a free third display slot.
        var at=DateTimeOffset.Now;
        var live=new BatterySnapshot{
            Values=new Dictionary<string,BatteryReading>{
                ["Headset"]=new("Headset",75,"GG","",at),
                ["SpareBattery"]=new("SpareBattery",99,"GG","",at),
                ["Hid:third-model"]=new("Hid:third-model",41,
                    "HID Battery Strength","",at)
                    {FriendlyName="Other-brand gaming mouse"}
            }
        };
        if(!BatteryPriority.SelectThree(live,brandNew)
            .SequenceEqual(["Headset","SpareBattery","Hid:third-model"]))
            throw new Exception("Other-brand verified battery does not auto-appear.");

        using(var doc=JsonDocument.Parse(
            """{"loadoutConfiguration":{"statuses":{"batteryLevels":{"type":"primary_and_backup_battery","primaryStatusKnown":true,"percent":62,"backupStatusKnown":true,"backupPercent":81}}}}"""))
        {
            if(!GgBatteryProvider.TryExtractBattery(doc.RootElement,out var battery)||
               !GgBatteryProvider.TryParseBattery(
                   "arctis_nova_pro_wireless_tx",battery,at,out var headset)||
               !GgBatteryProvider.TryParseSpareBattery(
                   "arctis_nova_pro_wireless_tx",battery,at,out var spare)||
               headset.Percent!=62||spare.Percent!=81)
                throw new Exception("GG classic Nova Pro dual-battery state failed.");
        }
        // Newer GG formats may expose live statuses on the device itself
        // rather than through an active loadout ID; type may be omitted
        // but explicitly verified primary/spare status is mandatory.
        using(var doc=JsonDocument.Parse(
            """{"statuses":{"batteryLevels":{"primaryStatusKnown":true,"percent":55,"backupStatusKnown":true,"backupPercent":91}}}"""))
        {
            if(!GgBatteryProvider.TryExtractBattery(doc.RootElement,out var battery)||
               !GgBatteryProvider.TryParseBattery(
                   "arctis_nova_pro_wireless_tx_x",battery,at,out var headset)||
               !GgBatteryProvider.TryParseSpareBattery(
                   "arctis_nova_pro_wireless_tx_x",battery,at,out var spare)||
               headset.Percent!=55||spare.Percent!=91)
                throw new Exception("GG alternate Nova Pro status format failed.");
        }
        using(var doc=JsonDocument.Parse(
            """{"statuses":{"batteryLevels":{"type":"primary_and_backup_battery","primaryStatusKnown":false,"percent":88,"backupStatusKnown":false,"backupPercent":100}}}"""))
        {
            if(!GgBatteryProvider.TryExtractBattery(doc.RootElement,out var battery)||
               GgBatteryProvider.TryParseBattery(
                   "arctis_nova_pro_wireless_tx",battery,at,out _)||
               GgBatteryProvider.TryParseSpareBattery(
                   "arctis_nova_pro_wireless_tx",battery,at,out _))
                throw new Exception("Unverified GG battery percentage accepted.");
        }
        // The mock metadata below is synthetic, NOT copyrighted song text.
        var song=new MediaSnapshot(true,true,"Synthetic Song","Band, Guest",
            "Spotify",TimeSpan.Zero,TimeSpan.FromSeconds(201),at);
        using(var doc=JsonDocument.Parse(
            """{"trackName":"Synthetic Song (2019 Remastered)","artistName":"Band","duration":200,"syncedLyrics":"[00:00.00] TEST ONE\n[00:01.00] TEST TWO"}"""))
        {
            if(!SyncedLyricsProvider.TrySelectTrack(doc.RootElement,song,
                   out var lines)||lines.Count!=2)
                throw new Exception("LRCLIB search metadata matching failed.");
        }
        if(SyncedLyricsProvider.TitlesMatch("Different Track","Synthetic Song")||
           SyncedLyricsProvider.ArtistsMatch("Wrong Band","Band, Guest"))
            throw new Exception("Lyrics matching accepted wrong song.");

        var left=Enumerable.Repeat(.35f,12).ToArray();
        var right=Enumerable.Repeat(.45f,12).ToArray();
        foreach(var style in new[]{"Bubbles","Pulse"})
        {
            var frame=new bool[128*64];
            OledRenderer.DrawReactiveParticles(frame,style,22,18,48,30,
                left,right,1,2.45);
            if(!frame.Any(x=>x))
                throw new Exception(style+" audio visualization is empty.");
            for(int y=0;y<64;y++)for(int x=0;x<128;x++)
                if((x<22||x>=70||y<18||y>=48)&&frame[y*128+x])
                    throw new Exception(style+" visualizer escaped its widget.");
        }
        var quiet=new bool[128*64];
        OledRenderer.DrawReactiveParticles(quiet,"Bubbles",22,18,48,30,
            new float[12],new float[12],1,6.4);
        if(quiet.Any(x=>x))
            throw new Exception("Audio visualizer invented music in silence.");
        Console.WriteLine(
            "PASS: neutral two-device setup, other-brand verified battery, "+
            "Nova Pro GG dual batteries + alternate status, LRCLIB metadata "+
            "fallback, clipped Bubbles/Pulse, Phone Link stays opt-in.");
    }
}

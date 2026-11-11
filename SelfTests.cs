using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

internal static class SelfTests
{
    private static System.Collections.Generic.IEnumerable<System.Windows.Forms.Control>
        AllChildren(System.Windows.Forms.Control parent)
    {
        foreach(System.Windows.Forms.Control child in parent.Controls)
        {
            yield return child;
            foreach(var nested in AllChildren(child)) yield return nested;
        }
    }

    public static int Run(string? exportDemo)
    {
        try
        {
            // DualSense firmware reports *bands*, not a precise percentage.
            var dualSensePacket=new byte[78];
            dualSensePacket[0]=0x31; dualSensePacket[54]=0x06;
            if(DualSenseReadOnly.DecodeReport(dualSensePacket,dualSensePacket.Length)!=60)
                throw new Exception("Read-only DualSense decoder failed");
            dualSensePacket[0]=0xFF;
            if(DualSenseReadOnly.DecodeReport(dualSensePacket,dualSensePacket.Length)!=null)
                throw new Exception("Unsupported DualSense report was accepted");
            // GG's status JSON must be interpreted strictly, with no guessed readings.
            if(!GgBatteryProvider.TryEngineUri("127.0.0.1:49763",out _) ||
                GgBatteryProvider.TryEngineUri("example.com:443",out _) ||
                GgBatteryProvider.TryEngineUri("127.0.0.1:0",out _))
                throw new Exception("SteelSeries GG loopback validation failed");
            using(var mouseJson=JsonDocument.Parse("{\"type\":\"single_battery\",\"statusKnown\":true,\"percent\":73,\"charging\":false}"))
            {
                if(!GgBatteryProvider.TryParseBattery("aerox_3_wireless_gen2",mouseJson.RootElement,
                    DateTimeOffset.Now,out var item) || item.Kind!="Mouse" || item.Percent!=73)
                    throw new Exception("GG mouse battery parser failed");
            }
            using(var headsetJson=JsonDocument.Parse("{\"type\":\"primary_and_backup_battery\",\"primaryStatusKnown\":true,\"percent\":99,\"charging\":false,\"backupPercent\":82,\"backupCharging\":true}"))
            {
                if(!GgBatteryProvider.TryParseBattery("arctis_nova_pro_wireless_tx",headsetJson.RootElement,
                    DateTimeOffset.Now,out var item) || item.Kind!="Headset" || item.Percent!=99 ||
                    !item.Detail.Contains("82%"))
                    throw new Exception("GG dual-battery parser failed");
            }
            using(var unknownJson=JsonDocument.Parse("{\"type\":\"single_battery\",\"statusKnown\":false,\"percent\":100}"))
            {
                if(GgBatteryProvider.TryParseBattery("aerox_3_wireless_gen2",unknownJson.RootElement,
                    DateTimeOffset.Now,out _))
                    throw new Exception("GG unknown status was incorrectly treated as live");
            }
            // Phone Link exposes a localized accessible battery status group.
            if(!PhoneLinkBatteryProvider.TryParseAccessibleBattery(
                "Piliniz şu anda %54",out int currentPhone,out _)||currentPhone!=54)
                throw new Exception("Phone Link current Turkish battery label failed");
            if(!PhoneLinkBatteryProvider.TryParseAccessibleBattery(
                "Cihazınız şarj oluyor ve pil şu anda %84 düzeyinde",
                out int trPercent, out bool? trCharging) ||
                trPercent!=84 || trCharging!=true)
                throw new Exception("Phone Link Turkish accessibility parser failed");
            if(!PhoneLinkBatteryProvider.TryParseAccessibleBattery(
                "Your phone battery level is 47% and not charging",
                out int enPercent, out bool? enCharging) ||
                enPercent!=47 || enCharging!=false)
                throw new Exception("Phone Link English accessibility parser failed");
            if(PhoneLinkBatteryProvider.TryParseAccessibleBattery(
                "Pil düzeyi düşük olduğunda bildirimleri göster",out _,out _) ||
                PhoneLinkBatteryProvider.TryParseAccessibleBattery(
                "Phone battery level: 140%",out _,out _) ||
                PhoneLinkBatteryProvider.TryParseAccessibleBattery(
                "Mesaj: %100 tamamlandı",out _,out _))
                throw new Exception("Phone Link parser accepted non-battery or invalid data");
            // Continuous OLED marquee must advance by real pixels each second.
            if(OledRenderer.MarqueeOffset(210,80,0)!=0 ||
                OledRenderer.MarqueeOffset(210,80,1)!=3 ||
                OledRenderer.MarqueeOffset(210,80,2)!=6 ||
                OledRenderer.MarqueeOffset(18,80,10)!=0)
                throw new Exception("Per-second pixel marquee timing failed");
            // Phone ADB provider parses only system battery output, not demo percentages.
            string simulatedAdb="Current Battery Service state:\n"+
                "  AC powered: false\n  USB powered: false\n"+
                "  Wireless powered: false\n  status: 2\n"+
                "  health: 2\n  level: 84\n  scale: 100\n";
            if(!AdbPhoneBatteryProvider.TryParse(simulatedAdb,DateTimeOffset.Now,
                    out var phoneAdb) ||
                phoneAdb.Percent!=84 || phoneAdb.Kind!="Phone" ||
                !phoneAdb.Detail.Contains("Şarj oluyor"))
                throw new Exception("Read-only Android ADB battery parser failed");
            if(AdbPhoneBatteryProvider.TryParse("level: 999\nscale: 100",
                    DateTimeOffset.Now,out _) ||
                AdbPhoneBatteryProvider.TryParse("some other status: 80",
                    DateTimeOffset.Now,out _))
                throw new Exception("ADB parser accepted invalid/non-battery data");
            // v0.9.8: independent layout modes and fixed functional widgets.
            if(StudioProject.Choose(true,true)!=ScreenMode.Game ||
               StudioProject.Choose(false,true)!=ScreenMode.Media ||
               StudioProject.Choose(false,false)!=ScreenMode.Idle)
                throw new Exception("Game > media > idle priority failed");
            foreach(var mode in Enum.GetValues<ScreenMode>())
            {
                var state=StudioProject.Defaults(mode);
                StudioProject.Sanitize(state,mode,true);
                foreach(var required in mode switch
                {
                    ScreenMode.Media=>new[]{"Source","Track","Artist","Progress"},
                    ScreenMode.Game=>new[]{"GameLabel","GameTitle"},
                    _=>Array.Empty<string>()
                })
                {
                    if(state.Widgets.Count(w=>w.Kind==required)!=1)
                        throw new Exception("Required "+mode+" widget missing: "+required);
                }
                if(state.Widgets.Any(w=>!StudioProject.IsAllowed(mode,w.Kind)))
                    throw new Exception("Forbidden widget slipped into "+mode);
                if(mode!=ScreenMode.Media)
                {
                    var wheel=state.Widgets.SingleOrDefault(w=>w.Kind=="VolumeChatMix");
                    if(wheel?.DialStyle!="Wheel" || wheel.Y<10 ||
                       OledRenderer.GetBounds(wheel).Bottom>53)
                        throw new Exception("OEM-style wheel missing or clashes with battery row");
                }
                state.Widgets.Clear();
                StudioProject.Sanitize(state,mode,true);
                if(state.Widgets.Count!=0 || state.Pixels.Any(x=>x))
                    throw new Exception("A blank profile re-created default widgets");
            }
            var profileLibrary=new LayoutProfileLibrary();
            var blankProfile=profileLibrary.Add("Blank Game",ScreenMode.Game,
                new LayoutState());
            var copyProfile=profileLibrary.Add("Idle copy",ScreenMode.Idle,
                StudioProject.Defaults(ScreenMode.Idle));
            var serializedProfiles=System.Text.Json.JsonSerializer.Deserialize<LayoutProfileLibrary>(
                System.Text.Json.JsonSerializer.Serialize(profileLibrary));
            if(serializedProfiles?.Profiles.Count!=2 ||
               blankProfile.Layout.Widgets.Count!=0 ||
               copyProfile.Layout.Widgets.Count==0)
                throw new Exception("Local profile library roundtrip failed");
            using(var unshifted=new Bitmap(128,64))
            {
                unshifted.SetPixel(44,42,Color.White);
                unshifted.SetPixel(17,7,Color.White);
                using var moved=DisplayBurnIn.Apply(unshifted,true,true,
                    DateTimeOffset.FromUnixTimeSeconds(45));
                if(moved.GetPixel(45,42).R!=255 ||
                   moved.GetPixel(44,42).R!=0 ||
                   moved.GetPixel(17,7).R!=255)
                    throw new Exception("Optional OLED burn-in drift damaged pixels");
            }
            var systemLayout=new LayoutState{Widgets=[
                new DockWidget{Kind="SystemCpu",X=0,Y=10},
                new DockWidget{Kind="SystemRam",X=0,Y=20},
                new DockWidget{Kind="NetworkPing",X=0,Y=30}]};
            using(var known=OledRenderer.Render(systemLayout,false,true,false,
                telemetry:new SystemTelemetrySnapshot(62,79,25,DateTimeOffset.UtcNow)))
            using(var unknown=OledRenderer.Render(systemLayout,false,true,false))
            {
                int changed=0;
                for(int y=10;y<38;y++)for(int x=0;x<90;x++)
                    if(known.GetPixel(x,y).ToArgb()!=unknown.GetPixel(x,y).ToArgb())changed++;
                if(changed<15)throw new Exception("System CPU/RAM/Ping widget regression");
            }
            var legacyLayouts=new StudioProject
            {
                RadialDefaultsMigrated=false,
                Game=new LayoutState{Widgets=[
                    new DockWidget{Kind="GameLabel",X=3,Y=15,Text="NOW PLAYING:"},
                    new DockWidget{Kind="GameTitle",X=3,Y=30},
                    new DockWidget{Kind="Clock",X=92,Y=11,FontSize=7}
                ]},
                Idle=new LayoutState{Widgets=[
                    new DockWidget{Kind="VolumeChatMix",X=0,Y=21},
                    new DockWidget{Kind="Clock",X=72,Y=20,FontSize=20},
                    new DockWidget{Kind="Date",X=62,Y=38}
                ]}
            };
            legacyLayouts.MigrateRadialDefaults();
            if(legacyLayouts.Game.Widgets.Any(w=>w.Kind=="Clock" && w.X==92) ||
               legacyLayouts.Game.Widgets.All(w=>w.Kind!="VolumeChatMix"||w.DialStyle!="Wheel") ||
               legacyLayouts.Idle.Widgets.First(w=>w.Kind=="VolumeChatMix").DialStyle!="Wheel" ||
               legacyLayouts.Idle.Widgets.First(w=>w.Kind=="Date").Y!=45)
                throw new Exception("Legacy layouts were not repacked for radial defaults");
            legacyLayouts.Game.Widgets.RemoveAll(w=>w.Kind=="VolumeChatMix");
            legacyLayouts.MigrateRadialDefaults();
            if(legacyLayouts.Game.Widgets.Any(w=>w.Kind=="VolumeChatMix"))
                throw new Exception("Removed default dial was re-added after opt-out");
            bool originalLanguage=UiLanguage.English;
            try
            {
                UiLanguage.PreviewOnly(true);
                if(UiLanguage.T("Dairesel tekerlek")!="Radial wheel" ||
                   UiLanguage.T("Tekerleği piksel piksel düzenle")!="Edit wheel pixels" ||
                   UiLanguage.T("Düz çubuğa geç")!="Switch to flat bar" ||
                   UiLanguage.T("◉   Dil / Language")!="◉   Language" ||
                   UiLanguage.T("Tekerlek bekleniyor")!="Waiting for wheel input" ||
                   BatteryPriority.DisplayName("Headset")!="Nova Pro Headset")
                    throw new Exception("English UI translations are incomplete");
                UiLanguage.PreviewOnly(false);
                if(UiLanguage.T("Dairesel tekerlek")!="Dairesel tekerlek")
                    throw new Exception("Turkish UI fallback changed native labels");
            }
            finally{UiLanguage.PreviewOnly(originalLanguage);}
            // Nova Pro Wireless dial reports are decoded from real-looking
            // bytes in memory, without opening or writing a HID device.
            if(!ArctisDialMonitor.TryDecode(new byte[]{7,0x25,0},out var maximum)||
                maximum.Volume!=100||maximum.View!=DialView.Volume ||
                maximum.VolumeAttenuationSteps!=0 ||
               !ArctisDialMonitor.TryDecode(new byte[]{0,7,0x25,56},out var minimum)||
                minimum.Volume!=0||minimum.VolumeAttenuationSteps!=56 ||
               !ArctisDialMonitor.TryDecode(new byte[]{7,0x25,50},out var low)||
                low.VolumeAttenuationSteps!=50 ||
               !ArctisDialMonitor.TryDecode(new byte[]{7,0x45,75,25},out var mixed)||
                mixed.View!=DialView.ChatMix||mixed.Game!=75||mixed.Chat!=25 ||
               ArctisDialMonitor.TryDecode(new byte[]{7,0x25,99},out _)||
               ArctisDialMonitor.TryDecode(new byte[]{7,0x45,99,155},out _))
                throw new Exception("Nova Pro dial HID decoder regression");
            var dialLayout=new LayoutState{
                Widgets=[new DockWidget{Kind="VolumeChatMix",X=5,Y=21,MiniWidth=105}]
            };
            using(var unknownDial=OledRenderer.Render(dialLayout,false,true,false,
                layoutMode:ScreenMode.Idle))
            using(var knownDial=OledRenderer.Render(dialLayout,false,true,false,
                layoutMode:ScreenMode.Idle,
                dial:new DialSnapshot(DialView.Volume,75,null,null,
                    DateTimeOffset.UtcNow,"test")))
            {
                int different=0;
                for(int y=21;y<46;y++)
                for(int x=5;x<110;x++)
                    if(unknownDial.GetPixel(x,y).ToArgb()!=
                       knownDial.GetPixel(x,y).ToArgb())different++;
                if(different<5)
                    throw new Exception("Optional volume widget failed to render live data");
            }
            foreach(var mode in Enum.GetValues<ScreenMode>())
                if(!StudioProject.IsAllowed(mode,"VolumeChatMix"))
                    throw new Exception("Dial widget unavailable in "+mode);
            var radialDesign=StudioProject.Defaults(ScreenMode.Idle);
            var wheelWidget=radialDesign.Widgets.Single(w=>w.Kind=="VolumeChatMix");
            if(OledRenderer.GetBounds(wheelWidget).Width!=WheelArtwork.Width ||
               !WheelArtwork.IsValid(WheelArtwork.Factory))
                throw new Exception("Original-style wheel sprite has invalid geometry");
            // An empty gauge still has visible track borders, and its fill
            // rotates alongside those borders while VOL/MIX remains fixed.
            var ringEmpty=new bool[128*64];var maskEmpty=new bool[128*64];
            var ringFilled=new bool[128*64];var maskFilled=new bool[128*64];
            var ringTurned=new bool[128*64];var maskTurned=new bool[128*64];
            WheelArtwork.Draw(ringEmpty,maskEmpty,0,0,null,0,DialView.Volume);
            WheelArtwork.Draw(ringFilled,maskFilled,0,0,null,100,DialView.Volume);
            WheelArtwork.Draw(ringTurned,maskTurned,0,0,null,100,DialView.Volume,
                gapAngle:90);
            if(!ringEmpty[1*128+22] || ringEmpty[3*128+22] ||
               !ringFilled[3*128+22])
                throw new Exception("Native-style wheel must retain an empty outlined track");
            int fillRotationChanged=0;
            for(int yy=0;yy<WheelArtwork.Height;yy++)
            for(int xx=0;xx<WheelArtwork.Width;xx++)
            {
                if(ringFilled[yy*128+xx]!=ringTurned[yy*128+xx])
                    fillRotationChanged++;
                if(xx>=17&&xx<=28&&yy>=18&&yy<=24&&
                   ringFilled[yy*128+xx]!=ringTurned[yy*128+xx])
                    throw new Exception("The ring rotated its centre lettering");
            }
            if(fillRotationChanged<30 ||
               WheelArtwork.Sample(WheelArtwork.Factory,4,21,90)==
               WheelArtwork.Sample(WheelArtwork.Factory,4,21,0))
                throw new Exception("Rotating the wheel did not move its fill track");
            // Physical native ChatMix sends independent 0..100 channel gains.
            // G=100,C=100 is neutral, not G=50,C=50.
            static int? MixAt(int game,int chat)=>WheelArtwork.Value(
                new DialSnapshot(DialView.ChatMix,null,game,chat,
                    DateTimeOffset.UtcNow,"test"),true);
            if(MixAt(100,100)!=50 || MixAt(100,50)!=75 ||
               MixAt(100,0)!=100 || MixAt(50,100)!=25 ||
               MixAt(0,100)!=0 || MixAt(0,0)!=50)
                throw new Exception("Hardware ChatMix midpoint/edge mapping is incorrect");
            foreach(string key in new[]{"Game","Chat","Charging"})
            {
                if(!IconLibrary.Names.Contains(key))
                    throw new Exception("Icon Workshop is missing "+key);
                var rows=IconLibrary.GetCurrent(key);
                if(rows.Length<4||rows[0].Length<4||
                   rows.Any(row=>row.Length!=rows[0].Length ||
                       row.Any(pixel=>pixel!='0'&&pixel!='1')))
                    throw new Exception("Invalid editable OLED icon: "+key);
            }
            var transientLow=new bool[128*64];var maskLow=new bool[128*64];
            var transientMax=new bool[128*64];var maskMax=new bool[128*64];
            WheelArtwork.Draw(transientLow,maskLow,0,0,null,10,DialView.Volume,
                attenuationSteps:50,transientAttenuation:true);
            WheelArtwork.Draw(transientMax,maskMax,0,0,null,100,DialView.Volume,
                attenuationSteps:0,transientAttenuation:true);
            int transientDifference=0,blankDifference=0;
            for(int yy=19;yy<=24;yy++)for(int xx=16;xx<=29;xx++)
            {
                if(transientLow[yy*128+xx]!=ringFilled[yy*128+xx])
                    transientDifference++;
                if(transientMax[yy*128+xx]!=ringFilled[yy*128+xx])
                    blankDifference++;
            }
            if(transientDifference<4||blankDifference<4)
                throw new Exception("Negative step/blank-at-maximum timeout badges failed");
            var recentVolume=new DialSnapshot(DialView.Volume,11,null,null,
                DateTimeOffset.UtcNow,"test"){VolumeAttenuationSteps=50};
            var settledVolume=recentVolume with{
                UpdatedAt=DateTimeOffset.UtcNow.AddSeconds(-3)};
            using(var latest=OledRenderer.Render(radialDesign,false,true,false,
                layoutMode:ScreenMode.Idle,dial:recentVolume))
            using(var settled=OledRenderer.Render(radialDesign,false,true,false,
                layoutMode:ScreenMode.Idle,dial:settledVolume))
            {
                int changedBadge=0;
                for(int yy=29;yy<=35;yy++)for(int xx=19;xx<32;xx++)
                    if(latest.GetPixel(xx,yy).ToArgb()!=
                       settled.GetPixel(xx,yy).ToArgb())changedBadge++;
                if(changedBadge<3)
                    throw new Exception("After two seconds the volume step must return to VOL");
            }
            using(var radialZero=OledRenderer.Render(radialDesign,false,true,false,
                layoutMode:ScreenMode.Idle,
                dial:new DialSnapshot(DialView.Volume,0,null,null,
                    DateTimeOffset.UtcNow,"test")))
            using(var radialFull=OledRenderer.Render(radialDesign,false,true,false,
                layoutMode:ScreenMode.Idle,
                dial:new DialSnapshot(DialView.Volume,100,null,null,
                    DateTimeOffset.UtcNow,"test")))
            using(var radialMix=OledRenderer.Render(radialDesign,false,true,false,
                layoutMode:ScreenMode.Idle,
                dial:new DialSnapshot(DialView.ChatMix,null,75,25,
                    DateTimeOffset.UtcNow,"test")))
            {
                int changed=0,mixedChanged=0;
                for(int y=10;y<53;y++)for(int x=2;x<78;x++)
                {
                    if(radialZero.GetPixel(x,y).ToArgb()!=radialFull.GetPixel(x,y).ToArgb())
                        changed++;
                    if(radialMix.GetPixel(x,y).ToArgb()!=radialFull.GetPixel(x,y).ToArgb())
                        mixedChanged++;
                }
                if(changed<40 || mixedChanged<20)
                    throw new Exception("Radial VOL/ChatMix wheel did not change with HID data");
            var rotatedWheel=StudioProject.Defaults(ScreenMode.Idle);
            var rotatedWidget=rotatedWheel.Widgets.Single(w=>w.Kind=="VolumeChatMix");
            rotatedWidget.WheelGapAngle=90;
            using(var rotated=OledRenderer.Render(rotatedWheel,false,true,false,
                layoutMode:ScreenMode.Idle,
                dial:new DialSnapshot(DialView.Volume,100,null,null,
                    DateTimeOffset.UtcNow,"test")))
            {
                int moved=0;
                for(int y=11;y<53;y++)for(int x=2;x<47;x++)
                    if(radialFull.GetPixel(x,y).ToArgb()!=rotated.GetPixel(x,y).ToArgb())moved++;
                if(moved<10)throw new Exception("Wheel gap angle did not rotate the ring");
                // Rotating the outside must not turn the centre lettering.
                for(int y=29;y<37;y++)for(int x=17;x<32;x++)
                    if(radialFull.GetPixel(x,y).ToArgb()!=rotated.GetPixel(x,y).ToArgb())
                        throw new Exception("Volume centre label rotated with the ring");
            }
            var mixNeutral=StudioProject.Defaults(ScreenMode.Idle);
            using(var centered=OledRenderer.Render(mixNeutral,false,true,false,
                layoutMode:ScreenMode.Idle,
                dial:new DialSnapshot(DialView.ChatMix,null,50,50,
                    DateTimeOffset.UtcNow,"test")))
            {
                int centeredDiff=0;
                for(int y=11;y<53;y++)for(int x=2;x<47;x++)
                    if(centered.GetPixel(x,y).ToArgb()!=radialMix.GetPixel(x,y).ToArgb())
                        centeredDiff++;
                if(centeredDiff<10)
                    throw new Exception("ChatMix should fill outward from the center");
            }
                if(exportDemo!=null)
                {
                    string folder=Path.GetDirectoryName(Path.GetFullPath(exportDemo))!;
                    Directory.CreateDirectory(folder);
                    radialFull.Save(Path.Combine(folder,"radial-volume-128x64.png"),
                        ImageFormat.Png);
                    radialMix.Save(Path.Combine(folder,"radial-chatmix-128x64.png"),
                        ImageFormat.Png);
                }
                for(int y=0;y<10;y++)for(int x=0;x<128;x++)
                    if(radialFull.GetPixel(x,y).R>0)
                        throw new Exception("Radial wheel overwrote the OEM header");
            }
            radialDesign.Widgets.Remove(wheelWidget);
            // The wheel's one-bit artwork is fully serialized per widget.
            var userPixels=WheelArtwork.Factory.ToCharArray();
            userPixels[2*WheelArtwork.Width+2]='1';
            wheelWidget.WheelPixels=new string(userPixels);
            wheelWidget.WheelAutoLabel=false;
            wheelWidget.WheelGapAngle=-105;
            var customPixels=System.Text.Json.JsonSerializer.Deserialize<DockWidget>(
                System.Text.Json.JsonSerializer.Serialize(wheelWidget));
            if(customPixels?.WheelPixels!=wheelWidget.WheelPixels ||
               customPixels.WheelAutoLabel ||
               customPixels.WheelGapAngle!=-105 ||
               !WheelArtwork.IsValid(customPixels.WheelPixels) ||
               WheelArtwork.IsValid("wrong"))
                throw new Exception("Wheel pixel artwork failed persistence check");
            using(var dateOnly=OledRenderer.Render(radialDesign,false,true,false,
                layoutMode:ScreenMode.Idle))
            using(var withWheel=OledRenderer.Render(StudioProject.Defaults(ScreenMode.Idle),
                false,true,false,layoutMode:ScreenMode.Idle))
                for(int y=44;y<52;y++)for(int x=67;x<128;x++)
                    if(dateOnly.GetPixel(x,y).ToArgb()!=withWheel.GetPixel(x,y).ToArgb())
                        throw new Exception("Radial wheel erased a date pixel");
            var trios=new BatterySnapshot{
                Values=new(System.StringComparer.OrdinalIgnoreCase)
                {
                    ["Mouse"]=new("Mouse",96,"SteelSeries GG Engine (canlı)","",DateTimeOffset.Now),
                    ["Laptop"]=new("Laptop",100,"Windows PowerStatus","",DateTimeOffset.Now),
                    ["Phone"]=new("Phone",84,"Microsoft Phone Link","",DateTimeOffset.Now)
                },
                DualSenseConnected=false
            };
            var priority=new BatteryPreferences();
            if(!BatteryPriority.SelectThree(trios,priority)
                .SequenceEqual(new[]{"Mouse","Phone","Laptop"}))
                throw new Exception("Default Mouse / Phone / Laptop selection failed");
            var dsConnected=new BatterySnapshot{
                Values=trios.Values,DualSenseConnected=true
            };
            if(!BatteryPriority.SelectThree(dsConnected,priority)
                .SequenceEqual(new[]{"Mouse","Phone","DualSense"}))
                throw new Exception("Connected DualSense did not replace Laptop");
            using(var statusJson=JsonDocument.Parse(
                """{"type":"primary_and_backup_battery","primaryStatusKnown":true,"percent":97,"backupPercent":82,"backupCharging":true}"""))
            {
                if(!GgBatteryProvider.TryParseSpareBattery("arctis_nova_pro_wireless_tx",
                    statusJson.RootElement,DateTimeOffset.Now,out var spare)||
                    spare.Percent!=82 || spare.Kind!="SpareBattery")
                    throw new Exception("Nova Pro backup battery parser failed");
            }
            if(!GgGameDetector.NamesMatchPath(
                @"C:\Riot Games\VALORANT\valorant-win64-shipping.exe",
                "c:/riotgames/valorant/live/valorant-win64-shipping.exe"))
                throw new Exception("GG normalized game executable did not match");
            // Header protection changes photo and pixel output, but never deletes
            // the underlying user art when toggled back off.
            var headerProject=StudioProject.Defaults(ScreenMode.Idle);
            headerProject.Pixels[5*128+7]=true;
            StudioProject.Sanitize(headerProject,ScreenMode.Idle,true);
            if(!headerProject.Pixels[5*128+7])
                throw new Exception("Header protection destructively erased pixels");
            using(var masked=OledRenderer.Render(headerProject,false,true,false,
                protectHeader:true,layoutMode:ScreenMode.Idle))
            using(var unmasked=OledRenderer.Render(headerProject,false,true,false,
                protectHeader:false,layoutMode:ScreenMode.Idle))
            {
                if(masked.GetPixel(7,5).R!=0||unmasked.GetPixel(7,5).R!=255)
                    throw new Exception("Header protection renderer is inconsistent");
            }
            // v0.9.9: optional game Mini Player must never force full Media mode.
            if(!StudioProject.IsAllowed(ScreenMode.Game,"GameMiniPlayer") ||
                StudioProject.IsAllowed(ScreenMode.Media,"GameMiniPlayer") ||
                StudioProject.IsAllowed(ScreenMode.Idle,"GameMiniPlayer"))
                throw new Exception("Game Mini Player is not restricted to Game mode");
            if(StudioProject.Defaults(ScreenMode.Game).Widgets
                .Any(w=>w.Kind=="GameMiniPlayer"))
                throw new Exception("Game Mini Player is optional, not installed by default");

            var compact=new DockWidget {
                Kind="GameMiniPlayer", X=7,Y=35,FontSize=7,StrokeWidth=1,
                MiniWidth=66,Visible=true
            };
            var compactLayout=new LayoutState {
                Widgets=[compact]
            };
            // A stale pixel underneath the optional widget must become black
            // when media stops, with no leftover title/icon.
            compactLayout.Pixels[37*128+32]=true;
            var miniPlayback=new MediaSnapshot(
                true,true,"ONE STEP CLOSER","LINKIN PARK","Spotify",
                TimeSpan.FromSeconds(25),TimeSpan.FromSeconds(90),
                DateTimeOffset.Now);
            using(var playingMini=OledRenderer.Render(compactLayout,false,true,
                true,null,miniPlayback,new GameSnapshot("VALORANT","VALORANT",1),
                protectHeader:true,layoutMode:ScreenMode.Game))
            using(var stoppedMini=OledRenderer.Render(compactLayout,false,true,
                true,null,MediaSnapshot.Empty,new GameSnapshot("VALORANT","VALORANT",1),
                protectHeader:true,layoutMode:ScreenMode.Game))
            {
                var bounds=OledRenderer.GetBounds(compact);
                if(bounds.Width!=66 || bounds.Height<14)
                    throw new Exception("Mini Player configurable rectangle is incorrect");
                bool playingVisible=false;
                for(int yy=bounds.Top;yy<bounds.Bottom;yy++)
                for(int xx=bounds.Left;xx<bounds.Right;xx++)
                {
                    playingVisible|=playingMini.GetPixel(xx,yy).R==255;
                    if(stoppedMini.GetPixel(xx,yy).R!=0)
                        throw new Exception("Stopped/paused Game Mini Player isn't black");
                }
                if(!playingVisible)
                    throw new Exception("Active Game Mini Player did not draw music");
                for(int yy=0;yy<64;yy++)
                for(int xx=0;xx<128;xx++)
                {
                    if(xx>=bounds.Left && xx<bounds.Right &&
                       yy>=bounds.Top && yy<bounds.Bottom)continue;
                    if(playingMini.GetPixel(xx,yy).R!=
                       stoppedMini.GetPixel(xx,yy).R)
                        throw new Exception($"Mini Player writes outside its reserved area at {xx},{yy}; bounds={bounds}; playing={playingMini.GetPixel(xx,yy).R} paused={stoppedMini.GetPixel(xx,yy).R}");
                }
                if(playingMini.GetPixel(0,0).R!=0)
                    throw new Exception("Mini Player modified factory OLED header");
            }
            // Larger stroke thickness must render differently without changing
            // the widget's left/right clipping boundaries.
            compact.StrokeWidth=3;
            using(var thickMini=OledRenderer.Render(compactLayout,false,true,
                true,null,miniPlayback,new GameSnapshot("VALORANT","VALORANT",1),
                protectHeader:true,layoutMode:ScreenMode.Game))
            {
                var bounds=OledRenderer.GetBounds(compact);
                if(thickMini.GetPixel(7,35).R!=0)
                    throw new Exception("Mini Player should not fill its whole background");
                for(int yy=bounds.Top;yy<bounds.Bottom;yy++)
                for(int xx=bounds.Right;xx<128;xx++)
                    if(thickMini.GetPixel(xx,yy).R!=0)
                        throw new Exception("Bold mini song text escaped its selected width");
            }
            var compactClone=compactLayout.DeepClone();
            if(compactClone.Widgets[0].MiniWidth!=66 ||
               compactClone.Widgets[0].StrokeWidth!=3)
                throw new Exception("Mini Player size/weight not persisted in layout JSON");
            // Restrict, but preserve width when loading an existing Game layout.
            compact.MiniWidth=900;
            StudioProject.Sanitize(compactLayout,ScreenMode.Game,true);
            if(compact.MiniWidth!=128 ||
               compactLayout.Widgets.Count(w=>w.Kind=="GameMiniPlayer")!=1)
                throw new Exception("Mini Player width was not sanitized or preserved");

            // Cached Bluetooth percentages are never live. GG/Phone Link are fetched separately.
            var scan = BatterySources.PollNow();
            foreach(var unverified in new[]{"Mouse","Phone","Headset","DualSense"})
                if(scan[unverified].Available)
                    throw new Exception("Unverified Bluetooth cache exposed as live: "+unverified);
            var layout = LayoutDefaults.Create();
            if (layout.Pixels.Length != 8192) throw new Exception("Pixel grid is not 8192 bits");
            using var media = OledRenderer.Render(layout, true, true, true);
            using var generic = OledRenderer.Render(layout, true, true, false);
            using var idle = OledRenderer.Render(layout, false, true, true);
            if(media.Width != 128 || media.Height != 64)throw new Exception("OLED resolution mismatch");
            if(generic.Width != 128 || idle.Height != 64)throw new Exception("Secondary render dimensions mismatch");

            int nonBlack=0;
            foreach(var bmp in new[] { media, generic, idle })
            {
                for (int y=0; y<bmp.Height; y++)
                for (int x=0; x<bmp.Width; x++)
                {
                    var v=bmp.GetPixel(x,y);
                    if(v.R != 0 && v.R != 255)throw new Exception("Non-monochrome pixel detected");
                    if(v.R != v.G || v.G != v.B)throw new Exception("Non-grayscale pixel detected");
                    if(v.R==255)nonBlack++;
                }
            }
            if(nonBlack<100)throw new Exception("Frames unexpectedly blank");
            bool sourceDiff=false,modeDiff=false;
            for(int y=12;y<64;y++) for(int x=0;x<128;x++)
            {
                if(media.GetPixel(x,y).ToArgb()!=generic.GetPixel(x,y).ToArgb())sourceDiff=true;
                if(media.GetPixel(x,y).ToArgb()!=idle.GetPixel(x,y).ToArgb())modeDiff=true;
            }
            if(!sourceDiff)throw new Exception("Spotify / generic source switching has no effect");
            if(!modeDiff)throw new Exception("Media / clock mode switching has no effect");
            if(OledRenderer.ReservedTop!=10 || DockHardwareTest.EditableY!=10 || DockHardwareTest.MainY!=10)
                throw new Exception("The factory indicator strip must reserve precisely 10 rows");
            if(!MediaSnapshot.IsSpotifyApp("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify") ||
                MediaSnapshot.IsSpotifyApp("GoogleChromePlaying"))
                throw new Exception("Spotify source identification failed");
            var playback=new MediaSnapshot(true,true,"My Generation","Limp Bizkit",
                "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify",TimeSpan.FromSeconds(72),
                TimeSpan.FromSeconds(180),DateTimeOffset.Now);
            if(playback.ProgressPercent is <39 or >41)
                throw new Exception("Media position must map into progress percentage");
            using(var actual=OledRenderer.Render(layout,true,true,true,null,playback))
            using(var genericSession=OledRenderer.Render(layout,true,true,true,null,playback with { IsSpotify=false }))
            {
                bool changed=false;
                for(int yy=10;yy<49;yy++)for(int xx=0;xx<127;xx++)
                    if(actual.GetPixel(xx,yy).ToArgb()!=media.GetPixel(xx,yy).ToArgb())changed=true;
                if(!changed)throw new Exception("Real song metadata does not replace demo text");
                // Source logo must change when the active source is not Spotify.
                if(actual.GetPixel(6,24).ToArgb()==genericSession.GetPixel(6,24).ToArgb())
                {
                    bool sourceChanged=false;
                    for(int yy=18;yy<33;yy++)for(int xx=3;xx<17;xx++)
                        sourceChanged|=actual.GetPixel(xx,yy).ToArgb()!=genericSession.GetPixel(xx,yy).ToArgb();
                    if(!sourceChanged)throw new Exception("The real media source icon did not switch");
                }
            }
            var weightedClock=layout.DeepClone();
            weightedClock.Widgets.First(w=>w.Kind=="Clock").StrokeWidth=3;
            using(var thickClock=OledRenderer.Render(weightedClock,false,true,true))
            {
                bool changed=false;
                for(int yy=16;yy<36;yy++)for(int xx=33;xx<110;xx++)
                    changed|=idle.GetPixel(xx,yy).ToArgb()!=thickClock.GetPixel(xx,yy).ToArgb();
                if(!changed)throw new Exception("Clock 1–3 pixel stroke width had no effect");
            }
            var weightedDate=layout.DeepClone();
            weightedDate.Widgets.First(w=>w.Kind=="Date").StrokeWidth=3;
            using(var thickDate=OledRenderer.Render(weightedDate,false,true,true))
            {
                bool changed=false;
                for(int yy=37;yy<55;yy++)for(int xx=30;xx<125;xx++)
                    changed|=idle.GetPixel(xx,yy).ToArgb()!=thickDate.GetPixel(xx,yy).ToArgb();
                if(!changed)throw new Exception("Date 1–3 pixel stroke width had no effect");
            }

            var modified=layout.DeepClone();
            modified.Pixels[63*128+127]=true;
            modified.Widgets[0].X=66;
            if(layout.Pixels[63*128+127] || layout.Widgets[0].X==66)
                throw new Exception("Undo/redo history snapshot is not deep cloned");

            // User-painted pixels are blocked in all 16 reserved physical rows.
            using(var cropped=DockHardwareTest.ExtractEditableArea(media))
            using(var tail=DockHardwareTest.ExtractBottomTail(media))
            {
                if(cropped.Width!=128||cropped.Height!=48||tail.Width!=128||tail.Height!=8)
                    throw new Exception("OLED hardware patch size mismatch.");
                bool[] covered=new bool[64];
                foreach(var section in new[]{(image:cropped,physicalY:10),(image:tail,physicalY:56)})
                for(int y=0;y<section.image.Height;y++)
                {
                    int yy=section.physicalY+y;
                    if(yy<10||yy>=64)throw new Exception("Header or screen boundary modified!");
                    covered[yy]=true;
                    for(int x=0;x<128;x++)
                        if(section.image.GetPixel(x,y).ToArgb()!=media.GetPixel(x,yy).ToArgb())
                            throw new Exception("Pixel mismatch on physical row "+yy);
                }
                for(int y=0;y<64;y++)
                    if(covered[y]!=(y>=10))
                        throw new Exception("Bad OLED segment coverage at row "+y);
            }
            // User-painted pixels are blocked in the simulated firmware strip.
            modified=layout.DeepClone();
            modified.Pixels[9*128]=true;
            using(var changed=OledRenderer.Render(modified,true,true,true))
            if(changed.GetPixel(0,9).ToArgb()!=media.GetPixel(0,9).ToArgb())
                throw new Exception("Reserved factory region accepted user pixel");

            // Perspective overlay is a photo simulation only. The factory
            // indicator strip (first 10 native OLED rows) must stay untouched.
            using(var photo=new Bitmap(480,260))
            {
                using(var g=Graphics.FromImage(photo))
                    g.Clear(Color.FromArgb(33,37,42));
                var corners=new PointF[] {
                    new(80,25),new(400,32),new(392,195),new(86,187)
                };
                using var composited=DockPreviewPanel.ComposePhotograph(photo,media,corners,92);
                if(composited.Width!=480 || composited.Height!=260)
                    throw new Exception("Photo composition dimensions changed");
                if(composited.GetPixel(130,40).ToArgb()!=photo.GetPixel(130,40).ToArgb())
                    throw new Exception("Factory indicators area was overwritten");
                int changedCount=0;
                for(int y=80;y<185;y++)
                    for(int x=100;x<390;x++)
                        if(composited.GetPixel(x,y).ToArgb()!=photo.GetPixel(x,y).ToArgb())
                            changedCount++;
                if(changedCount<1000)
                    throw new Exception("Custom OLED overlay was not projected onto photograph");
                if(exportDemo!=null)
                    composited.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(exportDemo))!,
                        "dock-perspective-test.png"),ImageFormat.Png);
            }

            if(exportDemo!=null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(exportDemo))!);
                media.Save(exportDemo,ImageFormat.Png);
                using var clockFrame=OledRenderer.Render(layout,false,true,true);
                clockFrame.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(exportDemo))!,"clock-demo-128x64.png"),ImageFormat.Png);
            }
            // For a local-only optical check of the installed official photo,
            // export a preview. This image is NOT intended for GitHub distribution.
            var realPhoto=Path.Combine(AppContext.BaseDirectory,"dock-photo-local.png");
            if(exportDemo!=null && File.Exists(realPhoto))
            {
                using var photo=Image.FromFile(realPhoto);
                PointF[] corners=[new(346,301),new(589,302),new(589,420),new(346,419)];
                using var composite=DockPreviewPanel.ComposePhotograph(photo,media,corners,92);
                composite.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(exportDemo))!,
                    "real-dock-photo-preview-local.png"),ImageFormat.Png);
            }
            // v1.0 UI regression: one responsive page, photo and native pixel
            // editor visible in the same control hierarchy; no preview tab.
            using(var window=new PixelStudioForm())
            {
                if(window.Controls.OfType<System.Windows.Forms.TabControl>().Any())
                    throw new Exception("The old separate Preview tab is still visible");
                var root=window.Controls.OfType<System.Windows.Forms.TableLayoutPanel>().Single();
                if(root.ColumnCount!=3 || root.RowCount!=3 ||
                    !window.MaximizeBox || window.MinimumSize.Width<1100)
                    throw new Exception("Responsive v1.0 layout was not constructed");
                var children=AllChildren(root).ToArray();
                if(children.Count(c=>c is DockPreviewPanel)!=1 ||
                   children.Count(c=>c is System.Windows.Forms.PictureBox)==0)
                    throw new Exception("Integrated dock photo and pixel editor are missing");
                var buttons=children.OfType<System.Windows.Forms.Button>().ToArray();
                if(buttons.Any(b=>b.Text is "Fotoğraf" or "Hizala" or "10 sn test") ||
                   !buttons.Any(b=>b.Text.Contains("Uygula")||b.Text.Contains("Apply")) ||
                   !buttons.Any(b=>b.Text.Contains("Reset")) ||
                   !buttons.Any(b=>b.Text.Contains("Canlı")||b.Text.Contains("Live")))
                    throw new Exception("Studio Apply/Reset toolbar regression: "+
                        string.Join(" / ",buttons.Select(b=>b.Text)));
                if(typeof(DockHardwareTest).GetMethod("ApplyStaticAsync")==null ||
                   typeof(DockHardwareTest).GetMethod("ShowForTenSecondsAsync")!=null)
                    throw new Exception("Timed hardware test remains in the API");
            }

            // An illuminated factory OLED has a slightly grey glass shade;
            // off pixels in a custom overlay should match the darker bezel,
            // not leave a visible bright rectangle in the dock photograph.
            using(var bezelPhoto=new Bitmap(480,260))
            using(var darkOled=new Bitmap(128,64))
            {
                using(var g=Graphics.FromImage(bezelPhoto))
                {
                    g.Clear(Color.FromArgb(24,24,24));
                    using var grey=new SolidBrush(Color.FromArgb(40,40,40));
                    g.FillRectangle(grey,80,25,320,170);
                }
                PointF[] bezelCorners=[
                    new(80,25),new(400,25),new(400,195),new(80,195)
                ];
                using var corrected=DockPreviewPanel.ComposePhotograph(
                    bezelPhoto,darkOled,bezelCorners,92,protectHeader:false);
                var offPixel=corrected.GetPixel(240,110);
                if(offPixel.R>31 || offPixel.G>31 || offPixel.B>31)
                    throw new Exception("OLED glass still has a grey overlay rectangle");
                if(corrected.GetPixel(25,120).ToArgb()!=
                   bezelPhoto.GetPixel(25,120).ToArgb())
                    throw new Exception("Bezel color fix changed pixels outside OLED");
            }

            // The secret code unlocks only after all ten keys in order.
            var konami=new KonamiSequence();
            Keys[] sequence=[
                Keys.Up,Keys.Up,Keys.Down,Keys.Down,Keys.Left,
                Keys.Right,Keys.Left,Keys.Right,Keys.B,Keys.A
            ];
            for(int i=0;i<sequence.Length;i++)
                if(konami.Feed(sequence[i])!=(i==sequence.Length-1))
                    throw new Exception("Konami-code unlock regression");
            using(var options=new SettingsDialog(()=>false,
                new BatteryPreferences(),()=>new BatterySnapshot(),
                ()=>System.Threading.Tasks.Task.CompletedTask,()=>{}))
            {
                if(!AllChildren(options).OfType<System.Windows.Forms.Button>()
                    .Any(b=>b.Text.Contains("Pil ve öncelik") ||
                             b.Text.Contains("Battery & priority")))
                    throw new Exception("Battery preferences missing from Settings");
                var menuButtons=AllChildren(options).OfType<System.Windows.Forms.Button>().ToArray();
                var aboutButton=menuButtons.FirstOrDefault(b=>b.Text.Contains("Hakkında") ||
                    b.Text.Contains("About"));
                var languageButton=menuButtons.FirstOrDefault(b=>b.Text.Contains("Language"));
                if(aboutButton==null || languageButton==null ||
                   aboutButton.Top<=languageButton.Top)
                    throw new Exception("About is not the bottom Settings navigation item");
            }
            using(var about=new SettingsDialog())
            {
                if(!AllChildren(about).OfType<Label>().Any(
                    c=>c.Text=="Yakup Can Palaz"))
                    throw new Exception("About author signature is missing");

            }
            using(var game=new DoomModeForm())
            using(var preview=game.SnapshotForTest())
            {
                if(preview.Width!=128 || preview.Height!=64)
                    throw new Exception("Secret mini FPS has wrong native OLED size");
                var gameButtons=AllChildren(game).OfType<System.Windows.Forms.Button>().ToArray();
                if(gameButtons.Any(b=>b.Text.Contains("20 sn")) ||
                   !gameButtons.Any(b=>b.Text.Contains("CANLI OYNA") ||
                       b.Text.Contains("PLAY ON DOCK")))
                    throw new Exception("Timed DOOM OLED button was not removed");
                int lit=0;
                for(int y=0;y<64;y++)
                    for(int x=0;x<128;x++)
                        if(preview.GetPixel(x,y).R>127)lit++;
                if(lit<70)throw new Exception("Secret mini FPS renders a blank OLED");
                if(exportDemo!=null)
                    preview.Save(Path.Combine(
                        Path.GetDirectoryName(Path.GetFullPath(exportDemo))!,
                        "doom-mode-preview-128x64.png"),ImageFormat.Png);
            }

            // Fast OLED reports are tested entirely in memory: the HID
            // device is never opened and nothing is sent to hardware.
            var sampleTime=DateTimeOffset.UtcNow;
            if(DockPreviewPanel.ReconnectDelaySeconds(1)!=2 ||
               DockPreviewPanel.ReconnectDelaySeconds(4)!=15 ||
               DockPreviewPanel.ReconnectDelaySeconds(12)!=15)
                throw new Exception("OLED hot-reconnect backoff changed");
            if(DockPreviewPanel.NeedsReassert("same","same",sampleTime,
                    sampleTime.AddMilliseconds(40),false) ||
               !DockPreviewPanel.NeedsReassert("same","same",sampleTime,
                    sampleTime.AddMilliseconds(65),false) ||
               !DockPreviewPanel.NeedsReassert("same","same",sampleTime,
                    sampleTime.AddMilliseconds(100),true) ||
               !DockPreviewPanel.NeedsReassert("previous","changed",sampleTime,
                    sampleTime.AddMilliseconds(100),false))
                throw new Exception("Native OLED overlay reassert scheduling regression");
            using(var pattern=new Bitmap(128,64))
            {
                pattern.SetPixel(0,0,Color.White);
                pattern.SetPixel(63,7,Color.White);
                pattern.SetPixel(64,8,Color.White);
                pattern.SetPixel(127,63,Color.White);
                var first=FastOledTransport.EncodeHalf(pattern,0);
                var second=FastOledTransport.EncodeHalf(pattern,64);
                if(first.Length!=1024 || second.Length!=1024 ||
                   first[0]!=6 || second[0]!=6 ||
                   first[1]!=0x93 || second[1]!=0x93 ||
                   first[2]!=0 || second[2]!=64 ||
                   first[6]!=1 || first[6+63*8]!=128 ||
                   second[6+1]!=1 || second[6+63*8+7]!=128)
                    throw new Exception("Fast OLED feature report bit packing regression");
                pattern.SetPixel(12,10,Color.White);
                pattern.SetPixel(65,57,Color.White);
                pattern.SetPixel(127,63,Color.White);
                var topLeft=FastOledTransport.EncodePatch(pattern,0,10,48);
                var topRight=FastOledTransport.EncodePatch(pattern,64,10,48);
                var tailRight=FastOledTransport.EncodePatch(pattern,64,56,8);
                if(topLeft[3]!=10||topLeft[5]!=48||
                   topLeft[6+12*6]!=1 ||
                   topRight[6+1*6+47/8]!=(1<<(47%8)) ||
                   tailRight[3]!=56||tailRight[5]!=8||
                   tailRight[6+63]!=128)
                    throw new Exception("Protected live OLED patch encoding regression");
            }

            // Charging bolt: only a provider-confirmed true value makes pixels.
            var chargingBattery=new BatteryReading("Mouse",84,"GG","Şarj oluyor",
                DateTimeOffset.Now,true);
            var notCharging=chargingBattery with {Charging=false};
            var chargingDesign=new LayoutState{
                Widgets=[new DockWidget{Kind="BatterySlot1",X=0,Y=23}]
            };
            var chargingSnapshot=new BatterySnapshot{
                Values=new(StringComparer.OrdinalIgnoreCase){
                    ["Mouse"]=chargingBattery
                }
            };
            var unpluggedSnapshot=new BatterySnapshot{
                Values=new(StringComparer.OrdinalIgnoreCase){
                    ["Mouse"]=notCharging
                }
            };
            using(var chargeFrame=OledRenderer.Render(chargingDesign,false,true,false,
                    chargingSnapshot,layoutMode:ScreenMode.Idle))
            using(var noChargeFrame=OledRenderer.Render(chargingDesign,false,true,false,
                    unpluggedSnapshot,layoutMode:ScreenMode.Idle))
            {
                bool diff=false;
                for(int y=24;y<34;y++)
                for(int x=30;x<43;x++)
                    diff|=chargeFrame.GetPixel(x,y).R!=noChargeFrame.GetPixel(x,y).R;
                if(!diff) throw new Exception("Confirmed charging bolt failed to render");
            }
            if(new BatteryReading("Headset",91,"GG",
                "Yedek pil şarj oluyor",DateTimeOffset.Now,false).IsCharging)
                throw new Exception("Charging inferred incorrectly from spare-battery detail");

            Console.WriteLine("PASS: single-page v1.0 UI, responsive editor, native dock photo");
            Console.WriteLine("PASS: Phone Link battery parser, optional read-only dial widget");
            Console.WriteLine("PASS: charging icon appears only for confirmed charging");
            Console.WriteLine("All OLED Studio self-tests passed.");
            return 0;
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine("SELF-TEST FAILURE: "+ex);
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"selftest-error.txt"),ex.ToString());
            return 1;
        }
    }
}

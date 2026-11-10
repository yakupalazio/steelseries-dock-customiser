using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

internal static class SelfTests
{
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
            }
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
            // UI regression: there must be only Editor + Dock Preview tabs.
            using(var window=new PixelStudioForm())
            {
                var tab=window.Controls.OfType<System.Windows.Forms.TabControl>().Single();
                if(tab.TabPages.Count!=2 || tab.TabPages[1].Text!="Dock Preview")
                    throw new Exception("An obsolete Live Preview tab remains");
                if(!tab.TabPages[1].Controls.OfType<DockPreviewPanel>().Any())
                    throw new Exception("Dock Preview panel was not attached");
            }
            Console.WriteLine("PASS: OLED renderer, factory indicators, perspective overlay, exactly two tabs");
            Console.WriteLine("All preview-only self tests passed.");
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

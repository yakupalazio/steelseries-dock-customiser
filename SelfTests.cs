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
                foreach(var section in new[]{(image:cropped,physicalY:12),(image:tail,physicalY:56)})
                for(int y=0;y<section.image.Height;y++)
                {
                    int yy=section.physicalY+y;
                    if(yy<12||yy>=64)throw new Exception("Header or screen boundary modified!");
                    covered[yy]=true;
                    for(int x=0;x<128;x++)
                        if(section.image.GetPixel(x,y).ToArgb()!=media.GetPixel(x,yy).ToArgb())
                            throw new Exception("Pixel mismatch on physical row "+yy);
                }
                for(int y=0;y<64;y++)
                    if(covered[y]!=(y>=12))
                        throw new Exception("Bad OLED segment coverage at row "+y);
            }
            // User-painted pixels are blocked in the simulated firmware strip.
            modified=layout.DeepClone();
            modified.Pixels[11*128]=true;
            using(var changed=OledRenderer.Render(modified,true,true,true))
            if(changed.GetPixel(0,11).ToArgb()!=media.GetPixel(0,11).ToArgb())
                throw new Exception("Reserved factory region accepted user pixel");

            // Perspective overlay is a photo simulation only. The factory
            // indicator strip (first 12 native OLED rows) must stay untouched.
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

using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Offline and UI regressions: uses synthetic files, no network, hardware,
/// user profile writes, proprietary game images or generated screenshots.
/// </summary>
internal static class ReleaseSevenThreeSelfTests
{
    internal static void Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),
            "dock-rc73-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var fake=new SyncedLyricsSnapshot("song-a|artist-a|222",
                SyncedLyricsProvider.Parse(
                    "[00:00.00]First line\n[00:03.10]Second line\n"+
                    "[00:06.00]\n[00:09.00]Fourth line"),"synthetic");
            if(!OfflineLyricsCache.Store(fake,folder))
                throw new Exception("Offline lyrics did not save");
            var restored=OfflineLyricsCache.Read(fake.TrackKey,folder);
            if(restored is null||restored.Lines.Count!=4||
               restored.Lines[1].At!=TimeSpan.FromMilliseconds(3100)||
               restored.Lines[2].Text!="" ||
               !restored.Source.Contains("Offline cache"))
                throw new Exception("Offline timestamp or instrumental cue lost");
            if(OfflineLyricsCache.Read("different-artist-song",folder)!=null)
                throw new Exception("Lyrics cache matched the wrong song");
            var importedSong=new MediaSnapshot(true,true,"Local Song",
                "Local Singer","SMTC",TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(210),DateTimeOffset.Now);
            string lrc=Path.Combine(folder,"user-timecoded.lrc");
            File.WriteAllText(lrc,
                "[00:00.00]First imported line\n"+
                "[00:05.65]Second imported line\n"+
                "[00:07.00]\n");
            // Test harness has WinForms STA context. A UI event awaits
            // naturally; synchronous test invocation must run off-context.
            if(!System.Threading.Tasks.Task.Run(()=>
                SyncedLyricsProvider.ImportLocalLrcAsync(
                    importedSong,lrc,folder)).GetAwaiter().GetResult())
                throw new Exception("User-supplied LRC import rejected");
            var cachedImport=OfflineLyricsCache.Read(
                SyncedLyricsProvider.Key(importedSong),folder);
            if(cachedImport is null || cachedImport.Lines.Count!=3 ||
               cachedImport.Lines[1].At.TotalMilliseconds!=5650 ||
               cachedImport.Lines[2].Text!="")
                throw new Exception("Offline LRC import lost timecodes");
            if(!GgGameDetector.IsInstalledGamePath(
                @"C:\SteamLibrary\steamapps\common\Example\Game.exe") ||
               GgGameDetector.IsInstalledGamePath(
                @"C:\Windows\System32\cmd.exe"))
                throw new Exception("Foreground game fallback path safety");
            if(OfflineLyricsCache.Store(SyncedLyricsSnapshot.Empty,folder))
                throw new Exception("Empty lyrics should not persist");
            var target=new DockWidget{
                Kind="Lyrics",X=12,Y=16,LyricLines=2,WidgetWidth=102};
            var song=new MediaSnapshot(true,true,"song-a","artist-a",
                "spotify",TimeSpan.FromMilliseconds(2500),
                TimeSpan.FromSeconds(222),DateTimeOffset.Now);
            using(var bmp=OledRenderer.Render(
                new LayoutState{Widgets=[target]},true,true,true,
                protectHeader:true,layoutMode:ScreenMode.Media,
                playback:song,lyrics:SyncedLyricsSnapshot.Empty))
            {
                for(int y=10;y<64;y++)for(int x=0;x<128;x++)
                    if(bmp.GetPixel(x,y).R>100)
                        throw new Exception(
                            "No cached lyrics must leave OLED widget blank");
            }

            using(var light=new Bitmap(32,32))
            {
                using(var g=Graphics.FromImage(light))
                {
                    g.Clear(Color.White);
                    using var pen=new Pen(Color.Black,4);
                    g.DrawEllipse(pen,6,6,20,20);
                }
                var pixels=GameLogoProvider.ConvertImage(light);
                int count=pixels.Count(x=>x);
                if(count is <40 or >450||pixels[0]||
                   !pixels[8*32+16])
                    throw new Exception(
                        "Black logo on white PNG became a white rectangle");
            }
            using(var dark=new Bitmap(32,32))
            {
                using(var g=Graphics.FromImage(dark))
                {
                    g.Clear(Color.Black);
                    using var brush=new SolidBrush(Color.White);
                    g.FillEllipse(brush,6,6,20,20);
                }
                var pixels=GameLogoProvider.ConvertImage(dark);
                if(pixels.Count(x=>x) is <80 or >450||pixels[0]||
                   !pixels[16*32+16])
                    throw new Exception("White logo on black PNG unreadable");
            }
            using(var alpha=new Bitmap(32,32,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using(var g=Graphics.FromImage(alpha))
                {
                    g.Clear(Color.Transparent);
                    using var brush=new SolidBrush(Color.Black);
                    g.FillRectangle(brush,10,10,12,12);
                }
                var pixels=GameLogoProvider.ConvertImage(alpha);
                if(pixels.Count(x=>x) is <100 or >200||
                   pixels[0]||!pixels[16*32+16])
                    throw new Exception("Transparent black icon unreadable");
            }

            var mouse=new ConnectedPeripheral(
                "mouse","WLMOUSE Beast X Pro 8K Receiver",
                "USB / Dock","WLMOUSE","Mouse",null,"catalogue",
                "Reader unavailable",null);
            var hub=new ConnectedPeripheral(
                "hub","Generic USB Hub","USB / Dock","Generic","USB",
                null,"no source","No battery",null);
            var earbuds=new ConnectedPeripheral(
                "bt","HUAWEI FreeBuds SE 2","Bluetooth","Huawei",
                "Earbuds",null,"Bluetooth","Needs readable source",null);
            if(!BatteryStatusDialog.IsBatteryCandidate(mouse)||
                BatteryStatusDialog.IsBatteryCandidate(hub)||
               !BatteryStatusDialog.IsBatteryCandidate(earbuds)||
               new BatteryPreferences().PriorityKinds.Length!=2)
                throw new Exception("Found-device tracking candidate selection");

            Console.WriteLine(
                "PASS: offline synchronized lyrics with restart-safe LRC "+
                "timing, no-cache silent OLED, PNG transparency and inverted "+
                "foreground art, wireless discovery and two-slot defaults.");
        }
        finally{try{Directory.Delete(folder,true);}catch{}}
    }
}

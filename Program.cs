using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        var args=Environment.GetCommandLineArgs().Skip(1).ToArray();
        if(args.Contains("--battery-scan") || args.Contains("--battery-scan-file"))
        {
            var snapshot=BatterySources.PollAsync().GetAwaiter().GetResult();
            var lines=BatterySources.Kinds.Select(kind =>
            {
                var b=snapshot[kind];
                return $"{kind}: {b.Display} | {b.Source} | {b.Detail}";
            }).ToArray();
            foreach(var line in lines) Console.WriteLine(line);
            var at=Array.IndexOf(args,"--battery-scan-file");
            if(at>=0 && at+1<args.Length)
                System.IO.File.WriteAllLines(args[at+1],lines);
            return;
        }
        if(args.Contains("--battery-preview"))
        {
            var at=Array.IndexOf(args,"--battery-preview");
            var filename=at>=0 && at+1<args.Length ? args[at+1] : "battery-live-preview.png";
            using var image=OledRenderer.Render(LayoutDefaults.Create(),true,true,true,BatterySources.PollAsync().GetAwaiter().GetResult());
            image.Save(filename,System.Drawing.Imaging.ImageFormat.Png);
            return;
        }
        if(args.Contains("--media-preview-file"))
        {
            int at=Array.IndexOf(args,"--media-preview-file");
            if(at+1>=args.Length)return;
            string output=args[at+1];
            var playback=MediaSessionProvider.PollAsync().GetAwaiter().GetResult();
            using var rendered=OledRenderer.Render(LayoutDefaults.Create(),playback.IsPlaying,true,
                playback.IsSpotify,BatterySources.PollAsync().GetAwaiter().GetResult(),playback);
            rendered.Save(output,System.Drawing.Imaging.ImageFormat.Png);
            System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(output,".txt"),
                $"Playing={playback.IsPlaying}; Spotify={playback.IsSpotify}; Title={playback.Title}; Artist={playback.Artist}; Progress={playback.ProgressPercent}%");
            return;
        }
        if(args.Contains("--game-scan-file"))
        {
            int pos=Array.IndexOf(args,"--game-scan-file");
            if(pos>=0 && pos+1<args.Length)
            {
                var catalog=GgGameDetector.LoadGgLibrary();
                var active=GgGameDetector.Poll();
                var lines=new[]{
                    "GG Library entries="+catalog.Count,
                    "Includes VALORANT="+catalog.Any(g=>g.Name.Equals("VALORANT",
                        StringComparison.OrdinalIgnoreCase)),
                    "Running game="+(active.IsRunning?active.Name:"None"),
                    "Detector source=read-only GG library_scan + Windows process list"
                };
                System.IO.File.WriteAllLines(args[pos+1],lines);
            }
            return;
        }
        if(args.Contains("--phone-recovery-test-file"))
        {
            int pos=Array.IndexOf(args,"--phone-recovery-test-file");
            if(pos>=0 && pos+1<args.Length)
            {
                var first=PhoneLinkBatteryProvider.TryRead();
                string prior=PhoneLinkBatteryProvider.LastDiagnostic;
                var second=PhoneLinkBatteryProvider.TryReadAsync(true)
                    .GetAwaiter().GetResult();
                var lines=new[]{
                    "Without opening: "+(first?.Display??"--%")+" | "+prior,
                    "After optional Phone Link minimize: "+(second?.Display??"--%")+
                        " | "+PhoneLinkBatteryProvider.LastDiagnostic,
                    "This method uses only Phone Link UI Automation, not a background API."
                };
                System.IO.File.WriteAllLines(args[pos+1],lines);
            }
            return;
        }
        if(args.Contains("--mini-player-preview-file"))
        {
            var at=Array.IndexOf(args,"--mini-player-preview-file");
            if(at>=0 && at+1<args.Length)
            {
                var location=args[at+1];
                var design=StudioProject.Defaults(ScreenMode.Game);
                design.Widgets.Add(new DockWidget{
                    Kind="GameMiniPlayer",X=4,Y=38,FontSize=7,
                    StrokeWidth=1,MiniWidth=88
                });
                var game=new GameSnapshot("VALORANT","valorant",1);
                var song=new MediaSnapshot(true,true,
                    "IN THE END","LINKIN PARK","Spotify",
                    TimeSpan.FromSeconds(48),TimeSpan.FromSeconds(210),
                    DateTimeOffset.UtcNow);
                using(var playing=OledRenderer.Render(design,false,true,
                    false,null,song,game,protectHeader:true,layoutMode:ScreenMode.Game))
                    playing.Save(location,System.Drawing.Imaging.ImageFormat.Png);
                using(var paused=OledRenderer.Render(design,false,true,
                    false,null,MediaSnapshot.Empty,game,protectHeader:true,
                    layoutMode:ScreenMode.Game))
                    paused.Save(System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(location)!,
                        System.IO.Path.GetFileNameWithoutExtension(location)+"-paused.png"),
                        System.Drawing.Imaging.ImageFormat.Png);
            }
            return;
        }
        if(args.Contains("--dial-hid-open-test"))
        {
            Console.WriteLine(ArctisDialMonitor.ReadOnlyOpenCheck());
            return;
        }
        if(args.Contains("--dial-hid-check"))
        {
            Console.WriteLine(ArctisDialMonitor.ReadOnlyDeviceCheck());
            return;
        }
        if(args.Contains("--dial-event-watch"))
        {
            // Read-only diagnostic: no audio adjustment, OLED report or
            // firmware command. A human must rotate the physical wheel to
            // generate events; this tool cannot synthesize hardware input.
            int count=0;
            using var monitor=new ArctisDialMonitor();
            monitor.Updated+=()=>
            {
                var eventInfo=monitor.Snapshot;
                System.Threading.Interlocked.Increment(ref count);
                Console.WriteLine($"{eventInfo.UpdatedAt:O} {eventInfo.View} "+
                    $"vol={eventInfo.Volume} step={eventInfo.VolumeAttenuationSteps} "+
                    $"game={eventInfo.Game} chat={eventInfo.Chat}");
            };
            Console.WriteLine("Watching the read-only Nova Pro dial for 20 seconds.");
            monitor.Start();
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(20));
            monitor.Stop();
            Console.WriteLine($"Dial events received: {count}; status: {monitor.Snapshot.Status}");
            Environment.ExitCode=count>0?0:2;
            return;
        }
        if(args.Contains("--dial-raw-watch"))
        {
            try{ArctisDialMonitor.ReadOnlyRawWatch(TimeSpan.FromSeconds(20),
                line=>Console.WriteLine(line));}
            catch(Exception ex)
            {
                Console.WriteLine("Raw HID read failed: "+ex.GetType().Name+
                    ": "+ex.Message);
                Environment.ExitCode=1;
            }
            return;
        }
        if(args.Contains("--oled-hid-check"))
        {
            var error=FastOledTransport.ValidateAvailable();
            Console.WriteLine(error.Length==0
                ? "Nova Pro Wireless OLED HID: detected (read-only)"
                : "Nova Pro Wireless OLED HID: "+error);
            return;
        }
        if(args.Contains("--oled-hid-open-test"))
        {
            try
            {
                using var stream=new FastOledTransport();
                stream.Open();
                Console.WriteLine("Nova Pro Wireless OLED HID: open/close succeeded; no frame sent");
            }
            catch(Exception ex)
            {
                Console.WriteLine("OLED HID open failed: "+ex.Message);
                Environment.ExitCode=1;
            }
            return;
        }
        if(args.Contains("--oled-retention-watch")||
           args.Contains("--oled-full-retention-watch")||
           args.Contains("--oled-reclaim-watch")||
           args.Contains("--oled-reclaim-fast-watch")||
           args.Contains("--oled-reclaim-burst-watch"))
        {
            bool burstReclaim=args.Contains("--oled-reclaim-burst-watch");
            bool fastReclaim=burstReclaim||args.Contains("--oled-reclaim-fast-watch");
            bool reclaim=fastReclaim||args.Contains("--oled-reclaim-watch");
            bool fullScreen=reclaim||args.Contains("--oled-full-retention-watch");
            // Persist diagnostics even in a WinExe process with no console attached.
            string diagnosticDir=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "SteelseriesDockCustomiser");
            System.IO.Directory.CreateDirectory(diagnosticDir);
            string diagnosticFile=System.IO.Path.Combine(diagnosticDir,
                "oled-retention-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".log");
            using var diagnosticLog=new System.IO.StreamWriter(diagnosticFile)
                { AutoFlush=true };
            void Log(string message)
            {
                Console.WriteLine(message);
                diagnosticLog.WriteLine(DateTimeOffset.Now.ToString("O")+" "+message);
            }
            Log("Diagnostic log: "+diagnosticFile);
            // Interactive diagnostic only: keep the user's normal idle design
            // on the OLED while they rotate the hardware knob. No firmware,
            // audio or configuration writes are made. Always hand back.
            using var monitor=new ArctisDialMonitor();
            int seen=0, reclaimPending=0, reclaimAttempts=0;
            long lastDialAt=0, reclaimWindowStartedAt=0;
            string lastDialView="";
            monitor.Updated+=()=>
            {
                var value=monitor.Snapshot;
                System.Threading.Interlocked.Increment(ref seen);
                if(reclaim)
                {
                    long now=Environment.TickCount64;
                    long previous=System.Threading.Interlocked.Exchange(ref lastDialAt,now);
                    bool changedView=!string.Equals(lastDialView,value.View.ToString(),StringComparison.Ordinal);
                    lastDialView=value.View.ToString();
                    if(burstReclaim && (previous==0||now-previous>1100||changedView))
                        System.Threading.Interlocked.Exchange(ref reclaimWindowStartedAt,now);
                    System.Threading.Interlocked.Exchange(ref reclaimPending,1);
                }
                Log($"DIAL {value.View}: vol={value.Volume}, "+
                    $"steps={value.VolumeAttenuationSteps}, G={value.Game}, C={value.Chat}");
            };
            var stopwatch=System.Diagnostics.Stopwatch.StartNew();
            int frames=0,slowest=0;
            long lastProgress=0, lastReclaim=-1000;
            try
            {
                using var bitmap=OledRenderer.Render(
                    StudioProject.Defaults(ScreenMode.Idle),false,true,false,
                    layoutMode:ScreenMode.Idle);
                using var transport=new FastOledTransport();
                transport.Open();
                monitor.Start();
                Log("30s OLED retention check ("+
                    (burstReclaim?"BURST RECLAIM 180ms for 550ms after dial":
                     fastReclaim?"FAST RECLAIM 180ms + 750ms tail":
                     reclaim?"RECLAIM 0x95->0x93":
                     fullScreen?"128x64 FULL":"protected 10..63")+
                    ") started. Rotate and press the physical dial now.");
                while(stopwatch.Elapsed<TimeSpan.FromSeconds(30))
                {
                    long before=stopwatch.ElapsedMilliseconds;
                    long now=Environment.TickCount64;
                    long sinceDial=now-
                        System.Threading.Interlocked.Read(ref lastDialAt);
                    long burstStart=System.Threading.Interlocked.Read(ref reclaimWindowStartedAt);
                    bool burstWindow=burstReclaim && burstStart>0 &&
                        now-burstStart>=0 && now-burstStart<=550;
                    bool dialTail=fastReclaim && !burstReclaim && lastDialAt>0 &&
                        sinceDial>=0 && sinceDial<=750;
                    bool pending=System.Threading.Volatile.Read(ref reclaimPending)!=0;
                    int limit=fastReclaim?150:12;
                    int cooldown=fastReclaim?180:700;
                    bool shouldReclaim=burstReclaim?burstWindow:(pending||dialTail);
                    if(reclaim && reclaimAttempts<limit &&
                       stopwatch.ElapsedMilliseconds-lastReclaim>=cooldown &&
                       shouldReclaim)
                    {
                        System.Threading.Interlocked.Exchange(ref reclaimPending,0);
                        // Bounded, diagnostic-only release/draw burst after wheel input.
                        // Subsequent frames use the existing 0x93 transport.
                        transport.ReclaimNativeOverlay(bitmap);
                        lastReclaim=stopwatch.ElapsedMilliseconds;
                        reclaimAttempts++;
                        Log($"RECLAIM attempt={reclaimAttempts} at={lastReclaim}ms "+
                            "0x95 output -> 0x93 full frame");
                    }
                    else transport.SendLiveFrame(bitmap,!fullScreen);
                    slowest=Math.Max(slowest,(int)(stopwatch.ElapsedMilliseconds-before));
                    frames++;
                    if(stopwatch.ElapsedMilliseconds-lastProgress>=500)
                    {
                        lastProgress=stopwatch.ElapsedMilliseconds;
                        Log($"PROGRESS elapsed={lastProgress}ms frames={frames} slowest={slowest}ms");
                    }
                    System.Threading.Thread.Sleep(35);
                }
                Log($"Completed frames={frames}; worst transfer={slowest}ms; "+
                    $"dial events={seen}; reclaim attempts={reclaimAttempts}; "+
                    $"elapsed={stopwatch.ElapsedMilliseconds}ms");
            }
            catch(Exception ex)
            {
                Log("Retention check failed: "+ex.GetType().Name+": "+ex.Message);
                Environment.ExitCode=1;
            }
            finally
            {
                monitor.Stop();
                Log(new DockHardwareTest().RestoreNow());
            }
            return;
        }
        if(args.Contains("--oled-hardware-smoke"))
        {
            // Explicit, temporary physical test. It only writes rows 10–63,
            // uses the normal user design and restores firmware ownership in
            // finally. It does not change audio, knob settings or firmware.
            var writer=new DockHardwareTest();
            string validation=writer.Validate();
            if(validation.Length!=0)
            {
                Console.WriteLine(validation);
                Environment.ExitCode=1;
                return;
            }
            try
            {
                using var image=OledRenderer.Render(
                    StudioProject.Defaults(ScreenMode.Idle),false,true,false,
                    layoutMode:ScreenMode.Idle);
                using(var transport=new FastOledTransport())
                {
                    transport.Open();
                    transport.SendLiveFrame(image,true);
                    System.Threading.Thread.Sleep(850);
                }
                Console.WriteLine("OLED protected feature reports accepted.");
            }
            catch(Exception ex)
            {
                Console.WriteLine("OLED smoke test failed: "+ex.Message);
                Environment.ExitCode=1;
            }
            finally
            {
                string result=writer.RestoreNow();
                Console.WriteLine(result);
                if(!result.StartsWith("SteelSeries varsayılan",StringComparison.Ordinal))
                    Environment.ExitCode=1;
            }
            return;
        }
        if(args.Contains("--telemetry-smoke"))
        {
            var reader=new SystemTelemetry();
            reader.Read();
            System.Threading.Thread.Sleep(700);
            var sample=reader.Read();
            Console.WriteLine($"CPU={sample.Cpu?.ToString()??"--"}% RAM="+
                $"{sample.Memory?.ToString()??"--"}%");
            Environment.ExitCode=sample.Cpu is >=0 and <=100 &&
                sample.Memory is >=0 and <=100 ?0:1;
            return;
        }
        if(args.Contains("--self-test"))
        {
            ApplicationConfiguration.Initialize();
            Application.SetColorMode(SystemColorMode.Dark);
            var at=Array.IndexOf(args,"--demo-output");
            var export=at>=0 && at+1<args.Length ? args[at+1] : null;
            Environment.ExitCode=SelfTests.Run(export);
            return;
        }
        if(args.Contains("--ui-screenshot"))
        {
            // Off-screen WinForms smoke preview: no HID output and no new
            // visible app window. Used only to inspect responsive geometry.
            var at=Array.IndexOf(args,"--ui-screenshot");
            if(at+1>=args.Length)throw new ArgumentException("Missing screenshot path");
            ApplicationConfiguration.Initialize();
            Application.SetColorMode(SystemColorMode.Dark);
            if(args.Contains("--capture-english"))UiLanguage.PreviewOnly(true);
            if(args.Contains("--capture-wheel"))
            {
                using var wheel=new WheelArtworkDialog(null);
                wheel.Show();
                Application.DoEvents();
                using var wheelCapture=new System.Drawing.Bitmap(wheel.Width,wheel.Height);
                wheel.DrawToBitmap(wheelCapture,new System.Drawing.Rectangle(0,0,
                    wheelCapture.Width,wheelCapture.Height));
                wheelCapture.Save(args[at+1],System.Drawing.Imaging.ImageFormat.Png);
                wheel.Close();
                return;
            }
            if(args.Contains("--capture-profiles"))
            {
                using var profiles=new LayoutProfileDialog(ScreenMode.Idle,
                    ()=>StudioProject.Defaults(ScreenMode.Idle),_=>{});
                profiles.StartPosition=System.Windows.Forms.FormStartPosition.Manual;
                profiles.Location=new System.Drawing.Point(-5000,-5000);
                profiles.ShowInTaskbar=false;
                profiles.Show();
                Application.DoEvents();
                using var capture=new System.Drawing.Bitmap(profiles.Width,profiles.Height);
                profiles.DrawToBitmap(capture,new System.Drawing.Rectangle(0,0,
                    capture.Width,capture.Height));
                capture.Save(args[at+1],System.Drawing.Imaging.ImageFormat.Png);
                profiles.Close();
                return;
            }
            if(args.Contains("--capture-settings"))
            {
                using var settings=new SettingsDialog(()=>false,new BatteryPreferences(),
                    ()=>new BatterySnapshot(),
                    ()=>System.Threading.Tasks.Task.CompletedTask,()=>{});
                settings.EnableOffscreenCapture();
                settings.Show();
                Application.DoEvents();
                using var image=new System.Drawing.Bitmap(settings.Width,settings.Height);
                settings.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,
                    image.Width,image.Height));
                image.Save(args[at+1],System.Drawing.Imaging.ImageFormat.Png);
                settings.Close();
                return;
            }
            using var studio=new PixelStudioForm();
            if(args.Contains("--capture-idle"))studio.SelectCaptureMode(ScreenMode.Idle);
            else if(args.Contains("--capture-game"))studio.SelectCaptureMode(ScreenMode.Game);
            studio.EnableOffscreenCapture();
            studio.Show();
            Application.DoEvents();
            studio.PerformLayout();
            Application.DoEvents();
            using var preview=new System.Drawing.Bitmap(studio.Width,studio.Height);
            studio.DrawToBitmap(preview,new System.Drawing.Rectangle(0,0,
                preview.Width,preview.Height));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(
                System.IO.Path.GetFullPath(args[at+1]))!);
            preview.Save(args[at+1],System.Drawing.Imaging.ImageFormat.Png);
            studio.Close();
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.Dark);
        Application.Run(new PixelStudioForm());
    }
}

public sealed class DockWidget
{
    public string Kind { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; } = 17;
    public int FontSize { get; set; } = 10;
    public int StrokeWidth { get; set; } = 1;
    public int MiniWidth { get; set; } = 88;
    // "Wheel" renders the OEM-inspired 1-bit radial gauge; "Bar" preserves
    // older designs without requiring a layout-file format migration.
    public string DialStyle { get; set; } = "Bar";
    // Optional user-authored 45×42 one-bit sprite. Empty means native-style
    // factory template; saved along with this widget, not globally.
    public string WheelPixels { get; set; } = "";
    public bool WheelAutoLabel { get; set; } = true;
    // Degrees clockwise relative to the factory ring. The VOL/MIX badge
    // remains upright, including when the surrounding artwork is rotated.
    public int WheelGapAngle { get; set; }
    public string Format { get; set; } = "";
    public string Text { get; set; } = "HELLO";
    public bool Visible { get; set; } = true;
    public override string ToString() => $"{WidgetLabels.Display(Kind)}  ({X}, {Y})";
}

public sealed class LayoutState
{
    public bool[] Pixels { get; set; } = new bool[128 * 64];
    public List<DockWidget> Widgets { get; set; } = new();

    public LayoutState DeepClone() =>
        JsonSerializer.Deserialize<LayoutState>(JsonSerializer.Serialize(this)) ?? new LayoutState();
}

internal static class LayoutDefaults
{
    public static LayoutState Create() => new()
    {
        Widgets = new List<DockWidget>
        {
            new() { Kind="Source", X=3, Y=18 },
            new() { Kind="Track", X=22, Y=19, FontSize=9 },
            new() { Kind="Artist", X=22, Y=30, FontSize=9 },
            new() { Kind="Progress", X=22, Y=42 },
            new() { Kind="Clock", X=35, Y=19, FontSize=20 },
            new() { Kind="Date", X=33, Y=39, FontSize=9 },
            new() { Kind="Mouse", X=2, Y=53 },
            new() { Kind="Phone", X=45, Y=53 },
            new() { Kind="DualSense", X=88, Y=53 }
        }
    };
}

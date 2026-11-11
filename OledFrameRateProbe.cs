using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Explicit 30-second experiment with pre-encoded 0x93 display reports.
/// No firmware operations, no 0x95 during drawing, no persistent configuration.
/// Temporarily enables ChatMix only while GG is closed; always rolls it back.
/// </summary>
internal static class OledFrameRateProbe
{
    private static bool GgActive()
    {
        foreach(var name in new[]{"SteelSeriesGG","SteelSeriesGGClient",
            "SteelSeriesEngine","SteelSeriesSonar"})
        {
            using var process=Process.GetProcessesByName(name).FirstOrDefault();
            if(process is not null)return true;
        }
        return false;
    }

    public static void Run(int fps)
    {
        if(fps is not (50 or 60))
        {
            Console.WriteLine("Only 50 or 60 FPS is permitted.");
            Environment.ExitCode=2;
            return;
        }
        var folder=Path.Combine(Path.GetTempPath(),"SteelseriesDockCustomiser");
        Directory.CreateDirectory(folder);
        var logfile=Path.Combine(folder,
            "oled-"+fps+"fps-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".log");
        using var log=new StreamWriter(logfile){AutoFlush=true};
        void Log(string message)
        {
            string line=DateTimeOffset.Now.ToString("O")+" "+message;
            Console.WriteLine(line);
            log.WriteLine(line);
        }

        Log("Diagnostic log: "+logfile);
        if(GgActive())
        {
            Log("ABORT: SteelSeries GG/Engine/Sonar running; ask Yakup to close it manually.");
            Environment.ExitCode=2;
            return;
        }
        string validation=new DockHardwareTest().Validate();
        if(validation.Length!=0)
        {
            Log("ABORT: OLED restore tool validation failed: "+validation);
            Environment.ExitCode=2;
            return;
        }

        FastOledTransport? transport=null;
        int frames=0, slowFrames=0;
        double slowest=0;
        try
        {
            using var image=OledRenderer.Render(
                StudioProject.Defaults(ScreenMode.Idle),false,true,false,
                layoutMode:ScreenMode.Idle);
            var left=FastOledTransport.EncodeHalf(image,0);
            var right=FastOledTransport.EncodeHalf(image,64);
            transport=new FastOledTransport();
            transport.Open();
            transport.SendChatMixTestCommand(0x49,1);
            transport.SendChatMixTestCommand(0x8d,1);
            Log("TEST START: "+fps+" FPS, 30 seconds; 0x93 only while drawing. ChatMix temporarily enabled.");
            var clock=Stopwatch.StartNew();
            const double duration=30000;
            double nextAt=0;
            double lastProgress=0;
            while(clock.Elapsed.TotalMilliseconds<duration)
            {
                double now=clock.Elapsed.TotalMilliseconds;
                double remaining=nextAt-now;
                if(remaining>2)Thread.Sleep((int)(remaining-1));
                while(clock.Elapsed.TotalMilliseconds<nextAt)
                    Thread.SpinWait(24);
                double before=clock.Elapsed.TotalMilliseconds;
                transport.SendEncodedFrame(left,right);
                double transfer=clock.Elapsed.TotalMilliseconds-before;
                if(transfer>slowest)slowest=transfer;
                if(transfer>100)slowFrames++;
                frames++;
                if(clock.Elapsed.TotalMilliseconds-lastProgress>=1000)
                {
                    lastProgress=clock.Elapsed.TotalMilliseconds;
                    Log("PROGRESS t="+Math.Round(lastProgress)+
                        "ms frames="+frames+" actual="+
                        Math.Round(frames*1000/Math.Max(1,lastProgress),1)+
                        " FPS maxSendMs="+Math.Round(slowest,1));
                }
                if(slowFrames>8)
                    throw new IOException("More than 8 HID frame sends exceeded 100 ms; stopped early.");
                nextAt=Math.Max(nextAt+(1000d/fps),clock.Elapsed.TotalMilliseconds);
            }
            clock.Stop();
            Log("RESULT requested="+fps+" FPS actual="+
                Math.Round(frames*1000/clock.Elapsed.TotalMilliseconds,2)+
                " FPS sent="+frames+" maxSendMs="+Math.Round(slowest,1)+
                " slowFrames="+slowFrames);
        }
        catch(Exception ex)
        {
            Log("TEST ERROR: "+ex.GetType().Name+" "+ex.Message);
            Environment.ExitCode=1;
        }
        finally
        {
            if(transport is not null)
            {
                try
                {
                    transport.SendChatMixTestCommand(0x8d,0);
                    transport.SendChatMixTestCommand(0x49,0);
                    Log("RESTORE: volatile ChatMix handshake disabled.");
                }
                catch(Exception ex){Log("Handshake rollback warning: "+ex.Message);}
                transport.Dispose();
            }
            Log("RESTORE: "+new DockHardwareTest().RestoreNow());
            Log("Test ended; no firmware changes.");
        }
    }
}

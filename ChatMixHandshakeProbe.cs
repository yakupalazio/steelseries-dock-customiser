using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Opt-in, temporary ChatMix handshake diagnostic. Does not touch firmware,
/// OLED pixels, Windows sound devices, GG files or persistent device settings.
/// Only writes documented 64-byte volatile HID commands, then undoes them.
/// Never starts until SteelSeries GG/Engine/Sonar processes are closed.
/// </summary>
internal static class ChatMixHandshakeProbe
{
    private const int Vendor = 0x1038;

    private static HidDevice[] Candidates() => DeviceList.Local.GetHidDevices(Vendor)
        .Where(d => d.ProductID is 0x12e0 or 0x12e5 or 0x225d)
        .Where(d => d.DevicePath.Contains("mi_04", StringComparison.OrdinalIgnoreCase))
        .Where(d =>
        {
            try
            {
                return d.GetMaxOutputReportLength() is >=64 and <=65 &&
                    d.GetMaxInputReportLength() is >=4 and <=128;
            }
            catch { return false; }
        })
        // The 1024-feature OLED collection also has the confirmed 64-byte
        // output transport (0x95); the other mi_04 collection rejects Write().
        .OrderBy(d =>
        {
            try { return d.GetMaxFeatureReportLength() >= 1024 ? 0 : 1; }
            catch { return 2; }
        }).ToArray();

    private static string LogFile
    {
        get
        {
            var folder=Path.Combine(Path.GetTempPath(), "SteelseriesDockCustomiser");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder,
                "chatmix-handshake-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".log");
        }
    }

    public static void Inspect()
    {
        var devices=DeviceList.Local.GetHidDevices(Vendor)
            .Where(d => d.ProductID is 0x12e0 or 0x12e5 or 0x225d)
            .ToArray();
        Console.WriteLine("Nova Pro Wireless HID collections: "+devices.Length);
        foreach(var d in devices)
        {
            try
            {
                string p=d.DevicePath;
                int at=p.IndexOf("mi_",StringComparison.OrdinalIgnoreCase);
                string iface=at>=0 && at+5<=p.Length ? p.Substring(at,5) : "unknown";
                Console.WriteLine($"  {iface}: IN={d.GetMaxInputReportLength()} "+
                    $"OUT={d.GetMaxOutputReportLength()} FEAT={d.GetMaxFeatureReportLength()} "+
                    $"candidate={Candidates().Contains(d)}");
            }
            catch(Exception e){Console.WriteLine("  HID descriptor: "+e.GetType().Name);}
        }
        Console.WriteLine("No reports sent.");
    }

    private static bool GgActive()
    {
        foreach(string name in new[]{"SteelSeriesGG","SteelSeriesGGClient",
            "SteelSeriesEngine","SteelSeriesSonar"})
        {
            using var active=Process.GetProcessesByName(name).FirstOrDefault();
            if(active!=null) return true;
        }
        return false;
    }

    private static void Send(HidStream stream,byte opcode,byte value)
    {
        var report=new byte[64];
        report[0]=0x06;
        report[1]=opcode;
        report[2]=value;
        stream.Write(report);
    }

    public static void Run(bool includeIcon,int durationSeconds=20,bool pollStatus=false)
    {
        string file=LogFile;
        using var writer=new StreamWriter(file) { AutoFlush=true };
        object logGate=new();
        void Log(string line)
        {
            lock(logGate)
            {
                string msg=DateTimeOffset.Now.ToString("O")+" "+line;
                Console.WriteLine(msg);
                writer.WriteLine(msg);
            }
        }

        Log("Log: "+file);
        if(GgActive())
        {
            Log("ABORT: SteelSeries GG/Engine/Sonar is running. Ask Yakup to close it; no command sent.");
            Environment.ExitCode=2;
            return;
        }
        var candidates=Candidates();
        if(candidates.Length==0)
        {
            Log("ABORT: expected Nova Pro mi_04 64-byte output HID missing; no command sent.");
            Environment.ExitCode=2;
            return;
        }

        bool enabled=false, iconEnabled=false;
        using var stream=candidates[0].Open();
        stream.ReadTimeout=300;
        stream.WriteTimeout=600;
        var watch=Stopwatch.StartNew();
        int count=0, statusDifferences=0;
        var responseBefore=new Dictionary<byte,byte[]>();
        object responseGate=new();
        try
        {
            Log("Starting nonpersistent ChatMix handshake; GG must stay closed.");
            Send(stream,0x49,0x01);
            enabled=true;
            Log("TX 06 49 01: ChatMix control enabled (volatile).");
            if(includeIcon)
            {
                Send(stream,0x8d,0x01);
                iconEnabled=true;
                Log("TX 06 8D 01: optional Sonar indicator enabled (volatile).");
            }
            Log($"{durationSeconds}s guided test: physical wheel commands only on instruction. Listening on {candidates.Length} mi_04 collections.");
            void Watch(HidStream reader,int channel,int maxInput)
            {
                var buffer=new byte[Math.Max(65,maxInput)];
                while(watch.Elapsed<TimeSpan.FromSeconds(durationSeconds))
                {
                    try
                    {
                        int size=reader.Read(buffer,0,buffer.Length);
                        if(size<1)continue;
                        var data=buffer.AsSpan(0,size);
                        bool parsed=ArctisDialMonitor.TryDecode(data,out var dial);
                        int n=Interlocked.Increment(ref count);
                        bool isStatus=pollStatus && size>=3 && data[0]==0x06 &&
                            data[1] is 0xb0 or 0x20;
                        if(isStatus)
                        {
                            byte opcode=data[1];
                            lock(responseGate)
                            {
                                if(!responseBefore.TryGetValue(opcode,out var prior))
                                {
                                    Log($"T={watch.ElapsedMilliseconds}ms STATUS FIRST "+
                                        $"{opcode:X2} channel={channel} "+
                                        Convert.ToHexString(data[..Math.Min(24,size)]));
                                }
                                else
                                {
                                    var changed=new List<string>();
                                    for(int k=2;k<Math.Min(size,prior.Length);k++)
                                        if(prior[k]!=data[k])
                                            changed.Add($"{k:X2}:{prior[k]:X2}->{data[k]:X2}");
                                    if(changed.Count>0 && ++statusDifferences<=80)
                                        Log($"T={watch.ElapsedMilliseconds}ms STATUS DELTA "+
                                            $"{opcode:X2} channel={channel} "+
                                            string.Join(" ",changed));
                                }
                                responseBefore[opcode]=data.ToArray();
                            }
                        }
                        else if(n<=100)
                            Log($"T={watch.ElapsedMilliseconds}ms RX channel={channel} "+
                                Convert.ToHexString(data[..Math.Min(16,size)])+
                                (parsed?$" MODE={dial.View} V={dial.Volume} "+
                                    $"G={dial.Game} C={dial.Chat}":" other"));
                    }
                    catch(TimeoutException) { }
                    catch(Exception ex)
                    {
                        Log($"Reader {channel} stopped: {ex.GetType().Name}");
                        break;
                    }
                }
            }
            var reads=candidates.Select((device,index)=>Task.Run(()=>
            {
                try
                {
                    if(index==0)Watch(stream,index+1,device.GetMaxInputReportLength());
                    else
                    {
                        using var reader=device.Open();
                        reader.ReadTimeout=300;
                        Watch(reader,index+1,device.GetMaxInputReportLength());
                    }
                }
                catch(Exception ex){Log($"Reader {index+1} unavailable: {ex.GetType().Name}");}
            })).ToArray();
            var poller=pollStatus?Task.Run(()=>
            {
                int polls=0;
                while(watch.Elapsed<TimeSpan.FromSeconds(durationSeconds))
                {
                    try
                    {
                        Send(stream,0xb0,0);
                        Thread.Sleep(40);
                        Send(stream,0x20,0);
                        polls++;
                        if(polls%10==0)
                            Log($"T={watch.ElapsedMilliseconds}ms POLL count={polls}");
                        Thread.Sleep(460);
                    }
                    catch(Exception ex)
                    {
                        Log("Status polling stopped: "+ex.GetType().Name);
                        break;
                    }
                }
                Log("Status poll pairs sent: "+polls);
            }):Task.CompletedTask;
            Task.WaitAll(reads.Append(poller).ToArray());
            Log($"Completed, reports observed: {count}; changed status reports: {statusDifferences}");
        }
        catch(Exception e)
        {
            Log("PROBE ERROR: "+e.GetType().Name+" "+e.Message);
            Environment.ExitCode=1;
        }
        finally
        {
            // Best-effort restoration of transient mode. No NVRAM commit.
            if(iconEnabled)
                try {Send(stream,0x8d,0x00); Log("RESTORE TX 06 8D 00");}
                catch(Exception e){Log("RESTORE indicator failed: "+e.GetType().Name);}
            if(enabled)
                try {Send(stream,0x49,0x00); Log("RESTORE TX 06 49 00");}
                catch(Exception e){Log("RESTORE ChatMix failed: "+e.GetType().Name);}
            Log("Probe ended; SteelSeries settings and firmware files untouched.");
        }
    }
}

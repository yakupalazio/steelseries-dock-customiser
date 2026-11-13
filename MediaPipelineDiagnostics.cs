using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

#pragma warning disable CS0618 // Dedicated read-only legacy WASAPI probe.

namespace SteelseriesDockCustomiser;

/// <summary>Read-only diagnostic; never saves audio or lyrics.</summary>
internal static class MediaPipelineDiagnostics
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Windows SMTC progression (2-second samples) ===");
        for(int i=0;i<4;i++)
        {
            var m=await MediaSessionProvider.PollAsync();
            // Do not print private song title or artist, only timing.
            Console.WriteLine("SMTC "+i+" PLAY="+m.IsPlaying+
                " SOURCE_SPOTIFY="+m.IsSpotify+
                " POS_MS="+Math.Round(m.Position.TotalMilliseconds)+
                " DURATION_MS="+Math.Round(m.Duration.TotalMilliseconds)+
                " SAMPLE_AGE_MS="+Math.Round(
                    (DateTimeOffset.Now-m.SampledAt).TotalMilliseconds));
            if(i<3)await Task.Delay(1600);
        }
        Console.WriteLine("=== WASAPI active render endpoints ===");
        using var enumerator=new MMDeviceEnumerator();
        using var def=enumerator.GetDefaultAudioEndpoint(
            DataFlow.Render,Role.Multimedia);
        Console.WriteLine("DEFAULT="+def.FriendlyName);
        var devices=enumerator.EnumerateAudioEndPoints(DataFlow.Render,
            DeviceState.Active).Take(10).ToArray();
        for(int i=0;i<devices.Length;i++)
        {
            using var device=devices[i];
            WasapiLoopbackCapture? capture=null;
            int bytes=0,events=0;double max=0;
            try
            {
                capture=new WasapiLoopbackCapture(device);
                var fmt=capture.WaveFormat;
                Console.WriteLine("ENDPOINT "+i+" NAME="+device.FriendlyName+
                    " FORMAT="+fmt.Encoding+"/"+fmt.BitsPerSample+
                    "/"+fmt.Channels+"ch/"+fmt.SampleRate);
                capture.DataAvailable+=(_,e)=>
                {
                    Interlocked.Increment(ref events);
                    Interlocked.Add(ref bytes,e.BytesRecorded);
                    // Independent byte-level activity estimate: no raw
                    // audio arrays or bytes are retained/logged.
                    if(fmt.BitsPerSample==32 &&
                       fmt is WaveFormatExtensible ext &&
                       ext.SubFormat==new Guid("00000003-0000-0010-8000-00aa00389b71"))
                    {
                        for(int off=0;off+4<=e.BytesRecorded;off+=fmt.BlockAlign)
                        {
                            float v=BitConverter.ToSingle(e.Buffer,off);
                            if(float.IsFinite(v))max=Math.Max(max,Math.Abs(v));
                        }
                    }
                };
                capture.StartRecording();
                await Task.Delay(1400);
                capture.StopRecording();
                Console.WriteLine("RESULT "+i+" CALLBACKS="+events+
                    " PCM_BYTES="+bytes+
                    " PEAK="+Math.Round(max,4));
            }
            catch(Exception ex)
            {
                Console.WriteLine("RESULT "+i+" ERROR="+ex.GetType().Name);
            }
            finally{capture?.Dispose();}
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SteelseriesDockCustomiser;

internal sealed record AudioVisualizationSnapshot(
    float[] Left,float[] Right,DateTimeOffset UpdatedAt,
    string SourceName="")
{
    internal static AudioVisualizationSnapshot Silent {get;}=
        new(new float[12],new float[12],DateTimeOffset.MinValue);

    internal bool Fresh=>UpdatedAt!=DateTimeOffset.MinValue&&
        DateTimeOffset.UtcNow-UpdatedAt<TimeSpan.FromSeconds(2);
}

internal sealed record AudioOutputOption(string Id,string Name)
{
    public override string ToString()=>Name;
}

/// <summary>
/// Opt-in, read-only WASAPI desktop-output spectrum. In Auto mode it
/// monitors the Windows default output plus a small set of Sonar/Voicemeeter
/// endpoints, then shows the currently strongest live source. This fixes
/// Spotify routed to Sonar Media while default audio is Sonar Gaming.
/// A specific endpoint can be selected per widget. No raw PCM recording,
/// disk persistence, microphone input, or network transfer.
/// </summary>
internal sealed class SystemAudioVisualizer:IDisposable
{
    // Avoid ten simultaneous FFT/loopback streams on Sonar+Voicemeeter PCs.
    // Active audio-meter endpoints are prioritized during periodic refresh.
    private const int MaxAutoEndpoints=6;
    private const int BandCount=12;
    private sealed class CaptureState:IDisposable
    {
        internal readonly MMDevice Device;
#pragma warning disable CS0618 // NAudio backward-compatible output loopback.
        internal readonly WasapiLoopbackCapture Capture;
#pragma warning restore CS0618
        internal readonly float[] LeftSamples=new float[2048];
        internal readonly float[] RightSamples=new float[2048];
        internal readonly object Gate=new();
        internal int Index,Available;
        internal long LastComputedMs;
        internal bool Stopped;
        internal AudioVisualizationSnapshot Latest=AudioVisualizationSnapshot.Silent;
        internal CaptureState(MMDevice device)
        {
            Device=device;
#pragma warning disable CS0618
            Capture=new WasapiLoopbackCapture(device);
#pragma warning restore CS0618
        }
        public void Dispose()
        {
            try{Capture.StopRecording();}catch{}
            try{Capture.Dispose();}catch{}
            try{Device.Dispose();}catch{}
        }
    }

    private readonly List<CaptureState> sources=new();
    private DateTimeOffset nextRetry=DateTimeOffset.MinValue;
    private DateTimeOffset nextRefresh=DateTimeOffset.MinValue;
    private string selection="";
    private string defaultId="";
    public bool Enabled=>sources.Any(s=>!s.Stopped);
    public int ActiveCaptures=>sources.Count(s=>!s.Stopped);
    public string ActiveSource=>Snapshot.SourceName;

    public AudioVisualizationSnapshot Snapshot
    {
        get
        {
            AudioVisualizationSnapshot? strongest=null;
            float max=0;
            foreach(var source in sources)
            {
                var value=Volatile.Read(ref source.Latest);
                if(!value.Fresh)continue;
                float energy=0;
                for(int i=0;i<BandCount;i++)
                    energy+=value.Left[i]+value.Right[i];
                // If all streams are silent, retain a fresh empty state.
                if(strongest is null||energy>max)
                {
                    strongest=value;
                    max=energy;
                }
            }
            return strongest??AudioVisualizationSnapshot.Silent;
        }
    }

    internal static IReadOnlyList<AudioOutputOption> AvailableOutputs()
    {
        var result=new List<AudioOutputOption>{
            new("","Otomatik (Sonar / Windows / Voicemeeter)")
        };
        try
        {
            using var list=new MMDeviceEnumerator();
            foreach(var endpoint in list.EnumerateAudioEndPoints(
                DataFlow.Render,DeviceState.Active))
            {
                using(endpoint)
                {
                    if(!string.IsNullOrEmpty(endpoint.ID))
                        result.Add(new(endpoint.ID,endpoint.FriendlyName));
                }
            }
        }
        catch{/* Audio service absent: the Auto fallback remains usable. */}
        return result;
    }

    public void SetEnabled(bool enabled,string? endpointId=null)
    {
        if(!enabled){Stop();return;}
        string request=endpointId??"";
        if(request.Length>512)request="";
        if(sources.Count>0&&selection==request&&
           DateTimeOffset.UtcNow<nextRefresh&&Enabled)return;
        if(DateTimeOffset.UtcNow<nextRetry&&sources.Count==0)return;
        Stop();
        selection=request;
        nextRefresh=DateTimeOffset.UtcNow.AddSeconds(18);
        try
        {
            using var enumerate=new MMDeviceEnumerator();
            try
            {
                using var deviceDefault=enumerate.GetDefaultAudioEndpoint(
                    DataFlow.Render,Role.Multimedia);
                defaultId=deviceDefault.ID;
            }
            catch{defaultId="";} // An explicit endpoint can still work.
            var endpoints=enumerate.EnumerateAudioEndPoints(
                DataFlow.Render,DeviceState.Active).ToArray();
            var sorted=endpoints.OrderBy(x=>
            {
                if(!string.IsNullOrEmpty(request))
                    return x.ID==request?0.0:99.0;
                float peak=0;
                try{peak=x.AudioMeterInformation.MasterPeakValue;}
                catch{}
                if(peak>.01f)return -10.0-peak; // Current audible route
                return x.ID==defaultId?0.0:
                x.FriendlyName.Contains("Sonar - Media",
                    StringComparison.OrdinalIgnoreCase)?1:
                x.FriendlyName.Contains("Sonar - Gaming",
                    StringComparison.OrdinalIgnoreCase)?2:
                x.FriendlyName.Contains("Sonar - Chat",
                    StringComparison.OrdinalIgnoreCase)?3:
                x.FriendlyName.Contains("Sonar - Aux",
                    StringComparison.OrdinalIgnoreCase)?4:
                x.FriendlyName.Contains("Voicemeeter",
                    StringComparison.OrdinalIgnoreCase)?5.0:6.0;
            }).ToArray();
            int limit=string.IsNullOrEmpty(request)?MaxAutoEndpoints:1;
            foreach(var endpoint in sorted)
            {
                if(!string.IsNullOrEmpty(request)&&endpoint.ID!=request)
                {endpoint.Dispose();continue;}
                if(sources.Count>=limit||
                   endpoint.FriendlyName.Contains("Sonar - Microphone",
                       StringComparison.OrdinalIgnoreCase))
                {endpoint.Dispose();continue;}
                CaptureState? item=null;
                try
                {
                    item=new CaptureState(endpoint);
                    var active=item; // capture stable instance, not mutable local
                    active.Capture.DataAvailable+=(_,e)=>ProcessAudio(active,e);
                    active.Capture.RecordingStopped+=(_,_)=>active.Stopped=true;
                    active.Capture.StartRecording();
                    sources.Add(active);
                    item=null; // Captures now own the MMDevice
                }
                catch
                {
                    if(item is not null)item.Dispose();
                    else endpoint.Dispose();
                }
            }
            if(sources.Count==0)
                nextRetry=DateTimeOffset.UtcNow.AddSeconds(10);
        }
        catch
        {
            Stop();
            nextRetry=DateTimeOffset.UtcNow.AddSeconds(10);
        }
    }

    private static void ProcessAudio(CaptureState source,WaveInEventArgs e)
    {
        try
        {
            var format=source.Capture.WaveFormat;
            int channels=Math.Clamp(format.Channels,1,8);
            var extended=format as WaveFormatExtensible;
            bool ieeeSubtype=extended?.SubFormat==
                new Guid("00000003-0000-0010-8000-00aa00389b71");
            bool pcmSubtype=extended?.SubFormat==
                new Guid("00000001-0000-0010-8000-00aa00389b71");
            bool float32=(format.Encoding==WaveFormatEncoding.IeeeFloat||
                ieeeSubtype)&&format.BitsPerSample==32;
            bool pcm16=(format.Encoding==WaveFormatEncoding.Pcm||
                pcmSubtype)&&format.BitsPerSample==16;
            if(!float32&&!pcm16)return;
            int sampleSize=float32?4:2;
            int stride=Math.Max(format.BlockAlign,channels*sampleSize);
            if(stride<=0||format.SampleRate<=0)return;
            lock(source.Gate)
            {
                for(int offset=0;offset+stride<=e.BytesRecorded;offset+=stride)
                {
                    float left=Sample(e.Buffer,offset,float32);
                    float right=channels>1
                        ?Sample(e.Buffer,offset+sampleSize,float32):left;
                    source.LeftSamples[source.Index]=Math.Clamp(left,-1f,1f);
                    source.RightSamples[source.Index]=Math.Clamp(right,-1f,1f);
                    source.Index=(source.Index+1)%source.LeftSamples.Length;
                    source.Available=Math.Min(source.Available+1,
                        source.LeftSamples.Length);
                }
                long now=Environment.TickCount64;
                if(source.Available<source.LeftSamples.Length||
                   now-source.LastComputedMs<80)return;
                source.LastComputedMs=now;
                float[] leftBands=Analyze(source.LeftSamples,source.Index,
                    format.SampleRate);
                float[] rightBands=Analyze(source.RightSamples,source.Index,
                    format.SampleRate);
                var old=source.Latest;
                for(int i=0;i<BandCount;i++)
                {
                    leftBands[i]=Math.Max(leftBands[i],old.Left[i]*.58f);
                    rightBands[i]=Math.Max(rightBands[i],old.Right[i]*.58f);
                }
                Volatile.Write(ref source.Latest,
                    new(leftBands,rightBands,DateTimeOffset.UtcNow,
                        source.Device.FriendlyName));
            }
        }
        catch{/* Endpoint stopped or changed; never crash the OLED. */}
    }

    private static float Sample(byte[] bytes,int offset,bool float32)
    {
        if(float32)
        {
            float v=BitConverter.ToSingle(bytes,offset);
            return float.IsFinite(v)?v:0;
        }
        return BitConverter.ToInt16(bytes,offset)/32768f;
    }

    internal static float[] Analyze(float[] ring,int write,int sampleRate)
    {
        int size=ring.Length;
        var output=new float[BandCount];
        for(int band=0;band<output.Length;band++)
        {
            double hz=75*Math.Pow(13000.0/75,band/11.0);
            double coefficient=2*Math.Cos(2*Math.PI*hz/sampleRate);
            double a=0,b=0;
            for(int i=0;i<size;i++)
            {
                double window=.5-.5*Math.Cos(2*Math.PI*i/(size-1));
                double sample=ring[(write+i)%size]*window;
                double value=sample+coefficient*a-b;
                b=a;a=value;
            }
            double power=Math.Max(0,a*a+b*b-coefficient*a*b);
            double amplitude=Math.Sqrt(power)/(size*.24);
            output[band]=(float)Math.Clamp(Math.Sqrt(amplitude)*2,0,1);
        }
        return output;
    }

    public void Stop()
    {
        foreach(var source in sources)
            source.Dispose();
        sources.Clear();
    }

    public void Dispose()=>Stop();
}

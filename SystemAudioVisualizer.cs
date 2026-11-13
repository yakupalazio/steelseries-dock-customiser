using System;
using System.Threading;
using NAudio.Wave;

namespace SteelseriesDockCustomiser;

internal sealed record AudioVisualizationSnapshot(
    float[] Left,float[] Right,DateTimeOffset UpdatedAt)
{
    internal static AudioVisualizationSnapshot Silent {get;}=
        new(new float[12],new float[12],DateTimeOffset.MinValue);

    internal bool Fresh=>UpdatedAt!=DateTimeOffset.MinValue&&
        DateTimeOffset.UtcNow-UpdatedAt<TimeSpan.FromSeconds(2);
}

/// <summary>
/// Opt-in desktop-output WASAPI loopback: PCM is used in RAM for FFT-style
/// frequency envelopes only. No microphone, recordings, raw audio retention,
/// disk export, cloud upload or process-scoped spying. One capture per app.
/// </summary>
internal sealed class SystemAudioVisualizer : IDisposable
{
#pragma warning disable CS0618 // Legacy NAudio API kept for stable callback contract.
    private WasapiLoopbackCapture? capture;
#pragma warning restore CS0618
    private readonly object gate=new();
    private AudioVisualizationSnapshot latest=AudioVisualizationSnapshot.Silent;
    private DateTimeOffset nextRetry=DateTimeOffset.MinValue;
    private readonly float[] leftSamples=new float[2048];
    private readonly float[] rightSamples=new float[2048];
    private int writeIndex,availableFrames;
    private long lastComputedMs;

    public AudioVisualizationSnapshot Snapshot=>Volatile.Read(ref latest);
    public bool Enabled=>capture!=null;

    public void SetEnabled(bool enabled)
    {
        if(enabled)
        {
            if(capture is not null||DateTimeOffset.UtcNow<nextRetry)return;
            try
            {
#pragma warning disable CS0618
                var started=new WasapiLoopbackCapture();
#pragma warning restore CS0618
                started.DataAvailable+=ReadAudio;
                started.RecordingStopped+=(_,_)=>Stop();
                started.StartRecording();
                capture=started;
            }
            catch
            {
                nextRetry=DateTimeOffset.UtcNow+TimeSpan.FromSeconds(20);
                Stop();
            }
        }
        else Stop();
    }

    private void ReadAudio(object? sender,WaveInEventArgs e)
    {
        try
        {
            var format=capture?.WaveFormat;
            if(format is null)return;
            int channels=Math.Clamp(format.Channels,1,8);
            // Current Windows 11 audio endpoints often use
            // WAVEFORMATEXTENSIBLE even for plain stereo float PCM.
            var extended=format as WaveFormatExtensible;
            bool ieeeSubtype=extended?.SubFormat==
                new Guid("00000003-0000-0010-8000-00aa00389b71");
            bool pcmSubtype=extended?.SubFormat==
                new Guid("00000001-0000-0010-8000-00aa00389b71");
            bool ieeeFloat=(format.Encoding==WaveFormatEncoding.IeeeFloat ||
                ieeeSubtype)&&format.BitsPerSample==32;
            bool pcm16=(format.Encoding==WaveFormatEncoding.Pcm ||
                pcmSubtype)&&format.BitsPerSample==16;
            if(!ieeeFloat&&!pcm16)return;
            int sampleBytes=ieeeFloat?4:2;
            int frameBytes=channels*sampleBytes;
            if(frameBytes<=0)return;
            lock(gate)
            {
                for(int offset=0;offset+frameBytes<=e.BytesRecorded;offset+=frameBytes)
                {
                    float l=Sample(e.Buffer,offset,ieeeFloat);
                    float r=channels>1
                        ?Sample(e.Buffer,offset+sampleBytes,ieeeFloat):l;
                    leftSamples[writeIndex]=Math.Clamp(l,-1f,1f);
                    rightSamples[writeIndex]=Math.Clamp(r,-1f,1f);
                    writeIndex=(writeIndex+1)%leftSamples.Length;
                    availableFrames=Math.Min(availableFrames+1,leftSamples.Length);
                }
                long now=Environment.TickCount64;
                if(availableFrames<leftSamples.Length ||
                   now-lastComputedMs<70)return;
                lastComputedMs=now;
                float[] lBands=Analyze(leftSamples,writeIndex,
                    format.SampleRate);
                float[] rBands=Analyze(rightSamples,writeIndex,
                    format.SampleRate);
                // A release envelope smooths transitions without inventing
                // signal while silence is captured.
                var prior=latest;
                for(int i=0;i<12;i++)
                {
                    lBands[i]=Math.Max(lBands[i],prior.Left[i]*.62f);
                    rBands[i]=Math.Max(rBands[i],prior.Right[i]*.62f);
                }
                Volatile.Write(ref latest,new(lBands,rBands,
                    DateTimeOffset.UtcNow));
            }
        }
        catch{/* A transient endpoint format must not crash OLED rendering. */}
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
        var output=new float[12];
        for(int band=0;band<output.Length;band++)
        {
            // 75 Hz .. 13 kHz logarithmic centers: perceptually useful
            // separation across bass, mids, highs in a tiny OLED.
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
            // Convert narrow-band magnitude to visually usable level.
            double amplitude=Math.Sqrt(power)/(size*.24);
            output[band]=(float)Math.Clamp(Math.Sqrt(amplitude)*2,0,1);
        }
        return output;
    }

    public void Stop()
    {
        var old=capture;
        if(old is null)return;
        capture=null;
        try{old.DataAvailable-=ReadAudio;old.StopRecording();}
        catch{}
        try{old.Dispose();}catch{}
        Volatile.Write(ref latest,AudioVisualizationSnapshot.Silent);
    }

    public void Dispose()=>Stop();
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SteelseriesDockCustomiser;

internal sealed record MonoGifFrame(int Width,int Height,bool[] Pixels,int DelayMs);
internal sealed record MonoGifAnimation(IReadOnlyList<MonoGifFrame> Frames,int TotalMs)
{
    internal MonoGifFrame? At(long elapsedMs,int speedPercent)
    {
        if(Frames.Count==0||TotalMs<=0)return null;
        long position=elapsedMs*Math.Clamp(speedPercent,25,400)/100;
        position=(position%TotalMs+TotalMs)%TotalMs;
        foreach(var frame in Frames)
        {
            if(position<frame.DelayMs)return frame;
            position-=frame.DelayMs;
        }
        return Frames[^1];
    }
}

/// <summary>
/// GIF importer and background one-bit converter. Source GIFs are copied
/// into user-local storage only. The EXE never bundles customer GIFs or
/// source-device information. No video/audio/network dependencies.
/// Conversion is background-only, capped and lazy until the widget exists.
/// </summary>
internal static class AnimatedGifWidgetStore
{
    private static string Root=>Path.Combine(
        Beta5Sandbox.SettingsDirectory,"AnimatedGifs");
    private static readonly ConcurrentDictionary<string,MonoGifAnimation> Cache=new();
    private static readonly ConcurrentDictionary<string,Task> Inflight=new();
    internal const int MaxInputBytes=12*1024*1024;
    internal const int MaxDecodedFrames=160;

    internal static string Import(string path)
    {
        if(!File.Exists(path)||!Path.GetExtension(path).Equals(
            ".gif",StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Lütfen bir GIF dosyası seçin.");
        var fi=new FileInfo(path);
        if(fi.Length<14||fi.Length>MaxInputBytes)
            throw new InvalidDataException("GIF dosyası 12 MB sınırını aşıyor.");
        using(var input=File.OpenRead(path))
        {
            Span<byte> magic=stackalloc byte[6];
            if(input.Read(magic)!=magic.Length ||
               !magic.SequenceEqual("GIF87a"u8)&&
               !magic.SequenceEqual("GIF89a"u8))
                throw new InvalidDataException("Dosya geçerli bir GIF değil.");
        }
        Directory.CreateDirectory(Root);
        string name=Guid.NewGuid().ToString("N")+".gif";
        string target=Path.Combine(Root,name);
        File.Copy(path,target,false);
        return name;
    }

    internal static bool IsSafeId(string? name)=>
        name is {Length:36} &&
        name.EndsWith(".gif",StringComparison.OrdinalIgnoreCase) &&
        name[..32].All(Uri.IsHexDigit);

    internal static string? SourceFile(string? id)=>
        IsSafeId(id)?Path.Combine(Root,id!):null;

    private static string Key(DockWidget widget)=>
        widget.GifAssetId+"|"+widget.WidgetWidth+"|"+
        widget.VisualizerHeight+"|"+widget.GifFit+"|"+
        widget.GifDither+"|"+widget.GifThreshold;

    internal static MonoGifFrame? Frame(DockWidget widget,DateTimeOffset now)
    {
        if(!IsSafeId(widget.GifAssetId))return null;
        string key=Key(widget);
        if(!Cache.TryGetValue(key,out var animation))
        {
            EnsureBackground(widget);
            return null;
        }
        return animation.At(now.ToUnixTimeMilliseconds(),
            widget.GifSpeedPercent);
    }

    internal static void EnsureBackground(DockWidget widget)
    {
        string? file=SourceFile(widget.GifAssetId);
        if(file is null||!File.Exists(file))return;
        string key=Key(widget);
        if(Cache.ContainsKey(key)||Inflight.ContainsKey(key))return;
        string fit=widget.GifFit,dither=widget.GifDither;
        int width=widget.WidgetWidth,height=widget.VisualizerHeight;
        int threshold=widget.GifThreshold;
        var task=Task.Run(()=>
        {
            try
            {
                var animation=Decode(file,width,height,fit,dither,threshold);
                // Cache failures too: a corrupt GIF must not trigger an
                // expensive re-decode every 80 ms while the user edits.
                if(Cache.Count>=9)Cache.Clear();
                Cache[key]=animation;
            }
            catch{Cache[key]=new(Array.Empty<MonoGifFrame>(),0);}
            finally{Inflight.TryRemove(key,out _);}
        });
        Inflight.TryAdd(key,task);
    }

    internal static MonoGifAnimation Decode(string path,int width,int height,
        string fit,string dithering,int threshold)
    {
        if(!File.Exists(path)||new FileInfo(path).Length>MaxInputBytes)
            return new(Array.Empty<MonoGifFrame>(),0);
        width=Math.Clamp(width,16,128);
        height=Math.Clamp(height,8,54);
        threshold=Math.Clamp(threshold,20,235);
        using var source=Image.FromFile(path);
        if(source.Width>3000||source.Height>3000||
           source.Width*source.Height>3_000_000)
            return new(Array.Empty<MonoGifFrame>(),0);
        var dimension=FrameDimension.Time;
        int frameCount;
        try{frameCount=source.GetFrameCount(dimension);}
        catch{frameCount=1;}
        frameCount=Math.Clamp(frameCount,1,MaxDecodedFrames);
        frameCount=Math.Min(frameCount,Math.Max(1,
            70_000_000/(source.Width*source.Height)));
        byte[]? delays=null;
        try{delays=source.GetPropertyItem(0x5100)?.Value;}
        catch(ArgumentException){}
        var frames=new List<MonoGifFrame>(frameCount);
        int total=0;
        for(int i=0;i<frameCount;i++)
        {
            if(source.FrameDimensionsList.Contains(dimension.Guid))
                source.SelectActiveFrame(dimension,i);
            using var resized=new Bitmap(width,height,PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(resized))
            {
                g.Clear(Color.Black);
                g.InterpolationMode=InterpolationMode.HighQualityBilinear;
                g.CompositingQuality=CompositingQuality.HighQuality;
                g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                RectangleF dest=ComputeRect(source.Width,source.Height,
                    width,height,fit);
                g.DrawImage(source,dest);
            }
            var mono=ConvertToMono(resized,dithering,threshold);
            int delay=100;
            if(delays is not null && delays.Length>=4*(i+1))
                delay=(int)BitConverter.ToUInt32(delays,i*4)*10;
            delay=Math.Clamp(delay,60,1000);
            frames.Add(new(width,height,mono,delay));
            total+=delay;
        }
        return new(frames,total);
    }

    internal static RectangleF ComputeRect(int sw,int sh,int dw,int dh,
        string fit)
    {
        if(fit=="Stretch")return new RectangleF(0,0,dw,dh);
        float scale=fit=="Cover"
            ?Math.Max(dw/(float)sw,dh/(float)sh)
            :Math.Min(dw/(float)sw,dh/(float)sh);
        float w=sw*scale,h=sh*scale;
        return new RectangleF((dw-w)/2,(dh-h)/2,w,h);
    }

    internal static bool[] ConvertToMono(Bitmap src,string mode,int threshold)
    {
        int width=src.Width,height=src.Height;
        var result=new bool[width*height];
        var pixels=new float[result.Length];
        for(int y=0;y<height;y++)
        for(int x=0;x<width;x++)
        {
            var c=src.GetPixel(x,y);
            pixels[y*width+x]=(c.R*.2126f+c.G*.7152f+c.B*.0722f)*
                c.A/255f;
        }
        int[,] bayer={{0,8,2,10},{12,4,14,6},{3,11,1,9},{15,7,13,5}};
        for(int y=0;y<height;y++)
        for(int x=0;x<width;x++)
        {
            int idx=y*width+x;
            float level=pixels[idx];
            float limit=mode=="Bayer"
                ?threshold+(bayer[y%4,x%4]-7.5f)*12f:threshold;
            bool lit=level>=limit;
            result[idx]=lit;
            if(mode!="FloydSteinberg")continue;
            float err=level-(lit?255:0);
            Add(x+1,y,err*7f/16f);
            Add(x-1,y+1,err*3f/16f);
            Add(x,y+1,err*5f/16f);
            Add(x+1,y+1,err/16f);
        }
        return result;

        void Add(int x,int y,float amount)
        {
            if(x>=0&&y>=0&&x<width&&y<height)
                pixels[y*width+x]=Math.Clamp(
                    pixels[y*width+x]+amount,0,255);
        }
    }
}

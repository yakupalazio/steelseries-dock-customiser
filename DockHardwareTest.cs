using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Explicit user-initiated physical OLED output.
/// Delegates to independently released, SHA-verified ggoled v0.3.0.
/// Only 'img' and 'return' commands; no firmware or brightness change.
/// </summary>
internal sealed class DockHardwareTest
{
    public const string ExpectedCliSha256 =
        "C609B8D7E93441166CB92D652D476AE1C7DB89FDEC542ECEB389CD332CC47616";

    private readonly string cliPath=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Documents","Codex","DockHardwareResearch","ggoled-v0.3.0.exe");
    private volatile bool staticApplied,staticApplying;
    private readonly SemaphoreSlim liveGate=new(1,1);
    private FastOledTransport? directLive;
    public bool Active=>staticApplied||staticApplying;
    public const int EditableY=10;
    public const int EditableHeight=54;
    // HID packet heights are padded to a multiple of eight pixels.
    // Main patch: rows 10..57 (48 high). Final patch: 56..63 (8 high).
    // Overlap on rows 56..57 is intentional; no header row is transmitted.
    public const int MainY=10, MainHeight=48;
    public const int TailY=56, TailHeight=8;
    public static Bitmap ExtractEditableArea(Bitmap full) =>
        CropPatch(full,MainY,MainHeight);
    public static Bitmap ExtractBottomTail(Bitmap full) =>
        CropPatch(full,TailY,TailHeight);
    private static Bitmap CropPatch(Bitmap full,int y,int h)
    {
        if(full.Width!=128||full.Height!=64)
            throw new InvalidDataException("Only full 128×64 native frames are accepted.");
        return full.Clone(new Rectangle(0,y,128,h),PixelFormat.Format24bppRgb);
    }
    public string Validate()
    {
        if(!File.Exists(cliPath))
            return "ggoled v0.3.0 aracı bulunamadı. Araç yalnızca resmî GitHub sürümünden edinilmeli.";
        string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cliPath)));
        return hash.Equals(ExpectedCliSha256,StringComparison.OrdinalIgnoreCase)
            ? "" : "ggoled dosyasının SHA-256 kimliği doğrulanamadı. Donanım testi engellendi.";
    }

    /// <summary>
    /// Applies the chosen 128x64 design once and leaves it on the OLED until
    /// Reset/Restore. No ten-second timer and no background writer.
    /// All hardware writes still require confirmation at the UI layer.
    /// </summary>
    public async Task<string> ApplyStaticAsync(Bitmap image,bool protectHeader=true)
    {
        if(staticApplying)return "Dock uygulama işlemi zaten sürüyor.";
        if(image.Width!=128||image.Height!=64)
            return "Tasarım tam 128×64 piksel olmalı.";
        string problem=Validate();
        if(problem!="")return problem;
        staticApplying=true;
        await liveGate.WaitAsync().ConfigureAwait(false);
        string folder=Path.Combine(Path.GetTempPath(),"SteelseriesDockCustomiser");
        Directory.CreateDirectory(folder);
        string main=Path.Combine(folder,"applied-dock-main-128x48.png");
        string tail=Path.Combine(folder,"applied-dock-tail-128x8.png");
        string full=Path.Combine(folder,"applied-dock-full-128x64.png");
        try
        {
            using(var editable=ExtractEditableArea(image))
                editable.Save(main,ImageFormat.Png);
            using(var remainder=ExtractBottomTail(image))
                remainder.Save(tail,ImageFormat.Png);
            if(protectHeader)
            {
                await Task.Run(()=>
                {
                    RunCli("img","-x","0","-y","10",main);
                    RunCli("img","-x","0","-y","56",tail);
                }).ConfigureAwait(false);
            }
            else
            {
                image.Save(full,ImageFormat.Png);
                await Task.Run(()=>RunCli("img","-C",full)).ConfigureAwait(false);
            }
            staticApplied=true;
            return "Tasarım dock'a uygulandı. Orijinal ekrana dönmek için Reset'i kullan.";
        }
        catch(Exception ex)
        {
            staticApplied=false;
            try {RunCli("return");}catch { /* Reset remains available. */ }
            return "Uygulama hatası: "+ex.Message+"; orijinal ekrana dönüş denendi.";
        }
        finally
        {
            try{File.Delete(main);}catch{}
            try{File.Delete(tail);}catch{}
            try{File.Delete(full);}catch{}
            staticApplying=false;
            liveGate.Release();
        }
    }

    /// <summary>
    /// Sends exactly two bounded patches; caller must explicitly opt in and
    /// throttle calls. Never clears the full screen or alters the factory header.
    /// </summary>
    public async Task SendLiveFrameAsync(Bitmap frame,bool protectHeader=true)
    {
        if(Active)throw new InvalidOperationException("Sabit tasarım uygulanmış. Önce Reset yap.");
        string validation=Validate();
        if(validation!="")throw new InvalidOperationException(validation);
        await liveGate.WaitAsync().ConfigureAwait(false);
        string folder=Path.Combine(Path.GetTempPath(),"SteelseriesDockCustomiser");
        Directory.CreateDirectory(folder);
        string main=Path.Combine(folder,"live-dock-main-128x48.png");
        string tail=Path.Combine(folder,"live-dock-tail-128x8.png");
        string full=Path.Combine(folder,"live-dock-full-128x64.png");
        try
        {
            // A persistent OLED HID session is substantially faster than
            // spawning two separate ggoled.exe processes per frame. Its
            // 0x93 feature writes are the same bounded drawing operations;
            // audio and the knob's vendor-input interface are untouched.
            bool sentDirect=false;
            try
            {
                await Task.Run(()=>
                {
                    if(directLive==null)
                    {
                        var device=new FastOledTransport();
                        try{device.Open();directLive=device;}
                        catch{device.Dispose();throw;}
                    }
                    directLive.SendLiveFrame(frame,protectHeader);
                }).ConfigureAwait(false);
                sentDirect=true;
            }
            catch
            {
                directLive?.Dispose();
                directLive=null;
            }
            if(!sentDirect)
            {
                using(var top=ExtractEditableArea(frame))top.Save(main,ImageFormat.Png);
                using(var end=ExtractBottomTail(frame))end.Save(tail,ImageFormat.Png);
                if(protectHeader)
                    await Task.Run(()=>
                    {
                        RunCli("img","-x","0","-y","10",main);
                        RunCli("img","-x","0","-y","56",tail);
                    }).ConfigureAwait(false);
                else
                {
                    frame.Save(full,ImageFormat.Png);
                    await Task.Run(()=>RunCli("img","-C",full)).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            try{File.Delete(main);}catch{}
            try{File.Delete(tail);}catch{}
            try{File.Delete(full);}catch{}
            liveGate.Release();
        }
    }

    /// <summary>
    /// Experimental beta recovery: one release + immediate redraw, called
    /// only after a decoded physical dial burst has been silent for 450 ms.
    /// Retains the native top 10 rows unless full-screen mode was authorized.
    /// A missing HID mode-button event cannot be inferred from this method.
    /// </summary>
    public async Task ReclaimLiveFrameAsync(Bitmap frame,bool protectHeader)
    {
        if(Active)throw new InvalidOperationException("Önce Dock Reset yap.");
        string validation=Validate();
        if(validation!="")throw new InvalidOperationException(validation);
        await liveGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if(directLive==null)
            {
                var device=new FastOledTransport();
                try{device.Open();directLive=device;}
                catch{device.Dispose();throw;}
            }
            await Task.Run(()=>directLive.ReclaimNativeOverlay(frame,protectHeader))
                .ConfigureAwait(false);
        }
        catch
        {
            directLive?.Dispose();
            directLive=null;
            throw;
        }
        finally{liveGate.Release();}
    }

    public string RestoreNow()
    {
        string validation=Validate();
        if(validation!="")return validation;
        if(!liveGate.Wait(TimeSpan.FromSeconds(16)))
            return "Dock güncellemesi sürüyor; dönüş komutu henüz gönderilemedi.";
        try
        {
            directLive?.Dispose();
            directLive=null;
            RunCli("return");
            staticApplied=false;
            return "SteelSeries varsayılan OLED ekranına dönüş komutu başarılı.";
        }
        catch(Exception ex){return "Dock'a dönüş hatası: "+ex.Message;}
        finally{liveGate.Release();}
    }

    private void RunCli(params string[] arguments)
    {
        if(arguments.Length==0||arguments[0] is not ("img" or "return"))
            throw new InvalidOperationException("Only image/return commands allowed.");
        using var process=new Process();
        process.StartInfo=new ProcessStartInfo(cliPath)
        {
            UseShellExecute=false,
            CreateNoWindow=true,
            RedirectStandardOutput=true,
            RedirectStandardError=true
        };
        foreach(var arg in arguments)process.StartInfo.ArgumentList.Add(arg);
        if(!process.Start())throw new IOException("ggoled işlemi açılamadı.");
        if(!process.WaitForExit(7000))
        {
            process.Kill(entireProcessTree:true);
            throw new TimeoutException("ggoled 7 saniyede yanıt vermedi.");
        }
        string error=process.StandardError.ReadToEnd();
        if(process.ExitCode!=0)
            throw new IOException("ggoled exit="+process.ExitCode+
                (error.Length>0?"; "+error[..Math.Min(error.Length,200)]:""));
    }
}

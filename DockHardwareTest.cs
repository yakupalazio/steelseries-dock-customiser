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
/// Explicit user-initiated, time-limited physical OLED test.
/// Delegates to the independently released ggoled v0.3.0 CLI. Only 'img'
/// and 'return' commands are permitted. No persistent service, firmware,
/// brightness setting, unverified raw USB command, or background loop.
/// </summary>
internal sealed class DockHardwareTest
{
    public const string ExpectedCliSha256 =
        "C609B8D7E93441166CB92D652D476AE1C7DB89FDEC542ECEB389CD332CC47616";

    private readonly string cliPath=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Documents","Codex","DockHardwareResearch","ggoled-v0.3.0.exe");
    private CancellationTokenSource? active;
    public bool Active=>active!=null;
    public const int EditableY=12;
    public const int EditableHeight=52;
    // HID packet heights are padded to a multiple of eight pixels.
    // Main patch: rows 12..59 (48 high). Final patch: 56..63 (8 high).
    // Overlap on rows 56..59 is intentional; no header row is transmitted.
    public const int MainY=12, MainHeight=48;
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

    public async Task<string> ShowForTenSecondsAsync(Bitmap image)
    {
        if(Active)return "Zaten çalışan bir dock testi var.";
        if(image.Width!=128||image.Height!=64)
            return "Test görüntüsü tam 128×64 piksel olmalı.";
        string problem=Validate();
        if(problem!="")return problem;
        var cts=new CancellationTokenSource();
        active=cts;
        string folder=Path.Combine(Path.GetTempPath(),"SteelseriesDockCustomiser");
        Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"one-shot-dock-main-128x48.png");
        string tailFile=Path.Combine(folder,"one-shot-dock-tail-128x8.png");
        using(var editable=ExtractEditableArea(image))
            editable.Save(file,ImageFormat.Png);
        using(var tail=ExtractBottomTail(image))
            tail.Save(tailFile,ImageFormat.Png);
        string result="12–63. satırlar için 10 saniyelik test tamamlandı.";
        try
        {
            // Never '-C': only these two bounded areas are changed.
            await Task.Run(()=>RunCli("img","-x","0","-y","12",file));
            await Task.Run(()=>RunCli("img","-x","0","-y","56",tailFile));
            await Task.Delay(TimeSpan.FromSeconds(10),cts.Token);
        }
        catch(OperationCanceledException) {result="Test erken sonlandırıldı.";}
        catch(Exception ex) {result="Görüntü gönderilemedi: "+ex.Message;}
        finally
        {
            try {await Task.Run(()=>RunCli("return"));result+=" SteelSeries ekranına dönüş komutu gönderildi.";}
            catch(Exception ex) {result+=" EKRANA DÖNÜŞ HATASI: "+ex.Message;}
            active=null; cts.Dispose();
            try{File.Delete(file);}catch{}
            try{File.Delete(tailFile);}catch{}
        }
        return result;
    }

    public void Stop()=>active?.Cancel();

    public string RestoreNow()
    {
        Stop();
        string validation=Validate();
        if(validation!="")return validation;
        try{RunCli("return");return "SteelSeries varsayılan OLED ekranına dönüş komutu başarılı.";}
        catch(Exception ex){return "Dock'a dönüş hatası: "+ex.Message;}
    }

    private void RunCli(params string[] arguments)
    {
        if(arguments.Length==0||arguments[0] is not ("img" or "return"))
            throw new InvalidOperationException("Only temporary image/return commands allowed.");
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

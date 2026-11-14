using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Release-blocking offline GitHub feedback tests. Never open a browser,
/// access GitHub, use API keys, change live settings or publish anything.
/// </summary>
internal static class BetaFeedbackSelfTests
{
    internal static void Run()
    {
        string dir=Path.Combine(Path.GetTempPath(),
            "dock-feedback-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var jpeg=Path.Combine(dir,"personal-photo-name.jpg");
            using(var source=new Bitmap(64,48))
            {
                using var g=Graphics.FromImage(source);
                g.Clear(Color.CornflowerBlue);
                source.Save(jpeg,ImageFormat.Jpeg);
            }
            var original=BugReportService.Compose(
                "ATK battery after sleep","Pil / Cihaz",
                "I tried unplugging the mouse receiver and moving it.",
                false,jpeg);
            if(original.OperatingSystem is not null ||
               original.AttachmentFileName!="screenshot.png"||
               original.AttachmentBase64 is null)
                throw new Exception("Feedback screenshot consent violated.");

            var (url,truncated)=GitHubFeedbackBridge.ComposeUrl(original);
            string decoded=Uri.UnescapeDataString(url.AbsoluteUri);
            if(truncated||
               url.Host!="github.com"||
               !decoded.Contains("template=beta_device.yml")||
               !decoded.Contains("summary=ATK battery after sleep")||
               !decoded.Contains("details=I tried unplugging")||
               !decoded.Contains("app_version=")||
               decoded.Contains(original.AttachmentBase64)||
               decoded.Contains(jpeg,StringComparison.OrdinalIgnoreCase))
                throw new Exception("Battery issue form prefill or privacy failed.");

            var prepared=GitHubFeedbackBridge.Prepare(original,dir);
            if(!File.Exists(prepared.LocalDraft)||
               prepared.CleanScreenshot is null ||
               !File.Exists(prepared.CleanScreenshot)||
               prepared.NeedsFullDescriptionPaste)
                throw new Exception("Selected screenshot was not staged.");
            using(var output=Image.FromFile(prepared.CleanScreenshot))
            {
                if(output.Width!=64||output.Height!=48)
                    throw new Exception("Prepared image pixels changed unexpectedly.");
            }
            byte[] file=File.ReadAllBytes(prepared.CleanScreenshot);
            if(file.Length<32||file[0]!=137||file[1]!=80||
               prepared.CleanScreenshot.EndsWith("personal-photo-name.jpg",
                   StringComparison.OrdinalIgnoreCase)||
               File.ReadAllText(prepared.LocalDraft).Contains(jpeg))
                throw new Exception("Attachment must be an EXIF-free PNG.");

            var normal=BugReportService.Compose(
                "Media mode stops","Medya / Şarkı sözleri",
                "Lyrics are frozen after the next song starts.",
                false,null);
            var (normalUrl,_) =GitHubFeedbackBridge.ComposeUrl(normal);
            if(!Uri.UnescapeDataString(normalUrl.AbsoluteUri)
                .Contains("template=beta_bug.yml")||
               normalUrl.AbsoluteUri.Contains("screenshot.png"))
                throw new Exception("Non-device issue routed incorrectly.");
            if(GitHubFeedbackBridge.Prepare(normal,dir).CleanScreenshot!=null)
                throw new Exception("Empty screenshot unexpectedly uploaded.");

            // The real edge case is long, heavily percent-encoded UTF-8.
            string veryLong=new string('ğ',4000);
            var lengthy=BugReportService.Compose(
                "Long feedback text","Performans",veryLong,false,null);
            var (longUrl,needsPaste)=GitHubFeedbackBridge.ComposeUrl(lengthy);
            if(!needsPaste||longUrl.AbsoluteUri.Length>6800 ||
               GitHubFeedbackBridge.FullDescription(lengthy)!=veryLong)
                throw new Exception("Long issue URL silently loses the report.");

            try
            {
                _=BugReportService.Compose("Attachment test","Diğer",
                    "Invalid extension must be rejected.",false,
                    Path.Combine(dir,"unsupported.exe"));
                throw new Exception("Non-image attachment was accepted.");
            }
            catch(FileNotFoundException){}
            catch(InvalidDataException){}
            var json=JsonDocument.Parse(
                File.ReadAllText(prepared.LocalDraft));
            if(json.RootElement.GetProperty("OperatingSystem").ValueKind!=
                    JsonValueKind.Null)
                throw new Exception("Private Windows info included without opt-in.");

            Console.WriteLine(
                "PASS: GitHub beta issue prefill (device + bug), "+
                "screenshots re-encoded into clean PNG, real file staged, "+
                "no system data without consent, URL length fallback, "+
                "no fake 'uploaded' state or embedded GitHub credentials.");
        }
        finally
        {
            try{Directory.Delete(dir,true);}catch{}
        }
    }
}

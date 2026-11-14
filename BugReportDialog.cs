using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Client side of private bug intake. No GitHub secret inside a distributed
/// binary. An optional HTTPS URL in a LOCAL config file is the only remote
/// dependency. Without it, reports are saved as explicitly unsent drafts.
/// </summary>
internal static class BugReportService
{
    internal static readonly string[] Categories=
        ["OLED / Dock","Medya / Şarkı sözleri","Pil / Cihaz",
        "Arayüz / Animasyon","Performans","Diğer"];
    private sealed class Config { public string Endpoint {get;set;}=""; }
    internal sealed record Report(string Title,string Category,
        string Description,string AppVersion,DateTimeOffset RecordedAt,
        string? OperatingSystem,string? AttachmentFileName,
        string? AttachmentBase64);
    internal sealed record Result(bool Uploaded,string Message,string? LocalPath);
    private static readonly HttpClient Http=new(){Timeout=TimeSpan.FromSeconds(12)};
    internal static string ConfigFile=>Path.Combine(
        Beta5Sandbox.SettingsDirectory,"bug-report-endpoint-v1.json");
    internal static string QueueFolder=>Path.Combine(
        Beta5Sandbox.SettingsDirectory,"unsent-bug-reports");

    internal static Report Compose(string title,string category,string detail,
        bool includeSystem,string? attachedFile)
    {
        title=title.Trim();detail=detail.Trim();
        if(title.Length<5||title.Length>120)
            throw new ArgumentException("Başlık 5–120 karakter olmalı.");
        if(detail.Length<10||detail.Length>4000)
            throw new ArgumentException("Hata açıklaması 10–4000 karakter olmalı.");
        if(!Categories.Contains(category))
            throw new ArgumentException("Geçersiz hata kategorisi.");
        string? encoded=null,name=null;
        if(!string.IsNullOrEmpty(attachedFile))
        {
            var source=new FileInfo(attachedFile);
            if(!source.Exists||source.Length>8_000_000)
                throw new InvalidDataException("Ekran görüntüsü 8 MB'dan küçük olmalı.");
            var extension=source.Extension.ToLowerInvariant();
            if(extension is not (".png" or ".jpg" or ".jpeg"))
                throw new InvalidDataException("Yalnızca PNG/JPG ekran görüntüsü ekleyebilirsin.");
            // Re-encode into a clean PNG. EXIF, GPS and the original local
            // filename are never transmitted; large captures are downscaled.
            using var stream=File.OpenRead(attachedFile);
            using var image=Image.FromStream(stream);
            if(image.Width<1||image.Height<1||image.Width>8192||
               image.Height>8192||(long)image.Width*image.Height>30_000_000)
                throw new InvalidDataException("Görsel boyutları desteklenmiyor.");
            double scale=Math.Min(1,Math.Min(
                1920.0/image.Width,1080.0/image.Height));
            using var clean=new Bitmap(
                Math.Max(1,(int)Math.Round(image.Width*scale)),
                Math.Max(1,(int)Math.Round(image.Height*scale)),
                PixelFormat.Format24bppRgb);
            using(var graphics=Graphics.FromImage(clean))
            {
                graphics.Clear(Color.Black);
                graphics.DrawImage(image,new Rectangle(
                    0,0,clean.Width,clean.Height));
            }
            using var output=new MemoryStream();
            clean.Save(output,ImageFormat.Png);
            if(output.Length>8_000_000)
                throw new InvalidDataException(
                    "Temizlenen ekran görüntüsü 8 MB sınırını aşıyor.");
            encoded=Convert.ToBase64String(output.ToArray());
            name="screenshot.png";
        }
        var version=typeof(BugReportService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion??"Unknown";
        return new(title,category,detail,version,DateTimeOffset.UtcNow,
            includeSystem?Environment.OSVersion.VersionString:null,
            name,encoded);
    }

    internal static Uri? Endpoint()
    {
        try
        {
            if(!File.Exists(ConfigFile))return null;
            var content=JsonSerializer.Deserialize<Config>(
                File.ReadAllText(ConfigFile));
            if(!Uri.TryCreate(content?.Endpoint,UriKind.Absolute,out var url)||
               url.Scheme!=Uri.UriSchemeHttps||!string.IsNullOrEmpty(url.UserInfo))
                return null;
            return url;
        }
        catch{return null;}
    }

    internal static async Task<Result> SubmitAsync(Report report)
    {
        Uri? endpoint=Endpoint();
        if(endpoint is not null)
        {
            try
            {
                using var response=await Http.PostAsJsonAsync(endpoint,report);
                if(response.IsSuccessStatusCode)
                    return new(true,"Rapor güvenli sunucuya ulaştı.",null);
                // Ingestion rejection must never be misreported as success.
            }
            catch(HttpRequestException){}
            catch(TaskCanceledException){}
        }
        var path=SavePending(report);
        return new(false,endpoint is null
            ?"Rapor yerel taslak olarak kaydedildi. Rapor sunucusu henüz yapılandırılmadığı için GitHub'a gönderilmedi."
            :"Rapor sunucuya ulaşamadı; gönderilmemiş yerel taslak olarak saklandı.",
            path);
    }

    internal static string SavePending(Report report)
    {
        Directory.CreateDirectory(QueueFolder);
        string name=DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss")+
            "-"+Guid.NewGuid().ToString("N")[..10]+".json";
        string file=Path.Combine(QueueFolder,name);
        using var stream=new FileStream(file,FileMode.CreateNew,FileAccess.Write);
        JsonSerializer.Serialize(stream,report,new JsonSerializerOptions{
            WriteIndented=true});
        return file;
    }
}

/// <summary>
/// Private, opt-in bug report form. No account required, no silent logging.
/// Unconfigured deployment clearly says the report is local-only.
/// </summary>
internal sealed class BugReportDialog:Form
{
    private readonly Color surface=Color.FromArgb(23,31,43);
    private readonly Color muted=Color.FromArgb(160,180,197);
    private readonly ComboBox category=new(){
        DropDownStyle=ComboBoxStyle.DropDownList,Left=30,Top=157,
        Width=590,Height=33};
    private readonly TextBox title=new(){Left=30,Top=222,Width=590,MaxLength=120};
    private readonly TextBox description=new(){
        Left=30,Top=292,Width=590,Height=130,
        Multiline=true,ScrollBars=ScrollBars.Vertical,MaxLength=4000};
    private readonly CheckBox includeSystem=new(){
        Text="Windows sürümünü tanılama bilgisine ekle",
        Left=30,Top=438,Width=590,Height=35,Checked=false};
    private readonly Label fileLabel=new(){
        Left=368,Top=483,Width=245,Height=34,AutoEllipsis=true};
    private readonly Label result=new(){
        Left=30,Top=573,Width=595,Height=59,AutoEllipsis=true};
    private string? file;
    private GitHubFeedbackBridge.Prepared? prepared;
    internal BugReportDialog()
    {
        UiPolish.Register(this);
        Text="Dock Customiser — Hata Bildir";
        BackColor=Color.FromArgb(13,18,26);
        ForeColor=Color.FromArgb(235,242,248);
        Font=new Font("Segoe UI",10);
        ClientSize=new Size(650,645);
        FormBorderStyle=FormBorderStyle.FixedDialog;
        StartPosition=FormStartPosition.CenterParent;
        MaximizeBox=false;MinimizeBox=false;
        Controls.Add(new Label{Text="Hata bildir",Left=30,Top=19,
            Height=43,Width=550,
            Font=new Font("Segoe UI Semibold",23f,FontStyle.Bold)});
        Controls.Add(new Label{
            Text="Beta geri bildirimi GitHub Issues'da herkese açık "+
                "paylaşılır. Gönderim öncesinde metni ve görseli kontrol et. "+
                "GitHub hesabıyla giriş yapılması gerekir.",
            Left=30,Top=73,Width=590,Height=52,ForeColor=muted});
        Controls.Add(Label("Kategori",132));Controls.Add(category);
        category.FormattingEnabled=true;
        category.Format+=(_,e)=>{if(e.ListItem is string key)
            e.Value=UiLanguage.T(key);};
        category.Items.AddRange(BugReportService.Categories);
        category.SelectedIndex=0;
        Controls.Add(Label("Kısa başlık",199));Controls.Add(title);
        Controls.Add(Label("Hata nasıl oluşuyor? Tekrar etme adımlarını yaz.",266));
        description.BackColor=surface;description.ForeColor=ForeColor;
        title.BackColor=surface;title.ForeColor=ForeColor;
        category.BackColor=surface;category.ForeColor=ForeColor;
        Controls.Add(description);Controls.Add(includeSystem);
        var attach=Button("Görsel ekle",30,480,142);
        attach.Click+=(_,_)=>
        {
            using var picker=new OpenFileDialog{
                Filter="Ekran görüntüsü|*.png;*.jpg;*.jpeg",
                CheckFileExists=true,Title="İsteğe bağlı ekran görüntüsü seç"};
            if(picker.ShowDialog(this)!=DialogResult.OK)return;
            file=picker.FileName;
            fileLabel.Text=Path.GetFileName(file)+" (en fazla 8 MB)";
        };
        fileLabel.ForeColor=muted;
        fileLabel.Cursor=Cursors.Hand;
        fileLabel.Click+=(_,_)=>{
            if(prepared?.CleanScreenshot is not string png)return;
            try{
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo{
                        FileName="explorer.exe",
                        Arguments="/select,\""+png+"\"",
                        UseShellExecute=true
                    })?.Dispose();
            }
            catch{}
        };
        Controls.Add(fileLabel);
        Controls.Add(attach);

        var copyImage=Button(UiLanguage.English
            ?"Copy clean image":"Temiz görseli kopyala",183,480,175);
        copyImage.Enabled=false;
        copyImage.Click+=(_,_) =>
        {
            if(prepared?.CleanScreenshot is not string png)return;
            try
            {
                using var fileImage=Image.FromFile(png);
                using var cleanBitmap=new Bitmap(fileImage);
                Clipboard.SetImage(cleanBitmap);
                result.ForeColor=Color.FromArgb(133,199,236);
                result.Text=UiLanguage.English
                    ?"Image copied. Paste it into the GitHub issue upload field, "+
                      "then press Submit new issue."
                    :"Görsel panoda. GitHub'daki dosya alanına yapıştır, "+
                      "ardından Submit new issue düğmesine bas.";
            }
            catch(Exception ex) when(ex is IOException or ArgumentException
                or System.Runtime.InteropServices.ExternalException)
            {
                result.ForeColor=Color.FromArgb(248,146,130);
                result.Text=ex.Message;
            }
        };
        Controls.Add(copyImage);

        var copyDetails=Button(UiLanguage.English
            ?"Copy full description":"Açıklamayı kopyala",30,526,208);
        copyDetails.Click+=(_,_) =>
        {
            try{Clipboard.SetText(description.Text);}
            catch{return;}
            result.ForeColor=muted;
            result.Text=UiLanguage.English
                ?"Full description copied; paste in GitHub before submitting."
                :"Tam açıklama kopyalandı; göndermeden önce GitHub'a yapıştır.";
        };
        Controls.Add(copyDetails);

        var send=Button(UiLanguage.English
            ?"Open GitHub report form":"GitHub bildirim formunu aç",
            304,526,314);
        send.Click+=(_,_) =>
        {
            send.Enabled=false;
            try
            {
                var report=BugReportService.Compose(
                    title.Text,category.SelectedItem?.ToString()??"",
                    description.Text,includeSystem.Checked,file);
                prepared=GitHubFeedbackBridge.Prepare(report);
                copyImage.Enabled=prepared.CleanScreenshot is not null;
                if(prepared.CleanScreenshot is string localScreenshot)
                {
                    fileLabel.Text=UiLanguage.English
                        ?"Screenshot ready (click to locate)"
                        :"Temiz görsel hazır (klasörde göster)";
                    new ToolTip{AutoPopDelay=12000}.SetToolTip(
                        copyImage,localScreenshot);
                }
                GitHubFeedbackBridge.OpenBrowser(prepared);
                result.ForeColor=Color.FromArgb(244,195,115);
                result.Text=prepared.NeedsFullDescriptionPaste
                    ?(UiLanguage.English
                        ?"GitHub opened. The description is too long for the URL: "+
                          "Copy full description, attach image and submit there."
                        :"GitHub açıldı. Metin uzun: açıklamayı kopyala, "+
                          "görseli ekle ve GitHub'da gönder.")
                    :(UiLanguage.English
                        ?"GitHub opened; add your screenshot and press Submit "+
                          "new issue. Nothing has been sent yet."
                        :"GitHub açıldı; görseli ekleyip Submit new issue'ya "+
                          "bas. Henüz hiçbir rapor gönderilmedi.");
            }
            catch(Exception ex) when(ex is ArgumentException or
                InvalidDataException or IOException or UnauthorizedAccessException
                or InvalidOperationException or
                System.ComponentModel.Win32Exception or
                System.Runtime.InteropServices.ExternalException)
            {
                result.ForeColor=Color.FromArgb(248,146,130);
                result.Text=(UiLanguage.English
                    ?"Could not prepare/open GitHub report: "
                    :"GitHub formu hazırlanamadı/açılamadı: ")+ex.Message;
            }
            finally {send.Enabled=true;}
        };
        Controls.Add(send);Controls.Add(result);
        result.ForeColor=muted;
        result.Text=UiLanguage.English
            ?"Signed-in GitHub account required. Form and file are public "+
              "only after you submit on GitHub."
            :"GitHub hesabı gereklidir. Rapor ve görsel yalnızca GitHub'da "+
              "sen gönderince herkese açık olur.";
        UiLanguage.ApplyTree(this);
    }
    private Label Label(string text,int y)=>new(){
        Text=text,Left=30,Top=y,Width=588,Height=24,ForeColor=muted};
    private DarkButton Button(string text,int x,int y,int width)=>new(){
        Text=text,Left=x,Top=y,Width=width,Height=39,
        BackColor=surface,ForeColor=ForeColor};
}

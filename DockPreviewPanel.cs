using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Photo-based simulation with user-confirmed Apply / Live / Reset controls.
/// Does not capture or read the physical OLED framebuffer.
/// </summary>
internal sealed class DockPreviewPanel : UserControl
{
    private sealed class PhotoSettings
    {
        public string? ImagePath { get; set; }
        public List<PointF> Corners { get; set; } = [];
        public int Brightness { get; set; } = 92;
    }

    private readonly Func<Bitmap> _frameFactory;
    private readonly Func<Bitmap> _liveFrameFactory;
    private readonly Func<bool> _protectHeader;
    private bool _embeddedStudio;
    private Rectangle _visiblePhotoCrop;
    private readonly PictureBox _photoBox = new()
    {
        Left = 16, Top = 104, Width = 770, Height = 482,
        BackColor = Color.FromArgb(18,22,30), BorderStyle = BorderStyle.FixedSingle
    };
    private readonly RadioButton _custom = new() { Text = "Custom (canlı tasarım)", Checked = true, AutoSize = true };
    private readonly RadioButton _factory = new() { Text = "SteelSeries Default (fotoğraf)", AutoSize = true };
    private readonly Button _choose = new() { Text = "Fotoğraf seç", Width = 114, Height = 31 };
    private readonly Button _restore = new() { Text = "Örnek fotoğrafa dön", Width = 154, Height = 31 };
    private readonly Button _calibrate = new() { Text = "Ekranı hizala", Width = 119, Height = 31 };
    private readonly Button _cancelCalibration = new() { Text = "İptal", Width = 70, Height = 31, Enabled = false };
    private readonly DockHardwareTest _hardware = new();
    private readonly Button _sendHardware = new() { Text="Uygula", Width=110,Height=31 };
    private readonly Button _restoreHardware = new() { Text="Dock Reset", Width=120,Height=31 };
    private readonly Button _liveToggle = new() { Text="Canlı dock modunu başlat", Width=191,Height=31 };
    // Even when GG consumes dial notifications, keep asserting the chosen
    // OLED framebuffer so the native knob overlay is short-lived.
    private readonly System.Windows.Forms.Timer _liveTimer=new() { Interval=60 };
    // The dock firmware draws its own volume overlay on physical input. Keep
    // our custom OLED frame ahead of the native animation while the wheel is
    // active, then return to the low-frequency idle refresh.
    private readonly System.Windows.Forms.Timer _dialRefreshTimer=new() { Interval=25 };
    private DateTimeOffset _dialTakeoverUntil=DateTimeOffset.MinValue;
    private bool _liveEnabled,_liveSending;
    private bool _forceNextLiveFrame;
    private int _reconnectFailures;
    private DateTimeOffset _nextReconnect=DateTimeOffset.MinValue;
    private string? _lastLiveFrame;
    private DateTimeOffset _lastLiveWrite=DateTimeOffset.MinValue;
    private readonly Label _message = new() { Left=16,Top=725,Width=775,Height=42,
        Text = "Önizleme simülasyondur. Uygula sabit görüntüyü gönderir; Reset orijinal ekranı geri yükler.",
        ForeColor=Color.FromArgb(42,90,75)
    };

    private readonly string _assetDefault = Path.Combine(AppContext.BaseDirectory, "dock-photo-local.png");
    private readonly string _settingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","dock-preview-v07.json");
    private Image? _photo;
    private Color[]? _cachedGlassBackground;
    private PhotoSettings _settings = new();
    private bool _calibrating;
    private readonly List<PointF> _newCorners = [];
    private RectangleF _imageRect;
    private readonly PointF[] _factoryCorners = [
        new(346,301),new(589,302),new(589,420),new(346,419)
    ];

    public DockPreviewPanel(Func<Bitmap> frameFactory,Func<bool>? protectHeader=null,
        Func<Bitmap>? liveFrameFactory=null)
    {
        _frameFactory = frameFactory;
        _liveFrameFactory=liveFrameFactory??frameFactory;
        _protectHeader=protectHeader??(()=>true);
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245,247,249);

        Controls.Add(new Label { Left=16,Top=14,Width=777,Height=30,
            Font=new Font("Segoe UI",15,FontStyle.Bold), Text="Dock Preview — gerçek ürün fotoğrafında önizleme" });
        var row = new FlowLayoutPanel { Left=16,Top=50,Width=770,Height=42,WrapContents=false };
        row.Controls.AddRange([_custom,_factory]);
        Controls.Add(row);
        Controls.Add(_photoBox);

        var options = new FlowLayoutPanel { Left=16,Top=592,Width=770,Height=46,WrapContents=false,AutoScroll=true };
        options.Controls.AddRange([_choose,_restore,_calibrate,_cancelCalibration]);
        Controls.Add(options);

        var hardwareRow = new FlowLayoutPanel { Left=16,Top=641,Width=770,Height=43,WrapContents=false };
        hardwareRow.Controls.AddRange([_sendHardware,_restoreHardware,_liveToggle]);
        Controls.Add(hardwareRow);
        Controls.Add(new Label { Left=16,Top=686,Width=770,Height=37,
            Text="Deneysel test: 10–63 satırları iki parçada güncellenir; üst 10 satır korunur.",
            ForeColor=Color.FromArgb(137,72,30) });
        Controls.Add(_message);

        _sendHardware.Click += async (_,_) => await ApplyDesignAsync();
        _liveToggle.Click += async (_,_) => await ToggleLiveMode();
        _liveTimer.Tick += async (_,_) => await RefreshLiveDock();
        _dialRefreshTimer.Tick += async (_,_) =>
        {
            if(DateTimeOffset.UtcNow>=_dialTakeoverUntil)
            {
                _dialRefreshTimer.Stop();
                return;
            }
            await RefreshLiveDock(force:true);
        };
        _restoreHardware.Click += (_,_) => ResetDock();
        Disposed += (_,_) =>
        {
            _liveTimer.Stop();
            _dialRefreshTimer.Stop();
            _dialTakeoverUntil=DateTimeOffset.MinValue;
            bool mustRestore=_liveEnabled||_hardware.Active||_liveSending;
            _liveEnabled=false;
            if(mustRestore)_hardware.RestoreNow();
        };
        _photoBox.Paint += (_,e) => PaintPhoto(e.Graphics);
        _photoBox.MouseClick += PhotoClick;
        _custom.CheckedChanged += (_,_) =>
        {
            if(!_custom.Checked && _liveEnabled)StopLiveMode();
            _sendHardware.Enabled=_custom.Checked&&!_liveEnabled;
            InvalidateFrame();
        };
        _factory.CheckedChanged += (_,_) => InvalidateFrame();
        _choose.Click += (_,_) => ChoosePhoto();
        _restore.Click += (_,_) => RestoreExample();
        _calibrate.Click += (_,_) => StartCalibration();
        _cancelCalibration.Click += (_,_) => EndCalibration(false);

        LoadSettings();
        LoadImage();
    }

    public void InvalidateFrame() => _photoBox.Invalidate();

    public bool LiveOutputActive => _liveEnabled;
    public bool OledWriterActive => _liveEnabled||_hardware.Active;
    public string HardwareStatus => _message.Text;
    public bool FactoryPreviewActive => _factory.Checked;

    /// <summary>
    /// A physical input triggers a native firmware overlay. Request an
    /// immediate write and briefly stream our selected content so that the
    /// native screen cannot linger while the knob is being operated. This is
    /// display-only; input and device volume continue to work as designed.
    /// </summary>
    public void OnPhysicalDialEvent()
    {
        if(!_liveEnabled||IsDisposed)return;
        _forceNextLiveFrame=true;
        _dialTakeoverUntil=DateTimeOffset.UtcNow.AddSeconds(3);
        // Leading-edge throttle, not trailing-edge debounce: while the user
        // keeps spinning, the preview and physical OLED must keep advancing.
        if(!_dialRefreshTimer.Enabled)_dialRefreshTimer.Start();
        // A timer-only implementation leaves a plainly visible OEM frame
        // between the event and its first scheduled tick.
        _=RefreshLiveDock(force:true);
    }

    internal static int ReconnectDelaySeconds(int failures)
        =>Math.Clamp(1<<Math.Clamp(failures,1,4),2,15);

    // Public actions for the integrated, single-page v1.0 toolbar.
    // The legacy buttons are hidden in the integrated studio. PerformClick()
    // silently ignores an invisible control; invoke the handlers directly.
    public void ApplyDesign() => _ = ApplyDesignAsync();
    public void ToggleLiveOutput() => _ = ToggleLiveMode();
    public void RestoreOriginalScreen() => ResetDock();

    /// <summary>
    /// Grants the secret game exclusive OLED ownership. Never overlaps an
    /// in-flight ggoled update: RestoreNow uses the existing hardware gate.
    /// The return value records whether a user-authorized live session should
    /// resume after the game exits.
    /// </summary>
    public async System.Threading.Tasks.Task<bool> SuspendLiveForGameAsync()
    {
        bool wasLive=_liveEnabled;
        if(!wasLive)return false;
        _liveEnabled=false;
        _liveTimer.Stop();
        _dialRefreshTimer.Stop();
        _dialTakeoverUntil=DateTimeOffset.MinValue;
        _lastLiveFrame=null;
        _lastLiveWrite=DateTimeOffset.MinValue;
        _forceNextLiveFrame=false;
        _reconnectFailures=0;
        _nextReconnect=DateTimeOffset.MinValue;
        _liveToggle.Text="Canlı dock modunu başlat";
        _sendHardware.Enabled=false;
        _message.Text="DOOM MODE için OLED aktarımı duraklatılıyor…";
        // Execute the potentially long CLI return off the UI thread. The
        // previous send is drained first by DockHardwareTest.liveGate.
        string result=await System.Threading.Tasks.Task.Run(_hardware.RestoreNow);
        if(!IsDisposed)_message.Text=result;
        if(!result.StartsWith("SteelSeries varsayılan",StringComparison.Ordinal))
            throw new InvalidOperationException(result);
        return wasLive;
    }

    public async System.Threading.Tasks.Task ResumeLiveAfterGameAsync(bool wasLive)
    {
        if(!wasLive||IsDisposed||_liveEnabled)return;
        if(!_custom.Checked || _hardware.Active)
        {
            _message.Text="DOOM kapandı; önce Özel tasarım ve Dock Reset durumunu kontrol et.";
            return;
        }
        string validation=_hardware.Validate();
        if(validation!=""){_message.Text=validation;return;}
        _lastLiveFrame=null;
        _lastLiveWrite=DateTimeOffset.MinValue;
        _forceNextLiveFrame=false;
        _liveEnabled=true;
        _sendHardware.Enabled=false;
        _liveToggle.Text="Canlı modu durdur";
        _message.Text="DOOM MODE kapandı, önceki canlı OLED oturumu geri yüklendi.";
        _liveTimer.Start();
        await RefreshLiveDock();
    }

    private void ResetDock()
    {
        if(_liveEnabled)StopLiveMode();
        else _message.Text=_hardware.RestoreNow();
    }
    public void ChooseDockPhoto() => _choose.PerformClick();
    public void ResetDockPhoto() => _restore.PerformClick();
    public void CalibrateDockPhoto() => _calibrate.PerformClick();
    public void ShowOriginalPhoto(bool original)
    {
        if(original)_factory.Checked=true;
        else _custom.Checked=true;
    }

    public void EnableEmbeddedStudio()
    {
        if(_embeddedStudio)return;
        _embeddedStudio=true;
        SuspendLayout();
        BackColor=Color.FromArgb(24,30,40);
        foreach(Control child in Controls)
            child.Visible=ReferenceEquals(child,_photoBox);
        _photoBox.Dock=DockStyle.Fill;
        _photoBox.BorderStyle=BorderStyle.None;
        _photoBox.BackColor=BackColor;
        _photoBox.Padding=new Padding(8);
        ResumeLayout(true);
        ComputeVisiblePhotoCrop();
        InvalidateFrame();
    }

    // The official PNG has alpha margins. Crop them only in the on-screen
    // compositing projection; source photo and saved screen corner
    // coordinates remain unchanged.
    private void ComputeVisiblePhotoCrop()
    {
        if(_photo is not Bitmap bitmap)
        {
            _visiblePhotoCrop=Rectangle.Empty;
            return;
        }
        if(!_embeddedStudio || _settings.ImagePath!=null)
        {
            _visiblePhotoCrop=new Rectangle(0,0,bitmap.Width,bitmap.Height);
            return;
        }
        int minX=bitmap.Width,minY=bitmap.Height,maxX=0,maxY=0;
        for(int y=0;y<bitmap.Height;y+=3)
        for(int x=0;x<bitmap.Width;x+=3)
        {
            if(bitmap.GetPixel(x,y).A<=12)continue;
            minX=Math.Min(minX,x);minY=Math.Min(minY,y);
            maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
        }
        if(minX>maxX || minY>maxY)
        {
            _visiblePhotoCrop=new Rectangle(0,0,bitmap.Width,bitmap.Height);
            return;
        }
        int pad=20;
        int l=Math.Max(0,minX-pad),t=Math.Max(0,minY-pad);
        int r=Math.Min(bitmap.Width,maxX+pad),b=Math.Min(bitmap.Height,maxY+pad);
        _visiblePhotoCrop=Rectangle.FromLTRB(l,t,r,b);
    }
    public void OnProtectionChanged()
    {
        // Changing from protected to full-screen requires a fresh confirmation.
        if(_liveEnabled)StopLiveMode();
        _cachedGlassBackground=null;
        InvalidateFrame();
        _message.Text=_protectHeader()
            ?"Üst 10 piksel korunuyor. Dock'a yalnızca Y=10–63 gönderilecek."
            :"Üst koruma kapalı. Bir sonraki test onaylandığında TÜM 128×64 OLED yenilenecek.";
    }

    private async System.Threading.Tasks.Task ToggleLiveMode()
    {
        if(_liveEnabled)
        {
            StopLiveMode();
            return;
        }
        if(!_custom.Checked || _hardware.Active)
        {
            _message.Text="Önce Özel tasarım modunu seç; uygulanmış görüntü varsa Reset'e bas.";
            return;
        }
        string validation=_hardware.Validate();
        if(validation!="")
        {
            _message.Text=validation;
            return;
        }
        string liveConsent=UiLanguage.English
            ?"Live OLED refreshes at up to 16 frames/sec and reasserts after dial input.\n"+
                (_protectHeader()
                    ?"Only rows 10–63 will update; original top 10 pixels remain untouched.\n"
                    :"WARNING: Full-screen 128×64 output will replace the OEM header.\n")+
                "SteelSeries GG Now Playing may conflict with this feature.\n"+
                "Closing live mode restores the original OLED. Start live mode?"
            :"Canlı OLED saniyede 16 kareye kadar yenilenir; fiziksel tekerlekten sonra ekran geri alınır.\n"+
                (_protectHeader()
                    ?"Yalnızca 10–63. satırlar güncellenir; orijinal üst bölüm korunur.\n"
                    :"UYARI: Üst koruma kapalı, tüm 128×64 ekran değiştirilecek.\n")+
                "Bu mod SteelSeries GG Now Playing ile yarışabilir. Mümkünse GG Now Playing'i kapat.\n"+
                "Gerektiğinde OLED ekranı tekrar çizilir. Kapatınca SteelSeries ekranı geri yüklenir.\n"+
                "Canlı moda geçilsin mi?";
        if(MessageBox.Show(this,liveConsent,
            UiLanguage.English?"Dock live mode (experimental)":"Dock canlı modu (deneysel)",
            MessageBoxButtons.YesNo,MessageBoxIcon.Information)
            !=DialogResult.Yes) return;

        _liveEnabled=true;
        _lastLiveFrame=null;
        _lastLiveWrite=DateTimeOffset.MinValue;
        _forceNextLiveFrame=false;
        _reconnectFailures=0;
        _nextReconnect=DateTimeOffset.MinValue;
        _sendHardware.Enabled=false;
        _liveToggle.Text="Canlı modu durdur";
        _message.Text=_protectHeader()
            ?"Canlı dock modu etkin, üst 10 satır korunuyor."
            :"Canlı dock modu etkin, TÜM 128×64 ekran kullanılıyor.";
        _liveTimer.Start();
        await RefreshLiveDock();
    }

    private void StopLiveMode()
    {
        if(!_liveEnabled)return;
        _liveEnabled=false;
        _liveTimer.Stop();
        _dialRefreshTimer.Stop();
        _dialTakeoverUntil=DateTimeOffset.MinValue;
        _lastLiveFrame=null;
        _lastLiveWrite=DateTimeOffset.MinValue;
        _forceNextLiveFrame=false;
        _reconnectFailures=0;
        _nextReconnect=DateTimeOffset.MinValue;
        if(!IsDisposed)
        {
            _liveToggle.Text="Canlı dock modunu başlat";
            _sendHardware.Enabled=_custom.Checked&&!_hardware.Active;
            _message.Text="Canlı mod durdu: SteelSeries ekranı geri yükleniyor.";
        }
        // The restore command is serialized after any in-flight OLED update.
        string message=_hardware.RestoreNow();
        if(!IsDisposed)_message.Text=message;
    }

    internal static bool NeedsReassert(string? prior,string next,
        DateTimeOffset last,DateTimeOffset now,bool force)
        =>force||prior!=next||now-last>=TimeSpan.FromMilliseconds(55);

    private async System.Threading.Tasks.Task RefreshLiveDock(bool force=false)
    {
        if(!_liveEnabled||IsDisposed)return;
        if(DateTimeOffset.UtcNow<_nextReconnect)return;
        if(_liveSending)
        {
            if(force)_forceNextLiveFrame=true;
            return;
        }
        _liveSending=true;
        try
        {
            using var bitmap=_liveFrameFactory();
            var pixels=new byte[128*64];
            int ptr=0;
            for(int y=_protectHeader()?10:0;y<64;y++)
            for(int x=0;x<128;x++)
                pixels[ptr++]=bitmap.GetPixel(x,y).R>127?(byte)1:(byte)0;
            string stamp=(_protectHeader()?"protected:":"full:")+
                Convert.ToHexString(SHA256.HashData(pixels[..ptr]));
            // OLED report 0x93 already overwrites the display RAM, but the
            // firmware may draw its volume animation afterward. Reassert the
            // selected design without attempting undocumented mode changes.
            if(NeedsReassert(_lastLiveFrame,stamp,_lastLiveWrite,
                DateTimeOffset.UtcNow,force||_forceNextLiveFrame))
            {
                await _hardware.SendLiveFrameAsync(bitmap,_protectHeader());
                _lastLiveFrame=stamp;
                _lastLiveWrite=DateTimeOffset.UtcNow;
                _forceNextLiveFrame=false;
                if(_reconnectFailures>0)
                {
                    _reconnectFailures=0;
                    _nextReconnect=DateTimeOffset.MinValue;
                    if(!IsDisposed)
                        _message.Text=UiLanguage.T("OLED bağlantısı yeniden kuruldu.");
                }
            }
        }
        catch(Exception ex)
        {
            // Keep the user's authorized live session armed on unplug. The
            // transport is rebuilt by DockHardwareTest at the next attempt.
            _reconnectFailures++;
            int delay=ReconnectDelaySeconds(_reconnectFailures);
            _nextReconnect=DateTimeOffset.UtcNow.AddSeconds(delay);
            _lastLiveFrame=null;
            _lastLiveWrite=DateTimeOffset.MinValue;
            _forceNextLiveFrame=false;
            if(!IsDisposed)
            {
                _message.Text=UiLanguage.T("OLED bağlantısı bekleniyor")+
                    $" ({delay}s): "+ex.Message;
            }
        }
        finally
        {
            _liveSending=false;
            if(_liveEnabled&&_forceNextLiveFrame&&!IsDisposed &&
               DateTimeOffset.UtcNow>=_nextReconnect)
            {
                // Hardware events arriving during an in-flight HID transfer
                // must get the newest frame immediately after it completes.
                try{BeginInvoke((Action)(()=>_ = RefreshLiveDock(force:true)));}
                catch(InvalidOperationException){ /* closing */ }
            }
        }
    }

    private async System.Threading.Tasks.Task ApplyDesignAsync()
    {
        if(_liveEnabled)return;
        if(!_custom.Checked)
        {
            _message.Text="Önce Custom (canlı tasarım) seç.";
            return;
        }

        string validation=_hardware.Validate();
        if(validation!="")
        {
            _message.Text=validation;
            return;
        }
        string applyConsent=UiLanguage.English
            ?(_protectHeader()
                ?"Only rows Y=10–63 will change; OEM battery indicators are preserved.\n"
                :"WARNING: Full-screen 128×64 will temporarily replace the OEM indicators.\n")+
                "The image remains on the dock until Reset or app exit.\n"+
                "If the app crashes, reopen it and choose Dock Reset.\n"+
                "Avoid leaving static OLED images on for extended periods. Apply now?"
            :(_protectHeader()
                ?"Yalnızca Y=10–63. Orijinal pil göstergeleri aktarım dışında.\n"
                :"UYARI: 128×64 tam ekran modu. OEM pil göstergeleri geçici olarak silinir.\n")+
            "Tasarım Reset'e basana veya uygulamayı kapatana kadar ekranda kalır.\n"+
            "Uygulama çökerse yeniden açıp Dock Reset'i kullanabilirsin.\n"+
            "OLED yanığı riskini azaltmak için statik görüntüyü uzun süre bırakma.\n"+
            "Uygulansın mı?";
        var answer=MessageBox.Show(this,applyConsent,
            UiLanguage.English?"Physical dock — Apply":"Gerçek dock ekranı — Uygula",
            MessageBoxButtons.YesNo,MessageBoxIcon.Information);
        if(answer!=DialogResult.Yes)return;

        _sendHardware.Enabled=false;
        _message.Text="Tasarım dock'a gönderiliyor; Reset'e kadar görüntü korunacak.";
        try
        {
            using var frame=_frameFactory();
            string result=await _hardware.ApplyStaticAsync(frame,_protectHeader());
            if(!IsDisposed)_message.Text=result;
        }
        catch(Exception ex)
        {
            if(!IsDisposed)_message.Text="Dock test hatası: "+ex.Message;
        }
        finally
        {
            if(!IsDisposed)_sendHardware.Enabled=_custom.Checked;
        }
    }

    private void RestoreExample()
    {
        _settings.ImagePath=null;
        _settings.Corners=_factoryCorners.ToList();
        SaveSettings();
        LoadImage();
        _message.Text="Resmî SteelSeries ürün fotoğrafı yüklendi. Fotoğraf yalnızca yerel önizleme içindir.";
    }

    private void ChoosePhoto()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Görüntüler|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Tüm dosyalar|*.*",
            Title = "Kendi dock fotoğrafını seç"
        };
        if(dialog.ShowDialog()!=DialogResult.OK)return;
        try
        {
            // Validate file before copying into user-local preview cache.
            using(var image = Image.FromFile(dialog.FileName))
                if(image.Width>6000 || image.Height>6000)
                    throw new InvalidDataException("Fotoğraf 6000x6000 pikselle sınırlıdır.");
            var folder=Path.GetDirectoryName(_settingsFile)!;
            Directory.CreateDirectory(folder);
            string extension=Path.GetExtension(dialog.FileName).ToLowerInvariant();
            string saved=Path.Combine(folder,"user-dock-photo"+extension);
            if(!Path.GetFullPath(dialog.FileName).Equals(Path.GetFullPath(saved),StringComparison.OrdinalIgnoreCase))
                File.Copy(dialog.FileName,saved,true);
            _settings.ImagePath=saved;
            _settings.Corners=[];
            SaveSettings();
            LoadImage();
            _message.Text="Fotoğraf yüklendi. Şimdi 'Ekranı hizala' ile OLED'in dört köşesini işaretle.";
        }
        catch(Exception ex){ _message.Text="Fotoğraf yüklenemedi: "+ex.Message; }
    }

    private void LoadSettings()
    {
        try
        {
            if(File.Exists(_settingsFile))
                _settings=JsonSerializer.Deserialize<PhotoSettings>(File.ReadAllText(_settingsFile))??new PhotoSettings();
        }
        catch { _settings=new PhotoSettings(); }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
            File.WriteAllText(_settingsFile,JsonSerializer.Serialize(_settings));
        }
        catch { }
    }

    private void LoadImage()
    {
        // v1.0.1 uses the original dock photograph exclusively. Legacy
        // photo overrides remain untouched on disk but are not loaded.
        _settings.ImagePath=null;
        _settings.Corners=_factoryCorners.ToList();
        var path = _assetDefault;
        try
        {
            if(!File.Exists(path))throw new FileNotFoundException("Fotoğraf bulunamadı.",path);
            using var image = Image.FromFile(path);
            var clone = new Bitmap(image);
            var old=_photo;
            _photo=clone;
            ComputeVisiblePhotoCrop();
            _cachedGlassBackground=null;
            old?.Dispose();
            if(_settings.Corners.Count!=4 && _settings.ImagePath==null)
                _settings.Corners=_factoryCorners.ToList();
            InvalidateFrame();
        }
        catch(Exception ex)
        {
            _message.Text="Orijinal dock fotoğrafı bulunamadı: "+ex.Message;
        }
    }

    private void StartCalibration()
    {
        if(_photo==null)return;
        _calibrating=true;
        _newCorners.Clear();
        _cancelCalibration.Enabled=true;
        _photoBox.Cursor=Cursors.Cross;
        _message.Text="Ekranı sırayla işaretle: 1) sol üst, 2) sağ üst, 3) sağ alt, 4) sol alt. Orijinal batarya simgeleri korunacak.";
        InvalidateFrame();
    }

    private void EndCalibration(bool save)
    {
        _calibrating=false;
        _cancelCalibration.Enabled=false;
        _photoBox.Cursor=Cursors.Default;
        if(save && _newCorners.Count==4)
        {
            _settings.Corners=_newCorners.ToList();
            _cachedGlassBackground=null;
            SaveSettings();
            _message.Text="OLED perspektifi kaydedildi. Tasarım şimdi işaretlediğin alana yerleşiyor.";
        }
        else _message.Text="Hizalama iptal edildi; önceki ekran koordinatları korundu.";
        _newCorners.Clear();
        InvalidateFrame();
    }

    private void PhotoClick(object? sender, MouseEventArgs e)
    {
        if(!_calibrating||_photo==null||!_imageRect.Contains(e.Location))return;
        var x=_visiblePhotoCrop.X+
            (e.X-_imageRect.X)/_imageRect.Width*_visiblePhotoCrop.Width;
        var y=_visiblePhotoCrop.Y+
            (e.Y-_imageRect.Y)/_imageRect.Height*_visiblePhotoCrop.Height;
        _newCorners.Add(new PointF(x,y));
        if(_newCorners.Count==4)EndCalibration(true);
        else _message.Text="Köşe "+_newCorners.Count+"/4 işaretlendi. Sonraki köşe: "+
            new[]{"sol üst","sağ üst","sağ alt","sol alt"}[_newCorners.Count];
        InvalidateFrame();
    }

    private void PaintPhoto(Graphics g)
    {
        g.Clear(_embeddedStudio?Color.FromArgb(24,30,40):
            Color.FromArgb(25,29,37));
        if(_photo==null)
        {
            DrawFallbackDock(g);
            return;
        }

        var renderBox=_embeddedStudio
            ?new Rectangle(18,15,Math.Max(1,_photoBox.Width-36),
                Math.Max(1,_photoBox.Height-30))
            :_photoBox.ClientRectangle;
        var crop=_visiblePhotoCrop.IsEmpty
            ?new Rectangle(Point.Empty,_photo.Size):_visiblePhotoCrop;
        _imageRect=FitToBox(crop.Size,renderBox);
        if(!_embeddedStudio)
        {
            using var backgroundBrush = new LinearGradientBrush(
                new Point(0,0),new Point(0,_photoBox.Height),
                Color.FromArgb(49,52,61),Color.FromArgb(24,27,34));
            g.FillRectangle(backgroundBrush,_photoBox.ClientRectangle);
        }
        g.SmoothingMode=SmoothingMode.HighQuality;
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;

        if(_factory.Checked||_settings.Corners.Count!=4)
        {
            g.DrawImage(_photo,_imageRect,crop,GraphicsUnit.Pixel);
        }
        else
        {
            using var render=_frameFactory();
            // This dark-glass reference only depends on the photo/corners, not
            // on the changing OLED framebuffer. Cache the expensive medians.
            if(_cachedGlassBackground is null)
            {
                using var reference=new Bitmap(_photo);
                _cachedGlassBackground=BuildSmoothBackground(reference,_settings.Corners);
            }
            using var combined=ComposePhotograph(_photo,render,
                _settings.Corners,92,_cachedGlassBackground,_protectHeader());
            g.DrawImage(combined,_imageRect,crop,GraphicsUnit.Pixel);
        }

        if(_calibrating)
        {
            using var p=new Pen(Color.FromArgb(80,220,235),2);
            for(int i=0;i<_newCorners.Count;i++)
            {
                var pt=ToScreen(_newCorners[i]);
                g.FillEllipse(Brushes.Cyan,pt.X-5,pt.Y-5,10,10);
                if(i>0)g.DrawLine(p,ToScreen(_newCorners[i-1]),pt);
            }
        }
    }

    // A self-drawn, redistribution-safe fallback for release packages that
    // intentionally omit the licensed local product photograph. The editor
    // remains useful on another PC without asking users to download assets.
    private void DrawFallbackDock(Graphics g)
    {
        _imageRect=RectangleF.Empty;
        var area=_photoBox.ClientRectangle;
        if(area.Width<120 || area.Height<90)return;
        g.SmoothingMode=SmoothingMode.AntiAlias;
        float width=Math.Min(area.Width-42,700);
        float height=Math.Min(area.Height-38,238);
        float x=(area.Width-width)/2,y=(area.Height-height)/2;
        var body=new RectangleF(x,y,width,height);
        using var shape=new GraphicsPath();
        float radius=Math.Min(43,height/4);
        float diam=radius*2;
        shape.AddArc(x,y,diam,diam,180,90);
        shape.AddArc(x+width-diam,y,diam,diam,270,90);
        shape.AddArc(x+width-diam,y+height-diam,diam,diam,0,90);
        shape.AddArc(x,y+height-diam,diam,diam,90,90);
        shape.CloseFigure();
        using(var shadow=new SolidBrush(Color.FromArgb(80,0,0,0)))
            g.FillEllipse(shadow,x+20,y+height-6,width-30,25);
        using(var gradient=new LinearGradientBrush(body,
            Color.FromArgb(51,53,55),Color.FromArgb(11,13,15),90f))
            g.FillPath(gradient,shape);
        using(var outline=new Pen(Color.FromArgb(94,99,105),1.4f))
            g.DrawPath(outline,shape);

        float knobSize=Math.Min(height*.86f,width*.32f);
        var knob=new RectangleF(x+width-knobSize-20,y+(height-knobSize)/2,
            knobSize,knobSize);
        using(var ring=new LinearGradientBrush(knob,
            Color.FromArgb(128,130,132),Color.FromArgb(38,41,45),45f))
            g.FillEllipse(ring,knob);
        using(var outer=new Pen(Color.FromArgb(163,167,170),1.6f))
            g.DrawEllipse(outer,knob);
        var center=RectangleF.Inflate(knob,-knobSize*.095f,-knobSize*.095f);
        using(var face=new SolidBrush(Color.FromArgb(30,31,33)))
            g.FillEllipse(face,center);
        using(var rim=new Pen(Color.FromArgb(71,74,77),2))
            g.DrawEllipse(rim,center);

        int screenW=(int)Math.Min(width*.45f,280);
        int screenH=screenW/2;
        int screenX=(int)(x+width*.12f);
        int screenY=(int)(y+(height-screenH)/2-13);
        using(var screenBack=new SolidBrush(Color.FromArgb(14,15,17)))
            g.FillRectangle(screenBack,screenX-4,screenY-4,screenW+8,screenH+8);
        using(var border=new Pen(Color.FromArgb(37,40,43)))
            g.DrawRectangle(border,screenX-4,screenY-4,screenW+8,screenH+8);
        if(_factory.Checked)
        {
            using var font=new Font("Consolas",9.5f,FontStyle.Bold);
            g.DrawString("NOVA PRO  WIRELESS",font,Brushes.White,screenX+10,screenY+20);
            g.DrawString("48kHz / 24bit",font,Brushes.White,screenX+10,screenY+48);
        }
        else
        {
            using var oled=_frameFactory();
            g.InterpolationMode=InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode=PixelOffsetMode.Half;
            g.DrawImage(oled,new Rectangle(screenX,screenY,screenW,screenH),
                new Rectangle(0,0,128,64),GraphicsUnit.Pixel);
        }
        using(var muted=new SolidBrush(Color.FromArgb(143,149,155)))
        using(var font=new Font("Segoe UI Semibold",Math.Max(9,height*.066f),FontStyle.Bold))
            g.DrawString("steelseries",font,muted,screenX+32,
                y+height-Math.Max(35,height*.2f));
        using(var marker=new Pen(Color.FromArgb(129,133,138),2.2f))
            g.DrawEllipse(marker,x+width*.67f,y+height*.72f,31,31);
    }

    private PointF ToScreen(PointF pt) =>
        new(_imageRect.Left+(pt.X-_visiblePhotoCrop.X)/
                _visiblePhotoCrop.Width*_imageRect.Width,
            _imageRect.Top+(pt.Y-_visiblePhotoCrop.Y)/
                _visiblePhotoCrop.Height*_imageRect.Height);

    private static RectangleF FitToBox(Size image,Rectangle box)
    {
        float scale=Math.Min((float)box.Width/image.Width,(float)box.Height/image.Height);
        float w=image.Width*scale,h=image.Height*scale;
        return new((box.Width-w)/2,(box.Height-h)/2,w,h);
    }

    /// <summary>
    /// Keeps the actual glass appearance of a photographed OLED display.
    /// Reconstructs the dark off-pixel background from dark, nearby photo pixels,
    /// removes stale factory text beneath the custom area, feathers edges, and
    /// blends native-pixel glyphs in photo space. Never affects the actual dock.
    /// </summary>
    public static Bitmap ComposePhotograph(Image photo, Bitmap oled,
        IReadOnlyList<PointF> corners,int brightness,
        Color[]? cachedBackground=null,bool protectHeader=true)
    {
        var output=new Bitmap(photo.Width,photo.Height,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(output))g.DrawImage(photo,0,0,photo.Width,photo.Height);
        if(corners.Count!=4)return output;

        var map=PerspectiveMap.Create(corners);
        if(map==null)return output;
        int x0=Math.Max(0,(int)Math.Floor(corners.Min(p=>p.X)));
        int x1=Math.Min(output.Width-1,(int)Math.Ceiling(corners.Max(p=>p.X)));
        int y0=Math.Max(0,(int)Math.Floor(corners.Min(p=>p.Y)));
        int y1=Math.Min(output.Height-1,(int)Math.Ceiling(corners.Max(p=>p.Y)));
        if(x1<=x0 || y1<=y0)return output;

        var smooth=cachedBackground ?? BuildSmoothBackground(output,corners);
        // OLED is only 128×64; cache its on/off pixels rather than calling
        // Bitmap.GetPixel four times per source-photo pixel.
        var sourcePixels=new bool[128*64];
        for(int oy=protectHeader?10:0;oy<64;oy++)
        for(int ox=0;ox<128;ox++)
            sourcePixels[oy*128+ox]=oled.GetPixel(ox,oy).R>127;

        double white=222.0 + 0.24*Math.Clamp(brightness,35,100);
        for(int y=y0;y<=y1;y++)
        for(int x=x0;x<=x1;x++)
        {
            if(!map.TryInverse(x+.5,y+.5,out var u,out var v))continue;
            if(u<0 || u>=1 || v<0 || v>=1)continue;
            double nx=u*128,ny=v*64;
            if(protectHeader && ny<10)continue; // Keep OEM icons only if enabled.

            var old=output.GetPixel(x,y);
            var bg=smooth[y];

            // Sample in native pixel coordinates with fractional coverage:
            // camera-like edges without inventing extra OLED resolution.
            double a=NativeCoverage(sourcePixels,nx-.5,ny-.5,protectHeader);

            double distance=Math.Min(
                Math.Min(nx,128-nx),
                Math.Min(ny-(protectHeader?10:0),64-ny));
            double edge=Math.Clamp(distance/3.0,0.0,1.0);
            // Restrained photo texture, clamped so that original white text
            // cannot leak through the new black pixels.
            double noiseR=Math.Min(old.R,bg.R+7)*.08;
            double noiseG=Math.Min(old.G,bg.G+7)*.08;
            double noiseB=Math.Min(old.B,bg.B+7)*.08;
            double offR=bg.R*.92+noiseR;
            double offG=bg.G*.92+noiseG;
            double offB=bg.B*.92+noiseB;
            double r=offR + (white-offR)*a;
            double g=offG + (white+3-offG)*a;
            double b=offB + (white+4-offB)*a;

            // Blend at screen boundaries so no pasted-on black rectangle is visible.
            output.SetPixel(x,y,Color.FromArgb(255,
                (int)Math.Clamp(old.R*(1-edge)+r*edge,0,255),
                (int)Math.Clamp(old.G*(1-edge)+g*edge,0,255),
                (int)Math.Clamp(old.B*(1-edge)+b*edge,0,255)));
        }
        return output;
    }

    private static Color[] BuildSmoothBackground(Bitmap output,
        IReadOnlyList<PointF> corners)
    {
        int x0=Math.Max(0,(int)Math.Floor(corners.Min(p=>p.X)));
        int x1=Math.Min(output.Width-1,(int)Math.Ceiling(corners.Max(p=>p.X)));
        int y0=Math.Max(0,(int)Math.Floor(corners.Min(p=>p.Y)));
        int y1=Math.Min(output.Height-1,(int)Math.Ceiling(corners.Max(p=>p.Y)));
        if(x1<=x0||y1<=y0)return new Color[output.Height];

        // Sample the bezel immediately beside the OLED as well as its own
        // dark pixels. The factory screen is slightly grey from reflections;
        // reusing only that shade creates a visible grey rectangle on the
        // otherwise black dock. Prefer the darker neighboring body shade,
        // but reject transparent/photo-background samples.
        var background=new Color[output.Height];
        for(int row=y0;row<=y1;row++)
        {
            var innerR=new List<byte>();var innerG=new List<byte>();var innerB=new List<byte>();
            var outerR=new List<byte>();var outerG=new List<byte>();var outerB=new List<byte>();
            for(int col=Math.Max(0,x0-26);col<=Math.Min(output.Width-1,x1+26);col++)
            {
                bool inner=col>=x0 && col<=x1;
                bool neighbor=(col>=x0-26 && col<=x0-6) ||
                              (col>=x1+6 && col<=x1+26);
                if(!inner && !neighbor)continue;
                var c=output.GetPixel(col,row);
                if(c.A<250 || c.R>=72 || c.G>=72 || c.B>=72)continue;
                if(inner)
                {
                    innerR.Add(c.R);innerG.Add(c.G);innerB.Add(c.B);
                }
                else if(c.R>=8 && c.G>=8 && c.B>=8)
                {
                    outerR.Add(c.R);outerG.Add(c.G);outerB.Add(c.B);
                }
            }
            if(innerR.Count==0 && outerR.Count==0)
            {
                background[row]=Color.FromArgb(23,24,26);
                continue;
            }
            static byte Median(List<byte> values,byte fallback)
            {
                if(values.Count==0)return fallback;
                values.Sort();
                return values[values.Count/2];
            }
            byte ir=Median(innerR,23),ig=Median(innerG,24),ib=Median(innerB,26);
            byte or=Median(outerR,ir),og=Median(outerG,ig),ob=Median(outerB,ib);
            background[row]=Color.FromArgb(
                Math.Min(ir,or),Math.Min(ig,og),Math.Min(ib,ob));
        }

        // Smooth the source-photo dark-glass background vertically; this avoids
        // reproducing the old stock display's text as horizontal dark bands.
        var smooth=new Color[output.Height];
        for(int row=y0;row<=y1;row++)
        {
            int rr=0,gg=0,bb=0,count=0;
            for(int near=Math.Max(y0,row-10);near<=Math.Min(y1,row+10);near++)
            {
                rr+=background[near].R;gg+=background[near].G;bb+=background[near].B;count++;
            }
            smooth[row]=Color.FromArgb(rr/count,gg/count,bb/count);
        }

        return smooth;
    }

    private static double NativeCoverage(bool[] oled,double x,double y,bool protectHeader)
    {
        int x0=(int)Math.Floor(x), y0=(int)Math.Floor(y);
        double fx=x-x0,fy=y-y0;
        double a=IsLit(oled,x0,y0,protectHeader)*(1-fx)+
            IsLit(oled,x0+1,y0,protectHeader)*fx;
        double b=IsLit(oled,x0,y0+1,protectHeader)*(1-fx)+
            IsLit(oled,x0+1,y0+1,protectHeader)*fx;
        return a*(1-fy)+b*fy;
    }

    private static int IsLit(bool[] image,int x,int y,bool protectHeader) =>
        x>=0 && x<128 && y>=(protectHeader?10:0) && y<64 &&
        image[y*128+x] ? 1 : 0;

    protected override void Dispose(bool disposing)
    {
        if(disposing)_photo?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>8-parameter perspective transform using exact quad-to-rect mapping.</summary>
internal sealed class PerspectiveMap
{
    private readonly double[] h;
    private PerspectiveMap(double[] transform) => h=transform;

    public static PerspectiveMap? Create(IReadOnlyList<PointF> corners)
    {
        if(corners.Count!=4)return null;
        var uv=new (double u,double v)[]{(0,0),(1,0),(1,1),(0,1)};
        var a=new double[8,9];
        for(int i=0;i<4;i++)
        {
            double u=uv[i].u,v=uv[i].v,x=corners[i].X,y=corners[i].Y;
            int r=2*i;
            a[r,0]=u;a[r,1]=v;a[r,2]=1;a[r,6]=-u*x;a[r,7]=-v*x;a[r,8]=x;
            a[r+1,3]=u;a[r+1,4]=v;a[r+1,5]=1;a[r+1,6]=-u*y;a[r+1,7]=-v*y;a[r+1,8]=y;
        }
        for(int col=0;col<8;col++)
        {
            int pivot=col;
            for(int row=col+1;row<8;row++)
                if(Math.Abs(a[row,col])>Math.Abs(a[pivot,col]))pivot=row;
            if(Math.Abs(a[pivot,col])<1e-10)return null;
            for(int c=col;c<9;c++)(a[col,c],a[pivot,c])=(a[pivot,c],a[col,c]);
            var div=a[col,col];
            for(int c=col;c<9;c++)a[col,c]/=div;
            for(int row=0;row<8;row++)if(row!=col)
            {
                var k=a[row,col];
                for(int c=col;c<9;c++)a[row,c]-=k*a[col,c];
            }
        }
        var h=new double[8];
        for(int i=0;i<8;i++)h[i]=a[i,8];
        return new PerspectiveMap(h);
    }

    public bool TryInverse(double x,double y,out double u,out double v)
    {
        double a=x*h[6]-h[0],b=x*h[7]-h[1],c=h[2]-x;
        double d=y*h[6]-h[3],e=y*h[7]-h[4],f=h[5]-y;
        double det=a*e-b*d;
        if(Math.Abs(det)<1e-12){u=v=0;return false;}
        u=(c*e-b*f)/det;v=(a*f-c*d)/det;
        return !double.IsNaN(u) && !double.IsNaN(v);
    }
}

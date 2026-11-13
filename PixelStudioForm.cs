using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

internal sealed partial class PixelStudioForm : Form
{
    private bool captureMode;
    private bool traySmokeMode;
    internal void EnableTraySmokeMode()
    {
        traySmokeMode=true;
        StartPosition=FormStartPosition.Manual;
        Location=new Point(-5000,-5000);
        ShowInTaskbar=false;
    }
    internal void EnableOffscreenCapture()
    {
        captureMode=true;
        StartPosition=FormStartPosition.Manual;
        Location=new Point(-5000,-5000);
        ShowInTaskbar=false;
    }
    internal void SelectCaptureMode(ScreenMode mode)
    {
        SwitchEditingMode(mode);
        selected=state.Widgets.FirstOrDefault(w=>w.Kind=="VolumeChatMix");
        RefreshWidgetList();
    }
    // Pure synthetic preview: never alters the saved user's game design.
    internal void SelectGameLogoForCapture()
    {
        SwitchEditingMode(ScreenMode.Game);
        var widget=state.Widgets.FirstOrDefault(w=>w.Kind=="GameLogo");
        if(widget is null)
        {
            widget=new DockWidget{
                Kind="GameLogo",X=96,Y=37,GameLogoSize=16};
            state.Widgets.Add(widget);
        }
        selected=widget;
        gameSnapshot=new GameSnapshot("SYNTHETIC GAME","synthetic",9999);
        var pixels=new bool[32*32];
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)
        {
            // An explicit TEST-only logo (not a game/brand logo).
            bool ring=Math.Abs((x-16)*(x-16)+(y-16)*(y-16)-155)<30;
            bool bolt=x>=14&&x<=18&&y>=8&&y<=24;
            pixels[y*32+x]=ring||bolt;
        }
        gameLogoSnapshot=new GameLogoSnapshot("synthetic","SYNTHETIC GAME",
            32,pixels,"Preview only");
        RefreshWidgetList();
        Redraw();
    }

    // Offscreen acceptance only: no hardware write or persistence.
    internal void SetHeaderProtectionForCapture(bool protect)
        =>headerProtect.Checked=protect;

    // Inspect the actual rendered editor canvas without the outer window.
    internal Bitmap CaptureEditorCanvasForTest()
    {
        var capture=new Bitmap(editor.Width,editor.Height);
        editor.DrawToBitmap(capture,new Rectangle(0,0,
            capture.Width,capture.Height));
        return capture;
    }
    protected override bool ShowWithoutActivation=>captureMode||traySmokeMode;
    private const int CanvasWidth=128, CanvasHeight=64;
    private int Zoom=6;
    private int LockedRows=>project.ProtectHeader?10:0;
    private StudioProject project=StudioProject.Load();
    private ScreenMode editingMode=ScreenMode.Media;
    private GameSnapshot gameSnapshot=GameSnapshot.None;
    private GameLogoSnapshot? gameLogoSnapshot;
    private bool gamePolling,controllerConnectionPolling;
    private int gameCounter,controllerCounter;
    private readonly TabControl modeTabs=new(){
        Left=18,Top=8,Width=544,Height=38,SizeMode=TabSizeMode.Fixed,ItemSize=new Size(180,25)};
    private readonly DarkCheckBox headerProtect=new(){
        Left=570,Top=13,Width=240,Text="Üst 10 pikseli koru",Checked=true};
    private readonly DarkCheckBox liveOwnsDisplay=new(){
        Width=269,Height=34,Text="Canlıda tam ekran (128×64)",Checked=false};
    private readonly DarkNumberBox strokeWidth=new(){Minimum=1,Maximum=3,Width=85};
    private readonly DarkNumberBox progressWidth=new(){
        Minimum=12,Maximum=128,Value=98,Width=85};
    private readonly DarkNumberBox progressThickness=new(){
        Minimum=3,Maximum=14,Value=5,Width=85};
    private readonly DarkNumberBox gameLogoSize=new(){
        Minimum=12,Maximum=54,Value=28,Width=85};
    private readonly Button gameLogoEditButton=new DarkButton{
        Text="Aktif oyun logosu değiştir...",Width=268,Height=35,
        BackColor=Color.FromArgb(38,68,82)};

    private readonly DarkNumberBox textWidth=new(){Minimum=16,Maximum=128,Value=96,Width=85};
    private readonly DarkNumberBox miniWidth=new(){Minimum=35,Maximum=128,Value=88,Width=100};
    private readonly DarkNumberBox barFrame=new(){Minimum=1,Maximum=2,Width=85};
    private readonly DarkNumberBox barFill=new(){Minimum=1,Maximum=5,Width=85};
    private readonly string savePath = Path.Combine(
        Beta5Sandbox.SettingsDirectory,"studio-v05.json");

    private LayoutState state = LayoutDefaults.Create();
    private BatteryPreferences batteryPreferences = BatteryPreferences.Load();
    private readonly DisplayPreferences displayPreferences = DisplayPreferences.Load();
    private BatterySnapshot batterySnapshot = new();
    private void CheckNativeHeaderBatteryChange(BatterySnapshot before,
        BatterySnapshot after)
    {
        if(dockPreview is null || !dockPreview.LiveOutputActive)return;
        // A minor 1% discharge does not justify an OEM-screen takeover.
        // Battery swaps typically make a much larger confirmed change.
        foreach(var kind in new[]{"Headset","SpareBattery"})
        {
            var old=before[kind];var fresh=after[kind];
            if(!old.Available||!fresh.Available)continue;
            if(!BatteryPriority.IsCurrent(old,DateTimeOffset.Now)||
               !BatteryPriority.IsCurrent(fresh,DateTimeOffset.Now))continue;
            if(Math.Abs(old.Percent!.Value-fresh.Percent!.Value)>=10)
            {
                dockPreview.OnBatteryPackChanged();
                break;
            }
        }
    }
    private readonly BatteryWarningMonitor warningMonitor=new();
    private readonly BatteryChargingMonitor chargingMonitor=new();
    private BatteryStatusDialog? batteryDialog;
    private SettingsDialog? settingsDialog;
    private readonly ArctisDialMonitor dialMonitor = new();
    private readonly SystemTelemetry telemetryReader = new();
    private SystemTelemetrySnapshot telemetry = new(null,null,null,
        DateTimeOffset.MinValue);
    private bool telemetryPolling;
    private int telemetryTick;
    private bool dialMonitorStarted;
    private int dialDispatchPending;
    private bool batteryPolling;
    private bool fastBatteryPolling;
    private int batteryRefreshCounter;
    private readonly DarkCheckBox autoMedia = new(){Text="Otomatik medya",Checked=true,AutoSize=true};
    private MediaSnapshot mediaSnapshot = MediaSnapshot.Empty;
    private bool mediaPolling;
    private bool MediaActive => autoMedia.Checked ? mediaSnapshot.IsPlaying : media.Checked;
    private bool SpotifyActive => autoMedia.Checked ? mediaSnapshot.IsSpotify : source.SelectedIndex==0;
    private MediaSnapshot? LiveMedia => autoMedia.Checked && mediaSnapshot.IsPlaying ? mediaSnapshot : null;
    private readonly Stack<LayoutState> undo = new();
    private readonly Stack<LayoutState> redo = new();
    private DockWidget? selected;
    private bool changeUi, drawing, dragging, gestureChanged;
    private Point dragAnchor;
    private int lastPixel = -1;

    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private DockPreviewPanel? dockPreview;
    private readonly PictureBox editor = new() { Left = 18, Top = 16, Width = CanvasWidth*6, Height = CanvasHeight*6, BackColor = Color.Black, BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Cross };
    private readonly ComboBox tool = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox kind = new() { Width = 267, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox kindCategory = new() {
        Width=267,DropDownStyle=ComboBoxStyle.DropDownList
    };
    private readonly Label kindHelp = new() {
        Width=268,Height=42,ForeColor=Color.FromArgb(151,169,187),
        Font=new Font("Segoe UI",8.5f),AutoEllipsis=true
    };
    private Button? catalogAddButton;
    private Label? catalogSummary;
    private bool rebuildingKindChoices;
    private readonly ComboBox dialStyle = new() { Width=153,DropDownStyle=ComboBoxStyle.DropDownList };
    private readonly Button mediaWidgetSettingsButton=new DarkButton(){
        Text="Widget ayarları...",Width=264,Height=36,
        BackColor=Color.FromArgb(38,68,82),
        ForeColor=Color.FromArgb(134,220,249),
        Cursor=Cursors.Hand
    };
    private readonly Button wheelEditorButton=new DarkButton(){
        Text="Tekerleği piksel piksel düzenle",Width=265,Height=34,
        BackColor=Color.FromArgb(34,58,76),
        ForeColor=Color.FromArgb(134,220,249),Cursor=Cursors.Hand};
    private readonly ComboBox source = new() { Width = 116, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListBox widgets = new() { Width = 232, Height = 106 };
    private readonly TextBox customText = new() { Width = 166,MaxLength=160 };
    private readonly DarkNumberBox fontSize = new() { Minimum=7,Maximum=24,Value=10,Width=85 };
    private readonly DarkNumberBox xPos = new() { Minimum=0,Maximum=127,Width=85 };
    private readonly DarkNumberBox yPos = new() { Minimum=10,Maximum=63,Width=85 };
    private readonly DarkCheckBox widgetVisible = new() { Text="Görünür", Checked=true,AutoSize=true };
    private readonly CheckBox media = new() { Text="Medya çalıyor (demo)", Checked=true, AutoSize=true };
    private readonly CheckBox showDate = new() { Text="Tarihi göster", Checked=true, AutoSize=true };
    private readonly DarkCheckBox showGrid = new() { Text="Piksel kılavuzu", AutoSize=true };
    private readonly Button undoButton = new DarkButton() { Text="↶ Geri al", Width=96,Height=30 };
    private readonly Button redoButton = new DarkButton() { Text="↷ İleri al", Width=96,Height=30 };
    private readonly Label status = new() { Width=755,Height=52,ForeColor=Color.FromArgb(44,170,104) };
    private readonly System.Windows.Forms.Timer ticker = new() { Interval=1000 };
    private readonly System.Windows.Forms.Timer animationTicker = new() { Interval=60 };
    private readonly SystemAudioVisualizer audioVisualizer=new();
    private bool mediaVisualsNeeded;

    public PixelStudioForm()
    {
        UiPolish.Register(this);
        Text = "Steelseries Dock Customiser 1.1.1 RC4.1 | OLED Studio";
        try { Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { /* Default system icon if running from a DLL in development. */ }
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1540,960);
        MinimumSize = new Size(1120,730);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        Font = new Font("Segoe UI",9f);

        Controls.Add(tabs);
        state=project.Media;
        var editTab = new TabPage("Tasarım & Piksel Editörü") { BackColor=Color.FromArgb(246,248,250) };
        var dockTab = new TabPage("Dock Preview") { BackColor=Color.FromArgb(246,248,250) };
        tabs.TabPages.Add(editTab); tabs.TabPages.Add(dockTab);
        editTab.Controls.Add(editor);
        dockPreview=new DockPreviewPanel(
            ()=>RenderScreen(editingMode),()=>project.ProtectHeader,
            ()=>DisplayBurnIn.Apply(RenderLiveScreen(),
                project.ProtectHeader&&!project.LiveOwnsDisplay,
                displayPreferences.BurnInDrift,DateTimeOffset.UtcNow),
            ()=>project.LiveOwnsDisplay);
        dockTab.Controls.Add(dockPreview);
        dockPreview.ConfigureModeTransitions(()=>ActiveMode,()=>displayPreferences);
        editor.Paint += (_,e) => PaintFrame(e.Graphics,true,Zoom);

        var subtitle = new Label { Left=18,Top=405,Width=780,Height=39,
            Text="OLED: 128 × 64 piksel. İlk 10 satır SteelSeries pil göstergelerine ayrıldı; fiziksel dock’a gönderilmez.\nÖğeleri sürüklemek için 'Taşı / Seç' aracını kullan." };
        editTab.Controls.Add(subtitle);

        tool.Items.AddRange(["Kalem","Silgi","Taşı / Seç"]);
        tool.SelectedIndex=2;
        kind.FormattingEnabled=true;
        kind.Format+=(_,e)=>{
            if(e.ListItem is string name)
                e.Value=WidgetLabels.Display(name);
        };
        kindCategory.SelectedIndexChanged+=(_,_)=>RefillCategoryKinds();
        kind.SelectedIndexChanged+=(_,_)=>UpdateKindHelp();
        // The legacy editor is kept in memory, but the visible modern
        // picker is now categorized and populated by UpdateKindChoices().
        dialStyle.Items.AddRange(["Bar","Wheel","MiniWheel"]);
        dialStyle.Format+=(_,e)=>
        {
            if(e.ListItem is string name)
                e.Value=name switch{
                    "Wheel"=>UiLanguage.T("Dairesel tekerlek"),
                    "MiniWheel"=>UiLanguage.T("Mini dairesel tekerlek"),
                    _=>UiLanguage.T("Düz çubuk")};
        };
        dialStyle.FormattingEnabled=true;
        dialStyle.SelectedIndex=0;
        wheelEditorButton.Click+=(_,_)=>EditWheelArtwork();
        mediaWidgetSettingsButton.Click+=(_,_)=>OpenOptionalMediaSettings();
        dialMonitor.Updated+=() =>
        {
            if(IsDisposed||!IsHandleCreated)return;
            // One UI dispatch owns the newest state; a fast physical spin
            // must not pile hundreds of stale preview frames in the queue.
            if(System.Threading.Interlocked.Exchange(ref dialDispatchPending,1)!=0)return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    System.Threading.Interlocked.Exchange(ref dialDispatchPending,0);
                    if(IsDisposed)return;
                    dockPreview?.OnPhysicalDialEvent();
                    Redraw();
                }));
            }
            catch(InvalidOperationException)
            {
                System.Threading.Interlocked.Exchange(ref dialDispatchPending,0);
            }
        };
        source.Items.AddRange(["Spotify","Diğer medya"]);
        source.SelectedIndex=0;

        var toolbar = new FlowLayoutPanel { Left=15,Top=444,Width=795,Height=42,WrapContents=false,AutoScroll=true };
        toolbar.Controls.AddRange([L("Araç:"),tool,L("Widget:"),kind,autoMedia,showGrid]);
        editTab.Controls.Add(toolbar);

        var actions = new FlowLayoutPanel { Left=15,Top=488,Width=795,Height=43,WrapContents=false };
        var add = B("Ekle",72);
        var remove = B("Sil",68);
        var save = B("Kaydet",76);
        var load = B("Yükle",76);
        var reset = B("Sıfırla",75);
        var export = B("PNG çıkar",90);
        var clear = B("Çizimi sil",92);
        actions.Controls.AddRange([undoButton,redoButton,add,remove,save,load,reset,export,clear]);
        editTab.Controls.Add(actions);

        var content = new FlowLayoutPanel { Left=18,Top=538,Width=785,Height=119,WrapContents=false };
        content.Controls.Add(widgets);
        var editorControls = new FlowLayoutPanel { Width=529,Height=112,FlowDirection=FlowDirection.TopDown,WrapContents=false };
        var line1 = new FlowLayoutPanel { Width=525,Height=37,WrapContents=false };
        line1.Controls.AddRange([L("Metin:"),customText,L("Boyut:"),fontSize,L("X:"),xPos,L("Y:"),yPos]);
        var line2 = new FlowLayoutPanel { Width=525,Height=37,WrapContents=false };
        line2.Controls.AddRange([widgetVisible,media,L("Kaynak:"),source,
            L("Kalınlık:"),strokeWidth]);
        var line3=new FlowLayoutPanel{ Width=525,Height=30,WrapContents=false };
        line3.Controls.AddRange([L("Mini Player genişliği:"),miniWidth,
            L("Müzik yokken alan siyah kalır")]);
        editorControls.Controls.AddRange([line1,line2,line3]);
        // Original keyboard shortcut hint is also available through tooltips.
        var keyboardTip=new ToolTip();
        keyboardTip.SetToolTip(editor,"Ctrl+Z / Ctrl+Y / Delete / Ok tuşları (Shift: 5 piksel)");

        content.Controls.Add(editorControls);editTab.Controls.Add(content);
        status.Left=18;status.Top=680;status.Width=256;editTab.Controls.Add(status);
        var clockDateButton=B("Saat / Tarih",159);
        clockDateButton.Left=287;clockDateButton.Top=674;
        editTab.Controls.Add(clockDateButton);
        clockDateButton.Click+=(_,_)=>OpenClockDateSettings();
        var batteryButton = B("Cihaz pilleri",152);
        batteryButton.Left=638;batteryButton.Top=674;
        editTab.Controls.Add(batteryButton);
        batteryButton.Click += (_,_)=>OpenBatteryDialog();
        var iconButton = B("İkon düzenleyici",160);
        iconButton.Left=467;iconButton.Top=674;
        editTab.Controls.Add(iconButton);
        iconButton.Click+=(_,_)=>new IconEditorDialog(Redraw).ShowDialog(this);
        status.Text="Modların düzenleri bağımsızdır. Fiziksel ekrana göndermek için Uygula veya Canlı'yı kullan.";
        // Three editor tabs, with one independent layout and undo history per mode.
        foreach(var c in editTab.Controls.Cast<Control>().ToArray())c.Top+=45;
        modeTabs.TabPages.Add("Medya çalarken");
        modeTabs.TabPages.Add("Oyun açıkken");
        modeTabs.TabPages.Add("Idle");
        editTab.Controls.Add(modeTabs);
        headerProtect.Checked=project.ProtectHeader;
        liveOwnsDisplay.Checked=project.LiveOwnsDisplay;
        editTab.Controls.Add(headerProtect);
        modeTabs.SelectedIndexChanged+=(_,_)=>SwitchEditingMode((ScreenMode)modeTabs.SelectedIndex);
        headerProtect.CheckedChanged+=(_,_)=>{
            project.ProtectHeader=headerProtect.Checked;
            foreach(var m in Enum.GetValues<ScreenMode>())
                StudioProject.Sanitize(project.For(m),m,project.ProtectHeader);
            yPos.Minimum=LockedRows;
            dockPreview?.OnProtectionChanged();
            Redraw();
        };
        liveOwnsDisplay.CheckedChanged+=(_,_)=>
        {
            project.LiveOwnsDisplay=liveOwnsDisplay.Checked;
            dockPreview?.OnProtectionChanged();
            Redraw();
        };
        UpdateKindChoices();
        undoButton.Click += (_,_) => Undo();
        redoButton.Click += (_,_) => Redo();
        add.Click += (_,_) => AddWidget();
        remove.Click += (_,_) => RemoveWidget();
        save.Click += (_,_) => SaveLayout();
        load.Click += (_,_) => LoadLayout();
        reset.Click += (_,_)=>
        {
            string modeName=editingMode switch
            {
                ScreenMode.Media=>"Medya",
                ScreenMode.Game=>"Oyun",
                _=>"Boşta"
            };
            if(MessageBox.Show(this,
                modeName+" ekranındaki öğeleri ve çizimi varsayılana döndürmek istiyor musun?\n"+
                "Bu işlemden sonra Geri Al seçeneği kullanılabilir.",
                "Tasarımı sıfırla",MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=
                DialogResult.Yes)return;
            Record();
            state=StudioProject.UserDefault(editingMode);
            project.Set(editingMode,state);
            selected=null;RefreshWidgetList();Redraw();
            SetStatus(modeName+" ekranı sıfırlandı. Geri Al ile dönebilirsin.");
        };
        clear.Click += (_,_) => { if(state.Pixels.Any(x=>x)){Record();Array.Clear(state.Pixels,0,state.Pixels.Length);Redraw();SetStatus("Serbest çizim pikselleri temizlendi.");} };
        export.Click += (_,_) => ExportPng();

        widgets.SelectedIndexChanged += (_,_) =>
        {
            if(changeUi)return;
            selected=widgets.SelectedItem as DockWidget;
            UpdateEditorControls();Redraw();
        };
        media.Text="Medya demo";
        media.Enabled=false;
        source.Enabled=false;
        autoMedia.CheckedChanged += (_,_) =>
        {
            media.Enabled=!autoMedia.Checked;
            source.Enabled=!autoMedia.Checked;
            Redraw();
        };
        media.CheckedChanged += (_,_) => Redraw();
        showDate.CheckedChanged += (_,_) => Redraw();
        new ToolTip().SetToolTip(showGrid,
            "Yalnızca büyütülmüş editörde her OLED pikselinin hücre sınırını gösterir.\nGerçek OLED veya Dock Preview'da görünmez.");
        showGrid.CheckedChanged += (_,_) => editor.Invalidate();
        source.SelectedIndexChanged += (_,_) => Redraw();
        customText.Leave += (_,_) => ChangeText();
        customText.KeyDown += (_,e) => { if(e.KeyCode==Keys.Enter){ChangeText();e.SuppressKeyPress=true;} };
        fontSize.ValueChanged += (_,_) => ChangeNumber();
        strokeWidth.ValueChanged += (_,_) => ChangeNumber();
        textWidth.ValueChanged += (_,_) => ChangeNumber();
        progressWidth.ValueChanged += (_,_) => ChangeNumber();
        progressThickness.ValueChanged += (_,_) => ChangeNumber();
        gameLogoSize.ValueChanged += (_,_) => ChangeNumber();
        gameLogoEditButton.Click+=(_,_)=>EditGameLogo();
        miniWidth.ValueChanged += (_,_) => ChangeNumber();
        barFrame.ValueChanged += (_,_) => ChangeNumber();
        barFill.ValueChanged += (_,_) => ChangeNumber();
        xPos.ValueChanged += (_,_) => ChangeNumber();
        yPos.ValueChanged += (_,_) => ChangeNumber();
        widgetVisible.CheckedChanged += (_,_) =>
        {
            if(changeUi||selected==null||selected.Visible==widgetVisible.Checked)return;
            Record();selected.Visible=widgetVisible.Checked;Redraw();
        };
        dialStyle.SelectedIndexChanged+=(_,_)=>
        {
            if(changeUi||selected?.Kind!="VolumeChatMix")return;
            string value=dialStyle.SelectedItem?.ToString()??"Bar";
            if(value==selected.DialStyle)return;
            Record();selected.DialStyle=value;
            RefreshWidgetList();Redraw();
        };

        editor.MouseDown += BeginGesture;
        editor.MouseMove += ContinueGesture;
        editor.MouseUp += EndGesture;
        editor.MouseLeave += (_,_) => EndGesture(null,null);
        KeyDown += HandleKeys;
        animationTicker.Tick+=(_,_)=>
        {
            if(mediaVisualsNeeded && !IsDisposed)
            {
                editor.Invalidate();
                dockPreview?.InvalidateFrame();
            }
        };
        ticker.Tick += (_,_) =>
        {
            RefreshOptionalMediaServices();
            bool dialNeeded=Enum.GetValues<ScreenMode>().Any(m=>
                project.For(m).Widgets.Any(w=>w.Kind=="VolumeChatMix"&&w.Visible));
            if(dialNeeded!=dialMonitorStarted)
            {
                if(dialNeeded)dialMonitor.Start();
                else dialMonitor.Stop();
                dialMonitorStarted=dialNeeded;
            }
            // One frame deadline every 1,000 ms. Actual OLED writes are
            // skipped when unchanged, and in-flight sends never overlap.
            Redraw();
            _=RefreshMediaAsync();
            if(++gameCounter>=3)
            {
                gameCounter=0;
                _=RefreshGameAsync();
            }
            if(++controllerCounter>=10)
            {
                controllerCounter=0;
                _=RefreshControllerConnectionAsync();
            }
            if(++batteryRefreshCounter>=10)
            {
                batteryRefreshCounter=0;
                _=RefreshBatteriesAsync();
            }
            else _=RefreshFastBatteriesAsync();
            if(++telemetryTick>=5)
            {
                telemetryTick=0;
                _=RefreshTelemetryAsync();
            }
        };
        BuildModernShell(editTab,add,remove,save,load,reset,export,clear,
            clockDateButton,batteryButton,iconButton);
        // Begin with the user's personal Idle/Boşta layout in the editor.
        // Automatic Game > Media > Idle priority for the LIVE dock is unchanged.
        SelectModernMode(ScreenMode.Idle);
        // Start as soon as the form is constructed, not at the next 1s
        // timer tick: initial ChatMix/Volume input must update immediately.
        if(Enum.GetValues<ScreenMode>().Any(m=>
            project.For(m).Widgets.Any(w=>w.Kind=="VolumeChatMix"&&w.Visible)))
        {
            dialMonitor.Start();
            dialMonitorStarted=true;
        }
        ticker.Start();
        FormClosing += HandleTrayClosing;
        FormClosed += (_,_) => {
            ticker.Stop();animationTicker.Stop();animationTicker.Dispose();
            audioVisualizer.Dispose();EndBatteryDeviceWatching();
            dialMonitor.Dispose();DisposeTray();};
        Shown += async (_,_) =>
        {
            bool actualStartup=!captureMode&&!traySmokeMode;
            BootOverlay? loading=null;
            ticker.Stop();
            if(actualStartup)
            {
                loading=new BootOverlay();
                Controls.Add(loading);loading.BringToFront();
                await System.Threading.Tasks.Task.Yield();
            }
            try
            {
                if(!captureMode){InitializeTray();LoadMediaPreference();}
                if(actualStartup)BeginBatteryDeviceWatching();
                // Live startup and slow optional providers run concurrently.
                // A disconnected phone must never keep the UI locked.
                var live=actualStartup&&dockPreview is not null
                    ?dockPreview.StartLiveOnLaunchAsync()
                    :System.Threading.Tasks.Task.CompletedTask;
                if(!traySmokeMode)
                {
                    var initial=System.Threading.Tasks.Task.WhenAll(
                        live,RefreshGameAsync(),RefreshMediaAsync(),
                        RefreshBatteriesAsync());
                    var complete=await System.Threading.Tasks.Task.WhenAny(
                        initial,System.Threading.Tasks.Task.Delay(4400));
                    if(complete==initial)
                    {
                        try{await initial;}catch{
                            // Optional device errors keep unknown readings.
                        }
                    }
                }
            }
            finally
            {
                if(!IsDisposed)
                {
                    loading?.Dispose();
                    ticker.Start();
                    if(actualStartup&&!FirstRunGuideState.Completed())
                        BeginInvoke((Action)(()=>ShowFirstRunGuide()));
                }
            }
        };
        RefreshWidgetList();UpdateHistoryButtons();Redraw();
        tabs.SelectedIndex=1; // Open the photographic preview first.
    }

    private static readonly string[] AllWidgetKinds=[
        "BatterySlot1","BatterySlot2","BatterySlot3","Mouse","Phone",
        "DualSense","Laptop","Headset","SpareBattery","Source","Track",
        "Artist","Progress","MediaClock","Lyrics","AudioVisualizer","AnimatedGif",
        "GameLabel","GameTitle","GameLogo","GameMiniPlayer",
        "Clock","Date","VolumeChatMix","SystemCpu","SystemRam",
        "NetworkPing","Custom text"
    ];

    private ScreenMode ActiveMode =>
        StudioProject.Choose(gameSnapshot.IsRunning,MediaActive);

    private Bitmap RenderScreen(ScreenMode mode,bool liveFull=false)
    {
        var frame=OledRenderer.Render(project.For(mode),mode==ScreenMode.Media,true,
            SpotifyActive,batterySnapshot,
            mode==ScreenMode.Game ? (mediaSnapshot.IsPlaying ? mediaSnapshot : null) : LiveMedia,
            gameSnapshot,
            batteryPreferences,project.ProtectHeader&&!liveFull,mode,dialMonitor.Snapshot,
            telemetry,SyncedLyricsProvider.Current(mediaSnapshot),
            audioVisualizer.Snapshot,gameLogoSnapshot);
        if(liveFull&&project.ProtectHeader)
        {
            // Full ownership cannot retain the firmware's status strip.
            // Recreate it with three real, user-prioritized battery slots;
            // only this live bitmap gets the automatic header. Do not alter
            // the saved design or the user's protected editing coordinates.
            var stripLayout=new LayoutState{Widgets=[
                new DockWidget{Kind="BatterySlot1",X=0,Y=0},
                new DockWidget{Kind="BatterySlot2",X=43,Y=0},
                new DockWidget{Kind="BatterySlot3",X=86,Y=0}
            ]};
            using var strip=OledRenderer.Render(stripLayout,false,true,false,
                batterySnapshot,preferences:batteryPreferences,protectHeader:false);
            for(int y=0;y<10;y++)for(int x=0;x<128;x++)
                frame.SetPixel(x,y,strip.GetPixel(x,y));
        }
        return frame;
    }

    private Bitmap RenderLiveScreen()=>
        RenderScreen(ActiveMode,project.LiveOwnsDisplay);

    private async System.Threading.Tasks.Task RefreshTelemetryAsync()
    {
        if(telemetryPolling||IsDisposed)return;
        string[] keys=["SystemCpu","SystemRam","NetworkPing"];
        bool needed=Enum.GetValues<ScreenMode>().Any(mode=>
            project.For(mode).Widgets.Any(w=>w.Visible&&keys.Contains(w.Kind)));
        if(!needed)return;
        telemetryPolling=true;
        try
        {
            var next=await System.Threading.Tasks.Task.Run(telemetryReader.Read);
            bool needsPing=Enum.GetValues<ScreenMode>().Any(mode=>
                project.For(mode).Widgets.Any(w=>w.Visible&&w.Kind=="NetworkPing"));
            if(needsPing)next=next with{Ping=await SystemTelemetry.ReadPingAsync()};
            telemetry=next;
            if(!IsDisposed)Redraw();
        }
        catch { /* Show unknown measurements, never fake a live percentage. */ }
        finally{telemetryPolling=false;}
    }

    private void SwitchEditingMode(ScreenMode next)
    {
        if(editingMode==next)return;
        project.Set(editingMode,state);
        editingMode=next;
        state=project.For(next);
        selected=null;undo.Clear();redo.Clear();UpdateHistoryButtons();
        UpdateKindChoices();RefreshWidgetList();Redraw();
        UpdateModernUi();
    }

    internal void SelectWidgetCategoryForCapture(string key)
    {
        var category=kindCategory.Items.Cast<WidgetCatalog.Category>()
            .FirstOrDefault(c=>c.Key==key);
        if(category!=null)kindCategory.SelectedItem=category;
    }

    private void UpdateKindChoices()
    {
        if(rebuildingKindChoices)return;
        string? selectedKind=kind.SelectedItem as string;
        string? currentCategory=(kindCategory.SelectedItem as
            WidgetCatalog.Category)?.Key;
        rebuildingKindChoices=true;
        try
        {
            var categories=WidgetCatalog.AvailableCategories(
                editingMode,state.Widgets);
            kindCategory.Items.Clear();
            kindCategory.Items.AddRange(categories.Cast<object>().ToArray());
            kindCategory.SelectedItem=categories.FirstOrDefault(
                c=>c.Key==currentCategory)??categories.FirstOrDefault();
            FillKinds(selectedKind);
        }
        finally{rebuildingKindChoices=false;}
        UpdateKindHelp();
    }

    private void RefillCategoryKinds()
    {
        if(rebuildingKindChoices)return;
        rebuildingKindChoices=true;
        try{FillKinds(null);}
        finally{rebuildingKindChoices=false;}
        UpdateKindHelp();
    }

    private void FillKinds(string? previous)
    {
        string? category=(kindCategory.SelectedItem as
            WidgetCatalog.Category)?.Key;
        var available=category is null?Array.Empty<string>():
            WidgetCatalog.Available(editingMode,category,state.Widgets)
            .ToArray();
        kind.Items.Clear();
        kind.Items.AddRange(available.Cast<object>().ToArray());
        kind.SelectedItem=previous is not null && available.Contains(previous)
            ?previous:(available.Length>0?available[0]:null);
    }

    private void UpdateKindHelp()
    {
        if(rebuildingKindChoices)return;
        string? value=kind.SelectedItem as string;
        kindHelp.Text=value is null
            ?(UiLanguage.English
                ?"No more widgets available in this category."
                :"Bu kategoride eklenebilecek başka öğe bulunmuyor.")
            :WidgetCatalog.Description(value);
        if(catalogAddButton is not null)
            catalogAddButton.Enabled=value is not null;
        if(catalogSummary is not null)
        {
            int remaining=WidgetCatalog.Categories.Sum(c=>
                WidgetCatalog.Available(editingMode,c.Key,
                    state.Widgets).Count);
            catalogSummary.Text=UiLanguage.English
                ?$"{state.Widgets.Count} added  •  {remaining} available"
                :$"{state.Widgets.Count} öğe eklendi  •  {remaining} eklenebilir";
        }
    }

    private async System.Threading.Tasks.Task RefreshGameAsync()
    {
        if(gamePolling||IsDisposed)return;
        gamePolling=true;
        try{
            var latest=await GgGameDetector.PollAsync();
            if(IsDisposed)return;
            if(!Equals(latest,gameSnapshot))
            {
                gameSnapshot=latest;
                gameLogoSnapshot=null;
                Redraw();
                if(latest.IsRunning)_=RefreshGameLogoAsync(latest);
            }
        }catch{}finally{gamePolling=false;}
    }

    private async System.Threading.Tasks.Task RefreshGameLogoAsync(GameSnapshot game)
    {
        try
        {
            var icon=await System.Threading.Tasks.Task.Run(()=>
                GameLogoProvider.Resolve(game));
            if(IsDisposed || !Equals(gameSnapshot,game))return;
            gameLogoSnapshot=icon;
            Redraw();
        }
        catch { /* Absence of a game icon never blocks OLED Studio. */ }
    }

    private async System.Threading.Tasks.Task RefreshControllerConnectionAsync()
    {
        if(controllerConnectionPolling||batteryPolling||IsDisposed)return;
        controllerConnectionPolling=true;
        try{
            bool connected=await DualSenseConnection.IsConnectedAsync();
            if(IsDisposed||connected==batterySnapshot.DualSenseConnected)return;
            batterySnapshot=new BatterySnapshot{
                CheckedAt=batterySnapshot.CheckedAt,Values=batterySnapshot.Values,
                DualSenseConnected=connected
            };
            Redraw();batteryDialog?.UpdateRows();settingsDialog?.UpdateBatteryRows();
        }catch{}finally{controllerConnectionPolling=false;}
    }

    private async System.Threading.Tasks.Task RefreshMediaAsync()
    {
        if(mediaPolling||IsDisposed)return;
        mediaPolling=true;
        try
        {
            var newest=await MediaSessionProvider.PollAsync();
            if(IsDisposed)return;
            mediaSnapshot=newest;
            Redraw();
            if(newest.IsPlaying && Enum.GetValues<ScreenMode>().Any(mode=>
                project.For(mode).Widgets.Any(w=>w.Kind=="Lyrics"&&w.Visible)))
                _=LoadSyncedLyricsAsync(newest);
        }
        catch {mediaSnapshot=MediaSnapshot.Empty;}
        finally {mediaPolling=false;}
    }

    private async System.Threading.Tasks.Task LoadSyncedLyricsAsync(
        MediaSnapshot track)
    {
        try
        {
            await SyncedLyricsProvider.EnsureAsync(track);
            if(!IsDisposed && mediaSnapshot.IsPlaying &&
               SyncedLyricsProvider.Key(mediaSnapshot)==SyncedLyricsProvider.Key(track))
                Redraw();
        }
        catch{/* Lyrics are optional; media and OLED never depend on network. */}
    }

    private void RefreshOptionalMediaServices()
    {
        // Listen only if the live mode or the currently edited preview
        // uses a visible visualizer; inactive saved modes must cost nothing.
        bool audioNeeded=!captureMode&&!traySmokeMode &&
            new[]{ActiveMode,editingMode}.Distinct().Any(mode=>
                project.For(mode).Widgets.Any(w=>
                    w.Visible&&w.Kind=="AudioVisualizer"));
        string endpoint=audioNeeded?new[]{ActiveMode,editingMode}.Distinct()
            .SelectMany(mode=>project.For(mode).Widgets)
            .FirstOrDefault(w=>w.Visible&&w.Kind=="AudioVisualizer")
            ?.AudioEndpointId??"":"";
        audioVisualizer.SetEnabled(audioNeeded,endpoint);
        bool any=Enum.GetValues<ScreenMode>().Any(mode=>
            project.For(mode).Widgets.Any(w=>w.Visible&&
                w.Kind is "Lyrics" or "AudioVisualizer" or "AnimatedGif"));
        // A fast marquee needs 60ms preview frames too, but avoid polling
        // when every visible text fits its user-selected width.
        bool marquee=new[]{ActiveMode,editingMode}.Distinct().Any(mode=>
            project.For(mode).Widgets.Any(w=>w.Visible&&
                OledRenderer.IsTextWidget(w.Kind) &&
                OledRenderer.NeedsMarquee(w,w.Kind switch
                {
                    "GameTitle"=>gameSnapshot.IsRunning
                        ?gameSnapshot.Name:"GAME NOT RUNNING",
                    "GameMiniPlayer"=>mediaSnapshot.IsPlaying
                        ?mediaSnapshot.Title:"PLAYING",
                    "SystemCpu"=>"CPU 100%",
                    "SystemRam"=>"RAM 100%",
                    "NetworkPing"=>"PING 9999MS",
                    _=>OledRenderer.DisplayText(w,mediaSnapshot)
                })));
        mediaVisualsNeeded=any||marquee;
        if(mediaVisualsNeeded&&!animationTicker.Enabled)animationTicker.Start();
        else if(!mediaVisualsNeeded&&animationTicker.Enabled)animationTicker.Stop();
    }

    private void OpenClockDateSettings()
    {
        if(editingMode==ScreenMode.Media)
        {
            selected=state.Widgets.FirstOrDefault(w=>w.Kind=="MediaClock");
            if(selected==null)
            {
                Record();
                selected=new DockWidget{Kind="MediaClock",X=90,Y=11,FontSize=7};
                state.Widgets.Add(selected);
            }
            RefreshWidgetList();
            SetStatus("Küçük medya saatini Boyut / Kalınlık / X / Y alanlarından düzenle.");
            return;
        }
        using var dialog=new ClockDateSettingsDialog(state,(clockFmt,clockStroke,dateFmt,dateStroke)=>
        {
            Record();
            var clock=state.Widgets.FirstOrDefault(w=>w.Kind=="Clock");
            var date=state.Widgets.FirstOrDefault(w=>w.Kind=="Date");
            if(clock==null)
            {
                clock=new DockWidget{Kind="Clock",X=36,Y=17,FontSize=20};
                state.Widgets.Add(clock);
            }
            if(date==null)
            {
                date=new DockWidget{Kind="Date",X=35,Y=37,FontSize=10};
                state.Widgets.Add(date);
            }
            clock.Format=clockFmt;
            clock.StrokeWidth=clockStroke;
            date.Format=dateFmt;
            date.StrokeWidth=dateStroke;
            RefreshWidgetList();Redraw();
            SetStatus("Saat ve tarih ayarları güncellendi. Kaydet ile sakla.");
        });
        dialog.ShowDialog(this);
    }

    private async System.Threading.Tasks.Task RefreshFastBatteriesAsync()
    {
        if(batteryPolling||fastBatteryPolling||IsDisposed)return;
        fastBatteryPolling=true;
        try
        {
            var updated=await BatterySources.PollRealtimeAsync(batterySnapshot);
            if(IsDisposed)return;
            var before=batterySnapshot;
            batterySnapshot=updated;
            CheckNativeHeaderBatteryChange(before,updated);
            foreach(var (kind,pct) in chargingMonitor.Evaluate(
                updated,DateTimeOffset.Now))
                dockPreview?.QueueChargingAlert(kind,pct,updated[kind].IconKind,
                    UiLanguage.English);
            batteryDialog?.UpdateRows();settingsDialog?.UpdateBatteryRows();
            Redraw();
        }
        catch{}
        finally {fastBatteryPolling=false;}
    }

    private async System.Threading.Tasks.Task RefreshBatteriesAsync()
    {
        if(batteryPolling||fastBatteryPolling||IsDisposed)return;
        batteryPolling=true;
        try
        {
            var newest=await BatterySources.PollAsync();
            if(IsDisposed)return;
            batterySnapshot=newest;
            foreach(var (kind,pct) in chargingMonitor.Evaluate(
                newest,DateTimeOffset.Now))
                dockPreview?.QueueChargingAlert(kind,pct,newest[kind].IconKind,
                    UiLanguage.English);
            foreach(var (kind,pct) in warningMonitor.Evaluate(
                newest,batteryPreferences,DateTimeOffset.Now))
            {
                NotifyLowBattery(kind,pct);
                if(pct<=20)
                    dockPreview?.QueueBatteryAlert(kind,pct,newest[kind].IconKind,
                        critical:pct<=10,english:UiLanguage.English);
            }
            Redraw();
            batteryDialog?.UpdateRows();settingsDialog?.UpdateBatteryRows();
            SetStatus("Piller: "+
                string.Join(" · ",BatteryPriority.SelectThree(newest,batteryPreferences)
                    .Select(k=>k+" "+newest[k].Display))+
                " | GG canlı; diğer pil kaynakları 10 sn");
        }
        catch(Exception ex)
        {
            if(!IsDisposed)SetStatus("Pil kaynağı okunamadı: "+ex.GetType().Name);
        }
        finally { batteryPolling=false; }
    }

    private void OpenBatteryDialog()
    {
        if(batteryDialog is { IsDisposed:false })
        {
            batteryDialog.Activate();return;
        }
        batteryDialog=new BatteryStatusDialog(batteryPreferences,
            ()=>batterySnapshot,RefreshBatteriesAsync,
            ()=>{
                Redraw();
                _=RefreshBatteriesAsync();
            });
        batteryDialog.Show(this);
        _=RefreshBatteriesAsync();
    }

    private static Button B(string text,int width) => new DarkButton(){Text=text,Width=width,Height=30};
    private static Label L(string text) => new(){Text=text,AutoSize=true,Padding=new Padding(2,8,2,0)};
    private void SetStatus(string message) => status.Text=message;
    private void Redraw()
    {
        editor.Invalidate();
        dockPreview?.InvalidateFrame();
        UpdateModernUi();
    }
    private void UpdateHistoryButtons(){undoButton.Enabled=undo.Count>0;redoButton.Enabled=redo.Count>0;}
    private void Record()
    {
        undo.Push(state.DeepClone());
        if(undo.Count>100){var limited=undo.Take(100).Reverse().ToArray();undo.Clear();foreach(var v in limited)undo.Push(v);}
        redo.Clear();UpdateHistoryButtons();
    }

    private void Undo()
    {
        if(undo.Count==0)return;
        redo.Push(state.DeepClone());state=undo.Pop();project.Set(editingMode,state);
        selected=null;RefreshWidgetList();Redraw();UpdateHistoryButtons();SetStatus("Son işlem geri alındı.");
    }
    private void Redo()
    {
        if(redo.Count==0)return;
        undo.Push(state.DeepClone());state=redo.Pop();project.Set(editingMode,state);
        selected=null;RefreshWidgetList();Redraw();UpdateHistoryButtons();SetStatus("İşlem yeniden uygulandı.");
    }
    private void RefreshWidgetList()
    {
        changeUi=true;widgets.Items.Clear();
        foreach(var w in state.Widgets)widgets.Items.Add(w);
        widgets.SelectedItem=selected;
        changeUi=false;UpdateEditorControls();
        UpdateKindChoices();
        UpdateModernUi();
    }
    private void UpdateEditorControls()
    {
        changeUi=true;
        bool has=selected!=null;
        customText.Enabled=has && selected!.Kind is "Custom text" or "GameLabel";
        fontSize.Enabled=has && OledRenderer.IsTextWidget(selected!.Kind);
        strokeWidth.Enabled=fontSize.Enabled||has&&selected!.Kind=="Lyrics";
        textWidth.Enabled=strokeWidth.Enabled;
        fontSize.Enabled=fontSize.Enabled&&selected!.Kind!="Lyrics";
        progressWidth.Enabled=has && selected!.Kind=="Progress";
        progressThickness.Enabled=progressWidth.Enabled;
        gameLogoSize.Enabled=has && selected!.Kind=="GameLogo";
        miniWidth.Enabled=has && selected!.Kind is "GameMiniPlayer" or "VolumeChatMix";
        barFrame.Enabled=has && selected!.Kind=="VolumeChatMix"&&selected.DialStyle=="Bar";
        barFill.Enabled=barFrame.Enabled;
        dialStyle.Enabled=has && selected!.Kind=="VolumeChatMix";
        widgetVisible.Enabled=has && !StudioProject.IsLocked(editingMode,selected!.Kind);
        xPos.Enabled=has;yPos.Enabled=has;
        if(selected!=null)
        {
            customText.Text=selected.Text;
            fontSize.Value=Math.Clamp(selected.FontSize,7,24);
            strokeWidth.Value=Math.Clamp(selected.StrokeWidth,1,3);
            progressWidth.Value=Math.Clamp(selected.ProgressWidth,12,128);
            progressThickness.Value=Math.Clamp(selected.ProgressThickness,3,14);
            gameLogoSize.Value=Math.Clamp(selected.GameLogoSize,12,54);
            textWidth.Value=Math.Clamp(selected.Kind=="Lyrics"
                ?selected.WidgetWidth:selected.Kind=="GameMiniPlayer"
                ?selected.MiniWidth:selected.TextWidth>0
                    ?selected.TextWidth:128-selected.X,16,128);
            miniWidth.Minimum=selected.Kind=="VolumeChatMix"?60:35;
            miniWidth.Value=Math.Clamp(selected.MiniWidth,(int)miniWidth.Minimum,128);
            barFrame.Value=Math.Clamp(selected.BarFrameThickness,1,2);
            barFill.Value=Math.Clamp(selected.BarFillThickness,1,5);
            dialStyle.SelectedItem=selected.DialStyle;
            xPos.Value=Math.Clamp(selected.X,0,127);yPos.Value=Math.Clamp(selected.Y,LockedRows,63);
            widgetVisible.Checked=selected.Visible;
        }
        else { customText.Text="";widgetVisible.Checked=false;miniWidth.Minimum=35; }
        changeUi=false;
        UpdateModernUi();
    }
    private void ChangeText()
    {
        if(changeUi||selected==null||selected.Kind is not ("Custom text" or "GameLabel")||selected.Text==customText.Text)return;
        Record();selected.Text=customText.Text;RefreshWidgetList();Redraw();
    }
    private void EditWheelArtwork()
    {
        if(selected?.Kind!="VolumeChatMix"||
            selected.DialStyle is not ("Wheel" or "MiniWheel"))return;
        bool compact=selected.DialStyle=="MiniWheel";
        using var dialog=new WheelArtworkDialog(
            compact?selected.MiniWheelPixels:selected.WheelPixels,
            selected.WheelAutoLabel,selected.WheelGapAngle,
            selected.WheelIconsFollowRing,selected.WheelGameOffsetX,
            selected.WheelGameOffsetY,selected.WheelChatOffsetX,
            selected.WheelChatOffsetY,selected.WheelShowGame,
            selected.WheelShowChat,selected.WheelCloseEnds,
            selected.WheelRingThickness,compact,selected.MiniWheelSize);
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        if(dialog.UseBar)
        {
            Record();selected.DialStyle="Bar";
            RefreshWidgetList();Redraw();
            return;
        }
        if((compact?selected.MiniWheelPixels:selected.WheelPixels)==dialog.Artwork &&
           (!compact||selected.MiniWheelSize==dialog.CompactSize) &&
           selected.WheelAutoLabel==dialog.AutoLabel &&
           selected.WheelGapAngle==dialog.GapAngle &&
           selected.WheelIconsFollowRing==dialog.FollowModeIcons &&
           selected.WheelGameOffsetX==dialog.GameDX &&
           selected.WheelGameOffsetY==dialog.GameDY &&
           selected.WheelChatOffsetX==dialog.ChatDX &&
           selected.WheelChatOffsetY==dialog.ChatDY &&
           selected.WheelShowGame==dialog.ShowGameLogo &&
           selected.WheelShowChat==dialog.ShowChatLogo &&
           selected.WheelCloseEnds==dialog.CloseRingEnds &&
           selected.WheelRingThickness==dialog.RingThickness)return;
        Record();
        if(compact)
        {
            selected.MiniWheelPixels=dialog.Artwork;
            selected.MiniWheelSize=dialog.CompactSize;
        }
        else selected.WheelPixels=dialog.Artwork;
        selected.WheelAutoLabel=dialog.AutoLabel;
        selected.WheelGapAngle=dialog.GapAngle;
        selected.WheelIconsFollowRing=dialog.FollowModeIcons;
        selected.WheelGameOffsetX=dialog.GameDX;
        selected.WheelGameOffsetY=dialog.GameDY;
        selected.WheelChatOffsetX=dialog.ChatDX;
        selected.WheelChatOffsetY=dialog.ChatDY;
        selected.WheelShowGame=dialog.ShowGameLogo;
        selected.WheelShowChat=dialog.ShowChatLogo;
        selected.WheelCloseEnds=dialog.CloseRingEnds;
        selected.WheelRingThickness=dialog.RingThickness;
        Redraw();
        SetStatus(UiLanguage.T("Tekerlek pikselleri güncellendi. Kaydet ile sakla."));
    }
    private void ChangeNumber()
    {
        if(changeUi||selected==null)return;
        int newX=(int)xPos.Value,newY=(int)yPos.Value,newSize=(int)fontSize.Value;
        int newWeight=(int)strokeWidth.Value;
        int newTextWidth=(int)textWidth.Value;
        int newMiniWidth=selected.Kind=="GameMiniPlayer"&&textWidth.Enabled
            ?newTextWidth:(int)miniWidth.Value;
        int effectiveWidth=selected.TextWidth>0
            ?selected.TextWidth:Math.Clamp(128-selected.X,16,128);
        int savedTextWidth=textWidth.Enabled && selected.Kind is not
            ("GameMiniPlayer" or "Lyrics")
            ?(newTextWidth==effectiveWidth?selected.TextWidth:newTextWidth)
            :selected.TextWidth;
        int newLyricWidth=selected.Kind=="Lyrics"&&textWidth.Enabled
            ?newTextWidth:selected.WidgetWidth;
        int newProgressWidth=progressWidth.Enabled?(int)progressWidth.Value:
            selected.ProgressWidth;
        int newProgressThickness=progressThickness.Enabled
            ?(int)progressThickness.Value:selected.ProgressThickness;
        int newGameLogoSize=gameLogoSize.Enabled?(int)gameLogoSize.Value:
            selected.GameLogoSize;
        int newFrame=(int)barFrame.Value,newFill=(int)barFill.Value;
        if(selected.X==newX&&selected.Y==newY&&selected.FontSize==newSize&&
           selected.StrokeWidth==newWeight&&selected.MiniWidth==newMiniWidth&&
           selected.TextWidth==savedTextWidth&&
           selected.WidgetWidth==newLyricWidth&&
           selected.ProgressWidth==newProgressWidth&&
           selected.ProgressThickness==newProgressThickness&&
           selected.GameLogoSize==newGameLogoSize&&
           selected.BarFrameThickness==newFrame&&
           selected.BarFillThickness==newFill)return;
        Record();selected.X=newX;selected.Y=newY;selected.FontSize=newSize;
        selected.StrokeWidth=newWeight;selected.MiniWidth=newMiniWidth;
        selected.TextWidth=savedTextWidth;
        selected.WidgetWidth=newLyricWidth;
        selected.ProgressWidth=newProgressWidth;
        selected.ProgressThickness=newProgressThickness;
        selected.GameLogoSize=newGameLogoSize;
        selected.BarFrameThickness=newFrame;selected.BarFillThickness=newFill;
        RefreshWidgetList();Redraw();
    }
    private void AddWidget()
    {
        if(kind.SelectedItem is not string type)return;
        if(!WidgetCatalog.CanAdd(editingMode,type,state.Widgets))
        {
            var already=state.Widgets.FirstOrDefault(w=>w.Kind==type);
            if(already is not null){selected=already;RefreshWidgetList();}
            SetStatus("Bu öğe bu ekranda zaten bulunuyor veya bu moda uygun değil.");
            return;
        }
        if(state.Widgets.Count>=70){SetStatus("En fazla 70 widget.");return;}
        string gifId="";
        if(type=="AnimatedGif")
        {
            using var picker=new OpenFileDialog{
                Filter="Animasyonlu GIF|*.gif",
                Title="OLED için GIF seç",
                CheckFileExists=true};
            if(picker.ShowDialog(this)!=DialogResult.OK)return;
            try{gifId=AnimatedGifWidgetStore.Import(picker.FileName);}
            catch(Exception ex)
            {
                MessageBox.Show(this,ex.Message,"GIF eklenemedi",
                    MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
        }
        Record();
        var w=new DockWidget{Kind=type,X=5,Y=19};
        if(type=="MediaClock")w.FontSize=7;
        if(type=="Lyrics"){w.X=8;w.Y=34;w.WidgetWidth=114;w.FontSize=7;}
        if(type=="AudioVisualizer")
        {w.X=8;w.Y=18;w.WidgetWidth=112;w.VisualizerHeight=26;}
        if(type=="AnimatedGif")
        {
            w.X=16;w.Y=15;w.WidgetWidth=96;w.VisualizerHeight=34;
            w.GifAssetId=gifId;
            AnimatedGifWidgetStore.EnsureBackground(w);
        }
        if(type=="GameMiniPlayer")
        {
            w.X=4;w.Y=38;w.FontSize=7;w.MiniWidth=88;
        }
        if(type=="GameLogo")
        {
            w.X=96;w.Y=37;w.GameLogoSize=16;
        }
        if(type=="VolumeChatMix")
        {
            w.X=2;w.Y=11;w.MiniWidth=104;w.DialStyle="Wheel";
        }
        if(type=="GameLabel")w.Text="NOW PLAYING:";
        state.Widgets.Add(w);selected=w;RefreshWidgetList();Redraw();tool.SelectedIndex=2;
        if(type is "Lyrics" or "AudioVisualizer" or "AnimatedGif")
            OpenOptionalMediaSettings();
    }
    private void OpenOptionalMediaSettings()
    {
        if(selected?.Kind is not ("Lyrics" or "AudioVisualizer" or
            "AnimatedGif"))return;
        using var dialog=new OptionalMediaWidgetDialog(selected);
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        var updated=dialog.Updated;
        Record();
        selected.LyricOffsetMs=updated.LyricOffsetMs;
        selected.LyricLines=updated.LyricLines;
        selected.VisualizerStyle=updated.VisualizerStyle;
        selected.AudioEndpointId=updated.AudioEndpointId;
        selected.VisualizerGain=updated.VisualizerGain;
        selected.VisualizerHeight=updated.VisualizerHeight;
        selected.WidgetWidth=updated.WidgetWidth;
        selected.GifAssetId=updated.GifAssetId;
        selected.GifFit=updated.GifFit;
        selected.GifDither=updated.GifDither;
        selected.GifSpeedPercent=updated.GifSpeedPercent;
        selected.GifThreshold=updated.GifThreshold;
        if(selected.Kind=="AnimatedGif")
            AnimatedGifWidgetStore.EnsureBackground(selected);
        RefreshWidgetList();
        Redraw();
        SetStatus("Widget ayarları güncellendi. Kaydet ile sakla.");
    }

    private async void EditGameLogo()
    {
        if(selected?.Kind!="GameLogo")return;
        if(!gameSnapshot.IsRunning)
        {
            MessageBox.Show(this,
                "Önce SteelSeries GG tarafından tanınan bir oyunu aç. "+
                "Oyun açıkken bu bölümden kendi PNG, JPG veya ICO logonu "+
                "seçebilir veya oyunun orijinal EXE simgesine dönebilirsin.",
                "Oyun logosu",MessageBoxButtons.OK,MessageBoxIcon.Information);
            return;
        }
        GameSnapshot originallySelected=gameSnapshot;
        string name=originallySelected.Name;
        var answer=MessageBox.Show(this,
            $"Aktif oyun: {name}\n\nEvet: Özel logo seç\nHayır: Yerel EXE simgesine dön"+
            "\nİptal: Değişiklik yapma",
            "Oyun logosu",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
        if(answer==DialogResult.Cancel)return;
        if(answer==DialogResult.Yes)
        {
            using var picker=new OpenFileDialog{
                Filter="Logo görselleri|*.png;*.jpg;*.jpeg;*.ico;*.bmp",
                Title="Aktif oyunun logosunu seç",CheckFileExists=true};
            if(picker.ShowDialog(this)!=DialogResult.OK)return;
            if(!Equals(gameSnapshot,originallySelected))
            {
                SetStatus("Oyun değişti; yanlış oyuna logo uygulanmadı.");
                return;
            }
            try{GameLogoProvider.ImportOverride(originallySelected,picker.FileName);}
            catch(Exception ex)
            {
                MessageBox.Show(this,ex.Message,"Oyun logosu yüklenemedi",
                    MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
        }
        else
        {
            if(!Equals(gameSnapshot,originallySelected))return;
            try{GameLogoProvider.RemoveOverride(originallySelected);}
            catch(Exception ex)
            {
                MessageBox.Show(this,ex.Message,"Logo kaldırılamadı",
                    MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
        }
        gameLogoSnapshot=null;
        await RefreshGameLogoAsync(originallySelected);
        SetStatus("Oyun logosu güncellendi. Önizlemede kontrol edebilirsin.");
    }

    private void RemoveWidget()
    {
        if(selected==null)return;
        if(StudioProject.IsLocked(editingMode,selected.Kind))
        {
            SetStatus("Bu widget bu mod için zorunlu; konumunu ve görünümünü değiştirebilirsin.");
            return;
        }
        Record();state.Widgets.Remove(selected);selected=null;RefreshWidgetList();Redraw();
    }

    private void BeginGesture(object? sender,MouseEventArgs e)
    {
        if(e.Button!=MouseButtons.Left)return;
        var px=e.X/Zoom;var py=e.Y/Zoom;
        if(px<0||px>=128||py<LockedRows||py>=64)return;
        drawing=false;dragging=false;gestureChanged=false;lastPixel=-1;
        if(tool.SelectedIndex==2)
        {
            selected=state.Widgets.LastOrDefault(w=>w.Visible && Current(w) && OledRenderer.GetBounds(w).Contains(px,py));
            RefreshWidgetList();
            if(selected!=null){dragging=true;dragAnchor=new Point(px-selected.X,py-selected.Y);}
        }
        else
        {
            drawing=true;
            PaintPixel(px,py);
        }
    }
    private void ContinueGesture(object? sender,MouseEventArgs e)
    {
        if(!drawing&&!dragging)return;
        int px=Math.Clamp(e.X/Zoom,0,127),py=Math.Clamp(e.Y/Zoom,LockedRows,63);
        if(dragging && selected!=null)
        {
            int x=Math.Clamp(px-dragAnchor.X,0,127),y=Math.Clamp(py-dragAnchor.Y,LockedRows,63);
            if(selected.X==x&&selected.Y==y)return;
            if(!gestureChanged){Record();gestureChanged=true;}
            selected.X=x;selected.Y=y;Redraw();
        }
        else if(drawing)
        {
            PaintPixel(px,py);
        }
    }
    private void EndGesture(object? sender,MouseEventArgs? e)
    {
        drawing=false;dragging=false;gestureChanged=false;lastPixel=-1;
        RefreshWidgetList();
    }
    private void PaintPixel(int x,int y)
    {
        int index=y*128+x;
        bool value=tool.SelectedIndex==0;
        if(index==lastPixel||state.Pixels[index]==value)return;
        if(!gestureChanged){Record();gestureChanged=true;}
        state.Pixels[index]=value;lastPixel=index;Redraw();
    }
    private bool Current(DockWidget w) =>
        StudioProject.IsAllowed(editingMode,w.Kind);
    private void PaintFrame(Graphics g,bool edit,int zoom)
    {
        using var frame=RenderScreen(editingMode);
        g.Clear(Color.Black);
        g.InterpolationMode=InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode=PixelOffsetMode.Half;
        g.DrawImage(frame,new Rectangle(0,0,128*zoom,64*zoom),new Rectangle(0,0,128,64),GraphicsUnit.Pixel);
        if(edit)
        {
            // The protected top 10 rows are firmware-owned and deliberately
            // absent from the editable/saved bitmap. Show the same 1-bit OEM
            // indicators as the dock photograph here as a visual guide only.
            // Unchecking "Orijinal üst 10 pikseli koru" hides the guide and
            // reveals the user's editable full-screen pixels immediately.
            if(project.ProtectHeader &&
               dockPreview?.ProtectedHeaderReference() is Bitmap reference)
                g.DrawImage(reference,new Rectangle(0,0,128*zoom,10*zoom),
                    new Rectangle(0,0,128,10),GraphicsUnit.Pixel);
            using var rule=new Pen(Color.FromArgb(235,230,116,104),1);
            g.DrawLine(rule,0,LockedRows*zoom,128*zoom,LockedRows*zoom);
            if(selected!=null)
            {
                var b=OledRenderer.GetBounds(selected);
                using var box=new Pen(Color.Cyan,1) { DashStyle=DashStyle.Dash };
                g.DrawRectangle(box,b.X*zoom,b.Y*zoom,b.Width*zoom,b.Height*zoom);
            }
        }
    }
    private void HandleKeys(object? sender,KeyEventArgs e)
    {
        if(e.Control && e.KeyCode==Keys.Z && !e.Shift){Undo();e.SuppressKeyPress=true;return;}
        if(e.Control && (e.KeyCode==Keys.Y || (e.Shift && e.KeyCode==Keys.Z))){Redo();e.SuppressKeyPress=true;return;}
        if(ActiveControl==customText||ActiveControl==fontSize||
           ActiveControl==textWidth||ActiveControl==xPos||
           ActiveControl==yPos||ActiveControl==strokeWidth||
           ActiveControl==miniWidth||ActiveControl==progressWidth||
           ActiveControl==progressThickness||ActiveControl==gameLogoSize)return;
        if(e.KeyCode==Keys.Delete){RemoveWidget();e.SuppressKeyPress=true;return;}
        if(selected==null)return;
        int dx=e.KeyCode==Keys.Left?-1:e.KeyCode==Keys.Right?1:0;
        int dy=e.KeyCode==Keys.Up?-1:e.KeyCode==Keys.Down?1:0;
        if(dx==0&&dy==0)return;
        int step=e.Shift?5:1;
        int x=Math.Clamp(selected.X+dx*step,0,127),y=Math.Clamp(selected.Y+dy*step,LockedRows,63);
        if(x==selected.X&&y==selected.Y)return;
        Record();selected.X=x;selected.Y=y;RefreshWidgetList();Redraw();e.SuppressKeyPress=true;
    }

    private void SaveLayout()
    {
        try
        {
            project.Set(editingMode,state);
            project.Save();
            SetStatus("Üç mod kaydedildi: "+StudioProject.FilePath);
        }
        catch(Exception ex){SetStatus("Kayıt hatası: "+ex.Message);}
    }
    private void LoadLayout()
    {
        if(!File.Exists(StudioProject.FilePath))
        {
            SetStatus("Kaydedilmiş üç modlu tasarım bulunamadı.");
            return;
        }
        project=StudioProject.Load();
        state=project.For(editingMode);
        headerProtect.Checked=project.ProtectHeader;
        liveOwnsDisplay.Checked=project.LiveOwnsDisplay;
        selected=null;undo.Clear();redo.Clear();
        UpdateHistoryButtons();RefreshWidgetList();Redraw();
        SetStatus("Medya, Oyun, Idle ve üst ekran ayarı yüklendi.");
    }
    private void ExportPng()
    {
        using var dialog=new SaveFileDialog{Filter="PNG görüntüsü|*.png",FileName="oled-128x64.png"};
        if(dialog.ShowDialog()!=DialogResult.OK)return;
        try
        {
            using var img=RenderScreen(editingMode);
            img.Save(dialog.FileName,ImageFormat.Png);
            SetStatus("128×64 PNG kaydedildi: "+dialog.FileName);
        }
        catch(Exception ex){SetStatus("PNG kaydetme hatası: "+ex.Message);}
    }
}

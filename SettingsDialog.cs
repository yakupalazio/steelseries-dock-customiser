using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>Settings / About window. The Easter egg is deliberately not advertised.</summary>
internal sealed class SettingsDialog : Form
{
    private bool captureMode;
    internal void EnableOffscreenCapture()
    {
        captureMode=true;
        StartPosition=FormStartPosition.Manual;
        Location=new Point(-5000,-5000);
        ShowInTaskbar=false;
    }
    protected override bool ShowWithoutActivation=>captureMode;
    private readonly KonamiSequence secret = new();
    private readonly Func<bool> otherOledWriterActive;
    private readonly Func<Task<bool>> suspendLive;
    private readonly Func<bool,Task> resumeLive;
    private readonly Action onLanguageChanged;
    private bool aboutActive;
    private BatteryStatusDialog? batteryEditor;
    private Action<int>? selectPageForCapture;
    internal void SelectPageForCapture(int page)=>selectPageForCapture?.Invoke(page);

    public void UpdateBatteryRows()
    {
        if(batteryEditor is { IsDisposed:false })batteryEditor.UpdateRows();
    }
    private static readonly Color Background = Color.FromArgb(13, 18, 26);
    private static readonly Color Sidebar = Color.FromArgb(19, 26, 37);
    private static readonly Color Surface = Color.FromArgb(23, 31, 43);
    private static readonly Color Accent = Color.FromArgb(72, 194, 238);
    private static readonly Color Primary = Color.FromArgb(235, 242, 248);
    private static readonly Color Muted = Color.FromArgb(151, 169, 187);

    public SettingsDialog(Func<bool>? otherOledWriterActive = null,
        BatteryPreferences? batteryPreferences = null,
        Func<BatterySnapshot>? snapshotGetter = null,
        Func<Task>? refresh = null,
        Action? onApply = null,
        Func<Task<bool>>? suspendLive = null,
        Func<bool,Task>? resumeLive = null,
        Action? onLanguageChanged = null,
        DisplayPreferences? displayPreferences = null,
        Action? onGuideRequested = null)
    {
        UiPolish.Register(this);
        this.otherOledWriterActive = otherOledWriterActive ?? (() => false);
        this.suspendLive=suspendLive??(()=>Task.FromResult(false));
        this.resumeLive=resumeLive??(_=>Task.CompletedTask);
        this.onLanguageChanged=onLanguageChanged??(()=>{});
        Text = "Ayarlar | Dock Customiser";
        BackColor = Background;
        ForeColor = Primary;
        Font = new Font("Segoe UI", 9.5f);
        ClientSize = new Size(1130, 746);
        MinimumSize = Size;
        MaximumSize = Size;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        KeyPreview = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            BackColor = Background, Margin = Padding.Empty, Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var menu = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar };
        root.Controls.Add(menu, 0, 0);
        menu.Controls.Add(new Label
        {
            Text = "AYARLAR", Left = 22, Top = 25, Width = 154, Height = 24,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold), ForeColor = Muted
        });
        var aboutItem = new DarkButton
        {
            Text = "ⓘ   Hakkında", Left = 13, Top = 635, Width = 178, Height = 49,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0),
            FlatStyle = FlatStyle.Flat, BackColor = Surface,
            ForeColor = Muted, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold)
        };
        aboutItem.FlatAppearance.BorderSize = 0;
        menu.Controls.Add(aboutItem);
        var languageItem=new DarkButton
        {
            Text="◉   Dil / Language",Left=13,Top=79,Width=178,Height=49,
            TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,0,0,0),
            FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(38,68,82),
            ForeColor=Accent,Cursor=Cursors.Hand,
            Font=new Font("Segoe UI Semibold",10f,FontStyle.Bold)
        };
        languageItem.FlatAppearance.BorderSize=0;
        menu.Controls.Add(languageItem);
        menu.Controls.Add(new Label
        {
            Text = "OLED STUDIO • RC7.2", Left = 18, Top = 704,
            Width = 180, Height = 23, ForeColor = Muted,
            Font = new Font("Segoe UI", 7.6f)
        });

        var bugButton=new DarkButton{
            Text="◈   Hata Bildir",Left=13,Top=262,Width=178,Height=49,
            TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,0,0,0),
            BackColor=Surface,ForeColor=Muted,Cursor=Cursors.Hand};
        bugButton.Click+=(_,_)=>{
            using var report=new BugReportDialog();
            report.ShowDialog(this);
        };
        menu.Controls.Add(bugButton);
        var guideButton=new DarkButton{
            Text="ⓘ   Kullanım rehberi",Left=13,Top=323,Width=178,Height=49,
            TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,0,0,0),
            BackColor=Surface,ForeColor=Muted,Cursor=Cursors.Hand};
        guideButton.Click+=(_,_)=>{
            Close();
            onGuideRequested?.Invoke();
        };
        menu.Controls.Add(guideButton);
        var windowsItem=new DarkButton{
            Text="Windows ve performans",Left=13,Top=384,Width=178,Height=49,
            TextAlign=ContentAlignment.MiddleLeft,
            Padding=new Padding(12,0,0,0),
            BackColor=Surface,ForeColor=Muted,Cursor=Cursors.Hand};
        menu.Controls.Add(windowsItem);

        var contentHost = new Panel { Dock = DockStyle.Fill, BackColor = Background };
        root.Controls.Add(contentHost, 1, 0);
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Background };
        contentHost.Controls.Add(content);
        content.Controls.Add(new Label
        {
            Text = "HAKKINDA", Left = 32, Top = 27, Width = 445, Height = 21,
            ForeColor = Accent, Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
        });
        content.Controls.Add(new Label
        {
            Text = "Dock Customiser", Left = 30, Top = 57, Width = 475, Height = 48,
            ForeColor = Primary, Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold)
        });
        content.Controls.Add(new Label
        {
            Text = "SteelSeries Nova Pro Wireless için kişisel OLED stüdyosu",
            Left = 32, Top = 114, Width = 479, Height = 25,
            ForeColor = Muted, Font = new Font("Segoe UI", 10f)
        });

        var info = new Panel
        {
            Left = 30, Top = 161, Width = 472, Height = 92,
            BackColor = Surface, Padding = new Padding(15)
        };
        content.Controls.Add(info);
        info.Controls.Add(new Label
        {
            Text = "SÜRÜM   1.1.1 RC7.2\nPLATFORM   Windows 11  ·  .NET 10\nEKRAN   128 × 64  ·  Monokrom OLED",
            Dock = DockStyle.Fill, ForeColor = Primary,
            Font = new Font("Segoe UI", 9.3f), TextAlign = ContentAlignment.MiddleLeft
        });

        content.Controls.Add(new Label
        {
            Text = "Tasarlayan ve geliştiren", Left = 33, Top = 279,
            Width = 395, Height = 22, ForeColor = Muted,
            Font = new Font("Segoe UI", 9f)
        });
        content.Controls.Add(new Label
        {
            Text = "Yakup Can Palaz", Left = 32, Top = 307,
            Width = 462, Height = 53, ForeColor = Accent,
            Font = new Font("Segoe Script", 22f, FontStyle.Italic)
        });
        content.Controls.Add(new Label
        {
            Text = "© 2026 Yakup Can Palaz. Tüm hakları saklıdır. Ücretsiz kullanım; değiştirme ve yeniden dağıtım izne tabidir. SteelSeries ile bağlantılı değildir.",
            Left = 32, Top = 364, Width = 468, Height = 45,
            ForeColor = Muted, Font = new Font("Segoe UI", 8.4f)
        });

        var close = new DarkButton
        {
            Text = "Kapat", Width = 94, Height = 34,
            Left = 410, Top = 413, FlatStyle = FlatStyle.Flat,
            BackColor = Surface, ForeColor = Primary,
            Cursor = Cursors.Hand
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) => Close();
        content.Controls.Add(close);

        var languagePage=new Panel{Dock=DockStyle.Fill,BackColor=Background};
        contentHost.Controls.Add(languagePage);
        languagePage.Controls.Add(new Label
        {
            Text="DİL / LANGUAGE",Left=32,Top=28,Width=620,Height=25,
            ForeColor=Accent,Font=new Font("Segoe UI Semibold",10,FontStyle.Bold)
        });
        languagePage.Controls.Add(new Label
        {
            Text="Uygulama dili",Left=30,Top=66,Width=610,Height=47,
            ForeColor=Primary,Font=new Font("Segoe UI Semibold",24,FontStyle.Bold)
        });
        languagePage.Controls.Add(new Label
        {
            Text="Değişiklik anında uygulanır ve sonraki açılışlarda korunur.",
            Left=32,Top=130,Width=730,Height=36,ForeColor=Muted,
            Font=new Font("Segoe UI",10)
        });
        var languageSelector=new ComboBox
        {
            Left=32,Top=198,Width=328,Height=39,
            DropDownStyle=ComboBoxStyle.DropDownList,
            FlatStyle=FlatStyle.Flat,DrawMode=DrawMode.OwnerDrawFixed,
            ItemHeight=29,BackColor=Surface,ForeColor=Primary,
            Font=new Font("Segoe UI Semibold",11)
        };
        languageSelector.Items.AddRange(["Türkçe","İngilizce"]);
        languageSelector.DrawItem+=(_,e)=>
        {
            if(e.Index<0)return;
            using var bg=new SolidBrush((e.State&DrawItemState.Selected)!=0
                ?Color.FromArgb(38,68,82):Surface);
            e.Graphics.FillRectangle(bg,e.Bounds);
            TextRenderer.DrawText(e.Graphics,UiLanguage.T(languageSelector.Items[e.Index]?.ToString()),
                languageSelector.Font,new Rectangle(e.Bounds.Left+12,e.Bounds.Top,
                    e.Bounds.Width-14,e.Bounds.Height),Primary,
                TextFormatFlags.VerticalCenter);
        };
        languagePage.Controls.Add(languageSelector);

        // Settings use the same dark palette as the workspace. The battery
        // priority editor is hosted here, not in a separate top-level window.
        Button? batteryButton=null;
        Panel? batteryPage=null;
        if (batteryPreferences is not null && snapshotGetter is not null &&
            refresh is not null && onApply is not null)
        {
            batteryButton = new DarkButton
            {
                Text = "◫   Pil ve öncelik", Left = 13, Top = 140,
                Width = 178, Height = 49,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                FlatStyle = FlatStyle.Flat, BackColor = Surface,
                ForeColor = Muted, Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold)
            };
            batteryButton.FlatAppearance.BorderSize = 0;
            menu.Controls.Add(batteryButton);

            batteryPage = new Panel { Dock = DockStyle.Fill, BackColor = Background };
            batteryEditor = new BatteryStatusDialog(
                batteryPreferences, snapshotGetter, refresh, onApply)
            {
                TopLevel = false, FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill, BackColor = Background
            };
            batteryPage.Controls.Add(batteryEditor);
            contentHost.Controls.Add(batteryPage);
            batteryEditor.Show();
        }

        Button? displayButton=null;
        Panel? displayPage=null;
        if(displayPreferences is not null)
        {
            displayButton=new DarkButton{
                Text="◧   OLED ekran",Left=13,Top=201,Width=178,Height=49,
                TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,0,0,0),
                FlatStyle=FlatStyle.Flat,BackColor=Surface,ForeColor=Muted,
                Cursor=Cursors.Hand,
                Font=new Font("Segoe UI Semibold",10f,FontStyle.Bold)};
            displayButton.FlatAppearance.BorderSize=0;
            menu.Controls.Add(displayButton);
            displayPage=new Panel{Dock=DockStyle.Fill,BackColor=Background};
            displayPage.Controls.Add(new Label{
                Text="OLED EKRAN",Left=32,Top=28,Width=680,Height=24,
                ForeColor=Accent,Font=new Font("Segoe UI Semibold",10f,FontStyle.Bold)});
            displayPage.Controls.Add(new Label{
                Text="Ekran sağlığı",Left=30,Top=67,Width=680,Height=48,
                ForeColor=Primary,Font=new Font("Segoe UI Semibold",24f,FontStyle.Bold)});
            displayPage.Controls.Add(new Label{
                Text="Canlı OLED sırasında sabit piksellerin iz bırakmasını azaltmak için isteğe bağlı koruma.",
                Left=32,Top=129,Width=750,Height=54,ForeColor=Muted,
                Font=new Font("Segoe UI",10f)});
            var drift=new DarkCheckBox{
                Text="Yanmayı azaltan piksel kaydırma (45 sn)",
                Left=32,Top=209,Width=620,Height=41,AutoSize=false,
                Checked=displayPreferences.BurnInDrift,ForeColor=Primary};
            displayPage.Controls.Add(drift);
            displayPage.Controls.Add(new Label{
                Text="Yalnızca canlı dock çıktısı 1 piksel kayar; tasarım koordinatları ve üst 10 satır korunur.",
                Left=32,Top=273,Width=700,Height=58,
                ForeColor=Muted,Font=new Font("Segoe UI",9.3f)});
            drift.CheckedChanged+=(_,_)=>
            {
                displayPreferences.BurnInDrift=drift.Checked;
                displayPreferences.Save();
            };
            // Independent saved transition preference; no hardware write
            // happens when simply previewing effects in Settings.
            displayPage.Controls.Add(new Label{
                Text="MOD GEÇİŞLERİ · MEDIA / GAME / IDLE",
                Left=32,Top=350,Width=650,Height=26,ForeColor=Accent,
                Font=new Font("Segoe UI Semibold",10,FontStyle.Bold)});
            var effectEnabled=new DarkCheckBox{
                Text="OLED mod geçiş animasyonlarını kullan",
                Left=32,Top=379,Width=580,Height=35,
                Checked=displayPreferences.ModeTransitionEnabled,
                ForeColor=Primary};
            var transitionEffect=new ComboBox{
                Left=33,Top=424,Width=242,Height=34,
                BackColor=Surface,ForeColor=Primary,
                DropDownStyle=ComboBoxStyle.DropDownList};
            transitionEffect.Items.AddRange(
                OledModeTransition.Effects.Cast<object>().ToArray());
            transitionEffect.SelectedItem=OledModeTransition.Normalize(
                displayPreferences.ModeTransitionEffect);
            var transitionSpeed=new TrackBar{
                Left=293,Top=419,Width=282,Height=53,
                Minimum=25,Maximum=200,TickFrequency=25,
                Value=Math.Clamp(displayPreferences.ModeTransitionSpeed,25,200),
                BackColor=Background};
            var transitionInfo=new Label{
                Left=585,Top=431,Width=195,Height=32,
                ForeColor=Muted,Font=new Font("Segoe UI",9f)};
            var transitionHint=new Label{
                Left=32,Top=483,Width=700,Height=55,
                Text="Animasyon yalnızca mod değiştiğinde oynar; üstte korunan 10 pil satırı değişmez. "+
                    "Bağlantı koparsa veya donanım orijinal kontrol ekranını açarsa güvenli aktarım önceliklidir.",
                ForeColor=Muted,Font=new Font("Segoe UI",9f)};
            var preview=new OledTransitionPreview{
                Left=33,Top=546,Width=372,Height=112,
                BackColor=Color.Black};
            var play=new DarkButton{
                Text="▶ Geçişi önizle",Left=427,Top=563,
                Width=170,Height=38,BackColor=Surface,ForeColor=Primary};
            void SaveTransitionPreference(bool persist=true)
            {
                displayPreferences.ModeTransitionEnabled=effectEnabled.Checked;
                displayPreferences.ModeTransitionEffect=OledModeTransition.Normalize(
                    transitionEffect.SelectedItem?.ToString());
                displayPreferences.ModeTransitionSpeed=transitionSpeed.Value;
                if(persist)displayPreferences.Save();
                transitionInfo.Text=
                    $"{transitionSpeed.Value}% · "+
                    $"{OledModeTransition.DurationMs(displayPreferences.ModeTransitionEffect,
                        displayPreferences.ModeTransitionSpeed)} ms";
                play.Enabled=effectEnabled.Checked &&
                    displayPreferences.ModeTransitionEffect!="Instant";
            }
            effectEnabled.CheckedChanged+=(_,_)=>SaveTransitionPreference();
            transitionEffect.SelectedIndexChanged+=(_,_)=>SaveTransitionPreference();
            transitionSpeed.ValueChanged+=(_,_)=>SaveTransitionPreference();
            play.Click+=(_,_)=>preview.Play(
                displayPreferences.ModeTransitionEffect,
                displayPreferences.ModeTransitionSpeed);
            displayPage.Controls.AddRange([effectEnabled,transitionEffect,
                transitionSpeed,transitionInfo,transitionHint,preview,play]);
            SaveTransitionPreference(false);
            contentHost.Controls.Add(displayPage);
        }

        // Per-user Windows settings are independent from OLED design and
        // profile files. Initial display never creates an autorun entry.
        var windowsPage=new Panel{Dock=DockStyle.Fill,BackColor=Background};
        contentHost.Controls.Add(windowsPage);
        windowsPage.Controls.Add(new Label{
            Text="WINDOWS ENTEGRASYONU",Left=32,Top=29,Width=730,Height=24,
            ForeColor=Accent,Font=new Font("Segoe UI Semibold",10f,FontStyle.Bold)});
        windowsPage.Controls.Add(new Label{
            Text="Başlangıç ve performans",Left=30,Top=64,Width=770,Height=50,
            ForeColor=Primary,Font=new Font("Segoe UI Semibold",24f,FontStyle.Bold)});
        windowsPage.Controls.Add(new Label{
            Text="Bu ayarlar yalnızca senin Windows hesabına uygulanır. "+
                "Varsayılan durumda ikisi de kapalıdır.",
            Left=32,Top=128,Width=760,Height=50,
            ForeColor=Muted,Font=new Font("Segoe UI",10f)});

        var runOnLogin=new DarkCheckBox{
            Text="Windows açıldığında otomatik başlat",
            Left=32,Top=211,Width=680,Height=43,AutoSize=false,
            ForeColor=Primary,
            Checked=WindowsIntegration.IsAutostartEnabled()};
        var runDescription=new Label{
            Text="Yalnızca mevcut Windows kullanıcısında başlar. "+
                "Yönetici izni gerektirmez. Bu seçenek varsayılan olarak kapalıdır.",
            Left=34,Top=263,Width=739,Height=55,ForeColor=Muted,
            Font=new Font("Segoe UI",9.5f)};
        var runStatus=new Label{
            Left=34,Top=318,Width=740,Height=37,ForeColor=Muted,
            Font=new Font("Segoe UI",9f)};
        if(!WindowsIntegration.CanRegisterAutostart())
        {
            runOnLogin.Enabled=false;
            runStatus.Text=UiLanguage.English
                ?"Autostart is available in the published Windows EXE."
                :"Otomatik başlatma, yayımlanmış Windows EXE sürümünde kullanılabilir.";
        }
        else
            runStatus.Text=runOnLogin.Checked
                ?(UiLanguage.English
                    ?"Launch at Windows sign-in is enabled."
                    :"Windows oturum açılışında çalışacak.")
                :(UiLanguage.English
                    ?"Autostart is off."
                    :"Otomatik başlangıç kapalı.");
        bool updatingAutostart=false;
        runOnLogin.CheckedChanged+=(_,_)=>{
            if(updatingAutostart)return;
            try
            {
                WindowsIntegration.SetAutostartEnabled(runOnLogin.Checked);
                runStatus.Text=runOnLogin.Checked
                    ?(UiLanguage.English
                        ?"Enabled for your Windows sign-in."
                        :"Windows oturum açılışına eklendi.")
                    :(UiLanguage.English
                        ?"Removed from Windows sign-in."
                        :"Windows oturum açılışından kaldırıldı.");
                runStatus.ForeColor=Accent;
            }
            catch(Exception ex)
            {
                // Keep the UI truthful when registry access is denied.
                updatingAutostart=true;
                runOnLogin.Checked=WindowsIntegration.IsAutostartEnabled();
                updatingAutostart=false;
                runStatus.Text="Başlangıç ayarı uygulanamadı: "+ex.Message;
                runStatus.ForeColor=Color.FromArgb(244,195,115);
            }
        };
        windowsPage.Controls.AddRange([runOnLogin,runDescription,runStatus]);

        windowsPage.Controls.Add(new Label{
            Text="İŞLEM ÖNCELİĞİ",Left=32,Top=393,Width=700,Height=26,
            ForeColor=Accent,Font=new Font("Segoe UI Semibold",10f,FontStyle.Bold)});
        var highPriority=new DarkCheckBox{
            Text="Uygulamayı yüksek öncelikte çalıştır",
            Left=32,Top=433,Width=680,Height=42,AutoSize=false,
            ForeColor=Primary,Checked=WindowsIntegration.HighPriorityEnabled()};
        var priorityInfo=new Label{
            Text="High önceliği kullanılır; Realtime hiçbir zaman kullanılmaz. "+
                "Önce yönetici izni istemeden denenir. Windows izin vermezse "+
                "yalnızca sen onaylarsan UAC penceresi açılır.",
            Left=34,Top=489,Width=746,Height=73,
            ForeColor=Muted,Font=new Font("Segoe UI",9.5f)};
        var priorityStatus=new Label{
            Left=34,Top=583,Width=749,Height=50,
            ForeColor=Muted,Font=new Font("Segoe UI",9f)};
        priorityStatus.Text=highPriority.Checked
            ?(UiLanguage.English
                ?"High priority preference is saved."
                :"Yüksek öncelik tercihi kayıtlı.")
            :(UiLanguage.English
                ?"Normal priority (default)."
                :"Normal öncelik (varsayılan).");
        bool updatingPriority=false;
        highPriority.CheckedChanged+=async (_,_)=>{
            if(updatingPriority)return;
            bool desired=highPriority.Checked;
            highPriority.Enabled=false;
            try
            {
                bool succeeded=WindowsIntegration.TrySetCurrentPriority(
                    desired,out string error);
                if(!succeeded&&desired)
                {
                    if(MessageBox.Show(this,
                        "Windows yüksek önceliği doğrudan uygulayamadı. "+
                        "Yalnızca işlem önceliğini değiştiren yardımcı uygulama "+
                        "için yönetici izni istenecek. Devam edilsin mi?\n"+
                        error,"Yönetici izni",
                        MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes)
                    {
                        if(WindowsIntegration.CanRequestElevatedHelper())
                        {
                            try{succeeded=await WindowsIntegration
                                .RequestElevatedHighPriorityAsync();}
                            catch(System.ComponentModel.Win32Exception)
                            {succeeded=false;}
                        }
                    }
                }
                if(!succeeded)
                    throw new InvalidOperationException(
                        "İşlem önceliği değiştirilemedi; ayar korunmadı.");
                WindowsIntegration.SaveHighPriority(desired);
                priorityStatus.ForeColor=Accent;
                priorityStatus.Text=desired
                    ?(UiLanguage.English
                        ?"High priority enabled. It will also apply at next launch."
                        :"High öncelik etkin. Sonraki açılışlarda da uygulanacak.")
                    :(UiLanguage.English
                        ?"Restored Normal priority."
                        :"Normal önceliğe dönüldü.");
            }
            catch(Exception ex)
            {
                updatingPriority=true;
                highPriority.Checked=WindowsIntegration.HighPriorityEnabled();
                updatingPriority=false;
                priorityStatus.ForeColor=Color.FromArgb(244,195,115);
                priorityStatus.Text=ex.Message;
            }
            finally{if(!IsDisposed)highPriority.Enabled=true;}
        };
        windowsPage.Controls.AddRange([highPriority,priorityInfo,priorityStatus]);

        int currentPage=-1;
        void SwitchPage(int page)
        {
            if(page==currentPage)return;
            UiPageTransition.Swap(contentHost,()=>
            {
                currentPage=page;
                aboutActive=page==2;
                content.Visible=aboutActive;
                languagePage.Visible=page==0;
                if(batteryPage!=null)batteryPage.Visible=page==1;
                if(displayPage!=null)displayPage.Visible=page==3;
                windowsPage.Visible=page==4;
                foreach(var (button,active) in new[]{
                    (languageItem,page==0),(aboutItem,page==2),
                    (batteryButton,page==1),(displayButton,page==3),
                    (windowsItem,page==4)})
                {
                    if(button==null)continue;
                    button.BackColor=active?Color.FromArgb(38,68,82):Surface;
                    button.ForeColor=active?Accent:Muted;
                }
                if(page==1)batteryEditor?.UpdateRows();
            },!captureMode);
        }
        selectPageForCapture=SwitchPage;
        languageItem.Click+=(_,_)=>SwitchPage(0);
        aboutItem.Click+=(_,_)=>SwitchPage(2);
        if(batteryButton!=null)batteryButton.Click+=(_,_)=>SwitchPage(1);
        if(displayButton!=null)displayButton.Click+=(_,_)=>SwitchPage(3);
        windowsItem.Click+=(_,_)=>SwitchPage(4);
        languageSelector.SelectedIndexChanged+=(_,_)=>
        {
            bool english=languageSelector.SelectedIndex==1;
            if(UiLanguage.English==english)return;
            UiLanguage.Select(english);
            UiLanguage.ApplyTree(this);
            this.onLanguageChanged();
            languageSelector.Invalidate();
            batteryEditor?.ApplyLanguage();
        };
        languageSelector.SelectedIndex=UiLanguage.English?1:0;
        UiLanguage.ApplyTree(this);
        SwitchPage(0);
    }

    private void AdvanceSecret(Keys key)
    {
        if (aboutActive && secret.Feed(key))
        {
            BeginInvoke(new Action(async () =>
            {
                Hide();
                bool wasLive=false;
                try
                {
                    wasLive=await suspendLive();
                    using var game=new DoomModeForm(otherOledWriterActive,autoStart:true);
                    game.ShowDialog(Owner);
                }
                catch(Exception ex)
                {
                    MessageBox.Show(Owner,"OLED: "+ex.Message,"DOOM MODE",
                        MessageBoxButtons.OK,MessageBoxIcon.Warning);
                }
                finally
                {
                    try{await resumeLive(wasLive);}catch(Exception ex)
                    {
                        MessageBox.Show(Owner,ex.Message,"OLED restore",
                            MessageBoxButtons.OK,MessageBoxIcon.Warning);
                    }
                    Close();
                }
            }));
        }
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (aboutActive && (keyData & Keys.Modifiers) == Keys.None)
        {
            var key = keyData & Keys.KeyCode;
            if (key is Keys.Up or Keys.Down or Keys.Left or Keys.Right)
            {
                AdvanceSecret(key);
                return true;
            }
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (aboutActive && !e.Alt && !e.Control && !e.Shift &&
            (e.KeyCode is Keys.B or Keys.A))
        {
            AdvanceSecret(e.KeyCode);
            e.SuppressKeyPress = true;
        }
        base.OnKeyDown(e);
    }
}

internal sealed class KonamiSequence
{
    private static readonly Keys[] Pattern =
    [
        Keys.Up, Keys.Up, Keys.Down, Keys.Down,
        Keys.Left, Keys.Right, Keys.Left, Keys.Right, Keys.B, Keys.A
    ];
    private int index;

    public bool Feed(Keys key)
    {
        if (key == Pattern[index]) index++;
        else index = key == Pattern[0] ? 1 : 0;
        if (index != Pattern.Length) return false;
        index = 0;
        return true;
    }
}

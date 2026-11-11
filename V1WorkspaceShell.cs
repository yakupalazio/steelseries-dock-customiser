using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Responsive, single-page v1 design shell. Reuses all tested v0.9.9 editing,
/// battery, Game/Media/Idle and hardware components without forking behavior.
/// The interactive dock photograph and 128x64 pixel editor stay on one page.
/// </summary>
internal sealed partial class PixelStudioForm
{
    private static readonly Color UiBg=Color.FromArgb(13,18,26);
    private static readonly Color UiSidebar=Color.FromArgb(19,26,37);
    private static readonly Color UiSurface=Color.FromArgb(23,31,43);
    private static readonly Color UiStage=Color.FromArgb(24,30,40);
    private static readonly Color UiBorder=Color.FromArgb(45,59,74);
    private static readonly Color UiText=Color.FromArgb(235,242,248);
    private static readonly Color UiMuted=Color.FromArgb(151,169,187);
    private static readonly Color UiAccent=Color.FromArgb(72,194,238);
    private static readonly Color UiSuccess=Color.FromArgb(77,211,164);

    private Button[]? modernModes;
    private Button? modernLive,modernOriginal,modernCustom;
    private Label? modernModeLabel,modernRuntimeLabel,modernSelectedLabel;
    private Label? modernDockStatus,modernFooter,modernHint;
    private Panel? modernCanvasHost;
    private Control? modernTextRow,modernFontRow,modernStrokeRow;
    private Control? modernVisibleRow,modernXRow,modernYRow;
    private Control? modernDialStyleRow,modernDialWidthRow,modernWheelEditRow;
    private bool modernShellInitialized;

    private void BuildModernShell(TabPage legacyEditTab,
        Button add,Button remove,Button save,Button load,Button reset,
        Button export,Button clear,Button clockDateButton,
        Button batteryButton,Button iconButton)
    {
        SuspendLayout();
        BackColor=UiBg;
        ForeColor=UiText;
        DoubleBuffered=true;
        Font=new Font("Segoe UI",9.5f);
        // Old TabPages stay in memory to preserve existing TabControl state,
        // but neither the old editor tab nor a Preview tab is shown.
        Controls.Remove(tabs);
        tabs.Visible=false;

        var root=new TableLayoutPanel
        {
            Dock=DockStyle.Fill,
            ColumnCount=3,RowCount=3,
            BackColor=UiBg,Margin=Padding.Empty,Padding=Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,224));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,326));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,73));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,43));
        Controls.Add(root);

        // Branding and document actions live in one persistent toolbar.
        var top=new Panel{Dock=DockStyle.Fill,BackColor=UiSidebar,Padding=new Padding(21,8,18,7)};
        root.Controls.Add(top,0,0);
        root.SetColumnSpan(top,3);
        var accentStripe=new Panel{Dock=DockStyle.Left,Width=4,BackColor=UiAccent};
        top.Controls.Add(accentStripe);
        var brand=new Label
        {
            Text="DOCK  /  CUSTOMISER",
            ForeColor=UiText,Font=new Font("Segoe UI Semibold",17f,FontStyle.Bold),
            AutoSize=true,Left=19,Top=11
        };
        top.Controls.Add(brand);
        top.Controls.Add(new Label
        {
            Text="OLED STUDIO    •    VERSION 1.1",
            ForeColor=UiMuted,Font=new Font("Segoe UI",8.1f,FontStyle.Bold),
            AutoSize=true,Left=21,Top=44
        });
        var headerButtons=new FlowLayoutPanel
        {
            Dock=DockStyle.Right,Width=386,FlowDirection=FlowDirection.RightToLeft,
            WrapContents=false,AutoScroll=false,BackColor=UiSidebar,Padding=new Padding(0,7,0,0)
        };
        top.Controls.Add(headerButtons);
        Decorate(save,true);
        Decorate(load);
        Decorate(export);
        save.Text="Kaydet";save.Width=105;save.Height=37;
        load.Text="Tasarımı aç";load.Width=106;load.Height=37;
        export.Text="PNG";export.Width=80;export.Height=37;
        headerButtons.Controls.AddRange([save,load,export]);

        var sidebar=new Panel{Dock=DockStyle.Fill,BackColor=UiSidebar};
        root.Controls.Add(sidebar,0,1);
        var left=new FlowLayoutPanel
        {
            Dock=DockStyle.Fill,
            FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,
            Padding=new Padding(15,22,12,12),BackColor=UiSidebar
        };
        sidebar.Controls.Add(left);

        left.Controls.Add(Section("ÇALIŞMA MODU",190));
        var mediaMode=ModeButton("♫    Medya çalarken");
        var gameMode=ModeButton("▣    Oyun açıkken");
        var idleMode=ModeButton("◷    Boştayken");
        modernModes=[mediaMode,gameMode,idleMode];
        left.Controls.Add(mediaMode);
        left.Controls.Add(gameMode);
        left.Controls.Add(idleMode);
        mediaMode.Click+=(_,_)=>SelectModernMode(ScreenMode.Media);
        gameMode.Click+=(_,_)=>SelectModernMode(ScreenMode.Game);
        idleMode.Click+=(_,_)=>SelectModernMode(ScreenMode.Idle);

        left.Controls.Add(Spacer(24,190));
        left.Controls.Add(Section("DÜZENLEME ARAÇLARI",190));
        RefitToolButton(iconButton,"♢   İkon atölyesi");
        RefitToolButton(clockDateButton,"◷   Saat ve tarih");
        // Device priority now lives in Settings > Pil ve öncelik.
        left.Controls.Add(iconButton);
        left.Controls.Add(clockDateButton);
        var profileButton=ModeButton("▤   Profiller");
        profileButton.Height=44;
        left.Controls.Add(profileButton);
        profileButton.Click+=(_,_)=>
        {
            using var dialog=new LayoutProfileDialog(editingMode,
                ()=>state.DeepClone(),profile=>
                {
                    Record();
                    state=profile;
                    project.Set(editingMode,state);
                    selected=null;
                    RefreshWidgetList();Redraw();
                    SetStatus(UiLanguage.T("Profil uygulandı. Kaydet ile sakla."));
                });
            dialog.ShowDialog(this);
        };

        left.Controls.Add(Spacer(23,190));
        left.Controls.Add(Section("DURUM",190));
        modernRuntimeLabel=new Label
        {
            Width=188,Height=86,Padding=new Padding(8),
            BackColor=UiSurface,ForeColor=UiMuted,
            Font=new Font("Segoe UI",9f)
        };
        left.Controls.Add(modernRuntimeLabel);
        left.Controls.Add(Spacer(12,190));
        left.Controls.Add(new Label
        {
            Text="GG ile veri bağlantısı\n• Gerçek pil / medya bilgileri\n• Fiziksel dock'a yazma isteğe bağlı",
            Width=194,Height=64,ForeColor=UiMuted,
            Font=new Font("Segoe UI",8.4f)
        });

        // Single central page: realistic photographed OLED at the top,
        // editable native pixel canvas immediately below.
        var workspace=new TableLayoutPanel
        {
            Dock=DockStyle.Fill,BackColor=UiBg,
            ColumnCount=1,RowCount=6,Padding=new Padding(18,10,18,10)
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute,52));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent,52));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute,47));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent,48));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute,49));
        root.Controls.Add(workspace,1,1);

        var titleRow=new Panel{Dock=DockStyle.Fill,BackColor=UiBg};
        modernModeLabel=new Label{
            Left=3,Top=4,AutoSize=true,Font=new Font("Segoe UI Semibold",17,FontStyle.Bold),
            ForeColor=UiText,Text="Canlı önizleme"
        };
        modernHint=new Label{
            Left=5,Top=32,Width=710,Height=16,ForeColor=UiMuted,
            Font=new Font("Segoe UI",8.3f),
            Text="Seçili moddaki her değişiklik bu gerçekçi dock üzerinde anında görünür."
        };
        titleRow.Controls.Add(modernModeLabel);
        titleRow.Controls.Add(modernHint);
        workspace.Controls.Add(titleRow,0,0);

        var stage=new Panel
        {
            Dock=DockStyle.Fill,BackColor=UiStage,
            Margin=new Padding(2,2,2,5)
        };
        workspace.Controls.Add(stage,0,1);
        dockPreview!.EnableEmbeddedStudio();
        stage.Controls.Add(dockPreview);

        var photoActions=new FlowLayoutPanel{
            Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true,
            BackColor=UiBg,Padding=new Padding(2,4,0,0),Margin=Padding.Empty
        };
        workspace.Controls.Add(photoActions,0,2);
        modernCustom=ModernButton("Özel tasarım",133);
        modernOriginal=ModernButton("Orijinal",82);
        var apply=ModernButton("✓ Uygula",105);
        modernLive=ModernButton("▶ Canlı",105);
        var home=ModernButton("↻ Dock Reset",112);
        photoActions.Controls.AddRange([
            modernCustom,modernOriginal,apply,modernLive,home
        ]);
        modernCustom.Click+=(_,_)=>{dockPreview.ShowOriginalPhoto(false);UpdateModernUi();};
        modernOriginal.Click+=(_,_)=>{dockPreview.ShowOriginalPhoto(true);UpdateModernUi();};
        apply.Click+=(_,_)=>dockPreview.ApplyDesign();
        modernLive.Click+=(_,_)=>{dockPreview.ToggleLiveOutput();UpdateModernUi();};
        home.Click+=(_,_)=>{dockPreview.RestoreOriginalScreen();UpdateModernUi();};

        var canvasTitle=new Panel{Dock=DockStyle.Fill,BackColor=UiBg};
        canvasTitle.Controls.Add(new Label
        {
            Text="PİKSEL EDİTÖRÜ  ·  128 × 64",
            Font=new Font("Segoe UI Semibold",12.3f,FontStyle.Bold),
            ForeColor=UiText,Left=4,Top=12,AutoSize=true
        });
        var canvasTools=new FlowLayoutPanel{
            Dock=DockStyle.Right,Width=310,Height=40,
            FlowDirection=FlowDirection.RightToLeft,WrapContents=false,
            BackColor=UiBg,Padding=new Padding(0,5,0,0)
        };
        canvasTitle.Controls.Add(canvasTools);
        tool.Width=145;
        DecorateInput(tool);
        DecorateCombo(tool);
        showGrid.Text="Piksel ızgarası";
        showGrid.ForeColor=UiMuted;showGrid.AutoSize=false;
        showGrid.Width=135;showGrid.Height=30;
        showGrid.BackColor=UiBg;
        showGrid.Padding=Padding.Empty;
        canvasTools.Controls.Add(showGrid);
        canvasTools.Controls.Add(tool);
        workspace.Controls.Add(canvasTitle,0,3);

        modernCanvasHost=new Panel{
            Dock=DockStyle.Fill,BackColor=UiSurface,
            Margin=new Padding(2,0,2,4)
        };
        workspace.Controls.Add(modernCanvasHost,0,4);
        editor.BorderStyle=BorderStyle.None;
        editor.BackColor=Color.Black;
        modernCanvasHost.Controls.Add(editor);
        modernCanvasHost.Resize+=(_,_)=>PositionCanvas();
        workspace.Resize+=(_,_)=>PositionCanvas();
        Shown+=(_,_)=>PositionCanvas();

        var editActions=new FlowLayoutPanel{
            Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true,
            BackColor=UiBg,Padding=new Padding(0,5,0,0),
            Margin=Padding.Empty
        };
        workspace.Controls.Add(editActions,0,5);
        Decorate(undoButton);undoButton.Text="↶ Geri al";undoButton.Width=96;
        Decorate(redoButton);redoButton.Text="↷ İleri al";redoButton.Width=96;
        Decorate(reset);reset.Text="Modu sıfırla";reset.Width=114;
        Decorate(clear);clear.Text="Çizimi sil";clear.Width=126;
        editActions.Controls.AddRange([undoButton,redoButton,reset,clear]);

        // Inspector: widget hierarchy, choices and context-sensitive controls.
        var inspectorSurface=new Panel{
            Dock=DockStyle.Fill,BackColor=UiSidebar
        };
        root.Controls.Add(inspectorSurface,2,1);
        var inspector=new FlowLayoutPanel
        {
            Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,
            WrapContents=false,AutoScroll=true,
            Padding=new Padding(15,18,10,12),BackColor=UiSidebar
        };
        inspectorSurface.Controls.Add(inspector);
        inspector.Controls.Add(Section("WIDGET YÖNETİMİ",275));
        inspector.Controls.Add(new Label{
            Text="Öğe ekle",Height=21,Width=274,ForeColor=UiMuted
        });
        kind.Width=267;
        DecorateInput(kind);
        DecorateCombo(kind);
        inspector.Controls.Add(kind);
        var addRow=Row(288,41);
        Decorate(add,true);Decorate(remove);
        add.Text="+ Öğe ekle";remove.Text="Seçileni sil";
        add.Width=122;remove.Width=145;
        addRow.Controls.AddRange([add,remove]);
        inspector.Controls.Add(addRow);
        inspector.Controls.Add(Spacer(9,266));
        inspector.Controls.Add(Section("EKRANDAKİ ÖĞELER",275));
        widgets.Width=268;widgets.Height=147;
        widgets.BorderStyle=BorderStyle.None;
        widgets.BackColor=UiSurface;
        widgets.ForeColor=UiText;
        widgets.Font=new Font("Segoe UI",9.4f);
        widgets.DrawMode=DrawMode.OwnerDrawFixed;
        widgets.ItemHeight=24;
        widgets.DrawItem+=(_,e)=>
        {
            if(e.Index<0)return;
            bool active=(e.State&DrawItemState.Selected)!=0;
            using var fill=new SolidBrush(active?Color.FromArgb(38,68,82):
                (e.Index%2==0?UiSurface:Color.FromArgb(27,37,50)));
            e.Graphics.FillRectangle(fill,e.Bounds);
            var value=widgets.Items[e.Index]?.ToString()??"";
            TextRenderer.DrawText(e.Graphics,value,widgets.Font,
                new Rectangle(e.Bounds.Left+8,e.Bounds.Top,e.Bounds.Width-12,e.Bounds.Height),
                active?UiAccent:UiText,TextFormatFlags.VerticalCenter|
                TextFormatFlags.EndEllipsis);
        };
        inspector.Controls.Add(widgets);
        inspector.Controls.Add(Spacer(10,268));
        modernSelectedLabel=Section("ÖĞE AYARLARI",268);
        inspector.Controls.Add(modernSelectedLabel);
        inspector.Controls.Add(new Label{
            Text="Seçtiğin öğenin özellikleri",Width=268,Height=24,
            Font=new Font("Segoe UI",8.5f),ForeColor=UiMuted
        });
        modernVisibleRow=PropertyRow("Göster",widgetVisible);
        modernTextRow=PropertyRow("Metin",customText);
        modernFontRow=PropertyRow("Font",fontSize);
        modernStrokeRow=PropertyRow("Kalınlık",strokeWidth);
        modernXRow=PropertyRow("X konumu",xPos);
        modernYRow=PropertyRow("Y konumu",yPos);
        inspector.Controls.Add(modernVisibleRow);
        inspector.Controls.Add(modernTextRow);
        inspector.Controls.Add(modernFontRow);
        inspector.Controls.Add(modernStrokeRow);
        inspector.Controls.Add(modernXRow);
        inspector.Controls.Add(modernYRow);
        modernDialStyleRow=PropertyRow("Gösterge tipi",dialStyle);
        DecorateCombo(dialStyle);
        inspector.Controls.Add(modernDialStyleRow);
        modernWheelEditRow=wheelEditorButton;
        inspector.Controls.Add(modernWheelEditRow);
        modernDialWidthRow=PropertyRow("Widget eni",miniWidth);
        inspector.Controls.Add(modernDialWidthRow);
        inspector.Controls.Add(Spacer(12,268));
        inspector.Controls.Add(Section("EKRAN KORUMASI",268));
        headerProtect.Text="Orijinal üst 10 pikseli koru";
        headerProtect.AutoSize=false;
        headerProtect.Width=269;headerProtect.Height=38;
        headerProtect.ForeColor=UiText;
        inspector.Controls.Add(headerProtect);
        liveOwnsDisplay.ForeColor=UiAccent;
        liveOwnsDisplay.BackColor=UiSidebar;
        inspector.Controls.Add(liveOwnsDisplay);
        inspector.Controls.Add(new Label{
            Text="Deneysel: Tam OLED yazımı firmware'in tekerlek ekranını engellemez. Dock Reset ile geri dön.",
            Width=267,Height=60,ForeColor=UiMuted,Font=new Font("Segoe UI",8.3f)
        });
        inspector.Controls.Add(new Label{
            Text="Kapalıysa tam 128×64 OLED alanını düzenlersin. Dock'a göndermeden önce onay istenir.",
            Width=267,Height=54,ForeColor=UiMuted,Font=new Font("Segoe UI",8.4f)
        });
        inspector.Controls.Add(Spacer(8,268));
        // Keep the inspector compact and fully visible at normal desktop sizes.
        // Runtime toggles and device state belong with the sidebar, not with
        // the selected widget's property sheet.
        var staticNote=left.Controls.OfType<Label>()
            .FirstOrDefault(c=>c.Text.StartsWith("GG ile veri"));
        if(staticNote!=null)
        {
            left.Controls.Remove(staticNote);
            staticNote.Dispose();
        }
        autoMedia.Text="Medya otomatik algılansın";
        autoMedia.ForeColor=UiMuted;
        autoMedia.AutoSize=false;
        autoMedia.Width=190;
        autoMedia.Height=32;
        left.Controls.Add(autoMedia);
        media.Visible=false;source.Visible=false;showDate.Visible=false;
        modernDockStatus=new Label{
            Width=188,Height=82,Padding=new Padding(8),
            BackColor=UiSurface,ForeColor=UiMuted,
            Font=new Font("Segoe UI",8.1f)
        };
        left.Controls.Add(modernDockStatus);
        left.Controls.Add(Spacer(18,190));
        left.Controls.Add(Section("AYARLAR",190));
        var settingsButton=ModeButton("⚙   Ayarlar");
        settingsButton.Height=44;
        left.Controls.Add(settingsButton);
        settingsButton.Click+=(_,_)=>{
            using var dialog=new SettingsDialog(
                ()=>dockPreview?.OledWriterActive==true,
                batteryPreferences,()=>batterySnapshot,RefreshBatteriesAsync,
                ()=>{Redraw();_ = RefreshBatteriesAsync();},
                ()=>dockPreview!.SuspendLiveForGameAsync(),
                wasLive=>dockPreview!.ResumeLiveAfterGameAsync(wasLive),
                ApplyLanguage,displayPreferences);
            settingsDialog=dialog;
            try{dialog.ShowDialog(this);}finally{settingsDialog=null;}
        };

        var footer=new Panel{Dock=DockStyle.Fill,BackColor=UiSidebar};
        root.Controls.Add(footer,0,2);
        root.SetColumnSpan(footer,3);
        modernFooter=new Label{
            Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,
            Padding=new Padding(20,0,20,0),AutoEllipsis=true,
            ForeColor=UiMuted,Font=new Font("Segoe UI",8.9f)
        };
        footer.Controls.Add(modernFooter);

        // Reuse status from earlier versions: all existing methods and
        // asynchronous refreshes continue writing to the same Label object.
        status.Parent=footer;
        status.Visible=false;
        status.TextChanged+=(_,_)=>UpdateModernUi();
        modernShellInitialized=true;
        UiLanguage.ApplyTree(this);
        UpdateModernUi();
        ResumeLayout(true);
        PositionCanvas();
    }

    private void SelectModernMode(ScreenMode screen)
    {
        SwitchEditingMode(screen);
        UpdateModernUi();
    }

    private void ApplyLanguage()
    {
        UiLanguage.ApplyTree(this);
        kind.Refresh();tool.Refresh();dialStyle.Refresh();widgets.Refresh();
        UpdateModernUi();
    }

    private void PositionCanvas()
    {
        if(modernCanvasHost is null || modernCanvasHost.Width<=16 ||
           modernCanvasHost.Height<=16)return;
        int horizontal=Math.Max(1,(modernCanvasHost.Width-24)/CanvasWidth);
        int vertical=Math.Max(1,(modernCanvasHost.Height-22)/CanvasHeight);
        Zoom=Math.Clamp(Math.Min(horizontal,vertical),1,8);
        editor.Width=CanvasWidth*Zoom;
        editor.Height=CanvasHeight*Zoom;
        editor.Left=Math.Max(0,(modernCanvasHost.ClientSize.Width-editor.Width)/2);
        editor.Top=Math.Max(0,(modernCanvasHost.ClientSize.Height-editor.Height)/2);
        editor.Invalidate();
    }

    private void UpdateModernUi()
    {
        if(!modernShellInitialized || modernModes is null || dockPreview is null)
            return;
        for(int i=0;i<modernModes.Length;i++)
        {
            var b=modernModes[i];
            bool active=i==(int)editingMode;
            b.BackColor=active?Color.FromArgb(38,68,82):UiSurface;
            b.ForeColor=active?UiAccent:UiMuted;
            b.FlatAppearance.BorderSize=active?1:0;
            b.FlatAppearance.BorderColor=active?UiAccent:UiSurface;
        }
        if(modernHint!=null)
            modernHint.Text=UiLanguage.T(selected?.Kind=="VolumeChatMix"
                ?"Salt-okunur tekerlek: "+dialMonitor.Snapshot.Status+
                  " · Mod, ses veya ChatMix hareketi algılandığında değişir."
                :"Seçili moddaki her değişiklik dock üzerinde anında görünür.");
        if(modernModeLabel!=null)
            modernModeLabel.Text=UiLanguage.T(editingMode switch
            {
                ScreenMode.Media=>"Medya tasarımı",
                ScreenMode.Game=>"Oyun tasarımı",
                _=>"Boşta ekranı"
            });
        if(modernRuntimeLabel!=null)
        {
            string runtime=ActiveMode switch
            {
                ScreenMode.Game=>"OYUN AKTİF",
                ScreenMode.Media=>"MEDYA AKTİF",
                _=>"BOŞTA"
            };
            modernRuntimeLabel.Text=UiLanguage.T("Bilgisayarın durumu: "+runtime+
                "\n"+(gameSnapshot.IsRunning?
                    ("Oyun: "+gameSnapshot.Name):
                    mediaSnapshot.IsPlaying?
                    ("Medya: "+mediaSnapshot.Title):
                    "Oyun veya medya bulunmadı."));
        }
        if(modernSelectedLabel!=null)
            modernSelectedLabel.Text=UiLanguage.T(selected is null?
                "ÖĞE AYARLARI":("SEÇİLİ  ·  "+WidgetLabels.Display(selected.Kind)));
        if(modernVisibleRow!=null)modernVisibleRow.Visible=selected!=null;
        if(modernTextRow!=null)modernTextRow.Visible=customText.Enabled;
        if(modernFontRow!=null)modernFontRow.Visible=fontSize.Enabled;
        if(modernStrokeRow!=null)modernStrokeRow.Visible=strokeWidth.Enabled;
        if(modernXRow!=null)modernXRow.Visible=selected!=null;
        if(modernYRow!=null)modernYRow.Visible=selected!=null;
        if(modernDialStyleRow!=null)
            modernDialStyleRow.Visible=selected?.Kind=="VolumeChatMix";
        if(modernWheelEditRow!=null)
            modernWheelEditRow.Visible=selected?.Kind=="VolumeChatMix" &&
                selected.DialStyle=="Wheel";
        if(modernDialWidthRow!=null)
            modernDialWidthRow.Visible=selected?.Kind=="GameMiniPlayer" ||
                (selected?.Kind=="VolumeChatMix"&&selected.DialStyle!="Wheel");
        if(modernDockStatus!=null)
        {
            string message=dockPreview.HardwareStatus;
            modernDockStatus.Text=UiLanguage.T((dockPreview.LiveOutputActive?
                "● Dock üzerinde canlı mod açık":
                "○ Dock canlı modu kapalı")+"\n"+
                (message.Length>110?message[..110]+"…":message));
        }
        if(modernLive!=null)
        {
            modernLive.Text=UiLanguage.T(dockPreview.LiveOutputActive?"■ Durdur":"▶ Canlı");
            modernLive.BackColor=dockPreview.LiveOutputActive?
                Color.FromArgb(90,46,44):UiSurface;
            modernLive.ForeColor=dockPreview.LiveOutputActive?
                Color.FromArgb(255,165,150):UiText;
        }
        if(modernCustom!=null && modernOriginal!=null)
        {
            modernCustom.BackColor=dockPreview.FactoryPreviewActive?
                UiSurface:Color.FromArgb(38,68,82);
            modernOriginal.BackColor=dockPreview.FactoryPreviewActive?
                Color.FromArgb(38,68,82):UiSurface;
            modernCustom.ForeColor=dockPreview.FactoryPreviewActive?
                UiMuted:UiAccent;
            modernOriginal.ForeColor=dockPreview.FactoryPreviewActive?
                UiAccent:UiMuted;
        }
        if(modernFooter!=null)
            modernFooter.Text="  ●  "+UiLanguage.T(status.Text)+
                "     |     OLED: 128×64, "+(dockPreview.LiveOutputActive&&project.LiveOwnsDisplay
                ?UiLanguage.T("Canlı tam ekran devralma")
                :project.ProtectHeader?UiLanguage.T("üst 10 piksel korunuyor")
                    :UiLanguage.T("tam ekran düzenleme"));
    }

    private static Label Section(string text,int width)=>new()
    {
        Text=text,Width=width,Height=27,
        ForeColor=UiAccent,Font=new Font("Segoe UI Semibold",9.1f,FontStyle.Bold),
        TextAlign=ContentAlignment.MiddleLeft
    };
    private static Control Spacer(int height,int width)=>new Panel{
        Height=height,Width=width,BackColor=UiSidebar
    };
    private static Button ModeButton(string name)=>new DarkButton()
    {
        Width=192,Height=54,Text=name,
        TextAlign=ContentAlignment.MiddleLeft,
        Padding=new Padding(12,0,0,0),
        FlatStyle=FlatStyle.Flat,
        FlatAppearance={BorderSize=0},
        BackColor=UiSurface,ForeColor=UiMuted,
        Font=new Font("Segoe UI Semibold",10.1f,FontStyle.Bold),
        Margin=new Padding(0,0,0,7),Cursor=Cursors.Hand
    };
    private static Button ModernButton(string text,int width)
    {
        var b=new DarkButton{Text=text,Width=width,Height=33,
            Margin=new Padding(0,0,6,0)};
        Decorate(b);
        return b;
    }
    private static void RefitToolButton(Button b,string text)
    {
        b.Text=text;b.Width=191;b.Height=43;
        b.TextAlign=ContentAlignment.MiddleLeft;
        b.Padding=new Padding(12,0,0,0);
        b.Margin=new Padding(0,0,0,6);
        Decorate(b);
    }
    private static void Decorate(Button b,bool primary=false)
    {
        b.UseVisualStyleBackColor=false;
        b.FlatStyle=FlatStyle.Flat;
        b.FlatAppearance.BorderSize=0;
        b.ForeColor=primary?UiBg:UiText;
        b.BackColor=primary?UiAccent:UiSurface;
        b.Font=new Font("Segoe UI Semibold",9.1f,FontStyle.Bold);
        b.Cursor=Cursors.Hand;
        b.Margin=new Padding(0,0,7,0);
        b.MouseEnter+=(_,_)=>{
            if(!b.Enabled)return;
            b.BackColor=primary?Color.FromArgb(122,218,248):
                Color.FromArgb(44,58,73);
        };
        b.MouseLeave+=(_,_)=>{
            b.BackColor=primary?UiAccent:UiSurface;
        };
    }
    private static void DecorateInput(Control c)
    {
        c.BackColor=UiSurface;c.ForeColor=UiText;
        c.Font=new Font("Segoe UI",9.4f);
        c.Margin=new Padding(3,3,3,3);
        if(c is ComboBox combo)combo.FlatStyle=FlatStyle.Flat;
        if(c is TextBox edit)edit.BorderStyle=BorderStyle.None;
    }
    private void DecorateCombo(ComboBox combo)
    {
        combo.BackColor=UiSurface;combo.ForeColor=UiText;
        combo.FlatStyle=FlatStyle.Flat;
        combo.DrawMode=DrawMode.OwnerDrawFixed;
        combo.ItemHeight=23;
        combo.DrawItem+=(_,e)=>
        {
            if(e.Index<0)return;
            bool active=(e.State&DrawItemState.Selected)!=0;
            using var background=new SolidBrush(active?Color.FromArgb(38,68,82):UiSurface);
            e.Graphics.FillRectangle(background,e.Bounds);
            var item=combo.Items[e.Index]?.ToString()??"";
            string label=ReferenceEquals(combo,dialStyle)
                ?(item=="Wheel"?UiLanguage.T("Dairesel tekerlek"):
                    UiLanguage.T("Düz çubuk"))
                :ReferenceEquals(combo,kind)?WidgetLabels.Display(item):
                    UiLanguage.T(item);
            TextRenderer.DrawText(e.Graphics,label,combo.Font,
                new Rectangle(e.Bounds.Left+7,e.Bounds.Top,e.Bounds.Width-9,e.Bounds.Height),
                active?UiAccent:UiText,TextFormatFlags.VerticalCenter|
                TextFormatFlags.EndEllipsis);
        };
    }
    private static FlowLayoutPanel Row(int width,int height)=>new(){
        Width=width,Height=height,WrapContents=false,BackColor=UiSidebar
    };
    private static Control PropertyRow(string title,Control ctrl)
    {
        var row=new TableLayoutPanel{
            Width=268,Height=39,BackColor=UiSidebar,
            ColumnCount=2,RowCount=1,
            Margin=new Padding(0,1,0,1),Padding=Padding.Empty,
            AutoSize=false
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,105));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        row.Controls.Add(new Label{
            Text=title,Dock=DockStyle.Fill,AutoEllipsis=true,
            TextAlign=ContentAlignment.MiddleLeft,
            Margin=new Padding(3,0,5,0),
            ForeColor=UiMuted,Font=new Font("Segoe UI",9f)
        },0,0);
        var inset=new Panel{
            Dock=DockStyle.Fill,
            BackColor=ctrl is TextBox?UiBorder:UiSidebar,
            Margin=new Padding(0,2,4,2),
            Padding=ctrl is TextBox?new Padding(2,7,2,2):new Padding(0,2,0,2)
        };
        row.Controls.Add(inset,1,0);
        ctrl.Dock=DockStyle.Fill;
        ctrl.Margin=Padding.Empty;
        if(ctrl is DarkCheckBox box)
        {
            box.Text="Görünür";
            box.ForeColor=UiText;
            box.BackColor=UiSidebar;
        }
        else DecorateInput(ctrl);
        ctrl.Dock=DockStyle.Fill;
        ctrl.Margin=Padding.Empty;
        inset.Controls.Add(ctrl);
        return row;
    }
}

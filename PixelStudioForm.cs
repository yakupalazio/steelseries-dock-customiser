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

internal sealed class PixelStudioForm : Form
{
    private const int Zoom = 6, CanvasWidth = 128, CanvasHeight = 64;
    private int LockedRows => workspace.PreserveFactoryHeader?10:0;
    private readonly string savePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","studio-v05.json");

    private StudioWorkspace workspace=StudioWorkspace.Load();
    private DockMode editorMode=DockMode.Media;
    private LayoutState state=LayoutDefaults.Create();
    private readonly TabControl modeTabs=new(){
        Left=16,Top=7,Width=793,Height=40,Multiline=false
    };
    private readonly CheckBox protectHeader=new(){
        Text="Üst 10 px koru",AutoSize=true,Checked=true
    };
    private readonly NumericUpDown strokeWeight=new(){
        Minimum=1,Maximum=3,Value=1,Width=48
    };
    private GameSnapshot gameSnapshot=GameSnapshot.None;
    private bool gamePolling;
    private int gameTickCounter;
    private BatteryPreferences batteryPreferences = BatteryPreferences.Load();
    private BatterySnapshot batterySnapshot = new();
    private BatteryStatusDialog? batteryDialog;
    private bool batteryPolling;
    private bool fastBatteryPolling;
    private int batteryRefreshCounter;
    private readonly CheckBox autoMedia = new(){Text="Otomatik medya",Checked=true,AutoSize=true};
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
    private readonly PictureBox editor = new() { Left = 18, Top = 16, Width = CanvasWidth*Zoom, Height = CanvasHeight*Zoom, BackColor = Color.Black, BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Cross };
    private readonly ComboBox tool = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox kind = new() { Width = 134, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox source = new() { Width = 116, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListBox widgets = new() { Width = 232, Height = 106 };
    private readonly TextBox customText = new() { Width = 166 };
    private readonly NumericUpDown fontSize = new() { Minimum=7,Maximum=24,Value=10,Width=49 };
    private readonly NumericUpDown xPos = new() { Minimum=0,Maximum=127,Width=48 };
    private readonly NumericUpDown yPos = new() { Minimum=10,Maximum=63,Width=48 };
    private readonly CheckBox widgetVisible = new() { Text="Görünür", Checked=true,AutoSize=true };
    private readonly CheckBox media = new() { Text="Medya çalıyor (demo)", Checked=true, AutoSize=true };
    private readonly CheckBox showDate = new() { Text="Tarihi göster", Checked=true, AutoSize=true };
    private readonly CheckBox showGrid = new() { Text="Piksel kılavuzu", AutoSize=true };
    private readonly Button undoButton = new() { Text="↶ Geri al", Width=96,Height=30 };
    private readonly Button redoButton = new() { Text="↷ İleri al", Width=96,Height=30 };
    private readonly Label status = new() { Width=755,Height=52,ForeColor=Color.FromArgb(44,170,104) };
    private readonly System.Windows.Forms.Timer ticker = new() { Interval=1000 };

    public PixelStudioForm()
    {
        Text = "Steelseries Dock Customiser v0.9.8 | Multi-Mode OLED Studio";
        state=workspace.Media;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(830,870);
        MinimumSize = new Size(850,910);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        KeyPreview = true;
        Font = new Font("Segoe UI",9f);

        Controls.Add(tabs);
        var editTab = new TabPage("Tasarım & Piksel Editörü") { BackColor=Color.FromArgb(246,248,250) };
        var dockTab = new TabPage("Dock Preview") { BackColor=Color.FromArgb(246,248,250) };
        tabs.TabPages.Add(editTab); tabs.TabPages.Add(dockTab);
        editTab.Controls.Add(editor);
        dockPreview = new DockPreviewPanel(()=>RenderCurrentMode(),()=>workspace.PreserveFactoryHeader);
        dockTab.Controls.Add(dockPreview);
        editor.Paint += (_,e) => PaintFrame(e.Graphics,true,Zoom);

        var subtitle = new Label { Left=18,Top=405,Width=780,Height=39,
            Text="OLED: 128 × 64 piksel. İlk 10 satır SteelSeries pil göstergelerine ayrıldı; fiziksel dock’a gönderilmez.\nÖğeleri sürüklemek için 'Taşı / Seç' aracını kullan." };
        editTab.Controls.Add(subtitle);

        tool.Items.AddRange(["Kalem","Silgi","Taşı / Seç"]);
        tool.SelectedIndex=2;
        FillWidgetKinds();
        source.Items.AddRange(["Spotify","Diğer medya"]);
        source.SelectedIndex=0;

        var toolbar = new FlowLayoutPanel { Left=15,Top=444,Width=795,Height=42,WrapContents=false,AutoScroll=true };
        toolbar.Controls.AddRange([L("Araç:"),tool,L("Öğe:"),kind,autoMedia,
            protectHeader,showGrid]);
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
        line2.Controls.AddRange([widgetVisible,L("Kalınlık:"),strokeWeight,media,
            L("Kaynak:"),source]);
        editorControls.Controls.AddRange([line1,line2,new Label { Width=520,Height=27,
            Text="Kısayollar: Ctrl+Z, Ctrl+Y, Delete, ok tuşları (Shift: 5 px)." }]);
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
        status.Text = "Fiziksel dock testi yalnızca Dock Preview sekmesindeki onaylı düğmeyle yapılır.";
        undoButton.Click += (_,_) => Undo();
        redoButton.Click += (_,_) => Redo();
        add.Click += (_,_) => AddWidget();
        remove.Click += (_,_) => RemoveWidget();
        save.Click += (_,_) => SaveLayout();
        load.Click += (_,_) => LoadLayout();
        reset.Click += (_,_) => {
            Record();
            state=editorMode switch {
                DockMode.Media=>StudioWorkspace.MakeMedia(),
                DockMode.Game=>StudioWorkspace.MakeGame(),
                _=>StudioWorkspace.MakeIdle()
            };
            workspace.Set(editorMode,state);
            selected=null;RefreshWidgetList();Redraw();
            SetStatus(ModeLabel(editorMode)+" varsayılan düzenine döndü.");
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
        protectHeader.Checked=workspace.PreserveFactoryHeader;
        protectHeader.CheckedChanged+=(_,_)=>{
            workspace.PreserveFactoryHeader=protectHeader.Checked;
            yPos.Minimum=LockedRows;
            if(selected is not null && selected.Y<LockedRows)
            {
                Record();selected.Y=LockedRows;
            }
            Redraw();
            SetStatus(protectHeader.Checked ?
                "Üst 10 piksel korunuyor; bu alan fiziksel dock'a gönderilmez.":
                "Üst 10 piksel de özelleştirilebilir; varsayılan SteelSeries göstergeleri gizlenebilir.");
        };
        strokeWeight.ValueChanged+=(_,_)=>ChangeNumber();
        new ToolTip().SetToolTip(showGrid,
            "Yalnızca büyütülmüş editörde her OLED pikselinin hücre sınırını gösterir.\nGerçek OLED veya Dock Preview'da görünmez.");
        showGrid.CheckedChanged += (_,_) => editor.Invalidate();
        source.SelectedIndexChanged += (_,_) => Redraw();
        customText.Leave += (_,_) => ChangeText();
        customText.KeyDown += (_,e) => { if(e.KeyCode==Keys.Enter){ChangeText();e.SuppressKeyPress=true;} };
        fontSize.ValueChanged += (_,_) => ChangeNumber();
        xPos.ValueChanged += (_,_) => ChangeNumber();
        yPos.ValueChanged += (_,_) => ChangeNumber();
        widgetVisible.CheckedChanged += (_,_) =>
        {
            if(changeUi||selected==null||selected.Visible==widgetVisible.Checked ||
           StudioWorkspace.Mandatory(editorMode,selected.Kind))return;
            Record();selected.Visible=widgetVisible.Checked;Redraw();
        };

        editor.MouseDown += BeginGesture;
        editor.MouseMove += ContinueGesture;
        editor.MouseUp += EndGesture;
        editor.MouseLeave += (_,_) => EndGesture(null,null);
        KeyDown += HandleKeys;
        ticker.Tick += (_,_) =>
        {
            // One frame deadline every 1,000 ms. Actual OLED writes are
            // skipped when unchanged, and in-flight sends never overlap.
            Redraw();
            _=RefreshMediaAsync();
            if(++gameTickCounter>=5)
            {
                gameTickCounter=0;
                _=RefreshGameAsync();
            }
            if(++batteryRefreshCounter>=60)
            {
                batteryRefreshCounter=0;
                _=RefreshBatteriesAsync();
            }
            else _=RefreshFastBatteriesAsync();
        };
        ticker.Start();
        FormClosed += (_,_) => ticker.Stop();
        modeTabs.TabPages.Add("Medya çalarken");
        modeTabs.TabPages.Add("Oyun açıkken");
        modeTabs.TabPages.Add("Idle");
        editTab.Controls.Add(modeTabs);
        modeTabs.SelectedIndexChanged+=(_,_)=>SwitchEditorMode();
        // Preserve the original controls while making room for sub-mode tabs.
        foreach(Control ctl in editTab.Controls)
            if(ctl!=modeTabs) ctl.Top+=40;
        Shown += async (_,_) =>
        {
            await RefreshMediaAsync();
            await RefreshGameAsync();
            await RefreshBatteriesAsync();
        };
        RefreshWidgetList();UpdateHistoryButtons();Redraw();
        tabs.SelectedIndex=1; // Open the photographic preview first.
    }

    private static string ModeLabel(DockMode mode)=>mode switch {
        DockMode.Game=>"Oyun",DockMode.Media=>"Medya",_=>"Idle"
    };

    private void FillWidgetKinds()
    {
        kind.Items.Clear();
        string[] common=[
            "BatterySlot1","BatterySlot2","BatterySlot3",
            "Mouse","Laptop","Phone","DualSense","Headset","SpareBattery",
            "SmallClock","Clock","Date","Custom text"
        ];
        foreach(var k in common)
            if(editorMode!=DockMode.Idle || k!="SmallClock")
                kind.Items.Add(k);
        if(kind.Items.Count>0)kind.SelectedIndex=0;
    }

    private void SwitchEditorMode()
    {
        if(modeTabs.SelectedIndex<0)return;
        workspace.Set(editorMode,state);
        editorMode=(DockMode)modeTabs.SelectedIndex;
        state=workspace.For(editorMode);
        selected=null;
        undo.Clear();redo.Clear();UpdateHistoryButtons();
        FillWidgetKinds();
        RefreshWidgetList();
        Redraw();
        SetStatus(ModeLabel(editorMode)+" düzeni seçildi. Değişiklikler diğer modlardan bağımsız.");
    }

    private DockMode CurrentDockMode=>gameSnapshot.Active ? DockMode.Game :
        MediaActive ? DockMode.Media : DockMode.Idle;

    private string[] ResolveBatteryDevices()=>BatteryPriority.Resolve(
        batterySnapshot,batteryPreferences.PriorityOrder,
        batteryPreferences.DualSenseReplacesLaptop);

    private Bitmap RenderCurrentMode()
    {
        var mode=CurrentDockMode;
        var current=workspace.For(mode);
        return OledRenderer.Render(current,mode==DockMode.Media,true,
            SpotifyActive,batterySnapshot,LiveMedia,mode,gameSnapshot,
            ResolveBatteryDevices(),workspace.PreserveFactoryHeader);
    }

    private Bitmap RenderEditorMode()
    {
        var mode=editorMode;
        return OledRenderer.Render(state,mode==DockMode.Media,true,
            SpotifyActive,batterySnapshot,LiveMedia,mode,gameSnapshot,
            ResolveBatteryDevices(),workspace.PreserveFactoryHeader);
    }

    private async System.Threading.Tasks.Task RefreshGameAsync()
    {
        if(gamePolling||IsDisposed)return;
        gamePolling=true;
        try
        {
            var latest=await GameDetector.PollAsync();
            if(IsDisposed)return;
            if(latest.Active!=gameSnapshot.Active || latest.Name!=gameSnapshot.Name)
            {
                gameSnapshot=latest;
                Redraw();
                SetStatus(latest.Active?"Oyun modu: "+latest.Name:
                    "Oyun kapandı; medya/idle moduna dönüldü.");
            }
            else gameSnapshot=latest;
        }
        finally {gamePolling=false;}
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
        }
        catch {mediaSnapshot=MediaSnapshot.Empty;}
        finally {mediaPolling=false;}
    }

    private void OpenClockDateSettings()
    {
        string clockKind=editorMode==DockMode.Idle?"Clock":"SmallClock";
        using var dialog=new ClockDateSettingsDialog(state,(clockFmt,clockStroke,dateFmt,dateStroke)=>
        {
            Record();
            var clock=state.Widgets.FirstOrDefault(w=>w.Kind==clockKind);
            var date=state.Widgets.FirstOrDefault(w=>w.Kind=="Date");
            if(clock==null)
            {
                clock=new DockWidget{Kind=clockKind,X=36,Y=17,
                    FontSize=clockKind=="Clock"?20:9};
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
        },clockKind);
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
            batterySnapshot=updated;
            batteryDialog?.UpdateRows();
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
            var newest=await BatterySources.PollAsync(batteryPreferences.ExperimentalControllerHid,
                false /* Phone Link preferred; debugging disabled */);
            if(IsDisposed)return;
            batterySnapshot=newest;
            Redraw();
            batteryDialog?.UpdateRows();
            SetStatus("Gerçek pil verileri: "+
                string.Join(" · ",batteryPreferences.DisplayDevices.Select(k=>k+" "+newest[k].Display))+
                "   | GG canlı, Bluetooth önbelleği yok sayılıyor.");
        }
        catch(Exception ex)
        {
            if(!IsDisposed)SetStatus("Pil kaynağı okunamadı: "+ex.GetType().Name);
        }
        finally { batteryPolling=false; }
    }

    private void ApplyBatteryWidgets(string[] selectedDevices,bool record)
    {
        // Widgets are independent per-mode BatterySlot1/2/3 positions.
        // Priority ordering only affects which device is rendered in each slot.
        Redraw();
    }

    private void OpenBatteryDialog()
    {
        if(batteryDialog is { IsDisposed:false })
        {
            batteryDialog.Activate();return;
        }
        batteryDialog=new BatteryStatusDialog(batteryPreferences,
            ()=>batterySnapshot,RefreshBatteriesAsync,
            selected=>{ApplyBatteryWidgets(selected,true);});
        batteryDialog.Show(this);
        _=RefreshBatteriesAsync();
    }

    private static Button B(string text,int width) => new(){Text=text,Width=width,Height=30};
    private static Label L(string text) => new(){Text=text,AutoSize=true,Padding=new Padding(2,8,2,0)};
    private void SetStatus(string message) => status.Text=message;
    private void Redraw(){editor.Invalidate();dockPreview?.InvalidateFrame();}
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
        redo.Push(state.DeepClone());state=undo.Pop();
        workspace.Set(editorMode,state);
        selected=null;RefreshWidgetList();Redraw();UpdateHistoryButtons();SetStatus("Son işlem geri alındı.");
    }
    private void Redo()
    {
        if(redo.Count==0)return;
        undo.Push(state.DeepClone());state=redo.Pop();
        workspace.Set(editorMode,state);
        selected=null;RefreshWidgetList();Redraw();UpdateHistoryButtons();SetStatus("İşlem yeniden uygulandı.");
    }
    private void RefreshWidgetList()
    {
        changeUi=true;widgets.Items.Clear();
        foreach(var w in state.Widgets)widgets.Items.Add(w);
        widgets.SelectedItem=selected;
        changeUi=false;UpdateEditorControls();
    }
    private void UpdateEditorControls()
    {
        changeUi=true;
        bool has=selected!=null;
        customText.Enabled=has && selected!.Kind=="Custom text";
        fontSize.Enabled=has && selected!.Kind is "Clock" or "SmallClock" or
            "Date" or "Track" or "Artist" or "GameLabel" or "GameTitle" or "Custom text";
        strokeWeight.Enabled=has && selected!.Kind is "Clock" or "SmallClock" or
            "Date" or "GameLabel" or "Custom text";
        widgetVisible.Enabled=has && selected is not null &&
            !StudioWorkspace.Mandatory(editorMode,selected.Kind);
        xPos.Enabled=has;yPos.Enabled=has;
        if(selected!=null)
        {
            customText.Text=selected.Text;
            fontSize.Value=Math.Clamp(selected.FontSize,7,24);
            strokeWeight.Value=Math.Clamp(selected.StrokeWidth,1,3);
            xPos.Value=Math.Clamp(selected.X,0,127);yPos.Value=Math.Clamp(selected.Y,LockedRows,63);
            widgetVisible.Checked=selected.Visible;
        }
        else { customText.Text="";widgetVisible.Checked=false; }
        changeUi=false;
    }
    private void ChangeText()
    {
        if(changeUi||selected==null||selected.Kind!="Custom text"||selected.Text==customText.Text)return;
        Record();selected.Text=customText.Text;RefreshWidgetList();Redraw();
    }
    private void ChangeNumber()
    {
        if(changeUi||selected==null)return;
        int newX=(int)xPos.Value,newY=(int)yPos.Value,newSize=(int)fontSize.Value;
        int newStroke=(int)strokeWeight.Value;
        if(selected.X==newX&&selected.Y==newY&&selected.FontSize==newSize
           && selected.StrokeWidth==newStroke)return;
        Record();selected.X=newX;selected.Y=newY;selected.FontSize=newSize;
        selected.StrokeWidth=newStroke;RefreshWidgetList();Redraw();
    }
    private void AddWidget()
    {
        if(StudioWorkspace.Mandatory(editorMode,kind.Text))return;
        Record();
        var w=new DockWidget{Kind=kind.Text,X=5,Y=Math.Max(LockedRows,19),
            FontSize=kind.Text=="SmallClock"?9:10};
        if(w.Kind is "BatterySlot1" or "BatterySlot2" or "BatterySlot3" &&
           state.Widgets.Any(existing=>existing.Kind==w.Kind))
        {
            SetStatus("Bu pil alanı zaten bu modda ekli; konumunu listeden düzenleyebilirsin.");
            return;
        }
        state.Widgets.Add(w);selected=w;RefreshWidgetList();Redraw();tool.SelectedIndex=2;
    }
    private void RemoveWidget()
    {
        if(selected==null)return;
        if(StudioWorkspace.Mandatory(editorMode,selected.Kind))
        {
            SetStatus("Bu öğe bu modda zorunlu; kaldırılamaz.");
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
    private bool Current(DockWidget w)=>true; // Separate profiles enforce mode-specific widgets.
    private void PaintFrame(Graphics g,bool edit,int zoom)
    {
        using var frame=RenderEditorMode();
        g.Clear(Color.Black);
        g.InterpolationMode=InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode=PixelOffsetMode.Half;
        g.DrawImage(frame,new Rectangle(0,0,128*zoom,64*zoom),new Rectangle(0,0,128,64),GraphicsUnit.Pixel);
        if(edit)
        {
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
        if(ActiveControl==customText||ActiveControl==fontSize||ActiveControl==xPos||
            ActiveControl==yPos||ActiveControl==strokeWeight)return;
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
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            workspace.Set(editorMode,state);
            workspace.Save();
            SetStatus("Üç mod ve OLED ayarları kaydedildi: "+StudioWorkspace.FilePath);
        }
        catch(Exception ex){SetStatus("Kayıt hatası: "+ex.Message);}
    }
    private void LoadLayout()
    {
        if(!File.Exists(StudioWorkspace.FilePath))
        {
            SetStatus("Yeni biçimde kayıt bulunamadı. Eski düzen ayrı tutuluyor.");
            return;
        }
        try
        {
            var loaded=StudioWorkspace.Load();
            loaded.Validate();
            workspace=loaded;
            state=workspace.For(editorMode);
            protectHeader.Checked=workspace.PreserveFactoryHeader;
            yPos.Minimum=LockedRows;
            selected=null;undo.Clear();redo.Clear();
            RefreshWidgetList();UpdateHistoryButtons();Redraw();
            SetStatus("Medya, oyun ve idle düzenleri yüklendi.");
        }
        catch(Exception ex){SetStatus("Yükleme hatası: "+ex.Message);}
    }
    private void ExportPng()
    {
        using var dialog=new SaveFileDialog{Filter="PNG görüntüsü|*.png",FileName="oled-128x64.png"};
        if(dialog.ShowDialog()!=DialogResult.OK)return;
        try
        {
            using var img=RenderEditorMode();
            img.Save(dialog.FileName,ImageFormat.Png);
            SetStatus("128×64 PNG kaydedildi: "+dialog.FileName);
        }
        catch(Exception ex){SetStatus("PNG kaydetme hatası: "+ex.Message);}
    }
}

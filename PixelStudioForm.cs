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
    private const int Zoom = 6, CanvasWidth = 128, CanvasHeight = 64, LockedRows = 12;
    private readonly string savePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","studio-v05.json");

    private LayoutState state = LayoutDefaults.Create();
    private BatteryPreferences batteryPreferences = BatteryPreferences.Load();
    private BatterySnapshot batterySnapshot = new();
    private BatteryStatusDialog? batteryDialog;
    private bool batteryPolling;
    private int batteryRefreshCounter;
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
    private readonly NumericUpDown yPos = new() { Minimum=12,Maximum=63,Width=48 };
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
        Text = "Steelseries Dock Customiser v0.9.2 | OLED Pixel Studio";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(830,795);
        MinimumSize = new Size(850,830);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        KeyPreview = true;
        Font = new Font("Segoe UI",9f);

        Controls.Add(tabs);
        var editTab = new TabPage("Tasarım & Piksel Editörü") { BackColor=Color.FromArgb(246,248,250) };
        var dockTab = new TabPage("Dock Preview") { BackColor=Color.FromArgb(246,248,250) };
        tabs.TabPages.Add(editTab); tabs.TabPages.Add(dockTab);
        editTab.Controls.Add(editor);
        dockPreview = new DockPreviewPanel(() => OledRenderer.Render(state,media.Checked,showDate.Checked,source.SelectedIndex==0,batterySnapshot));
        dockTab.Controls.Add(dockPreview);
        editor.Paint += (_,e) => PaintFrame(e.Graphics,true,Zoom);

        var subtitle = new Label { Left=18,Top=405,Width=780,Height=39,
            Text="OLED: 128 × 64 gerçek piksel. Üstteki sabit batarya bölgesi yalnızca temsili gösterilir; çizilemez.\nÖğeleri sürüklemek için 'Taşı / Seç' aracını kullan." };
        editTab.Controls.Add(subtitle);

        tool.Items.AddRange(["Kalem","Silgi","Taşı / Seç"]);
        tool.SelectedIndex=2;
        kind.Items.AddRange(["Mouse","Phone","DualSense","Headset","Laptop","Source","Track","Artist","Progress","Clock","Date","Custom text"]);
        kind.SelectedIndex=0;
        source.Items.AddRange(["Spotify","Diğer medya"]);
        source.SelectedIndex=0;

        var toolbar = new FlowLayoutPanel { Left=15,Top=444,Width=795,Height=42,WrapContents=false,AutoScroll=true };
        toolbar.Controls.AddRange([L("Araç:"),tool,L("Widget:"),kind,media,showDate,showGrid]);
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
        line2.Controls.AddRange([widgetVisible,L("Kaynak:"),source]);
        editorControls.Controls.AddRange([line1,line2,new Label { Width=520,Height=27,
            Text="Kısayollar: Ctrl+Z, Ctrl+Y, Delete, ok tuşları (Shift: 5 px)." }]);
        content.Controls.Add(editorControls);editTab.Controls.Add(content);
        status.Left=18;status.Top=680;status.Width=440;editTab.Controls.Add(status);
        var batteryButton = B("Cihaz pilleri",152);
        batteryButton.Left=638;batteryButton.Top=674;
        editTab.Controls.Add(batteryButton);
        batteryButton.Click += (_,_)=>OpenBatteryDialog();
        var iconButton = B("İkon düzenleyici",160);
        iconButton.Left=467;iconButton.Top=674;
        editTab.Controls.Add(iconButton);
        iconButton.Click+=(_,_)=>new IconEditorDialog(Redraw).ShowDialog(this);
        status.Text = "Yalnızca önizleme. Dock'a USB/HID/GameSense komutu gönderilmez.";
        undoButton.Click += (_,_) => Undo();
        redoButton.Click += (_,_) => Redo();
        add.Click += (_,_) => AddWidget();
        remove.Click += (_,_) => RemoveWidget();
        save.Click += (_,_) => SaveLayout();
        load.Click += (_,_) => LoadLayout();
        reset.Click += (_,_) => { Record(); state=LayoutDefaults.Create();selected=null;RefreshWidgetList();Redraw();SetStatus("Varsayılan yerleşime dönüldü."); };
        clear.Click += (_,_) => { if(state.Pixels.Any(x=>x)){Record();Array.Clear(state.Pixels,0,state.Pixels.Length);Redraw();SetStatus("Serbest çizim pikselleri temizlendi.");} };
        export.Click += (_,_) => ExportPng();

        widgets.SelectedIndexChanged += (_,_) =>
        {
            if(changeUi)return;
            selected=widgets.SelectedItem as DockWidget;
            UpdateEditorControls();Redraw();
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
        xPos.ValueChanged += (_,_) => ChangeNumber();
        yPos.ValueChanged += (_,_) => ChangeNumber();
        widgetVisible.CheckedChanged += (_,_) =>
        {
            if(changeUi||selected==null||selected.Visible==widgetVisible.Checked)return;
            Record();selected.Visible=widgetVisible.Checked;Redraw();
        };

        editor.MouseDown += BeginGesture;
        editor.MouseMove += ContinueGesture;
        editor.MouseUp += EndGesture;
        editor.MouseLeave += (_,_) => EndGesture(null,null);
        KeyDown += HandleKeys;
        ticker.Tick += (_,_) =>
        {
            if(!media.Checked)Redraw();
            if(++batteryRefreshCounter>=45)
            {
                batteryRefreshCounter=0;
                _ = RefreshBatteriesAsync();
            }
        };
        ticker.Start();
        FormClosed += (_,_) => ticker.Stop();
        ApplyBatteryWidgets(batteryPreferences.DisplayDevices,false);
        Shown += async (_,_)=>await RefreshBatteriesAsync();
        RefreshWidgetList();UpdateHistoryButtons();Redraw();
        tabs.SelectedIndex=1; // Open the photographic preview first.
    }

    private async System.Threading.Tasks.Task RefreshBatteriesAsync()
    {
        if(batteryPolling||IsDisposed)return;
        batteryPolling=true;
        try
        {
            var newest=await BatterySources.PollAsync(batteryPreferences.ExperimentalControllerHid);
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
        if(record)Record();
        var current=state.Widgets
            .Where(w=>BatterySources.Kinds.Contains(w.Kind))
            .OrderBy(w=>w.X).ToList();
        var positions=current.Select(w=>new Point(w.X,w.Y)).ToList();
        state.Widgets.RemoveAll(w=>BatterySources.Kinds.Contains(w.Kind));
        for(int i=0;i<selectedDevices.Length;i++)
        {
            var p=i<positions.Count?positions[i]:new Point(2+i*43,53);
            state.Widgets.Add(new DockWidget{Kind=selectedDevices[i],X=p.X,Y=p.Y,Visible=true});
        }
        RefreshWidgetList();Redraw();
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
        selected=null;RefreshWidgetList();Redraw();UpdateHistoryButtons();SetStatus("Son işlem geri alındı.");
    }
    private void Redo()
    {
        if(redo.Count==0)return;
        undo.Push(state.DeepClone());state=redo.Pop();
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
        fontSize.Enabled=has && selected!.Kind is "Clock" or "Date" or "Track" or "Artist" or "Custom text";
        widgetVisible.Enabled=has;xPos.Enabled=has;yPos.Enabled=has;
        if(selected!=null)
        {
            customText.Text=selected.Text;
            fontSize.Value=Math.Clamp(selected.FontSize,7,24);
            xPos.Value=Math.Clamp(selected.X,0,127);yPos.Value=Math.Clamp(selected.Y,12,63);
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
        if(selected.X==newX&&selected.Y==newY&&selected.FontSize==newSize)return;
        Record();selected.X=newX;selected.Y=newY;selected.FontSize=newSize;RefreshWidgetList();Redraw();
    }
    private void AddWidget()
    {
        Record();
        var w=new DockWidget{Kind=kind.Text,X=5,Y=19};
        state.Widgets.Add(w);selected=w;RefreshWidgetList();Redraw();tool.SelectedIndex=2;
    }
    private void RemoveWidget()
    {
        if(selected==null)return;
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
    private bool Current(DockWidget w)=>w.Kind switch
    {
        "Clock" or "Date"=>!media.Checked&&(w.Kind!="Date"||showDate.Checked),
        "Source" or "Track" or "Artist" or "Progress"=>media.Checked,
        _=>true
    };
    private void PaintFrame(Graphics g,bool edit,int zoom)
    {
        using var frame=OledRenderer.Render(state,media.Checked,showDate.Checked,source.SelectedIndex==0,batterySnapshot);
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
        if(ActiveControl==customText||ActiveControl==fontSize||ActiveControl==xPos||ActiveControl==yPos)return;
        if(e.KeyCode==Keys.Delete){RemoveWidget();e.SuppressKeyPress=true;return;}
        if(selected==null)return;
        int dx=e.KeyCode==Keys.Left?-1:e.KeyCode==Keys.Right?1:0;
        int dy=e.KeyCode==Keys.Up?-1:e.KeyCode==Keys.Down?1:0;
        if(dx==0&&dy==0)return;
        int step=e.Shift?5:1;
        int x=Math.Clamp(selected.X+dx*step,0,127),y=Math.Clamp(selected.Y+dy*step,12,63);
        if(x==selected.X&&y==selected.Y)return;
        Record();selected.X=x;selected.Y=y;RefreshWidgetList();Redraw();e.SuppressKeyPress=true;
    }

    private void SaveLayout()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            File.WriteAllText(savePath,JsonSerializer.Serialize(state,new JsonSerializerOptions{WriteIndented=false}));
            SetStatus("Yerleşim kaydedildi: "+savePath);
        }
        catch(Exception ex){SetStatus("Kayıt hatası: "+ex.Message);}
    }
    private void LoadLayout()
    {
        if(!File.Exists(savePath)){SetStatus("Henüz kayıtlı yerleşim bulunmuyor.");return;}
        try
        {
            var next=JsonSerializer.Deserialize<LayoutState>(File.ReadAllText(savePath));
            if(next==null||next.Pixels.Length!=8192||next.Widgets.Count>100)throw new InvalidDataException("Geçersiz yerleşim");
            foreach(var w in next.Widgets)
            {
                w.X=Math.Clamp(w.X,0,127);w.Y=Math.Clamp(w.Y,12,63);
                w.FontSize=Math.Clamp(w.FontSize,7,24);
            }
            Record();state=next;selected=null;RefreshWidgetList();Redraw();SetStatus("Kaydedilen yerleşim yüklendi.");
        }
        catch(Exception ex){SetStatus("Yükleme hatası: "+ex.Message);}
    }
    private void ExportPng()
    {
        using var dialog=new SaveFileDialog{Filter="PNG görüntüsü|*.png",FileName="oled-128x64.png"};
        if(dialog.ShowDialog()!=DialogResult.OK)return;
        try
        {
            using var img=OledRenderer.Render(state,media.Checked,showDate.Checked,source.SelectedIndex==0,batterySnapshot);
            img.Save(dialog.FileName,ImageFormat.Png);
            SetStatus("128×64 PNG kaydedildi: "+dialog.FileName);
        }
        catch(Exception ex){SetStatus("PNG kaydetme hatası: "+ex.Message);}
    }
}

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

internal sealed class LayoutProfileDialog : Form
{
    private static readonly Color Bg=Color.FromArgb(13,18,26);
    private static readonly Color Surface=Color.FromArgb(23,31,43);
    private static readonly Color Fore=Color.FromArgb(235,242,248);
    private static readonly Color Accent=Color.FromArgb(72,194,238);
    private readonly LayoutProfileLibrary library=LayoutProfileLibrary.Load();
    private readonly ScreenMode mode;
    private readonly Func<LayoutState> snapshot;
    private readonly Action<LayoutState> apply;
    private readonly ListBox list=new(){Left=23,Top=106,Width=340,Height=251,
        BorderStyle=BorderStyle.None,BackColor=Surface,ForeColor=Fore,
        Font=new Font("Segoe UI",11f)};
    // Never overlap "Apply profile" or the left-side mini preview.
    private readonly Label hint=new(){Left=390,Top=439,Width=247,Height=48,
        ForeColor=Color.FromArgb(151,169,187)};
    private readonly Panel miniPreview=new(){
        Name="ProfileMiniPreview",
        Left=23,Top=382,Width=340,Height=108,
        BorderStyle=BorderStyle.FixedSingle,BackColor=Color.Black};
    private Bitmap? miniFrame;

    public LayoutProfileDialog(ScreenMode editingMode,Func<LayoutState> current,
        Action<LayoutState> onApply)
    {
        UiPolish.Register(this);
        mode=editingMode;
        snapshot=current;
        apply=onApply;
        Text=UiLanguage.T("Profil yöneticisi");
        // Original compact window size; scrolling, not window growth,
        // handles unusual system DPI and large accessibility font settings.
        ClientSize=new Size(680,555);
        MinimumSize=Size;
        MaximumSize=Size;
        FormBorderStyle=FormBorderStyle.FixedDialog;
        StartPosition=FormStartPosition.CenterParent;
        MaximizeBox=false;MinimizeBox=false;
        BackColor=Bg;ForeColor=Fore;
        Font=new Font("Segoe UI",10f);
        var body=new Panel{
            Dock=DockStyle.Fill,AutoScroll=true,
            AutoScrollMinSize=new Size(654,490),
            BackColor=Bg};
        var footer=new Panel{Dock=DockStyle.Bottom,Height=56,BackColor=Bg};
        var frame=new TableLayoutPanel{
            Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,
            Margin=Padding.Empty,Padding=Padding.Empty,
            BackColor=Bg};
        frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        frame.RowStyles.Add(new RowStyle(SizeType.Absolute,56));
        body.Dock=DockStyle.Fill;
        footer.Dock=DockStyle.Fill;
        frame.Controls.Add(body,0,0);
        frame.Controls.Add(footer,0,1);
        Controls.Add(frame);
        body.Controls.Add(new Label{Left=23,Top=18,Width=620,Height=37,
            Text=UiLanguage.T("OLED tasarım profilleri"),ForeColor=Accent,
            Font=new Font("Segoe UI Semibold",19f,FontStyle.Bold)});
        body.Controls.Add(new Label{Left=23,Top=68,Width=630,Height=29,
            Text=UiLanguage.T("Bu ekran moduna ait profilleri yönet."),
            ForeColor=Color.FromArgb(151,169,187)});
        body.Controls.Add(list);
        body.Controls.Add(hint);
        body.Controls.Add(new Label{
            Text=UiLanguage.English?"Selected profile preview":"Seçili profil önizlemesi",
            Left=23,Top=361,Width=341,Height=21,ForeColor=Accent,
            Font=new Font("Segoe UI Semibold",9.3f,FontStyle.Bold)});
        body.Controls.Add(miniPreview);
        miniPreview.Paint+=(_,e)=>{
            e.Graphics.Clear(Color.Black);
            if(miniFrame is null)return;
            e.Graphics.InterpolationMode=
                System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode=
                System.Drawing.Drawing2D.PixelOffsetMode.Half;
            int h=Math.Max(1,miniPreview.ClientSize.Height-6);
            int w=Math.Min(miniPreview.ClientSize.Width-6,h*2);
            h=w/2;
            int x=(miniPreview.ClientSize.Width-w)/2;
            int y=(miniPreview.ClientSize.Height-h)/2;
            e.Graphics.DrawImage(miniFrame,new Rectangle(x,y,w,h));
        };
        list.SelectedIndexChanged+=(_,_)=>RefreshPreview();
        Disposed+=(_,_)=>{miniFrame?.Dispose();miniFrame=null;};
        var blank=Button("Boş oluştur",390,111,243);
        var copy=Button("Geçerli tasarımı kaydet",390,156,243);
        var overwrite=Button("↻ Seçili profilin üzerine kaydet",390,201,243);
        var rename=Button("Yeniden adlandır",390,246,243);
        var remove=Button("Profili sil",390,291,243);
        var import=Button("İçe aktar",390,336,117);
        var export=Button("Dışa aktar",516,336,117);
        var use=Button("✓ Profili uygula",390,390,243);
        use.BackColor=Accent;use.ForeColor=Bg;
        body.Controls.AddRange([blank,copy,overwrite,rename,remove,import,export,use]);
        var close=Button("Kapat",545,9,115);
        close.Anchor=AnchorStyles.Top|AnchorStyles.Right;
        close.Click+=(_,_)=>Close();
        footer.Controls.Add(close);
        footer.Resize+=(_,_)=>close.Left=Math.Max(12,footer.Width-130);
        close.Left=Math.Max(12,footer.Width-130);
        blank.Click+=(_,_)=>Create(false);
        copy.Click+=(_,_)=>Create(true);
        overwrite.Click+=(_,_)=>Run(()=>
        {
            var selected=Selected();
            if(MessageBox.Show(this,
                $"'{selected.Name}' profilinin kayıtlı tasarımı şu anki ekranla değiştirilsin?\n\nEski kopya .bak dosyasında korunacak.",
                "Profilin üzerine kaydet",MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)
                return;
            library.Overwrite(selected.Id,mode,snapshot());
            SaveAndRefresh(selected.Id);
        });
        rename.Click+=(_,_)=>Run(() => {
            var selected=Selected();
            string? name=Prompt(UiLanguage.T("Yeni profil adı"),selected.Name);
            if(name is null)return;
            selected.Name=ValidateName(name);
            SaveAndRefresh(selected.Id);
        });
        remove.Click+=(_,_)=>Run(() => {
            var selected=Selected();
            if(MessageBox.Show(this,
                UiLanguage.T("Seçili profil silinsin mi?"),Text,
                MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            library.Profiles.Remove(selected);
            SaveAndRefresh();
        });
        import.Click+=(_,_)=>Run(() => {
            using var picker=new OpenFileDialog{Filter="JSON (*.json)|*.json"};
            if(picker.ShowDialog(this)!=DialogResult.OK)return;
            var incoming=LayoutProfileLibrary.ReadImport(picker.FileName);
            if(incoming.Mode!=mode)
                throw new InvalidDataException(UiLanguage.T("Profil başka ekran moduna ait."));
            var added=library.Add(incoming.Name,mode,incoming.Layout);
            SaveAndRefresh(added.Id);
        });
        export.Click+=(_,_)=>Run(() => {
            var profile=Selected();
            using var picker=new SaveFileDialog{Filter="JSON (*.json)|*.json",
                FileName="oled-profile.json"};
            if(picker.ShowDialog(this)!=DialogResult.OK)return;
            File.WriteAllText(picker.FileName,JsonSerializer.Serialize(profile,
                new JsonSerializerOptions{WriteIndented=true}));
            hint.Text=UiLanguage.T("Profil dışa aktarıldı.");
        });
        use.Click+=(_,_)=>Run(() => {
            var profile=Selected();
            apply(profile.Layout.DeepClone());
            hint.Text=UiLanguage.T("Profil uygulandı. Kalıcı tutmak için Kaydet'e bas.");
        });
        RefreshList();
        UiSkin.ApplyDark(this);
        miniPreview.BackColor=Color.Black;
    }

    private static Button Button(string title,int x,int y,int width)=>new DarkButton{
        Left=x,Top=y,Width=width,Height=37,Text=UiLanguage.T(title),
        BackColor=Surface,ForeColor=Fore,Cursor=Cursors.Hand};

    private void Create(bool fromCurrent)=>Run(() => {
        string suggested=fromCurrent?UiLanguage.T("Tasarım kopyası"):
            UiLanguage.T("Boş profil");
        string? name=Prompt(UiLanguage.T("Profil adı"),suggested);
        if(name is null)return;
        var added=library.Add(ValidateName(name),mode,
            fromCurrent?snapshot():new LayoutState());
        SaveAndRefresh(added.Id);
    });

    private SavedLayoutProfile Selected()=>list.SelectedItem as SavedLayoutProfile
        ??throw new InvalidOperationException(UiLanguage.T("Önce bir profil seç."));

    private static string ValidateName(string value)
    {
        string cleaned=value.Trim();
        if(cleaned.Length is <1 or >64)
            throw new ArgumentException(UiLanguage.T("Profil adı 1–64 karakter olmalı."));
        return cleaned;
    }

    private void SaveAndRefresh(Guid? select=null)
    {
        library.Save();
        RefreshList(select);
        hint.Text=UiLanguage.T("Profil kaydedildi.");
    }

    private void RefreshList(Guid? select=null)
    {
        list.Items.Clear();
        foreach(var item in library.Profiles.Where(p=>p.Mode==mode))
            list.Items.Add(item);
        if(select.HasValue)
            list.SelectedItem=list.Items.Cast<SavedLayoutProfile>()
                .FirstOrDefault(x=>x.Id==select);
        else if(list.Items.Count>0)list.SelectedIndex=0;
        hint.Text=UiLanguage.T("Profiller yalnızca bu bilgisayarda saklanır.");
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        Bitmap? next=null;
        if(list.SelectedItem is SavedLayoutProfile chosen)
        {
            try
            {
                // A static local 128x64 render, never a HID write.
                next=OledRenderer.Render(chosen.Layout,
                    mode==ScreenMode.Media,true,false,protectHeader:true,
                    layoutMode:mode);
            }
            catch{ /* Corrupt import still cannot block profile management. */ }
        }
        var previous=miniFrame;
        miniFrame=next;
        previous?.Dispose();
        miniPreview.Invalidate();
    }

    private void Run(Action action)
    {
        try{action();}
        catch(Exception error)
        {
            MessageBox.Show(this,error.Message,Text,MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private string? Prompt(string title,string initial)
    {
        using var prompt=new Form{Text=title,ClientSize=new Size(400,143),
            StartPosition=FormStartPosition.CenterParent,
            FormBorderStyle=FormBorderStyle.FixedDialog,
            BackColor=Bg,ForeColor=Fore,MaximizeBox=false,MinimizeBox=false};
        var field=new TextBox{Left=19,Top=27,Width=358,Text=initial,
            BackColor=Surface,ForeColor=Fore,BorderStyle=BorderStyle.FixedSingle};
        var okay=Button("Kaydet",267,82,111);
        okay.DialogResult=DialogResult.OK;
        var cancel=Button("Vazgeç",145,82,111);
        cancel.DialogResult=DialogResult.Cancel;
        prompt.Controls.AddRange([field,okay,cancel]);
        prompt.AcceptButton=okay;prompt.CancelButton=cancel;
        return prompt.ShowDialog(this)==DialogResult.OK?field.Text:null;
    }
}

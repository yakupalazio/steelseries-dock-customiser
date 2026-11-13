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
    private readonly ListBox list=new(){Left=23,Top=107,Width=340,Height=285,
        BorderStyle=BorderStyle.None,BackColor=Surface,ForeColor=Fore,
        Font=new Font("Segoe UI",11f)};
    private readonly Label hint=new(){Left=24,Top=404,Width=610,Height=52,
        ForeColor=Color.FromArgb(151,169,187)};

    public LayoutProfileDialog(ScreenMode editingMode,Func<LayoutState> current,
        Action<LayoutState> onApply)
    {
        UiPolish.Register(this);
        mode=editingMode;
        snapshot=current;
        apply=onApply;
        Text=UiLanguage.T("Profil yöneticisi");
        ClientSize=new Size(705,628);
        MinimumSize=new Size(675,530);
        FormBorderStyle=FormBorderStyle.Sizable;
        StartPosition=FormStartPosition.CenterParent;
        MaximizeBox=true;MinimizeBox=false;
        BackColor=Bg;ForeColor=Fore;
        Font=new Font("Segoe UI",10f);
        var body=new Panel{
            Dock=DockStyle.Fill,AutoScroll=true,
            AutoScrollMinSize=new Size(662,500),
            BackColor=Bg};
        var footer=new Panel{Dock=DockStyle.Bottom,Height=65,BackColor=Bg};
        Controls.Add(body);
        Controls.Add(footer);
        body.Controls.Add(new Label{Left=23,Top=18,Width=620,Height=37,
            Text=UiLanguage.T("OLED tasarım profilleri"),ForeColor=Accent,
            Font=new Font("Segoe UI Semibold",19f,FontStyle.Bold)});
        body.Controls.Add(new Label{Left=23,Top=68,Width=630,Height=29,
            Text=UiLanguage.T("Bu ekran moduna ait profilleri yönet."),
            ForeColor=Color.FromArgb(151,169,187)});
        body.Controls.Add(list);
        body.Controls.Add(hint);
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
        var close=Button("Kapat",545,12,115);
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

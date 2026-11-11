using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

internal sealed class IconEditorDialog : Form
{
    private const int Cell=22,MaxCells=20;
    private readonly ComboBox iconNames=new(){Left=20,Top=48,Width=205,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly PictureBox pixelGrid=new(){Left=20,Top=93,Width=Cell*MaxCells,Height=Cell*MaxCells,BackColor=Color.FromArgb(25,28,31),BorderStyle=BorderStyle.FixedSingle};
    private readonly PictureBox preview=new(){Left=488,Top=112,Width=145,Height=145,BackColor=Color.Black,BorderStyle=BorderStyle.FixedSingle};
    private readonly DarkNumberBox iconWidth=new(){Minimum=4,Maximum=20,Width=75};
    private readonly DarkNumberBox iconHeight=new(){Minimum=4,Maximum=20,Width=75};
    private readonly Label info=new(){Left=20,Top=564,Width=615,Height=35,Text="Sol tık: piksel aç  |  Sağ tık: sil  |  Ctrl+Z / Ctrl+Y: geri al / ileri al"};
    private readonly Stack<string[]> undo=new();
    private readonly Stack<string[]> redo=new();
    private bool[,] pixels=new bool[20,20];
    private int width=14,height=14;
    private bool changing,draw,drawValue,dirty;
    private string loadedIcon="";
    private readonly Action onSaved;

    public IconEditorDialog(Action update)
    {
        onSaved=update;
        Text="İkon Düzenleyici — Steelseries Dock Customiser v0.9";
        ClientSize=new Size(664,615);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
        StartPosition=FormStartPosition.CenterParent;KeyPreview=true;
        Font=new Font("Segoe UI",10);
        Controls.Add(new Label{Left=20,Top=12,Width=600,Height=28,Font=new Font("Segoe UI",13,FontStyle.Bold),
            Text="İkonları tek tek piksel piksel düzenle"});
        Controls.Add(iconNames);
        iconNames.FormattingEnabled=true;
        iconNames.Format+=(_,e)=>
        {
            if(e.ListItem is not string name)return;
            e.Value=UiLanguage.English?name:name switch
            {
                "Game"=>"Oyun",
                "Chat"=>"Sohbet",
                "Charging"=>"Şarj oluyor",
                _=>name
            };
        };
        iconNames.Items.AddRange(IconLibrary.Names);
        iconNames.SelectedIndexChanged+=(_,_)=>ChangeIcon();
        Controls.Add(pixelGrid); Controls.Add(preview);Controls.Add(info);
        pixelGrid.Paint+=DrawGrid;
        preview.Paint+=DrawPreview;
        pixelGrid.MouseDown+=(_,e)=>{if(e.Button is MouseButtons.Left or MouseButtons.Right){draw=true;drawValue=e.Button==MouseButtons.Left;Record();EditAt(e.Location);}};
        pixelGrid.MouseMove+=(_,e)=>{if(draw)EditAt(e.Location);};
        pixelGrid.MouseUp+=(_,_)=>draw=false;
        pixelGrid.MouseLeave+=(_,_)=>draw=false;
        var note=new Label{Left=488,Top=88,Width=160,Text="1× / 8× önizleme"};
        Controls.Add(note);
        var sizeRow=new FlowLayoutPanel{Left=485,Top=278,Width=168,Height=84,WrapContents=true};
        sizeRow.Controls.AddRange([new Label{AutoSize=true,Text="Genişlik:"},iconWidth,
            new Label{AutoSize=true,Text="Yükseklik:"},iconHeight]);
        iconWidth.ValueChanged+=(_,_)=>ResizeIcon();
        iconHeight.ValueChanged+=(_,_)=>ResizeIcon();
        Controls.Add(sizeRow);
        var hint=new Label{Left=488,Top=383,Width=163,Height=112,
            Text="Kaydettiğinde ikonu hem editör hem de Dock Preview kullanır.\n\nDeğişiklikler yalnızca bilgisayarında tutulur.",
            ForeColor=Color.DarkSlateGray};
        Controls.Add(hint);

        var buttons=new FlowLayoutPanel{Left=20,Top=533,Width=615,Height=42,WrapContents=false};
        Button save=ButtonOf("Kaydet",78),reset=ButtonOf("Orijinal",80),clear=ButtonOf("Temizle",72);
        Button copy=ButtonOf("Kopyala",75),paste=ButtonOf("Yapıştır",77);
        Button backwards=ButtonOf("Geri al",76),forwards=ButtonOf("İleri al",72);
        buttons.Controls.AddRange([save,reset,clear,copy,paste,backwards,forwards]);Controls.Add(buttons);
        save.Click+=(_,_)=>Save();
        reset.Click+=(_,_)=>Reset();
        clear.Click+=(_,_)=>{Record();pixels=new bool[20,20];InvalidatePixels();};
        copy.Click+=(_,_)=>Clipboard.SetText(string.Join(Environment.NewLine,CaptureRows()));
        paste.Click+=(_,_)=>Paste();
        backwards.Click+=(_,_)=>Undo();
        forwards.Click+=(_,_)=>Redo();
        KeyDown+=(sender,e)=>{
            if(e.Control&&e.KeyCode==Keys.Z){Undo();e.SuppressKeyPress=true;}
            else if(e.Control&&e.KeyCode==Keys.Y){Redo();e.SuppressKeyPress=true;}
        };
        FormClosing+=(sender,e)=>{
            if(dirty && MessageBox.Show(UiLanguage.T("Kaydedilmemiş ikon değişiklikleri var. Çıkılsın mı?"),
                UiLanguage.T("İkon editörü"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)
                e.Cancel=true;
        };
        iconNames.SelectedIndex=0;
        UiSkin.ApplyDark(this);
        UiLanguage.ApplyTree(this);
    }

    private static Button ButtonOf(string label,int width)=>new DarkButton(){Text=label,Width=width,Height=31};
    private string CurrentIconName=>iconNames.SelectedItem?.ToString()??"Mouse";
    private void ChangeIcon()
    {
        if(changing)return;
        if(dirty && MessageBox.Show(UiLanguage.T("Kaydedilmemiş çizimi atlayıp başka ikona geçilsin mi?"),
            UiLanguage.T("İkon düzenleyici"),MessageBoxButtons.YesNo)!=DialogResult.Yes)
        {
            changing=true;
            iconNames.SelectedItem=loadedIcon;
            changing=false;
            return;
        }
        loadedIcon=CurrentIconName;
        LoadCurrent();
    }
    private void LoadCurrent()
    {
        changing=true;
        var rows=IconLibrary.GetCurrent(CurrentIconName);
        int maxWidth=CurrentIconName is "Game" or "Chat"?11:CurrentIconName=="Charging"?4:20;
        int maxHeight=CurrentIconName is "Game" or "Chat"?9:CurrentIconName=="Charging"?7:20;
        iconWidth.Maximum=maxWidth;iconHeight.Maximum=maxHeight;
        width=Math.Clamp(rows[0].Length,4,maxWidth);
        height=Math.Clamp(rows.Length,4,maxHeight);
        pixels=new bool[20,20];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            pixels[y,x]=rows[y][x]=='1';
        iconWidth.Value=width;iconHeight.Value=height;
        undo.Clear();redo.Clear();dirty=false;
        changing=false;InvalidatePixels();
    }
    private string[] CaptureRows()=>
        Enumerable.Range(0,height).Select(y=>new string(
            Enumerable.Range(0,width).Select(x=>pixels[y,x]?'1':'0').ToArray())).ToArray();

    private void Restore(string[] rows)
    {
        changing=true;
        width=rows[0].Length;height=rows.Length;pixels=new bool[20,20];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)pixels[y,x]=rows[y][x]=='1';
        iconWidth.Value=width;iconHeight.Value=height;
        changing=false;dirty=true;InvalidatePixels();
    }
    private void Record()
    {
        undo.Push(CaptureRows());
        redo.Clear();
    }
    private void Undo()
    {
        if(undo.Count==0)return;
        redo.Push(CaptureRows());Restore(undo.Pop());
    }
    private void Redo()
    {
        if(redo.Count==0)return;
        undo.Push(CaptureRows());Restore(redo.Pop());
    }
    private void EditAt(Point p)
    {
        int x=p.X/Cell,y=p.Y/Cell;
        if(x<0||x>=width||y<0||y>=height||pixels[y,x]==drawValue)return;
        pixels[y,x]=drawValue;dirty=true;InvalidatePixels();
    }
    private void ResizeIcon()
    {
        if(changing)return;
        Record();width=(int)iconWidth.Value;height=(int)iconHeight.Value;
        dirty=true;InvalidatePixels();
    }
    private void Reset()
    {
        Record();
        var rows=OledRenderer.GetOriginalIconRows(CurrentIconName);
        Restore(rows);
        info.Text=UiLanguage.T("Orijinal sprite yüklendi. Kaydet ile değişikliği uygula.");
    }
    private void Paste()
    {
        try
        {
            var rows=Clipboard.GetText().Trim().Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries);
            if(rows.Length<4||rows.Length>20||rows.Any(r=>r.Length<4||r.Length>20||r.Length!=rows[0].Length||r.Any(c=>c!='0'&&c!='1')))
                throw new FormatException(UiLanguage.T("Geçersiz 0/1 sprite matrisi."));
            Record();Restore(rows);info.Text=UiLanguage.T("Yapıştırıldı. Kaydet ile uygula.");
        }
        catch(Exception ex){MessageBox.Show(ex.Message,UiLanguage.T("Yapıştırma başarısız"));}
    }
    private void Save()
    {
        try
        {
            IconLibrary.Set(CurrentIconName,CaptureRows());dirty=false;
            onSaved();
            info.Text=UiLanguage.T(CurrentIconName+" ikonu kaydedildi ve önizlemeye uygulandı.");
        }
        catch(Exception ex){MessageBox.Show(ex.Message,UiLanguage.T("Kaydetme hatası"));}
    }
    private void InvalidatePixels(){pixelGrid.Invalidate();preview.Invalidate();}
    private void DrawGrid(object? sender,PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(Color.FromArgb(30,33,40));
        using var border=new Pen(Color.FromArgb(66,74,85),1);
        using var disabled=new SolidBrush(Color.FromArgb(24,26,30));
        for(int y=0;y<MaxCells;y++)for(int x=0;x<MaxCells;x++)
        {
            var rect=new Rectangle(x*Cell,y*Cell,Cell-1,Cell-1);
            if(x>=width||y>=height)g.FillRectangle(disabled,rect);
            else if(pixels[y,x])g.FillRectangle(Brushes.White,rect);
            else g.FillRectangle(Brushes.Black,rect);
            g.DrawRectangle(border,x*Cell,y*Cell,Cell,Cell);
        }
    }
    private void DrawPreview(object? sender,PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Black);
        int zoom=Math.Max(1,Math.Min(8,Math.Min((preview.Width-10)/width,(preview.Height-10)/height)));
        int x0=(preview.Width-width*zoom)/2,y0=(preview.Height-height*zoom)/2;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(pixels[y,x])
            e.Graphics.FillRectangle(Brushes.White,x0+x*zoom,y0+y*zoom,zoom,zoom);
    }
}

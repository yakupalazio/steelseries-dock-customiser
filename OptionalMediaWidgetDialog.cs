using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Contextual editor for opt-in media widgets. No system audio capture,
/// network lyrics fetch or GIF decode runs simply by opening this dialog.
/// </summary>
internal sealed class OptionalMediaWidgetDialog:Form
{
    public DockWidget Updated {get;}
    private readonly FlowLayoutPanel content=new()
    {
        Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,
        WrapContents=false,AutoScroll=true,
        Padding=new Padding(16,8,12,8)
    };
    private static readonly Color Bg=Color.FromArgb(19,26,37);
    private static readonly Color TextColor=Color.FromArgb(234,243,250);
    private static readonly Color Muted=Color.FromArgb(151,169,187);

    public OptionalMediaWidgetDialog(DockWidget source)
    {
        UiPolish.Register(this);
        Updated=JsonSerializer.Deserialize<DockWidget>(
            JsonSerializer.Serialize(source))??new DockWidget{Kind=source.Kind};
        Text=source.Kind switch
        {
            "Lyrics"=>"Senkronize şarkı sözü ayarları",
            "AudioVisualizer"=>"Ses görselleştirici ayarları",
            _=>"GIF widget ayarları"
        };
        StartPosition=FormStartPosition.CenterParent;
        ClientSize=new Size(475,550);
        MinimumSize=new Size(475,435);
        BackColor=Bg;
        ForeColor=TextColor;
        Font=new Font("Segoe UI",9.8f);
        content.Dock=DockStyle.None;
        content.Bounds=new Rectangle(0,0,ClientSize.Width,ClientSize.Height-64);
        content.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|
            AnchorStyles.Left|AnchorStyles.Right;
        Controls.Add(content);
        var name=new Label{
            Text=Text,Width=425,Height=35,
            Font=new Font("Segoe UI Semibold",15,FontStyle.Bold),
            ForeColor=TextColor
        };
        content.Controls.Add(name);
        switch(source.Kind)
        {
            case "Lyrics":BuildLyrics();break;
            case "AudioVisualizer":BuildVisualizer();break;
            case "AnimatedGif":BuildGif();break;
        }
        var actions=new Panel{
            Height=64,BackColor=Bg,
            Bounds=new Rectangle(0,ClientSize.Height-64,ClientSize.Width,64),
            Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom
        };
        var save=Button("Uygula");
        var cancel=Button("Vazgeç");
        save.Left=ClientSize.Width-200;save.Top=10;
        cancel.Left=ClientSize.Width-387;cancel.Top=10;
        actions.Controls.Add(save);
        actions.Controls.Add(cancel);
        save.Click+=(_,_)=>{DialogResult=DialogResult.OK;Close();};
        cancel.Click+=(_,_)=>{DialogResult=DialogResult.Cancel;Close();};
        AcceptButton=save;CancelButton=cancel;
        Controls.Add(actions);
        actions.BringToFront();
    }

    private void BuildLyrics()
    {
        Note("Dynamic Island benzeri: küçük medya ikonu solda sabit "+
            "kalır. Uzun sözler sola kayar; yeni satır alttan yukarı "+
            "geçerek görünür. Kaynak: Windows medya oturumu + LRCLIB.");
        var offset=Number(-10000,10000,10,Updated.LyricOffsetMs);
        Row("Senkron kaydırma (ms)",offset);
        Note("Negatif değer sözleri erkene alır; pozitif değer geç gösterir. "+
            "Örneğin -500 ms yarım saniye erken, +500 ms geç görünür. "+
            "10 ms adımlar matematiksel olarak uygulanır; dock'un gerçek "+
            "yenileme hızı bu hassasiyeti garanti etmez.");
        offset.ValueChanged+=(_,_)=>Updated.LyricOffsetMs=(int)offset.Value;
        var rowCount=Combo("1 satır","2 satır","3 satır");
        rowCount.SelectedIndex=Math.Clamp(Updated.LyricLines,1,3)-1;
        Row("Görüntülenecek satır",rowCount);
        rowCount.SelectedIndexChanged+=(_,_)=>Updated.LyricLines=
            Math.Clamp(rowCount.SelectedIndex+1,1,3);
        Note("Şarkı sözleri kelime sınırında 2 veya 3 satıra bölünür. "+
            "Enstrümantal aralıklarda yalnızca nota gösterilir.");
        var width=Number(16,128,1,Updated.WidgetWidth);
        Row("Yazı alanı genişliği",width);
        width.ValueChanged+=(_,_)=>Updated.WidgetWidth=(int)width.Value;
        Note("Bu öğe yalnızca Medya ekranında kullanılabilir. "+
            "Kaynakta senkronize söz yoksa sahte satır gösterilmez.");
    }

    private void BuildVisualizer()
    {
        Note("Otomatik mod Sonar/Windows çıkışlarında aktif sesi algılar. "+
            "İstersen belirli ses çıkışını seçebilirsin. Ham ses kaydedilmez.");
        var output=new ComboBox{
            Width=185,Height=29,DropDownStyle=ComboBoxStyle.DropDownList,
            DropDownWidth=440,BackColor=Color.FromArgb(23,31,43),
            ForeColor=TextColor,FlatStyle=FlatStyle.Flat
        };
        foreach(var endpoint in SystemAudioVisualizer.AvailableOutputs())
            output.Items.Add(endpoint);
        var saved=Updated.AudioEndpointId;
        int found=-1;
        for(int i=0;i<output.Items.Count;i++)
            if(output.Items[i] is AudioOutputOption item&&item.Id==saved)
                {found=i;break;}
        output.SelectedIndex=found<0?0:found;
        output.SelectedIndexChanged+=(_,_)=>
            Updated.AudioEndpointId=(output.SelectedItem as AudioOutputOption)?.Id??"";
        Row("Ses çıkışı",output);
        var mode=Combo("Bars","Stereo","Circular","Bubbles","Pulse");
        mode.FormattingEnabled=true;
        mode.Format+=(_,e)=>{
            if(e.ListItem is not string key)return;
            e.Value=key switch{
                "Bars"=>UiLanguage.English?"Bars":"Çubuklar",
                "Stereo"=>UiLanguage.English?"Stereo":"Stereo",
                "Circular"=>UiLanguage.English?"Circular":"Dairesel",
                "Bubbles"=>UiLanguage.English?"Bubbles":"Baloncuklar",
                "Pulse"=>UiLanguage.English?"Pulse":"Nabız",
                _=>key};
        };
        mode.SelectedItem=Updated.VisualizerStyle;
        Row("Görünüm",mode);
        mode.SelectedIndexChanged+=(_,_)=>Updated.VisualizerStyle=
            mode.SelectedItem?.ToString()??"Bars";
        var width=Number(16,128,1,Updated.WidgetWidth);
        var height=Number(8,54,1,Updated.VisualizerHeight);
        var gain=Number(10,400,10,Updated.VisualizerGain);
        Row("Genişlik (piksel)",width);
        Row("Yükseklik (piksel)",height);
        Row("Hassasiyet (%)",gain);
        width.ValueChanged+=(_,_)=>Updated.WidgetWidth=(int)width.Value;
        height.ValueChanged+=(_,_)=>Updated.VisualizerHeight=(int)height.Value;
        gain.ValueChanged+=(_,_)=>Updated.VisualizerGain=(int)gain.Value;
        Note(UiLanguage.English
            ?"Bars: frequency columns; Stereo: two channels; Circular: radial; "+
              "Bubbles: rising bubbles accelerate with the beat; Pulse: bass rings."
            :"Bars: frekans çubukları; Stereo: iki kanal; Circular: "+
              "dairesel tepkiler; Bubbles: ritimle hızlanan baloncuklar; "+
              "Pulse: bas ritmine tepki veren piksel halkaları.");
    }

    private void BuildGif()
    {
        Note("GIF dosyası yalnızca bu Windows kullanıcısının yerel "+
            "uygulama klasörüne kopyalanır; diğer kullanıcılara dağıtılmaz.");
        var file=new Label{
            Text=Updated.GifAssetId.Length>0?Updated.GifAssetId:"GIF seçilmedi",
            Width=414,Height=25,ForeColor=Muted,AutoEllipsis=true
        };
        content.Controls.Add(file);
        var choose=Button("GIF seç / değiştir");
        content.Controls.Add(choose);
        choose.Click+=(_,_)=>
        {
            using var picker=new OpenFileDialog{
                Filter="Animasyonlu GIF|*.gif",
                CheckFileExists=true,
                Title="128×64 OLED için GIF seç"
            };
            if(picker.ShowDialog(this)!=DialogResult.OK)return;
            try
            {
                Updated.GifAssetId=AnimatedGifWidgetStore.Import(picker.FileName);
                file.Text=Path.GetFileName(picker.FileName);
            }
            catch(Exception ex)
            {
                MessageBox.Show(this,ex.Message,"GIF yüklenemedi",
                    MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
        };
        var fit=Combo("Contain","Cover","Stretch");
        fit.SelectedItem=Updated.GifFit;
        Row("Ekrana sığdırma",fit);
        fit.SelectedIndexChanged+=(_,_)=>Updated.GifFit=
            fit.SelectedItem?.ToString()??"Contain";
        var dither=Combo("FloydSteinberg","Bayer","Threshold");
        dither.SelectedItem=Updated.GifDither;
        Row("Monokrom dönüşüm",dither);
        dither.SelectedIndexChanged+=(_,_)=>Updated.GifDither=
            dither.SelectedItem?.ToString()??"FloydSteinberg";
        var width=Number(16,128,1,Updated.WidgetWidth);
        var height=Number(8,54,1,Updated.VisualizerHeight);
        var speed=Number(25,400,25,Updated.GifSpeedPercent);
        var threshold=Number(20,235,5,Updated.GifThreshold);
        Row("Genişlik",width);Row("Yükseklik",height);
        Row("Animasyon hızı (%)",speed);
        Row("Siyah/beyaz eşiği",threshold);
        width.ValueChanged+=(_,_)=>Updated.WidgetWidth=(int)width.Value;
        height.ValueChanged+=(_,_)=>Updated.VisualizerHeight=(int)height.Value;
        speed.ValueChanged+=(_,_)=>Updated.GifSpeedPercent=(int)speed.Value;
        threshold.ValueChanged+=(_,_)=>Updated.GifThreshold=(int)threshold.Value;
        Note("En fazla 12 MB / 160 kare; OLED animasyonu güvenli "+
            "kare hızıyla çizilir. Kaynak GIF yeniden kodlanmaz.");
    }

    private static Button Button(string title)=>new DarkButton(){
        Text=title,Width=174,Height=35,BackColor=Color.FromArgb(38,68,82),
        ForeColor=TextColor,FlatStyle=FlatStyle.Flat,Margin=new Padding(3,6,5,4)
    };
    private static ComboBox Combo(params string[] values)
    {
        var c=new ComboBox{
            Width=185,Height=29,DropDownStyle=ComboBoxStyle.DropDownList,
            BackColor=Color.FromArgb(23,31,43),ForeColor=TextColor,
            FlatStyle=FlatStyle.Flat
        };
        c.Items.AddRange(values);
        c.SelectedIndex=0;
        return c;
    }
    private static DarkNumberBox Number(int min,int max,int increment,int value)=>
        new(){
            Minimum=min,Maximum=max,Increment=increment,
            Value=Math.Clamp(value,min,max),
            Width=173,Height=30,
            BackColor=Color.FromArgb(23,31,43),ForeColor=TextColor
        };
    private void Note(string text)
    {
        content.Controls.Add(new Label{
            Text=text,Width=419,Height=62,ForeColor=Muted,
            Margin=new Padding(2,8,2,5)
        });
    }
    private void Row(string caption,Control editor)
    {
        var row=new FlowLayoutPanel{
            Width=425,Height=38,WrapContents=false
        };
        row.Controls.Add(new Label{
            Text=caption,Width=226,Height=32,ForeColor=TextColor,
            TextAlign=ContentAlignment.MiddleLeft
        });
        row.Controls.Add(editor);
        content.Controls.Add(row);
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Real one-pixel-at-a-time wheel designer. The dynamic fill layer is sampled
/// with a percent slider; its bits follow hardware readings when sent live.
/// The original-style factory art is recoverable at any time.
/// </summary>
internal sealed class WheelArtworkDialog : Form
{
    private const int Zoom=10;
    private readonly char[] pixels;
    private readonly PictureBox canvas=new(){
        BackColor=Color.Black,BorderStyle=BorderStyle.None,
        Width=WheelArtwork.Width*Zoom,Height=WheelArtwork.Height*Zoom,
        Cursor=Cursors.Cross};
    private readonly TrackBar sample=new(){Minimum=0,Maximum=100,Value=55,
        TickFrequency=25,Width=215};
    private readonly Label previewLabel=new(){Width=230,Height=35};
    private readonly ComboBox brush=new(){Width=216,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ComboBox mode=new(){Width=216,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly TrackBar rotation=new(){Minimum=-180,Maximum=180,
        TickFrequency=45,SmallChange=5,LargeChange=15,Width=218};
    private readonly Label angleValue=new(){Width=218,Height=23};
    private readonly DarkCheckBox automaticLabel=new(){
        Width=218,Height=32,Checked=true,AutoSize=false};

    public string Artwork {get;private set;}="";
    public bool AutoLabel {get;private set;}=true;
    public bool UseBar {get;private set;}
    public int GapAngle {get;private set;}

    public WheelArtworkDialog(string? saved,bool labelEnabled=true,int gapAngle=0)
    {
        pixels=WheelArtwork.Resolve(saved).ToCharArray();
        Artwork=saved??"";
        AutoLabel=labelEnabled;
        GapAngle=Math.Clamp(gapAngle,-180,180);
        Text=UiLanguage.T("Tekerlek tasarım atölyesi");
        StartPosition=FormStartPosition.CenterParent;
        FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false;MinimizeBox=false;
        ClientSize=new Size(758,625);
        BackColor=Color.FromArgb(13,18,26);
        ForeColor=Color.FromArgb(235,242,248);
        Font=new Font("Segoe UI",9.3f);

        Controls.Add(new Label{
            Text=UiLanguage.T("45 × 42 OLED PİKSELİ  •  TEKERLEK TASARIMI"),
            Font=new Font("Segoe UI Semibold",14,FontStyle.Bold),
            ForeColor=Color.FromArgb(72,194,238),
            Left=24,Top=18,Width=700,Height=32});
        canvas.Left=24;canvas.Top=75;
        canvas.Paint+=PaintCanvas;
        canvas.MouseDown+=PaintPoint;
        canvas.MouseMove+=PaintPoint;
        Controls.Add(canvas);

        int right=500;
        Controls.Add(new Label{Text=UiLanguage.T("Boyama aracı"),
            Left=right,Top=85,Width=230,Height=26});
        brush.Items.AddRange([
            UiLanguage.T("Beyaz piksel"),
            UiLanguage.T("Sesle değişen halka"),
            UiLanguage.T("Siyah piksel"),
            UiLanguage.T("Saydam / Silgi")]);
        brush.SelectedIndex=0;
        brush.Left=right;brush.Top=113;
        Controls.Add(brush);

        Controls.Add(new Label{Text=UiLanguage.T("Orta yazı (otomatik)"),
            Left=right,Top=163,Width=230,Height=24});
        mode.Items.AddRange(["VOL","MIX"]);
        mode.SelectedIndex=0;
        mode.Left=right;mode.Top=190;
        mode.SelectedIndexChanged+=(_,_)=>canvas.Invalidate();
        Controls.Add(mode);

        automaticLabel.Text=UiLanguage.T("Otomatik VOL / MIX yazısı");
        automaticLabel.Checked=labelEnabled;
        automaticLabel.Left=right;automaticLabel.Top=227;
        automaticLabel.CheckedChanged+=(_,_)=>canvas.Invalidate();
        Controls.Add(automaticLabel);

        Controls.Add(new Label{Text=UiLanguage.T("Halka boşluğunun açısı"),
            Left=right,Top=266,Width=230,Height=22});
        rotation.Left=right;rotation.Top=286;
        rotation.Value=GapAngle;
        rotation.ValueChanged+=(_,_)=>{
            GapAngle=rotation.Value;
            angleValue.Text=UiLanguage.T("Açı")+$": {rotation.Value:+#;-#;0}°";
            canvas.Invalidate();
        };
        Controls.Add(rotation);
        angleValue.Left=right;angleValue.Top=327;
        angleValue.ForeColor=Color.FromArgb(151,169,187);
        angleValue.Text=UiLanguage.T("Açı")+$": {GapAngle:+#;-#;0}°";
        Controls.Add(angleValue);

        Controls.Add(new Label{Text=UiLanguage.T("Örnek doluluk"),
            Left=right,Top=354,Width=230,Height=22});
        sample.Left=right;sample.Top=375;
        sample.ValueChanged+=(_,_)=>UpdateSample();
        Controls.Add(sample);
        previewLabel.Left=right;previewLabel.Top=417;
        previewLabel.ForeColor=Color.FromArgb(151,169,187);
        Controls.Add(previewLabel);

        var reset=Button(UiLanguage.T("Orijinal tasarıma dön"),right,451,218);
        reset.Click+=(_,_)=>
        {
            WheelArtwork.Factory.CopyTo(0,pixels,0,pixels.Length);
            automaticLabel.Checked=true;
            rotation.Value=0;
            canvas.Invalidate();
        };
        Controls.Add(reset);
        var flatBar=Button(UiLanguage.T("Düz çubuğa geç"),right,492,218);
        flatBar.Click+=(_,_)=>
        {
            UseBar=true;
            DialogResult=DialogResult.OK;
            Close();
        };
        Controls.Add(flatBar);

        Controls.Add(new Label{
            Text=UiLanguage.T("Sol tık: seçilen piksel  •  Sağ tık: sil\nMavi katman, ses/ChatMix ile hareket eder."),
            ForeColor=Color.FromArgb(151,169,187),
            Left=24,Top=536,Width=452,Height=42});
        var cancel=Button(UiLanguage.T("Vazgeç"),500,542,100);
        cancel.DialogResult=DialogResult.Cancel;
        Controls.Add(cancel);
        var save=Button(UiLanguage.T("Kaydet"),610,542,108);
        save.BackColor=Color.FromArgb(72,194,238);
        save.ForeColor=Color.FromArgb(13,18,26);
        save.Click+=(_,_)=>
        {
            string next=new(pixels);
            Artwork=next==WheelArtwork.Factory?"":next;
            AutoLabel=automaticLabel.Checked;
            DialogResult=DialogResult.OK;
            Close();
        };
        Controls.Add(save);
        AcceptButton=save;CancelButton=cancel;
        UpdateSample();
        UiSkin.ApplyDark(this);
        // UiSkin is intentionally applied after configuring all controls, so
        // both Turkish and English displays get the same dark input chrome.
    }

    private static Button Button(string text,int x,int y,int width)=>new DarkButton{
        Text=text,Left=x,Top=y,Width=width,Height=36,
        BackColor=Color.FromArgb(27,38,51),
        ForeColor=Color.FromArgb(235,242,248),Cursor=Cursors.Hand};

    private void UpdateSample()
    {
        previewLabel.Text=UiLanguage.T("Önizleme")+$": {sample.Value}%";
        canvas.Invalidate();
    }

    private void PaintPoint(object? sender,MouseEventArgs e)
    {
        if(e.Button is not (MouseButtons.Left or MouseButtons.Right))return;
        int x=e.X/Zoom,y=e.Y/Zoom;
        if(x<0||y<0||x>=WheelArtwork.Width||y>=WheelArtwork.Height)return;
        (x,y)=WheelArtwork.SourceAt(x,y,GapAngle);
        if(x<0||y<0||x>=WheelArtwork.Width||y>=WheelArtwork.Height)return;
        char chosen=e.Button==MouseButtons.Right?'0':brush.SelectedIndex switch{
            0=>'1',1=>'2',2=>'3',_=>'0'};
        int index=y*WheelArtwork.Width+x;
        if(pixels[index]==chosen)return;
        pixels[index]=chosen;
        // Rotated source pixels affect different on-screen positions.
        canvas.Invalidate();
        // Centre mode text is also overlaid in the preview. Redraw the entire
        // badge when changing any pixel under that lettering.
        if(Math.Abs(x-WheelArtwork.CenterX)<=10 &&
           Math.Abs(y-WheelArtwork.CenterY)<=10)canvas.Invalidate();
    }

    private void PaintCanvas(object? sender,PaintEventArgs e)
    {
        var bitmap=new bool[128*64];
        var coverage=new bool[128*64];
        string source=new(pixels);
        WheelArtwork.Draw(bitmap,coverage,0,0,source,sample.Value,
            mode.SelectedIndex==1?DialView.ChatMix:DialView.Volume,
            automaticLabel.Checked,GapAngle);
        e.Graphics.Clear(Color.FromArgb(6,10,15));
        for(int y=0;y<WheelArtwork.Height;y++)for(int x=0;x<WheelArtwork.Width;x++)
        {
            char px=WheelArtwork.Sample(source,x,y,GapAngle);
            var fill=bitmap[y*128+x]?Color.FromArgb(235,242,248):
                px=='2'?Color.FromArgb(30,75,100):
                px=='3'?Color.FromArgb(30,26,31):Color.Black;
            using var brushFill=new SolidBrush(fill);
            e.Graphics.FillRectangle(brushFill,x*Zoom,y*Zoom,Zoom,Zoom);
        }
        using var grid=new Pen(Color.FromArgb(25,47,63),1f);
        for(int x=0;x<=WheelArtwork.Width;x++)
            e.Graphics.DrawLine(grid,x*Zoom,0,x*Zoom,WheelArtwork.Height*Zoom);
        for(int y=0;y<=WheelArtwork.Height;y++)
            e.Graphics.DrawLine(grid,0,y*Zoom,WheelArtwork.Width*Zoom,y*Zoom);
    }
}

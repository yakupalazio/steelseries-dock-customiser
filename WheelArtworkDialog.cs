using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Real one-pixel-at-a-time wheel designer. The dynamic fill layer is sampled
/// with a percent slider; its bits follow hardware readings when sent live.
/// The original-style factory art is recoverable at any time.
/// </summary>
internal sealed class WheelArtworkDialog : Form
{
    private readonly int pixelZoom;
    private readonly int gridWidth,gridHeight;
    private readonly char[] pixels;
    private readonly System.Collections.Generic.Stack<string> undoPixels=new();
    private readonly System.Collections.Generic.Stack<string> redoPixels=new();
    private bool strokeActive;
    private readonly Button undoButton=Button("Geri al",290,664,94);
    private readonly Button redoButton=Button("İleri al",394,664,94);
    private readonly PictureBox canvas=new(){
        BackColor=Color.Black,BorderStyle=BorderStyle.None,
        Width=450,Height=420, // updated for 31x29 mini art in constructor
        Cursor=Cursors.Cross};
    private readonly TrackBar sample=new(){Minimum=0,Maximum=100,Value=55,
        TickFrequency=25,Width=215};
    private readonly Label previewLabel=new(){Width=230,Height=35};
    private readonly ComboBox brush=new(){Width=216,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ComboBox mode=new(){Width=216,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly TrackBar rotation=new(){Minimum=-180,Maximum=180,
        TickFrequency=45,SmallChange=5,LargeChange=15,Width=218};
    private readonly Label angleValue=new(){Width=218,Height=23};
    private readonly DarkCheckBox followIcons=new(){Width=453,Height=30,Checked=true};
    private readonly PictureBox overview=new(){
        Width=228,Height=114,BackColor=Color.Black,
        BorderStyle=BorderStyle.FixedSingle,Cursor=Cursors.SizeAll};
    private readonly ComboBox dragTarget=new(){
        Width=104,Height=27,DropDownStyle=ComboBoxStyle.DropDownList};
    private bool draggingIcon;
    private readonly bool compact;
    private readonly DarkNumberBox compactSize=new(){
        Minimum=70,Maximum=100,Width=100
    };
    public int CompactSize {get;private set;}=100;
    private readonly DarkCheckBox showGame=new(){Width=212,Height=26,Checked=true};
    private readonly DarkCheckBox showChat=new(){Width=212,Height=26,Checked=true};
    private readonly DarkCheckBox closeArcEnds=new(){Width=218,Height=28,Checked=true};
    private readonly DarkNumberBox ringThickness=new(){Minimum=1,Maximum=7,Width=92,Value=3};
    private readonly DarkNumberBox gameDX=new(){Minimum=-64,Maximum=64,Width=100};
    private readonly DarkNumberBox gameDY=new(){Minimum=-64,Maximum=64,Width=100};
    private readonly DarkNumberBox chatDX=new(){Minimum=-64,Maximum=64,Width=100};
    private readonly DarkNumberBox chatDY=new(){Minimum=-64,Maximum=64,Width=100};
    private readonly DarkCheckBox automaticLabel=new(){
        Width=218,Height=32,Checked=true,AutoSize=false};

    public string Artwork {get;private set;}="";
    public bool AutoLabel {get;private set;}=true;
    public bool UseBar {get;private set;}
    public int GapAngle {get;private set;}
    public bool FollowModeIcons {get;private set;}=true;
    public int GameDX {get;private set;}
    public int GameDY {get;private set;}
    public int ChatDX {get;private set;}
    public int ChatDY {get;private set;}
    public bool ShowGameLogo { get;private set; } = true;
    public bool ShowChatLogo { get;private set; } = true;
    public bool CloseRingEnds { get;private set; } = true;
    public int RingThickness { get;private set; } = 3;
    private bool artworkEdited;
    private readonly string initialArtwork;

    public WheelArtworkDialog(string? saved,bool labelEnabled=true,int gapAngle=0,
        bool followModeIcons=true,int gameX=0,int gameY=0,int chatX=0,int chatY=0,
        bool showGameLogo=true,bool showChatLogo=true,bool closeRingEnds=true,int thickness=3,
        bool compact=false,int sizePercent=100)
    {
        this.compact=compact;
        CompactSize=Math.Clamp(sizePercent,70,100);
        pixelZoom=compact?13:10;
        gridWidth=compact?MiniWheelArtwork.Width:WheelArtwork.Width;
        gridHeight=compact?MiniWheelArtwork.Height:WheelArtwork.Height;
        pixels=(compact?MiniWheelArtwork.Resolve(saved):
            WheelArtwork.Resolve(saved)).ToCharArray();
        initialArtwork=compact
            ?MiniWheelArtwork.Normalize(saved)
            :WheelArtwork.IsValid(saved)?saved!:"";
        canvas.Width=gridWidth*pixelZoom;
        canvas.Height=gridHeight*pixelZoom;
        Artwork=initialArtwork;
        AutoLabel=labelEnabled;
        GapAngle=Math.Clamp(gapAngle,-180,180);
        FollowModeIcons=true; // Channel positions always follow rotation.
        GameDX=Math.Clamp(gameX,-64,64);
        GameDY=Math.Clamp(gameY,-64,64);
        ChatDX=Math.Clamp(chatX,-64,64);
        ChatDY=Math.Clamp(chatY,-64,64);
        ShowGameLogo=showGameLogo;
        ShowChatLogo=showChatLogo;
        CloseRingEnds=closeRingEnds;
        RingThickness=Math.Clamp(thickness,1,7);
        Text=compact?UiLanguage.T("Mini tekerlek tasarım atölyesi"):
            UiLanguage.T("Tekerlek tasarım atölyesi");
        StartPosition=FormStartPosition.CenterParent;
        FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false;MinimizeBox=false;
        ClientSize=new Size(758,850);
        BackColor=Color.FromArgb(13,18,26);
        ForeColor=Color.FromArgb(235,242,248);
        Font=new Font("Segoe UI",9.3f);

        Controls.Add(new Label{
            Text=compact?UiLanguage.T("MİNİ TEKERLEK  •  PİKSEL ATÖLYESİ"):
                UiLanguage.T("45 × 42 OLED PİKSELİ  •  TEKERLEK TASARIMI"),
            Font=new Font("Segoe UI Semibold",14,FontStyle.Bold),
            ForeColor=Color.FromArgb(72,194,238),
            Left=24,Top=18,Width=700,Height=32});
        canvas.Left=24;canvas.Top=75;
        canvas.Paint+=PaintCanvas;
        overview.Paint+=PaintOverview;
        overview.MouseDown+=(_,e)=>{
            if(e.Button!=MouseButtons.Left || mode.SelectedIndex!=1)return;
            SelectIconAt(e);
            draggingIcon=true;
            MoveIconOnOverview(e);
        };
        overview.MouseMove+=(_,e)=>{
            if(draggingIcon && e.Button==MouseButtons.Left)MoveIconOnOverview(e);
        };
        overview.MouseUp+=(_,_)=>draggingIcon=false;
        overview.MouseLeave+=(_,_)=>draggingIcon=false;
        canvas.Invalidated+=(_,_)=>overview.Invalidate();
        canvas.MouseDown+=PaintPoint;
        canvas.MouseMove+=PaintPoint;
        canvas.MouseUp+=(_,_)=>strokeActive=false;
        brush.SelectedIndexChanged+=(_,_)=>{
            canvas.Cursor=brush.SelectedIndex==4?Cursors.SizeAll:Cursors.Cross;
        };
        Controls.Add(canvas);

        int right=500;
        Controls.Add(new Label{Text=UiLanguage.T("Boyama aracı"),
            Left=right,Top=85,Width=230,Height=26});
        brush.Items.AddRange([
            UiLanguage.T("Beyaz piksel"),
            UiLanguage.T("Sesle değişen halka"),
            UiLanguage.T("Siyah piksel"),
            UiLanguage.T("Saydam / Silgi"),
            UiLanguage.T("Game/Chat logosunu taşı")]);
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

        Controls.Add(new Label{
            Text=UiLanguage.T("Halka kalınlığı (içe doğru)"),
            Left=right,Top=446,Width=235,Height=23});
        ringThickness.Left=right;ringThickness.Top=472;
        ringThickness.Value=RingThickness;
        ringThickness.ValueChanged+=(_,_)=>{
            RingThickness=(int)ringThickness.Value;canvas.Invalidate();
        };
        Controls.Add(ringThickness);
        closeArcEnds.Text=UiLanguage.T("Çember uçlarını kapat");
        closeArcEnds.Left=right;closeArcEnds.Top=500;
        closeArcEnds.Checked=CloseRingEnds;
        closeArcEnds.CheckedChanged+=(_,_)=>{
            CloseRingEnds=closeArcEnds.Checked;canvas.Invalidate();
        };
        Controls.Add(closeArcEnds);

        Controls.Add(new Label{
            Text=UiLanguage.T("ChatMix simgeleri: konum ve dönüş"),
            Left=24,Top=505,Width=455,Height=25,
            ForeColor=Color.FromArgb(134,220,249)});
        // The old auto-follow checkbox was a no-op in the renderer.
        // In Beta 5 each icon instead has an explicit, constrained target.
        Controls.Add(new Label{
            Text=UiLanguage.T("Game ve Chat konumlarını önizlemede sürükle veya X/Y ayarla."),
            Left=24,Top=529,Width=460,Height=30,
            ForeColor=Color.FromArgb(151,169,187)});
        var editors=new[]{
            (UiLanguage.T("Game X"),gameDX,GameDX),
            (UiLanguage.T("Game Y"),gameDY,GameDY),
            (UiLanguage.T("Chat X"),chatDX,ChatDX),
            (UiLanguage.T("Chat Y"),chatDY,ChatDY)
        };
        for(int i=0;i<editors.Length;i++)
        {
            int pos=24+i*114;
            Controls.Add(new Label{Text=editors[i].Item1,
                Left=pos,Top=560,Width=108,Height=23});
            var control=editors[i].Item2;
            control.Left=pos;control.Top=582;
            control.Value=editors[i].Item3;
            Controls.Add(control);
        }
        gameDX.ValueChanged+=(_,_)=>{GameDX=(int)gameDX.Value;canvas.Invalidate();};
        gameDY.ValueChanged+=(_,_)=>{GameDY=(int)gameDY.Value;canvas.Invalidate();};
        chatDX.ValueChanged+=(_,_)=>{ChatDX=(int)chatDX.Value;canvas.Invalidate();};
        chatDY.ValueChanged+=(_,_)=>{ChatDY=(int)chatDY.Value;canvas.Invalidate();};
        showGame.Text=UiLanguage.T("Game logosunu göster");
        showGame.Left=24;showGame.Top=624;showGame.Checked=ShowGameLogo;
        showGame.CheckedChanged+=(_,_)=>{ShowGameLogo=showGame.Checked;canvas.Invalidate();};
        Controls.Add(showGame);
        showChat.Text=UiLanguage.T("Chat logosunu göster");
        showChat.Left=251;showChat.Top=624;showChat.Checked=ShowChatLogo;
        showChat.CheckedChanged+=(_,_)=>{ShowChatLogo=showChat.Checked;canvas.Invalidate();};
        Controls.Add(showChat);
        undoButton.Click+=(_,_)=>UndoPixels();
        redoButton.Click+=(_,_)=>RedoPixels();
        Controls.Add(undoButton);Controls.Add(redoButton);
        UpdateUndoButtons();
        KeyPreview=true;
        KeyDown+=(_,e)=>{
            if(!e.Control)return;
            if(e.KeyCode==Keys.Z){UndoPixels();e.SuppressKeyPress=true;}
            if(e.KeyCode==Keys.Y){RedoPixels();e.SuppressKeyPress=true;}
        };
        if(compact)
        {
            Controls.Add(new Label{
                Text=UiLanguage.T("Mini boyut %"),Left=24,Top=662,
                Width=124,Height=24,
                ForeColor=Color.FromArgb(151,169,187)});
            compactSize.Left=150;compactSize.Top=659;
            compactSize.Value=CompactSize;
            compactSize.ValueChanged+=(_,_)=>{
                CompactSize=(int)compactSize.Value;
                canvas.Invalidate();overview.Invalidate();
            };
            Controls.Add(compactSize);
        }

        var reset=Button(UiLanguage.T("Orijinal tasarıma dön"),right,536,218);
        reset.Click+=(_,_)=>
        {
            PushHistory();
            string factory=compact?MiniWheelArtwork.Factory:WheelArtwork.Factory;
            factory.CopyTo(0,pixels,0,pixels.Length);
            artworkEdited=true;
            automaticLabel.Checked=true;
            rotation.Value=0;
            followIcons.Checked=true;
            showGame.Checked=true;showChat.Checked=true;
            closeArcEnds.Checked=true;ringThickness.Value=3;
            gameDX.Value=0;gameDY.Value=0;chatDX.Value=0;chatDY.Value=0;
            canvas.Invalidate();
        };
        Controls.Add(reset);
        var flatBar=Button(UiLanguage.T("Düz çubuğa geç"),right,582,218);
        flatBar.Click+=(_,_)=>
        {
            UseBar=true;
            DialogResult=DialogResult.OK;
            Close();
        };
        Controls.Add(flatBar);
        Controls.Add(new Label{
            Text=UiLanguage.T("OLED genel önizleme"),
            Left=500,Top=621,Width=230,Height=23,
            ForeColor=Color.FromArgb(134,220,249)});
        overview.Left=500;overview.Top=644;
        Controls.Add(overview);
        Controls.Add(new Label{
            Text=UiLanguage.T("Sürüklenen logo:"),Left=24,Top=749,
            Width=173,Height=26,ForeColor=Color.FromArgb(151,169,187)});
        dragTarget.Items.AddRange(["Game","Chat"]);
        dragTarget.SelectedIndex=0;dragTarget.Left=190;dragTarget.Top=745;
        Controls.Add(dragTarget);

        Controls.Add(new Label{
            Text=UiLanguage.T("Sol tık: seçilen piksel  •  Sağ tık: sil\nMavi katman, ses/ChatMix ile hareket eder."),
            ForeColor=Color.FromArgb(151,169,187),
            Left=24,Top=703,Width=452,Height=42});
        var cancel=Button(UiLanguage.T("Vazgeç"),500,794,100);
        cancel.DialogResult=DialogResult.Cancel;
        Controls.Add(cancel);
        var save=Button(UiLanguage.T("Kaydet"),610,794,108);
        save.BackColor=Color.FromArgb(72,194,238);
        save.ForeColor=Color.FromArgb(13,18,26);
        save.Click+=(_,_)=>
        {
            string next=new(pixels);
            string factory=compact?MiniWheelArtwork.Factory:WheelArtwork.Factory;
            Artwork=artworkEdited ? next==factory?"":next :
                initialArtwork;
            AutoLabel=automaticLabel.Checked;
            FollowModeIcons=followIcons.Checked;
            GameDX=(int)gameDX.Value;GameDY=(int)gameDY.Value;
            ChatDX=(int)chatDX.Value;ChatDY=(int)chatDY.Value;
            ShowGameLogo=showGame.Checked;ShowChatLogo=showChat.Checked;
            CloseRingEnds=closeArcEnds.Checked;
            RingThickness=(int)ringThickness.Value;
            CompactSize=(int)compactSize.Value;
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

    internal void SelectPreviewMode(DialView view)
        => mode.SelectedIndex=view==DialView.ChatMix?1:0;

    private static Button Button(string text,int x,int y,int width)=>new DarkButton{
        Text=text,Left=x,Top=y,Width=width,Height=36,
        BackColor=Color.FromArgb(27,38,51),
        ForeColor=Color.FromArgb(235,242,248),Cursor=Cursors.Hand};

    private void UpdateSample()
    {
        previewLabel.Text=UiLanguage.T("Önizleme")+$": {sample.Value}%";
        canvas.Invalidate();overview.Invalidate();
    }

    private void PushHistory()
    {
        undoPixels.Push(new string(pixels));
        if(undoPixels.Count>120)
        {
            var recent=undoPixels.Take(120).Reverse().ToArray();
            undoPixels.Clear();
            foreach(var state in recent)undoPixels.Push(state);
        }
        redoPixels.Clear();
        UpdateUndoButtons();
    }
    private void UndoPixels()
    {
        if(undoPixels.Count==0)return;
        redoPixels.Push(new string(pixels));
        undoPixels.Pop().CopyTo(0,pixels,0,pixels.Length);
        artworkEdited=true;
        strokeActive=false;
        UpdateUndoButtons();canvas.Invalidate();overview.Invalidate();
    }
    private void RedoPixels()
    {
        if(redoPixels.Count==0)return;
        undoPixels.Push(new string(pixels));
        redoPixels.Pop().CopyTo(0,pixels,0,pixels.Length);
        artworkEdited=true;
        strokeActive=false;
        UpdateUndoButtons();canvas.Invalidate();overview.Invalidate();
    }
    private void UpdateUndoButtons()
    {
        undoButton.Enabled=undoPixels.Count>0;
        redoButton.Enabled=redoPixels.Count>0;
    }

    internal bool TestHistoryRoundTrip()
    {
        // Execute the same editor history callbacks as the visible buttons.
        int index=gridWidth*(gridHeight/2)+gridWidth/2;
        char initial=pixels[index];
        char edited=initial=='1'?'0':'1';
        PushHistory();
        pixels[index]=edited;artworkEdited=true;
        UndoPixels();
        if(pixels[index]!=initial || !redoButton.Enabled)return false;
        RedoPixels();
        if(pixels[index]!=edited || !undoButton.Enabled)return false;
        UndoPixels();
        return pixels[index]==initial;
    }

    internal (int Width,int Height) EditorGridSize=>
        (gridWidth,gridHeight);

    private void SelectIconAt(MouseEventArgs e)
    {
        int x=Math.Clamp((int)Math.Round(e.X*128d/overview.ClientSize.Width),0,127);
        int y=Math.Clamp((int)Math.Round(e.Y*64d/overview.ClientSize.Height),10,63);
        int point=y*128+x;
        bool[] Ring(bool game,bool chat)
        {
            var px=new bool[8192];var mask=new bool[8192];
            if(compact)
                MiniWheelArtwork.Draw(px,mask,9,15,sample.Value,DialView.ChatMix,
                    GapAngle,RingThickness,game,chat,GameDX,GameDY,ChatDX,ChatDY,
                    CompactSize,artworkEdited?new string(pixels):initialArtwork,
                    automaticLabel.Checked,CloseRingEnds);
            else
                WheelArtwork.Draw(px,mask,1,10,
                    artworkEdited?new string(pixels):initialArtwork,
                    sample.Value,DialView.ChatMix,automaticLabel.Checked,GapAngle,
                    null,false,true,GameDX,GameDY,ChatDX,ChatDY,game,chat,
                    CloseRingEnds,RingThickness);
            return px;
        }
        var baseFrame=Ring(false,false);
        var gameFrame=Ring(true,false);
        var chatFrame=Ring(false,true);
        // Clicking a visible sprite picks the correct channel automatically.
        // Otherwise retain the user's explicit Game/Chat selector.
        if(gameFrame[point]&&!baseFrame[point])
        {
            dragTarget.SelectedIndex=0;return;
        }
        if(chatFrame[point]&&!baseFrame[point])
        {
            dragTarget.SelectedIndex=1;return;
        }
        // The icon is often only 7-11 OLED pixels: make grabbing intuitive
        // by accepting nearby clicks within its own wheel sector.
        double cx=compact?9+MiniWheelArtwork.CenterX:1+WheelArtwork.CenterX;
        double cy=compact?15+MiniWheelArtwork.CenterY:10+WheelArtwork.CenterY;
        double radius=Math.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy));
        if(radius<11||radius>40)return;
        double az=Math.Atan2(y-cy,x-cx)*180d/Math.PI;
        double arc=(az-WheelArtwork.StartAngle-GapAngle+7200d)%360d;
        if(arc<=WheelArtwork.Sweep)
            dragTarget.SelectedIndex=arc>=WheelArtwork.Sweep/2d?0:1;
    }

    private void MoveIconOnOverview(MouseEventArgs e)
    {
        int px=Math.Clamp((int)Math.Round(e.X*128d/overview.ClientSize.Width),0,127);
        int py=Math.Clamp((int)Math.Round(e.Y*64d/overview.ClientSize.Height),10,63);
        MoveIconToOled(px,py);
    }

    private void MoveIconToOled(int px,int py)
    {
        bool game=dragTarget.SelectedIndex==0;
        var sprite=IconLibrary.GetCurrent(game?"Game":"Chat");
        double angle=(WheelArtwork.StartAngle+GapAngle+
            WheelArtwork.Sweep*(game ? 0.75d : 0.25d))*Math.PI/180d;
        double size=compact?CompactSize/100d:1d;
        double radius=compact?18*size:24d;
        double baseX=(compact?9+MiniWheelArtwork.CenterX:
            1+WheelArtwork.CenterX)+radius*Math.Cos(angle)-sprite[0].Length/2d;
        double baseY=(compact?15+MiniWheelArtwork.CenterY:
            10+WheelArtwork.CenterY)+radius*Math.Sin(angle)-sprite.Length/2d;
        int dx=Math.Clamp((int)Math.Round(px-sprite[0].Length/2d-baseX),-64,64);
        int dy=Math.Clamp((int)Math.Round(py-sprite.Length/2d-baseY),-64,64);
        if(game){gameDX.Value=dx;gameDY.Value=dy;}
        else{chatDX.Value=dx;chatDY.Value=dy;}
        // The nearest valid position stays in the appropriate rotating arc.
        canvas.Invalidate();overview.Invalidate();
    }

    private void PaintPoint(object? sender,MouseEventArgs e)
    {
        if(e.Button is not (MouseButtons.Left or MouseButtons.Right))return;
        int x=e.X/pixelZoom,y=e.Y/pixelZoom;
        if(x<0||y<0||x>=gridWidth||y>=gridHeight)return;
        if(brush.SelectedIndex==4)
        {
            if(e.Button==MouseButtons.Left && mode.SelectedIndex==1)
                MoveIconToOled((compact?9:1)+x,(compact?15:10)+y);
            return;
        }
        (x,y)=compact?MiniWheelArtwork.SourceAt(x,y,GapAngle):
            WheelArtwork.SourceAt(x,y,GapAngle);
        if(x<0||y<0||x>=gridWidth||y>=gridHeight)return;
        char chosen=e.Button==MouseButtons.Right?'0':brush.SelectedIndex switch{
            0=>'1',1=>'2',2=>'3',_=>'0'};
        int index=y*gridWidth+x;
        if(pixels[index]==chosen)return;
        if(!strokeActive){PushHistory();strokeActive=true;}
        pixels[index]=chosen;
        artworkEdited=true;
        canvas.Invalidate();overview.Invalidate();
    }

    private void PaintOverview(object? sender,PaintEventArgs e)
    {
        using var bitmap=new Bitmap(128,64);
        var pixelsBuffer=new bool[128*64];
        var coverageBuffer=new bool[128*64];
        if(compact)
            MiniWheelArtwork.Draw(pixelsBuffer,coverageBuffer,9,15,sample.Value,
                mode.SelectedIndex==1?DialView.ChatMix:DialView.Volume,
                GapAngle,RingThickness,ShowGameLogo,ShowChatLogo,
                GameDX,GameDY,ChatDX,ChatDY,CompactSize,
                artworkEdited?new string(pixels):initialArtwork,
                automaticLabel.Checked,CloseRingEnds);
        else WheelArtwork.Draw(pixelsBuffer,coverageBuffer,1,10,
            artworkEdited?new string(pixels):initialArtwork,sample.Value,
            mode.SelectedIndex==1?DialView.ChatMix:DialView.Volume,
            automaticLabel.Checked,GapAngle,null,false,
            FollowModeIcons,GameDX,GameDY,ChatDX,ChatDY,
            ShowGameLogo,ShowChatLogo,CloseRingEnds,RingThickness);
        for(int yy=0;yy<64;yy++)for(int xx=0;xx<128;xx++)
            bitmap.SetPixel(xx,yy,pixelsBuffer[yy*128+xx]?Color.White:Color.Black);
        e.Graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
        e.Graphics.DrawImage(bitmap,new Rectangle(0,0,overview.Width,overview.Height),
            new Rectangle(0,0,128,64),GraphicsUnit.Pixel);
    }

    private void PaintCanvas(object? sender,PaintEventArgs e)
    {
        var bitmap=new bool[128*64];
        var coverage=new bool[128*64];
        string editSource=artworkEdited?new string(pixels):initialArtwork;
        string source=compact
            ?MiniWheelArtwork.PreviewSource(editSource,RingThickness)
            :WheelArtwork.PreviewSource(editSource,RingThickness);
        DialView active=mode.SelectedIndex==1?DialView.ChatMix:DialView.Volume;
        if(compact)
            MiniWheelArtwork.Draw(bitmap,coverage,0,0,sample.Value,active,
                GapAngle,RingThickness,false,false,0,0,0,0,
                100,editSource,automaticLabel.Checked,CloseRingEnds);
        else
            WheelArtwork.Draw(bitmap,coverage,0,0,editSource,sample.Value,
                active,automaticLabel.Checked,GapAngle,null,false,
                FollowModeIcons,GameDX,GameDY,ChatDX,ChatDY,
                ShowGameLogo,ShowChatLogo,CloseRingEnds,RingThickness);
        e.Graphics.Clear(Color.FromArgb(6,10,15));
        for(int y=0;y<gridHeight;y++)for(int x=0;x<gridWidth;x++)
        {
            char px=compact?MiniWheelArtwork.Sample(source,x,y,GapAngle):
                WheelArtwork.Sample(source,x,y,GapAngle);
            var fill=bitmap[y*128+x]?Color.FromArgb(235,242,248):
                px=='2'?Color.FromArgb(30,75,100):
                px=='3'?Color.FromArgb(30,26,31):Color.Black;
            using var brushFill=new SolidBrush(fill);
            e.Graphics.FillRectangle(brushFill,x*pixelZoom,y*pixelZoom,
                pixelZoom,pixelZoom);
        }
        using var grid=new Pen(Color.FromArgb(25,47,63),1f);
        for(int x=0;x<=gridWidth;x++)
            e.Graphics.DrawLine(grid,x*pixelZoom,0,x*pixelZoom,gridHeight*pixelZoom);
        for(int y=0;y<=gridHeight;y++)
            e.Graphics.DrawLine(grid,0,y*pixelZoom,gridWidth*pixelZoom,y*pixelZoom);
    }
}

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Mini radial display settings and live 128x64 preview. The original 45x42
/// user wheel sprite is not touched by the compact appearance editor.
/// </summary>
internal sealed class MiniWheelSettingsDialog : Form
{
    private readonly PictureBox preview=new(){
        Left=22,Top=56,Width=460,Height=230,
        BackColor=Color.Black,BorderStyle=BorderStyle.FixedSingle,
        Cursor=Cursors.SizeAll
    };
    private readonly ComboBox dragTarget=new(){
        Left=330,Top=480,Width=151,DropDownStyle=ComboBoxStyle.DropDownList};
    private bool draggingIcon;
    private readonly DarkNumberBox angle=new(){
        Minimum=-180,Maximum=180,Width=80
    };
    private readonly DarkNumberBox thickness=new(){
        Minimum=1,Maximum=5,Width=80
    };
    private readonly DarkNumberBox gameX=new(){
        Minimum=-45,Maximum=45,Width=70
    };
    private readonly DarkNumberBox gameY=new(){
        Minimum=-45,Maximum=45,Width=70
    };
    private readonly DarkNumberBox chatX=new(){
        Minimum=-45,Maximum=45,Width=70
    };
    private readonly DarkNumberBox chatY=new(){
        Minimum=-45,Maximum=45,Width=70
    };
    private readonly DarkCheckBox showGame=new(){
        Width=182,Height=25,Text="Game simgesini göster"
    };
    private readonly DarkCheckBox showChat=new(){
        Width=182,Height=25,Text="Chat simgesini göster"
    };
    private readonly ComboBox view=new(){
        Width=180,Height=30,DropDownStyle=ComboBoxStyle.DropDownList
    };
    public int GapAngle=0,Thickness=3,GameX,GameY,ChatX,ChatY;
    public bool ShowGame=true,ShowChat=true;

    public MiniWheelSettingsDialog(DockWidget source)
    {
        GapAngle=source.WheelGapAngle;
        Thickness=Math.Clamp(source.WheelRingThickness,1,5);
        GameX=source.WheelGameOffsetX;GameY=source.WheelGameOffsetY;
        ChatX=source.WheelChatOffsetX;ChatY=source.WheelChatOffsetY;
        ShowGame=source.WheelShowGame;ShowChat=source.WheelShowChat;
        Text=UiLanguage.T("Mini dairesel tekerlek");
        ClientSize=new Size(520,583);
        FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false;MinimizeBox=false;
        StartPosition=FormStartPosition.CenterParent;
        Font=new Font("Segoe UI",9.5f);
        ForeColor=Color.FromArgb(230,242,249);
        BackColor=Color.FromArgb(17,24,34);

        Controls.Add(new Label{Left=22,Top=13,Width=470,Height=32,
            Text=UiLanguage.T("Mini dairesel tekerlek"),
            Font=new Font("Segoe UI Semibold",16f,FontStyle.Bold)});
        Controls.Add(preview);
        preview.MouseDown+=(_,e)=>{
            if(e.Button!=MouseButtons.Left || view.SelectedIndex!=1)return;
            draggingIcon=true;MoveIconOnPreview(e);
        };
        preview.MouseMove+=(_,e)=>{
            if(draggingIcon && e.Button==MouseButtons.Left)MoveIconOnPreview(e);
        };
        preview.MouseUp+=(_,_)=>draggingIcon=false;
        preview.MouseLeave+=(_,_)=>draggingIcon=false;
        preview.Paint+=(_,e)=>
        {
            var px=new bool[128*64];var mask=new bool[128*64];
            MiniWheelArtwork.Draw(px,mask,27,15,70,
                view.SelectedIndex==1?DialView.ChatMix:DialView.Volume,
                (int)angle.Value,(int)thickness.Value,
                showGame.Checked,showChat.Checked,
                (int)gameX.Value,(int)gameY.Value,
                (int)chatX.Value,(int)chatY.Value);
            using var bitmap=new Bitmap(128,64);
            for(int y=0;y<64;y++)for(int x=0;x<128;x++)
                bitmap.SetPixel(x,y,px[y*128+x]?Color.White:Color.Black);
            e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode=PixelOffsetMode.Half;
            e.Graphics.DrawImage(bitmap,
                new Rectangle(0,0,preview.Width,preview.Height),
                new Rectangle(0,0,128,64),GraphicsUnit.Pixel);
        };
        void AddNumber(string label,DarkNumberBox control,int x,int y,int value)
        {
            Controls.Add(new Label{Text=UiLanguage.T(label),Left=x,Top=y,
                Width=130,Height=23});
            control.Left=x;control.Top=y+25;control.Value=
                Math.Clamp(value,(int)control.Minimum,(int)control.Maximum);
            control.ValueChanged+=(_,_)=>preview.Invalidate();
            Controls.Add(control);
        }
        view.Items.AddRange([UiLanguage.T("Ses"),UiLanguage.T("ChatMix")]);
        view.Left=22;view.Top=306;
        view.SelectedIndexChanged+=(_,_)=>preview.Invalidate();
        view.SelectedIndex=1;
        Controls.Add(view);
        AddNumber("Açı",angle,227,300,GapAngle);
        AddNumber("Kalınlık",thickness,370,300,Thickness);
        AddNumber("Game X",gameX,22,363,GameX);
        AddNumber("Game Y",gameY,137,363,GameY);
        AddNumber("Chat X",chatX,252,363,ChatX);
        AddNumber("Chat Y",chatY,367,363,ChatY);
        showGame.Left=22;showGame.Top=440;showGame.Checked=ShowGame;
        showChat.Left=245;showChat.Top=440;showChat.Checked=ShowChat;
        showGame.CheckedChanged+=(_,_)=>preview.Invalidate();
        showChat.CheckedChanged+=(_,_)=>preview.Invalidate();
        Controls.Add(showGame);Controls.Add(showChat);
        Controls.Add(new Label{
            Text=UiLanguage.T("Önizlemede sürüklemek için logo seç:"),
            Left=22,Top=480,Width=300,Height=24});
        dragTarget.Items.AddRange(["Game","Chat"]);
        dragTarget.SelectedIndex=0;Controls.Add(dragTarget);
        var cancel=new DarkButton{Left=265,Top=511,Width=96,Height=37,
            Text=UiLanguage.T("Vazgeç")};
        cancel.Click+=(_,_)=>Close();
        var save=new DarkButton{Left=376,Top=511,Width=105,Height=37,
            Text=UiLanguage.T("Kaydet")};
        save.Click+=(_,_)=>
        {
            GapAngle=(int)angle.Value;Thickness=(int)thickness.Value;
            GameX=(int)gameX.Value;GameY=(int)gameY.Value;
            ChatX=(int)chatX.Value;ChatY=(int)chatY.Value;
            ShowGame=showGame.Checked;ShowChat=showChat.Checked;
            DialogResult=DialogResult.OK;
            Close();
        };
        Controls.Add(cancel);Controls.Add(save);
        AcceptButton=save;CancelButton=cancel;
        UiSkin.ApplyDark(this);
    }

    private void MoveIconOnPreview(MouseEventArgs e)
    {
        bool game=dragTarget.SelectedIndex==0;
        int px=Math.Clamp((int)Math.Round(e.X*128d/preview.ClientSize.Width),0,127);
        int py=Math.Clamp((int)Math.Round(e.Y*64d/preview.ClientSize.Height),10,63);
        var sprite=IconLibrary.GetCurrent(game?"Game":"Chat");
        double theta=(WheelArtwork.StartAngle+(int)angle.Value+
            WheelArtwork.Sweep*(game ? 0.75d : 0.25d))*Math.PI/180d;
        double baseX=27+MiniWheelArtwork.CenterX+18*Math.Cos(theta)-
            sprite[0].Length/2d;
        double baseY=15+MiniWheelArtwork.CenterY+18*Math.Sin(theta)-
            sprite.Length/2d;
        int dx=Math.Clamp((int)Math.Round(px-sprite[0].Length/2d-baseX),-45,45);
        int dy=Math.Clamp((int)Math.Round(py-sprite.Length/2d-baseY),-45,45);
        if(game){gameX.Value=dx;gameY.Value=dy;}
        else{chatX.Value=dx;chatY.Value=dy;}
        preview.Invalidate();
    }
}

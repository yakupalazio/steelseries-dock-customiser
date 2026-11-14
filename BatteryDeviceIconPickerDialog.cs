using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// An opt-in local icon override for a connected device: built-in
/// monochrome icons, user-imported image, or reset to automatic.
/// Neither the source image nor hardware IDs are transmitted anywhere.
/// </summary>
internal sealed class BatteryDeviceIconPickerDialog:Form
{
    private readonly string? kind;
    private readonly string deviceName;
    private readonly ComboBox choices=new();
    private readonly Panel preview=new();
    private readonly Label note=new();

    internal BatteryDeviceIconPickerDialog(
        string? batteryKind,string friendlyName,string initialIcon)
    {
        kind=batteryKind;
        deviceName=friendlyName;
        Text=UiLanguage.English?"Customize device icon":"Cihaz ikonunu özelleştir";
        ClientSize=new Size(475,367);
        MinimumSize=Size;
        MaximumSize=Size;
        FormBorderStyle=FormBorderStyle.FixedDialog;
        StartPosition=FormStartPosition.CenterParent;
        MaximizeBox=false;MinimizeBox=false;
        BackColor=Color.FromArgb(17,24,34);
        ForeColor=Color.FromArgb(228,240,248);
        Font=new Font("Segoe UI",10f);

        var title=new Label{
            Text=UiLanguage.English?"OLED battery icon":"OLED pil ikonu",
            Font=new Font("Segoe UI Semibold",17f,FontStyle.Bold),
            ForeColor=ForeColor,Left=22,Top=18,Width=425,Height=39};
        var subtitle=new Label{
            Text=friendlyName,Left=22,Top=65,Width=422,Height=28,
            ForeColor=Color.FromArgb(157,182,199),AutoEllipsis=true};
        var label=new Label{
            Text=UiLanguage.English?"Select an icon":"İkon seç",
            Left=22,Top=105,Width=390,Height=21};
        choices.SetBounds(22,133,280,34);
        choices.DropDownStyle=ComboBoxStyle.DropDownList;
        choices.BackColor=Color.FromArgb(33,45,61);
        choices.ForeColor=ForeColor;
        choices.Items.AddRange(BatteryDeviceIconOverrides.Choices.Cast<object>().ToArray());
        choices.SelectedItem=BatteryDeviceIconOverrides.Choices.Contains(initialIcon)
            ?initialIcon:"Mouse";
        preview.SetBounds(321,130,125,75);
        preview.BackColor=Color.Black;
        preview.Paint+=(_,e)=>{
            e.Graphics.Clear(Color.Black);
            string key=choices.SelectedItem?.ToString()??"Mouse";
            var rows=IconLibrary.GetCurrent(key);
            if(rows.Length==0)return;
            int h=rows.Length,w=rows[0].Length;
            int scale=Math.Max(1,Math.Min(5,Math.Min(
                (preview.Width-8)/w,(preview.Height-8)/h)));
            int x=(preview.Width-scale*w)/2;
            int y=(preview.Height-scale*h)/2;
            using var white=new SolidBrush(Color.White);
            for(int yy=0;yy<h;yy++)for(int xx=0;xx<w;xx++)
                if(rows[yy][xx]=='1')
                    e.Graphics.FillRectangle(white,
                        x+xx*scale,y+yy*scale,scale,scale);
        };
        choices.SelectedIndexChanged+=(_,_)=>preview.Invalidate();
        var save=new DarkButton{
            Text=UiLanguage.English?"Save icon":"İkonu kaydet",
            Left=22,Top=193,Width=134,Height=39};
        save.Click+=(_,_)=>{
            if(choices.SelectedItem is not string icon)return;
            BatteryDeviceIconOverrides.Assign(kind,deviceName,icon);
            DialogResult=DialogResult.OK;Close();
        };
        var import=new DarkButton{
            Text=UiLanguage.English?"Import image…":"Resimden içe aktar…",
            Left=168,Top=193,Width=148,Height=39};
        import.Click+=(_,_)=>{
            using var dialog=new OpenFileDialog{
                Title=UiLanguage.English?"Choose icon image":"İkon resmi seç",
                Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                CheckFileExists=true};
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            try
            {
                BatteryDeviceIconOverrides.Import(kind,deviceName,
                    dialog.FileName);
                DialogResult=DialogResult.OK;Close();
            }
            catch(Exception ex)
            {
                MessageBox.Show(this,ex.Message,Text,
                    MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
        };
        var reset=new DarkButton{
            Text=UiLanguage.English?"Automatic":"Otomatik",
            Left=328,Top=193,Width=118,Height=39};
        reset.Click+=(_,_)=>{
            BatteryDeviceIconOverrides.Reset(kind,deviceName);
            DialogResult=DialogResult.OK;Close();
        };
        note.SetBounds(22,258,426,78);
        note.ForeColor=Color.FromArgb(149,173,190);
        note.Text=UiLanguage.English
            ?"An imported PNG/JPG is converted to 11×11 monochrome pixels. "+
             "Only the pixel pattern is stored locally, not your file path. "+
             "The icon is used for this device on the OLED."
            :"PNG/JPG resmi 11×11 siyah-beyaz piksele dönüştürülür. "+
             "Dosya yolu değil, yalnızca piksel deseni bu bilgisayarda "+
             "saklanır. İkon OLED'de bu cihaz için kullanılır.";
        Controls.AddRange([
            title,subtitle,label,choices,preview,save,import,reset,note]);
        UiPolish.Register(this);
    }
}

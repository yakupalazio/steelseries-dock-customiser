using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>Independent clock and date formatting/stroke-width settings.</summary>
internal sealed class ClockDateSettingsDialog : Form
{
    private readonly ComboBox clockFormat=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=173};
    private readonly ComboBox dateFormat=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=173};
    private readonly NumericUpDown clockWeight=new(){Minimum=1,Maximum=3,Width=68};
    private readonly NumericUpDown dateWeight=new(){Minimum=1,Maximum=3,Width=68};
    private readonly PictureBox preview=new(){Width=640,Height=320,BackColor=Color.Black,BorderStyle=BorderStyle.FixedSingle};
    private readonly LayoutState original;
    private readonly Action<string,int,string,int> save;
    private readonly string clockKind;
    public ClockDateSettingsDialog(LayoutState state,Action<string,int,string,int> onSave,
        string clockWidgetKind="Clock")
    {
        original=state.DeepClone();save=onSave;clockKind=clockWidgetKind;
        Text="Saat ve tarih — OLED kişiselleştirme";
        Font=new Font("Segoe UI",10f);
        StartPosition=FormStartPosition.CenterParent;
        ClientSize=new Size(705,570);
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
        var top=new Label{Text="Saat ve tarihi bağımsız olarak düzenle",
            Font=new Font("Segoe UI",13,FontStyle.Bold),
            Left=24,Top=15,Width=655,Height=31}; Controls.Add(top);
        var clk=original.Widgets.FirstOrDefault(w=>w.Kind==clockKind);
        var dt=original.Widgets.FirstOrDefault(w=>w.Kind=="Date");
        clockFormat.Items.AddRange(ClockDateFormats.Clock);
        dateFormat.Items.AddRange(ClockDateFormats.Date);
        clockFormat.SelectedItem=ClockDateFormats.ClockFormat(clk?.Format);
        dateFormat.SelectedItem=ClockDateFormats.DateFormat(dt?.Format);
        clockWeight.Value=Math.Clamp(clk?.StrokeWidth??1,1,3);
        dateWeight.Value=Math.Clamp(dt?.StrokeWidth??1,1,3);

        var clockGroup=new GroupBox{Left=23,Top=55,Width=320,Height=99,Text="Saat"};
        var dateGroup=new GroupBox{Left=361,Top=55,Width=320,Height=99,Text="Tarih"};
        clockGroup.Controls.Add(new Label{Left=13,Top=26,Text="Görünüm",AutoSize=true});
        clockFormat.Left=103;clockFormat.Top=21;clockGroup.Controls.Add(clockFormat);
        clockGroup.Controls.Add(new Label{Left=13,Top=65,Text="Piksel kalınlığı",AutoSize=true});
        clockWeight.Left=170;clockWeight.Top=58;clockGroup.Controls.Add(clockWeight);
        dateGroup.Controls.Add(new Label{Left=13,Top=26,Text="Görünüm",AutoSize=true});
        dateFormat.Left=103;dateFormat.Top=21;dateGroup.Controls.Add(dateFormat);
        dateGroup.Controls.Add(new Label{Left=13,Top=65,Text="Piksel kalınlığı",AutoSize=true});
        dateWeight.Left=170;dateWeight.Top=58;dateGroup.Controls.Add(dateWeight);
        Controls.Add(clockGroup);Controls.Add(dateGroup);
        Controls.Add(new Label{Left=24,Top=168,Width=655,Height=24,
            Text="OLED'in doğal 128×64 görüntüsü (5× büyütülmüş) — saat/tarih modu"});
        preview.Left=24;preview.Top=198;Controls.Add(preview);
        preview.Paint+=(sender,e)=>{
            var working=PreviewState();
            using var bitmap=OledRenderer.Render(working,false,true,false);
            e.Graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
            e.Graphics.DrawImage(bitmap,new Rectangle(0,0,640,320));
        };
        clockFormat.SelectedIndexChanged+=(_,_)=>preview.Invalidate();
        dateFormat.SelectedIndexChanged+=(_,_)=>preview.Invalidate();
        clockWeight.ValueChanged+=(_,_)=>preview.Invalidate();
        dateWeight.ValueChanged+=(_,_)=>preview.Invalidate();

        var hint=new Label{Left=24,Top=520,Width=440,Height=43,
            Text="Konum ve boyutu ana piksel editöründen değiştirebilirsin. Kalınlık 1, 2 veya 3 gerçek OLED pikselidir.",
            ForeColor=Color.DarkSlateGray};
        Controls.Add(hint);
        var cancel=new Button{Left=497,Top=528,Width=78,Text="İptal"};
        var okay=new Button{Left=583,Top=528,Width=96,Text="Uygula"};
        cancel.Click+=(_,_)=>Close();
        okay.Click+=(_,_)=>{
            save(clockFormat.Text,(int)clockWeight.Value,dateFormat.Text,(int)dateWeight.Value);
            Close();
        };
        Controls.Add(cancel);Controls.Add(okay);
    }

    private LayoutState PreviewState()
    {
        var clone=original.DeepClone();
        foreach(var w in clone.Widgets)
        {
            if(w.Kind==clockKind){w.Format=clockFormat.Text;w.StrokeWidth=(int)clockWeight.Value;}
            if(w.Kind=="Date"){w.Format=dateFormat.Text;w.StrokeWidth=(int)dateWeight.Value;}
        }
        return clone;
    }
}

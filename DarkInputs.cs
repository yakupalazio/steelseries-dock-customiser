using System;
using System.Drawing;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Small, theme-consistent numeric stepper. WinForms NumericUpDown delegates
/// its two white spinner buttons to the OS theme, so the native control can
/// never match the dark inspector reliably across Windows 11 variants.
/// </summary>
internal sealed class DarkNumberBox : UserControl
{
    private static readonly Color Surface=Color.FromArgb(27,38,51);
    private static readonly Color Inactive=Color.FromArgb(23,31,43);
    private static readonly Color Foreground=Color.FromArgb(235,242,248);
    private readonly TextBox edit=new()
    {
        BorderStyle=BorderStyle.None,TextAlign=HorizontalAlignment.Right,
        BackColor=Surface,ForeColor=Foreground,Font=new Font("Segoe UI",9.5f)
    };
    private readonly Button minus=new DarkButton(){Text="−",FlatStyle=FlatStyle.Flat,TabStop=false};
    private readonly Button plus=new DarkButton(){Text="+",FlatStyle=FlatStyle.Flat,TabStop=false};
    private decimal value,minimum,maximum=100;
    private bool updating;
    public event EventHandler? ValueChanged;

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public decimal Minimum
    {
        get=>minimum;
        set{minimum=value;Value=Math.Max(minimum,Math.Min(maximum,this.value));}
    }
    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public decimal Maximum
    {
        get=>maximum;
        set{maximum=value;Value=Math.Max(minimum,Math.Min(maximum,this.value));}
    }
    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public decimal Value
    {
        get=>value;
        set
        {
            decimal clamped=Math.Clamp(value,minimum,maximum);
            if(this.value==clamped && edit.Text==clamped.ToString())return;
            bool changed=this.value!=clamped;
            this.value=clamped;
            updating=true;
            edit.Text=clamped.ToString();
            updating=false;
            if(changed)ValueChanged?.Invoke(this,EventArgs.Empty);
        }
    }
    public DarkNumberBox()
    {
        BackColor=Surface;ForeColor=Foreground;
        Size=new Size(128,30);MinimumSize=new Size(75,30);
        foreach(var button in new[]{minus,plus})
        {
            button.FlatAppearance.BorderSize=0;
            button.BackColor=Color.FromArgb(37,52,68);
            button.ForeColor=Color.FromArgb(121,209,239);
            button.Font=new Font("Segoe UI Semibold",10f,FontStyle.Bold);
            button.Cursor=Cursors.Hand;
            Controls.Add(button);
        }
        edit.Text="0";
        Controls.Add(edit);
        minus.Click+=(_,_)=>Value--;
        plus.Click+=(_,_)=>Value++;
        edit.Leave+=(_,_)=>Commit();
        edit.KeyDown+=(_,e)=>
        {
            if(e.KeyCode==Keys.Enter){Commit();e.SuppressKeyPress=true;}
            if(e.KeyCode==Keys.Up){Value++;e.SuppressKeyPress=true;}
            if(e.KeyCode==Keys.Down){Value--;e.SuppressKeyPress=true;}
        };
        edit.TextChanged+=(_,_)=>{if(!updating)Invalidate();};
        EnabledChanged+=(_,_)=>
        {
            edit.BackColor=Enabled?Surface:Inactive;
            edit.ForeColor=Enabled?Foreground:Color.FromArgb(112,132,149);
            foreach(var b in new[]{plus,minus})b.Enabled=Enabled;
        };
        Resize+=(_,_)=>LayoutChildren();
        LayoutChildren();
    }
    private void Commit()
    {
        if(decimal.TryParse(edit.Text,out decimal parsed))Value=parsed;
        else edit.Text=value.ToString();
    }
    private void LayoutChildren()
    {
        int h=Math.Max(20,Height);
        plus.SetBounds(Width-26,1,25,h-2);
        minus.SetBounds(Width-52,1,25,h-2);
        edit.SetBounds(7,Math.Max(3,(h-edit.PreferredHeight)/2),
            Math.Max(12,Width-64),edit.PreferredHeight);
    }
}

internal sealed class DarkCheckBox : CheckBox
{
    private static readonly Color Accent=Color.FromArgb(72,194,238);
    public DarkCheckBox()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|
            ControlStyles.OptimizedDoubleBuffer,true);
        ForeColor=Color.FromArgb(235,242,248);
        CheckedChanged+=(_,_)=>Invalidate();
        EnabledChanged+=(_,_)=>Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        using var border=new Pen(Checked?Accent:Color.FromArgb(91,112,132));
        var square=new Rectangle(2,Math.Max(2,(Height-15)/2),14,14);
        using(var background=new SolidBrush(Checked?Color.FromArgb(38,68,82):
            Color.FromArgb(27,38,51)))e.Graphics.FillRectangle(background,square);
        e.Graphics.DrawRectangle(border,square);
        if(Checked)
        {
            using var pen=new Pen(Accent,2);
            e.Graphics.DrawLines(pen,[
                new Point(square.X+3,square.Y+7),
                new Point(square.X+6,square.Y+10),
                new Point(square.X+11,square.Y+4)]);
        }
        TextRenderer.DrawText(e.Graphics,Text,Font,
            new Rectangle(22,0,Math.Max(1,Width-24),Height),
            Enabled?ForeColor:Color.FromArgb(112,132,149),
            TextFormatFlags.Left|TextFormatFlags.VerticalCenter|
            TextFormatFlags.EndEllipsis);
    }
}

/// <summary>
/// Pixel-clean buttons without bright native Windows 11 chrome. We retain
/// normal Button semantics (focus, keyboard, click and accessibility).
/// </summary>
internal sealed class DarkButton : Button
{
    public DarkButton()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|
            ControlStyles.OptimizedDoubleBuffer,true);
        FlatStyle=FlatStyle.Flat;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var area=ClientRectangle;
        using(var background=new SolidBrush(BackColor))
            e.Graphics.FillRectangle(background,area);
        if(FlatAppearance.BorderSize>0)
        {
            using var border=new Pen(FlatAppearance.BorderColor);
            e.Graphics.DrawRectangle(border,0,0,Math.Max(0,Width-1),Math.Max(0,Height-1));
        }
        TextFormatFlags alignment=TextAlign switch
        {
            System.Drawing.ContentAlignment.MiddleLeft=>TextFormatFlags.Left,
            System.Drawing.ContentAlignment.MiddleRight=>TextFormatFlags.Right,
            _=>TextFormatFlags.HorizontalCenter
        };
        TextRenderer.DrawText(e.Graphics,Text,Font,
            new Rectangle(Padding.Left,0,
                Math.Max(1,Width-Padding.Left-Padding.Right),Height),
            Enabled?ForeColor:Color.FromArgb(89,110,129),
            TextFormatFlags.VerticalCenter|alignment|TextFormatFlags.EndEllipsis|
            TextFormatFlags.NoPrefix);
    }
}

internal static class UiSkin
{
    private static readonly Color Background=Color.FromArgb(13,18,26);
    private static readonly Color Surface=Color.FromArgb(23,31,43);
    private static readonly Color Text=Color.FromArgb(235,242,248);
    private static readonly Color Muted=Color.FromArgb(151,169,187);
    private static readonly Color Accent=Color.FromArgb(72,194,238);

    public static void ApplyDark(Control root)
    {
        if(root is Form or Panel or GroupBox)
        {
            root.BackColor=Background;
            root.ForeColor=Text;
        }
        switch(root)
        {
            case Label label:
                label.BackColor=Background;
                label.ForeColor=label.Font.Size>=12?Text:Muted;
                break;
            case Button button:
                button.UseVisualStyleBackColor=false;
                button.FlatStyle=FlatStyle.Flat;
                button.FlatAppearance.BorderSize=0;
                bool important=button.Text is "Kaydet" or "Uygula";
                button.BackColor=important?Accent:Surface;
                button.ForeColor=important?Background:Text;
                button.Cursor=Cursors.Hand;
                break;
            case ComboBox combo:
                combo.BackColor=Surface;
                combo.ForeColor=Text;
                combo.FlatStyle=FlatStyle.Flat;
                break;
            case TextBox edit:
                edit.BackColor=Surface;
                edit.ForeColor=Text;
                edit.BorderStyle=BorderStyle.FixedSingle;
                break;
        }
        foreach(Control child in root.Controls)ApplyDark(child);
    }
}

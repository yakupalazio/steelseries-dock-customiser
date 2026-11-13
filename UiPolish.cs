using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Consistent lightweight polish for native WinForms menu items and inputs:
/// clipped rounded corners, dark owner-drawn combo options, soft focus/hover
/// and one global inertia scroll filter for scrollable settings panels.
/// Never alters editor coordinates, selected values or dock frame timing.
/// </summary>
internal static class UiPolish
{
    private static readonly ConditionalWeakTable<Control,object> Styled=new();
    private static readonly MouseWheelInertia Wheel=new();
    private static bool filterInstalled;
    private static readonly Color Surface=Color.FromArgb(27,38,51);
    private static readonly Color Text=Color.FromArgb(231,240,248);
    private static readonly Color Selected=Color.FromArgb(38,68,82);
    private static readonly Color Accent=Color.FromArgb(109,211,241);

    internal static void Register(Form form)
    {
        form.Shown+=(_,_)=>Apply(form);
        // Other forms may embed this Form in Settings instead of showing
        // standalone; handle that, too, on first parent attachment.
        form.ParentChanged+=(_,_)=>
        {
            if(form.Parent is not null && form.IsHandleCreated)Apply(form);
        };
    }

    internal static void Apply(Form form)
    {
        if(form.IsDisposed)return;
        if(!filterInstalled)
        {
            Application.AddMessageFilter(Wheel);
            filterInstalled=true;
        }
        Visit(form);
    }

    private static void Visit(Control control)
    {
        if(control.IsDisposed)return;
        if(Styled.TryGetValue(control,out _))return;
        Styled.Add(control,new object());
        try
        {
            if(control is ScrollableControl scroll && scroll.AutoScroll)
                Wheel.Register(scroll);
            if(control is ComboBox combo)
            {
                combo.FlatStyle=FlatStyle.Flat;
                if(combo.DropDownStyle==ComboBoxStyle.DropDownList &&
                   combo.DrawMode==DrawMode.Normal)
                {
                    combo.DrawMode=DrawMode.OwnerDrawFixed;
                    combo.ItemHeight=Math.Max(22,combo.ItemHeight);
                    combo.DrawItem+=(_,e)=>DrawOption(combo,e);
                }
            }
            if(control is Button button && button is not DarkButton)
            {
                button.UseVisualStyleBackColor=false;
                button.FlatStyle=FlatStyle.Flat;
                button.FlatAppearance.BorderSize=0;
                new NativeButtonEase(button);
            }
            // Controls drawn by the OLED photo/pixel canvas are excluded.
            if(control is Button or ComboBox or TextBox or NumericUpDown
                or ListBox or DarkNumberBox ||
                (control is Panel && control is not FlowLayoutPanel
                  && control is not TableLayoutPanel
                  && control.Width>=85 && control.Height>=35
                  && control.BackColor!=control.Parent?.BackColor))
            {
                int corner=control is Button?9:control is Panel?9:7;
                void UpdateCorner()
                {
                    if(control.IsDisposed || control.Width<6||control.Height<6)
                        return;
                    using var path=Rounded(new Rectangle(0,0,
                        control.Width,control.Height),corner);
                    control.Region?.Dispose();
                    control.Region=new Region(path);
                }
                control.SizeChanged+=(_,_)=>UpdateCorner();
                UpdateCorner();
            }
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException)
        {
            // A native Windows control that declines owner drawing keeps
            // its original behavior; UI polish is never a startup failure.
        }
        control.ControlAdded+=(_,args)=>{if(args.Control!=null)Visit(args.Control);};
        foreach(Control child in control.Controls)Visit(child);
    }

    internal static bool RouteWheelForTest(Control hovered,int wheelDelta)=>
        Wheel.Route(hovered,wheelDelta);

    internal static ToolStripRenderer MakeMenuRenderer(ProfessionalColorTable palette)=>
        new RoundedMenuRenderer(palette);

    internal static void StyleMenu(ContextMenuStrip menu)
    {
        menu.Renderer=new RoundedMenuRenderer(new SoftMenuPalette());
        menu.BackColor=Color.FromArgb(20,27,37);
        menu.ForeColor=Text;
    }

    private sealed class SoftMenuPalette:ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground=>
            Color.FromArgb(20,27,37);
        public override Color MenuBorder=>Color.FromArgb(64,91,108);
        public override Color MenuItemSelected=>Color.FromArgb(36,64,83);
        public override Color MenuItemSelectedGradientBegin=>MenuItemSelected;
        public override Color MenuItemSelectedGradientEnd=>MenuItemSelected;
    }

    private sealed class RoundedMenuRenderer:ToolStripProfessionalRenderer
    {
        internal RoundedMenuRenderer(ProfessionalColorTable colors):base(colors){}
        protected override void OnRenderMenuItemBackground(
            ToolStripItemRenderEventArgs e)
        {
            var item=e.Item;
            var area=new Rectangle(0,0,item.Width,item.Height);
            using(var bg=new SolidBrush(item.Owner?.BackColor??
                Color.FromArgb(20,27,37)))
                e.Graphics.FillRectangle(bg,area);
            if(!item.Selected||!item.Enabled)return;
            var inset=new Rectangle(3,2,
                Math.Max(3,item.Width-6),Math.Max(3,item.Height-4));
            using var shape=Rounded(inset,7);
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using var fill=new SolidBrush(Color.FromArgb(37,68,88));
            e.Graphics.FillPath(fill,shape);
        }
    }

    private static void DrawOption(ComboBox combo,DrawItemEventArgs e)
    {
        if(e.Index<0||e.Index>=combo.Items.Count)return;
        bool selected=(e.State&DrawItemState.Selected)!=0;
        using var bg=new SolidBrush(selected?Selected:Surface);
        e.Graphics.FillRectangle(bg,e.Bounds);
        string title=combo.GetItemText(combo.Items[e.Index])??"";
        TextRenderer.DrawText(e.Graphics,title,combo.Font,
            new Rectangle(e.Bounds.X+9,e.Bounds.Y,
                Math.Max(1,e.Bounds.Width-13),e.Bounds.Height),
            selected?Accent:Text,TextFormatFlags.VerticalCenter|
            TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
    }

    private static GraphicsPath Rounded(Rectangle rect,int radius)
    {
        float width=Math.Max(2,rect.Width-1),height=Math.Max(2,rect.Height-1);
        int dia=Math.Max(2,Math.Min(radius*2,
            Math.Min(rect.Width-2,rect.Height-2)));
        var p=new GraphicsPath();
        p.AddArc(rect.X,rect.Y,dia,dia,180,90);
        p.AddArc(rect.X+width-dia,rect.Y,dia,dia,270,90);
        p.AddArc(rect.X+width-dia,rect.Y+height-dia,dia,dia,0,90);
        p.AddArc(rect.X,rect.Y+height-dia,dia,dia,90,90);
        p.CloseFigure();
        return p;
    }

    private sealed class NativeButtonEase
    {
        private readonly Button b;
        private readonly System.Windows.Forms.Timer timer=new(){Interval=16};
        private Color baseColor;
        private bool hover,down,changing;
        private float value;
        internal NativeButtonEase(Button b)
        {
            this.b=b;baseColor=b.BackColor;
            b.MouseEnter+=(_,_)=>{hover=true;timer.Start();};
            b.MouseLeave+=(_,_)=>{hover=false;down=false;timer.Start();};
            b.MouseDown+=(_,e)=>
            {if(e.Button==MouseButtons.Left){down=true;timer.Start();}};
            b.MouseUp+=(_,_)=>{down=false;timer.Start();};
            b.BackColorChanged+=(_,_)=>
            {if(!changing)baseColor=b.BackColor;};
            b.Disposed+=(_,_)=>{timer.Stop();timer.Dispose();};
            timer.Tick+=(_,_)=>
            {
                if(b.IsDisposed){timer.Stop();return;}
                float goal=b.Enabled&&hover?(down?.35f:1f):0;
                value+=(goal-value)*.30f;
                Color tinted=Mix(baseColor,Color.FromArgb(105,187,215),
                    value*.16f);
                changing=true;
                try{b.BackColor=tinted;}finally{changing=false;}
                if(Math.Abs(value-goal)<.02f)
                {
                    value=goal;timer.Stop();
                    changing=true;try{b.BackColor=Mix(baseColor,
                        Color.FromArgb(105,187,215),goal*.16f);}
                    finally{changing=false;}
                }
            };
        }
        private static Color Mix(Color a,Color b,float t)=>
            Color.FromArgb(
                (int)(a.R+(b.R-a.R)*t),
                (int)(a.G+(b.G-a.G)*t),
                (int)(a.B+(b.B-a.B)*t));
    }

    /// <summary>Scroll steps become a restrained ~160ms eased glide.</summary>
    private sealed class MouseWheelInertia:IMessageFilter
    {
        private readonly HashSet<ScrollableControl> targets=[];
        private readonly Dictionary<ScrollableControl,ScrollMotion> sessions=[];
        internal void Register(ScrollableControl panel)
        {
            targets.Add(panel);
            panel.Disposed+=(_,_)=>
            {
                targets.Remove(panel);
                if(sessions.Remove(panel,out var motion))motion.Dispose();
            };
        }
        public bool PreFilterMessage(ref Message m)
        {
            const int WM_MOUSEWHEEL=0x020A;
            if(m.Msg!=WM_MOUSEWHEEL)return false;
            var pointer=Control.FromChildHandle(m.HWnd);
            if(pointer is null)return false;
            int delta=unchecked((short)((m.WParam.ToInt64()>>16)&0xffff));
            return Route(pointer,delta);
        }
        internal bool Route(Control pointer,int delta)
        {
            if(delta==0)return false;
            // Dropdowns need their own native wheel behavior while open.
            for(var ancestor=pointer;ancestor!=null;ancestor=ancestor.Parent)
                if(ancestor is ComboBox box && box.DroppedDown)
                    return false;
            ScrollableControl? panel=null;
            for(var ancestor=pointer;ancestor!=null;ancestor=ancestor.Parent)
                if(ancestor is ScrollableControl scroll &&
                   targets.Contains(scroll) && scroll.AutoScroll &&
                   scroll.VerticalScroll.Visible)
                {panel=scroll;break;}
            if(panel is null || panel.IsDisposed)return false;
            int max=Math.Max(0,panel.VerticalScroll.Maximum-
                panel.VerticalScroll.LargeChange+1);
            if(max<=0)return false;
            if(!sessions.TryGetValue(panel,out var session))
            {
                session=new ScrollMotion(panel);
                sessions[panel]=session;
            }
            session.Add(-delta/120.0*90,max);
            return true;
        }
    }

    private sealed class ScrollMotion:IDisposable
    {
        private readonly ScrollableControl panel;
        private readonly System.Windows.Forms.Timer timer=new(){Interval=16};
        private double target;
        private bool active;
        internal ScrollMotion(ScrollableControl scroll)
        {
            panel=scroll;
            timer.Tick+=(_,_)=>Update();
        }
        internal void Add(double delta,int max)
        {
            if(!active)
            {
                target=Math.Abs(panel.AutoScrollPosition.Y);
                active=true;
            }
            target=Math.Clamp(target+delta,0,max);
            timer.Start();
        }
        private void Update()
        {
            if(panel.IsDisposed){Dispose();return;}
            int current=Math.Abs(panel.AutoScrollPosition.Y);
            double distance=target-current;
            if(Math.Abs(distance)<=1.2)
            {
                panel.AutoScrollPosition=new Point(
                    Math.Abs(panel.AutoScrollPosition.X),
                    (int)Math.Round(target));
                active=false;timer.Stop();return;
            }
            int next=(int)Math.Round(current+distance*.3);
            if(next==current)next=current+Math.Sign(distance);
            panel.AutoScrollPosition=new Point(
                Math.Abs(panel.AutoScrollPosition.X),next);
        }
        public void Dispose(){timer.Stop();timer.Dispose();}
    }
}

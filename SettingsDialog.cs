using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>Settings / About window. The Easter egg is deliberately not advertised.</summary>
internal sealed class SettingsDialog : Form
{
    private bool captureMode;
    internal void EnableOffscreenCapture()
    {
        captureMode=true;
        StartPosition=FormStartPosition.Manual;
        Location=new Point(-5000,-5000);
        ShowInTaskbar=false;
    }
    protected override bool ShowWithoutActivation=>captureMode;
    private readonly KonamiSequence secret = new();
    private readonly Func<bool> otherOledWriterActive;
    private readonly Func<Task<bool>> suspendLive;
    private readonly Func<bool,Task> resumeLive;
    private readonly Action onLanguageChanged;
    private bool aboutActive;
    private BatteryStatusDialog? batteryEditor;

    public void UpdateBatteryRows()
    {
        if(batteryEditor is { IsDisposed:false })batteryEditor.UpdateRows();
    }
    private static readonly Color Background = Color.FromArgb(13, 18, 26);
    private static readonly Color Sidebar = Color.FromArgb(19, 26, 37);
    private static readonly Color Surface = Color.FromArgb(23, 31, 43);
    private static readonly Color Accent = Color.FromArgb(72, 194, 238);
    private static readonly Color Primary = Color.FromArgb(235, 242, 248);
    private static readonly Color Muted = Color.FromArgb(151, 169, 187);

    public SettingsDialog(Func<bool>? otherOledWriterActive = null,
        BatteryPreferences? batteryPreferences = null,
        Func<BatterySnapshot>? snapshotGetter = null,
        Func<Task>? refresh = null,
        Action? onApply = null,
        Func<Task<bool>>? suspendLive = null,
        Func<bool,Task>? resumeLive = null,
        Action? onLanguageChanged = null)
    {
        this.otherOledWriterActive = otherOledWriterActive ?? (() => false);
        this.suspendLive=suspendLive??(()=>Task.FromResult(false));
        this.resumeLive=resumeLive??(_=>Task.CompletedTask);
        this.onLanguageChanged=onLanguageChanged??(()=>{});
        Text = "Ayarlar | Dock Customiser";
        BackColor = Background;
        ForeColor = Primary;
        Font = new Font("Segoe UI", 9.5f);
        ClientSize = new Size(1130, 746);
        MinimumSize = Size;
        MaximumSize = Size;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        KeyPreview = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            BackColor = Background, Margin = Padding.Empty, Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var menu = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar };
        root.Controls.Add(menu, 0, 0);
        menu.Controls.Add(new Label
        {
            Text = "AYARLAR", Left = 22, Top = 25, Width = 154, Height = 24,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold), ForeColor = Muted
        });
        var aboutItem = new DarkButton
        {
            Text = "ⓘ   Hakkında", Left = 13, Top = 635, Width = 178, Height = 49,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0),
            FlatStyle = FlatStyle.Flat, BackColor = Surface,
            ForeColor = Muted, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold)
        };
        aboutItem.FlatAppearance.BorderSize = 0;
        menu.Controls.Add(aboutItem);
        var languageItem=new DarkButton
        {
            Text="◉   Dil / Language",Left=13,Top=79,Width=178,Height=49,
            TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,0,0,0),
            FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(38,68,82),
            ForeColor=Accent,Cursor=Cursors.Hand,
            Font=new Font("Segoe UI Semibold",10f,FontStyle.Bold)
        };
        languageItem.FlatAppearance.BorderSize=0;
        menu.Controls.Add(languageItem);
        menu.Controls.Add(new Label
        {
            Text = "OLED STUDIO   •   1.1.0", Left = 18, Top = 704,
            Width = 160, Height = 23, ForeColor = Muted,
            Font = new Font("Segoe UI", 8f)
        });

        var contentHost = new Panel { Dock = DockStyle.Fill, BackColor = Background };
        root.Controls.Add(contentHost, 1, 0);
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Background };
        contentHost.Controls.Add(content);
        content.Controls.Add(new Label
        {
            Text = "HAKKINDA", Left = 32, Top = 27, Width = 445, Height = 21,
            ForeColor = Accent, Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
        });
        content.Controls.Add(new Label
        {
            Text = "Dock Customiser", Left = 30, Top = 57, Width = 475, Height = 48,
            ForeColor = Primary, Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold)
        });
        content.Controls.Add(new Label
        {
            Text = "SteelSeries Nova Pro Wireless için kişisel OLED stüdyosu",
            Left = 32, Top = 114, Width = 479, Height = 25,
            ForeColor = Muted, Font = new Font("Segoe UI", 10f)
        });

        var info = new Panel
        {
            Left = 30, Top = 161, Width = 472, Height = 92,
            BackColor = Surface, Padding = new Padding(15)
        };
        content.Controls.Add(info);
        info.Controls.Add(new Label
        {
            Text = "SÜRÜM   1.1.0\nPLATFORM   Windows 11  ·  .NET 10\nEKRAN   128 × 64  ·  Monokrom OLED",
            Dock = DockStyle.Fill, ForeColor = Primary,
            Font = new Font("Segoe UI", 9.3f), TextAlign = ContentAlignment.MiddleLeft
        });

        content.Controls.Add(new Label
        {
            Text = "Tasarlayan ve geliştiren", Left = 33, Top = 279,
            Width = 395, Height = 22, ForeColor = Muted,
            Font = new Font("Segoe UI", 9f)
        });
        content.Controls.Add(new Label
        {
            Text = "Yakup Can Palaz", Left = 32, Top = 307,
            Width = 462, Height = 53, ForeColor = Accent,
            Font = new Font("Segoe Script", 22f, FontStyle.Italic)
        });
        content.Controls.Add(new Label
        {
            Text = "© 2026 Yakup Can Palaz. Bağımsız bir proje; SteelSeries ile bağlantılı değildir.",
            Left = 32, Top = 372, Width = 468, Height = 35,
            ForeColor = Muted, Font = new Font("Segoe UI", 8.4f)
        });

        var close = new DarkButton
        {
            Text = "Kapat", Width = 94, Height = 34,
            Left = 410, Top = 413, FlatStyle = FlatStyle.Flat,
            BackColor = Surface, ForeColor = Primary,
            Cursor = Cursors.Hand
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) => Close();
        content.Controls.Add(close);

        var languagePage=new Panel{Dock=DockStyle.Fill,BackColor=Background};
        contentHost.Controls.Add(languagePage);
        languagePage.Controls.Add(new Label
        {
            Text="DİL / LANGUAGE",Left=32,Top=28,Width=620,Height=25,
            ForeColor=Accent,Font=new Font("Segoe UI Semibold",10,FontStyle.Bold)
        });
        languagePage.Controls.Add(new Label
        {
            Text="Uygulama dili",Left=30,Top=66,Width=610,Height=47,
            ForeColor=Primary,Font=new Font("Segoe UI Semibold",24,FontStyle.Bold)
        });
        languagePage.Controls.Add(new Label
        {
            Text="Değişiklik anında uygulanır ve sonraki açılışlarda korunur.",
            Left=32,Top=130,Width=730,Height=36,ForeColor=Muted,
            Font=new Font("Segoe UI",10)
        });
        var languageSelector=new ComboBox
        {
            Left=32,Top=198,Width=328,Height=39,
            DropDownStyle=ComboBoxStyle.DropDownList,
            FlatStyle=FlatStyle.Flat,DrawMode=DrawMode.OwnerDrawFixed,
            ItemHeight=29,BackColor=Surface,ForeColor=Primary,
            Font=new Font("Segoe UI Semibold",11)
        };
        languageSelector.Items.AddRange(["Türkçe","İngilizce"]);
        languageSelector.DrawItem+=(_,e)=>
        {
            if(e.Index<0)return;
            using var bg=new SolidBrush((e.State&DrawItemState.Selected)!=0
                ?Color.FromArgb(38,68,82):Surface);
            e.Graphics.FillRectangle(bg,e.Bounds);
            TextRenderer.DrawText(e.Graphics,UiLanguage.T(languageSelector.Items[e.Index]?.ToString()),
                languageSelector.Font,new Rectangle(e.Bounds.Left+12,e.Bounds.Top,
                    e.Bounds.Width-14,e.Bounds.Height),Primary,
                TextFormatFlags.VerticalCenter);
        };
        languagePage.Controls.Add(languageSelector);

        // Settings use the same dark palette as the workspace. The battery
        // priority editor is hosted here, not in a separate top-level window.
        Button? batteryButton=null;
        Panel? batteryPage=null;
        if (batteryPreferences is not null && snapshotGetter is not null &&
            refresh is not null && onApply is not null)
        {
            batteryButton = new DarkButton
            {
                Text = "◫   Pil ve öncelik", Left = 13, Top = 140,
                Width = 178, Height = 49,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                FlatStyle = FlatStyle.Flat, BackColor = Surface,
                ForeColor = Muted, Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold)
            };
            batteryButton.FlatAppearance.BorderSize = 0;
            menu.Controls.Add(batteryButton);

            batteryPage = new Panel { Dock = DockStyle.Fill, BackColor = Background };
            batteryEditor = new BatteryStatusDialog(
                batteryPreferences, snapshotGetter, refresh, onApply)
            {
                TopLevel = false, FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill, BackColor = Background
            };
            batteryPage.Controls.Add(batteryEditor);
            contentHost.Controls.Add(batteryPage);
            batteryEditor.Show();
        }

        void SwitchPage(int page)
        {
            aboutActive=page==2;
            content.Visible=aboutActive;
            languagePage.Visible=page==0;
            if(batteryPage!=null)batteryPage.Visible=page==1;
            foreach(var (button,active) in new[]{
                (languageItem,page==0),(aboutItem,page==2),
                (batteryButton,page==1)})
            {
                if(button==null)continue;
                button.BackColor=active?Color.FromArgb(38,68,82):Surface;
                button.ForeColor=active?Accent:Muted;
            }
            if(page==1)batteryEditor?.UpdateRows();
        }
        languageItem.Click+=(_,_)=>SwitchPage(0);
        aboutItem.Click+=(_,_)=>SwitchPage(2);
        if(batteryButton!=null)batteryButton.Click+=(_,_)=>SwitchPage(1);
        languageSelector.SelectedIndexChanged+=(_,_)=>
        {
            bool english=languageSelector.SelectedIndex==1;
            if(UiLanguage.English==english)return;
            UiLanguage.Select(english);
            UiLanguage.ApplyTree(this);
            this.onLanguageChanged();
            languageSelector.Invalidate();
            batteryEditor?.ApplyLanguage();
        };
        languageSelector.SelectedIndex=UiLanguage.English?1:0;
        UiLanguage.ApplyTree(this);
        SwitchPage(0);
    }

    private void AdvanceSecret(Keys key)
    {
        if (aboutActive && secret.Feed(key))
        {
            BeginInvoke(new Action(async () =>
            {
                Hide();
                bool wasLive=false;
                try
                {
                    wasLive=await suspendLive();
                    using var game=new DoomModeForm(otherOledWriterActive,autoStart:true);
                    game.ShowDialog(Owner);
                }
                catch(Exception ex)
                {
                    MessageBox.Show(Owner,"OLED: "+ex.Message,"DOOM MODE",
                        MessageBoxButtons.OK,MessageBoxIcon.Warning);
                }
                finally
                {
                    try{await resumeLive(wasLive);}catch(Exception ex)
                    {
                        MessageBox.Show(Owner,ex.Message,"OLED restore",
                            MessageBoxButtons.OK,MessageBoxIcon.Warning);
                    }
                    Close();
                }
            }));
        }
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (aboutActive && (keyData & Keys.Modifiers) == Keys.None)
        {
            var key = keyData & Keys.KeyCode;
            if (key is Keys.Up or Keys.Down or Keys.Left or Keys.Right)
            {
                AdvanceSecret(key);
                return true;
            }
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (aboutActive && !e.Alt && !e.Control && !e.Shift &&
            (e.KeyCode is Keys.B or Keys.A))
        {
            AdvanceSecret(e.KeyCode);
            e.SuppressKeyPress = true;
        }
        base.OnKeyDown(e);
    }
}

internal sealed class KonamiSequence
{
    private static readonly Keys[] Pattern =
    [
        Keys.Up, Keys.Up, Keys.Down, Keys.Down,
        Keys.Left, Keys.Right, Keys.Left, Keys.Right, Keys.B, Keys.A
    ];
    private int index;

    public bool Feed(Keys key)
    {
        if (key == Pattern[index]) index++;
        else index = key == Pattern[0] ? 1 : 0;
        if (index != Pattern.Length) return false;
        index = 0;
        return true;
    }
}

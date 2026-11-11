using System;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

// The tray remains a presentation layer for the same single Studio instance.
// Closing the window hides it; quitting or restarting exits deliberately.
internal sealed partial class PixelStudioForm
{
    private NotifyIcon? tray;
    private bool exitRequested;
    private ContextMenuStrip? trayMenu;
    private static string TrayPreferencesPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","tray-prefs.json");
    private void LoadMediaPreference()
    {
        try
        {
            if(!File.Exists(TrayPreferencesPath))return;
            using var settings=JsonDocument.Parse(File.ReadAllText(TrayPreferencesPath));
            if(settings.RootElement.TryGetProperty("ListenMedia",out var option) &&
               option.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                autoMedia.Checked=option.GetBoolean();
                if(!autoMedia.Checked)media.Checked=false;
            }
        }
        catch { /* Older preferences never block UI startup. */ }
    }
    private void ToggleMediaListening()
    {
        autoMedia.Checked=!autoMedia.Checked;
        if(!autoMedia.Checked)media.Checked=false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TrayPreferencesPath)!);
            File.WriteAllText(TrayPreferencesPath,
                JsonSerializer.Serialize(new{ListenMedia=autoMedia.Checked}));
        }
        catch { /* Local save failures do not interrupt the OLED. */ }
        Redraw();
    }

    private sealed class TrayPalette : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(20,27,37);
        public override Color MenuBorder => Color.FromArgb(64,111,133);
        public override Color MenuItemBorder => Color.FromArgb(77,175,213);
        public override Color MenuItemSelected => Color.FromArgb(35,65,84);
        public override Color MenuItemSelectedGradientBegin => MenuItemSelected;
        public override Color MenuItemSelectedGradientEnd => MenuItemSelected;
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(35,65,84);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(35,65,84);
        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
    }

    private void InitializeTray()
    {
        if(captureMode || tray is not null)return;
        trayMenu=new ContextMenuStrip
        {
            ShowImageMargin=false,
            Renderer=new ToolStripProfessionalRenderer(new TrayPalette()),
            ForeColor=Color.FromArgb(230,243,250),
            BackColor=Color.FromArgb(20,27,37),
            Font=new Font("Segoe UI",9f)
        };
        trayMenu.Opening+=(_,_)=>PopulateTrayMenu();
        Icon? appIcon=null;
        try { appIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { }
        tray=new NotifyIcon
        {
            Text="Steelseries Dock Customiser",
            Icon=appIcon??SystemIcons.Application,
            ContextMenuStrip=trayMenu,
            Visible=true
        };
        tray.MouseDoubleClick+=(_,_)=>OpenFromTray();
    }

    private void PopulateTrayMenu()
    {
        if(trayMenu is null)return;
        trayMenu.Items.Clear();
        var english=UiLanguage.English;
        void Item(string name,Action action,bool isChecked=false)
        {
            var menu=new ToolStripMenuItem(name){ForeColor=Color.FromArgb(230,243,250),Checked=isChecked};
            menu.Click+=(_,_)=>action();
            trayMenu.Items.Add(menu);
        }
        Item(english?"Open window":"Pencereyi aç",OpenFromTray);
        Item(dockPreview?.LiveOutputActive==true
                ?(english?"Stop live OLED":"Canlı OLED'i durdur")
                :(english?"Start live OLED":"Canlı OLED'i aç"),
            ()=>dockPreview?.ToggleLiveOutput(),dockPreview?.LiveOutputActive==true);
        Item(autoMedia.Checked
                ?(english?"Stop media listening":"Medyayı dinleme")
                :(english?"Listen to media":"Medyayı dinle"),
            ToggleMediaListening,autoMedia.Checked);
        trayMenu.Items.Add(new ToolStripSeparator());
        var root=new ToolStripMenuItem(english?"Choose profile":"Profil seç")
        {
            ForeColor=Color.FromArgb(230,243,250)
        };
        var profiles=LayoutProfileLibrary.Load().Profiles;
        foreach(var profile in profiles)
        {
            var stored=profile;
            var row=new ToolStripMenuItem(stored.Name){
                ForeColor=Color.FromArgb(230,243,250)
            };
            row.Click+=(_,_)=>ApplyProfileFromTray(stored);
            root.DropDownItems.Add(row);
        }
        if(profiles.Count==0)root.DropDownItems.Add(
            new ToolStripMenuItem(english?"No saved profiles":"Kayıtlı profil yok"){Enabled=false});
        trayMenu.Items.Add(root);
        trayMenu.Items.Add(new ToolStripSeparator());
        Item(english?"Restart":"Yeniden başlat",RestartFromTray);
        Item(english?"Quit application":"Uygulamayı kapat",QuitFromTray);
    }

    private void OpenFromTray()
    {
        if(IsDisposed)return;
        ShowInTaskbar=true;
        Show();
        if(WindowState==FormWindowState.Minimized)
            WindowState=FormWindowState.Normal;
        Activate();
    }

    private void ApplyProfileFromTray(SavedLayoutProfile profile)
    {
        if(IsDisposed)return;
        // Switch first so an unsaved current layout is not applied to the
        // wrong profile mode, and always clone the saved user artwork.
        SwitchEditingMode(profile.Mode);
        Record();
        state=profile.Layout.DeepClone();
        StudioProject.Sanitize(state,profile.Mode,project.ProtectHeader);
        project.Set(profile.Mode,state);
        selected=null;
        RefreshWidgetList();
        Redraw();
        SaveLayout();
    }

    private void RestartFromTray()
    {
        SaveLayout();
        exitRequested=true;
        if(tray!=null)tray.Visible=false;
        Application.Restart();
    }

    private void QuitFromTray()
    {
        SaveLayout();
        exitRequested=true;
        Close();
    }

    private void HandleTrayClosing(object? sender,FormClosingEventArgs e)
    {
        if(captureMode || exitRequested || e.CloseReason!=CloseReason.UserClosing)return;
        e.Cancel=true;
        SaveLayout();
        Hide();
        ShowInTaskbar=false;
        if(tray!=null)tray.BalloonTipTitle="Steelseries Dock Customiser";
    }

    private void DisposeTray()
    {
        if(tray!=null)
        {
            tray.Visible=false;
            tray.Dispose();
            tray=null;
        }
        trayMenu?.Dispose();
        trayMenu=null;
    }
}

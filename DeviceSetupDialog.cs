using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>One-time discover-and-assign guide; never pairs Bluetooth devices
/// or invents a battery reading for proprietary vendor hardware.</summary>
internal static class DeviceSetupState
{
    internal static string FilePath=>Path.Combine(
        Beta5Sandbox.SettingsDirectory,"device-setup-guide-v1.json");
    internal static bool Completed()
    {
        try{return File.Exists(FilePath)&&
            File.ReadAllText(FilePath).Trim()=="complete";}
        catch{return false;}
    }
    internal static void MarkCompleted()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath,"complete");
    }
}

internal sealed class DeviceSetupDialog:Form
{
    private readonly Func<BatterySnapshot> snapshot;
    private readonly BatteryPreferences preferences;
    private readonly Func<Task> refresh;
    private readonly DataGridView list=new();
    private readonly Label info=new();
    private readonly DarkButton assign=new();
    private readonly TextBox nickname=new(){
        Left=20,Top=104,Width=310,MaxLength=48};
    private readonly DarkButton rename=new(){
        Left=353,Top=104,Width=180,Height=33};
    private IReadOnlyList<ConnectedPeripheral> devices=[];
    private bool busy;
    private bool captureMode;
    internal void EnableOffscreenCapture()=>captureMode=true;
    internal DeviceSetupDialog(Func<BatterySnapshot> getBattery,
        BatteryPreferences prefs,Func<Task> refreshBatteries)
    {
        UiPolish.Register(this);
        snapshot=getBattery;preferences=prefs;refresh=refreshBatteries;
        Text=UiLanguage.English?"Device setup":"Cihaz kurulumu";
        StartPosition=FormStartPosition.CenterParent;
        ClientSize=new Size(865,570);
        MinimumSize=new Size(740,500);
        BackColor=Color.FromArgb(13,18,26);
        ForeColor=Color.FromArgb(232,242,248);
        Font=new Font("Segoe UI",9.6f);
        var header=new Label{
            Dock=DockStyle.Top,Height=62,Padding=new Padding(20,14,10,4),
            Font=new Font("Segoe UI Semibold",18f,FontStyle.Bold),
            Text=UiLanguage.English?"Connect your battery devices":
                "Pil cihazlarını tanı",
            ForeColor=ForeColor};
        var help=new Label{
            Dock=DockStyle.Top,Height=66,
            Padding=new Padding(20,0,15,8),
            ForeColor=Color.FromArgb(155,178,194),
            Text=UiLanguage.English
                ?"We detect USB and Bluetooth devices automatically. Only "+
                  "verified live battery readings can be added. "+
                  "Your headset and spare battery already have two default slots."
                :"USB ve Bluetooth cihazlarını otomatik tarıyoruz. "+
                  "Yalnızca doğrulanmış canlı pil değeri olan cihazlar "+
                  "önceliğe eklenebilir. Kulaklık ve yedek pil varsayılan iki yuvadır."};
        list.Dock=DockStyle.Fill;
        list.ReadOnly=true;
        list.MultiSelect=false;
        list.AllowUserToAddRows=false;
        list.AllowUserToDeleteRows=false;
        list.RowHeadersVisible=false;
        list.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
        list.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        list.BackgroundColor=Color.FromArgb(21,29,40);
        list.GridColor=Color.FromArgb(49,61,78);
        list.EnableHeadersVisualStyles=false;
        list.ColumnHeadersDefaultCellStyle.BackColor=
            Color.FromArgb(31,43,59);
        list.ColumnHeadersDefaultCellStyle.ForeColor=ForeColor;
        list.DefaultCellStyle.BackColor=Color.FromArgb(23,32,45);
        list.DefaultCellStyle.ForeColor=ForeColor;
        list.DefaultCellStyle.SelectionBackColor=
            Color.FromArgb(38,69,86);
        list.ColumnHeadersHeight=36;list.RowTemplate.Height=29;
        list.Columns.Add("device",UiLanguage.English?"Device":"Cihaz");
        list.Columns.Add("connection",UiLanguage.English?"Connection":"Bağlantı");
        list.Columns.Add("battery",UiLanguage.English?"Battery":"Pil");
        list.Columns.Add("source",UiLanguage.English?"Status":"Durum");
        list.Columns[0].FillWeight=150;
        list.Columns[1].FillWeight=65;
        list.Columns[2].FillWeight=40;
        list.Columns[3].FillWeight=170;

        var footer=new Panel{Dock=DockStyle.Bottom,Height=155};
        info.SetBounds(20,12,810,39);
        info.ForeColor=Color.FromArgb(153,176,193);
        info.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;
        footer.Controls.Add(info);
        assign.Text=UiLanguage.English?"Add selected to OLED priority":
            "Seçili cihazı OLED önceliğine ekle";
        assign.SetBounds(20,58,315,36);
        assign.BackColor=Color.FromArgb(38,75,90);
        assign.ForeColor=ForeColor;
        assign.Click+=(_,_)=>AssignSelected();
        footer.Controls.Add(assign);
        var retry=new DarkButton{
            Text=UiLanguage.English?"Scan again":"Yeniden tara",
            Width=140,Height=36};
        retry.Left=355;retry.Top=58;
        retry.Click+=async (_,_)=>await ScanAsync();
        footer.Controls.Add(retry);
        var close=new DarkButton{
            Text=UiLanguage.English?"Finish / skip":"Bitir / atla",
            Width=134,Height=36};
        close.Left=515;close.Top=58;
        close.Click+=(_,_)=>Close();
        footer.Controls.Add(close);
        nickname.BackColor=Color.FromArgb(31,43,59);
        nickname.ForeColor=ForeColor;
        nickname.PlaceholderText=UiLanguage.English
            ?"Custom display name":"Özel görünen cihaz adı";
        footer.Controls.Add(nickname);
        rename.Text=UiLanguage.English?"Save name":"Adı kaydet";
        rename.Click+=(_,_)=>{
            if(list.CurrentRow?.Tag is not ConnectedPeripheral item||
               string.IsNullOrEmpty(item.BatteryKind))return;
            try
            {
                BatteryDeviceAliases.Set(item.BatteryKind,nickname.Text);
                info.Text=UiLanguage.English
                    ?"Display name saved locally."
                    :"Cihaz adı yalnızca bu bilgisayara kaydedildi.";
            }
            catch(Exception ex)
            {info.Text=ex.Message;}
        };
        footer.Controls.Add(rename);
        Controls.Add(list);
        Controls.Add(footer);
        Controls.Add(help);
        Controls.Add(header);
        list.SelectionChanged+=(_,_)=>UpdateSelected();
        Shown+=async (_,_)=>{if(!captureMode)await ScanAsync();};
    }

    private async Task ScanAsync()
    {
        if(busy||IsDisposed)return;
        busy=true;
        assign.Enabled=false;
        info.Text=UiLanguage.English?"Scanning connected devices...":
            "Bağlı cihazlar taranıyor...";
        try
        {
            await refresh();
            var found=await ConnectedPeripheralInventory
                .DiscoverAsync(snapshot());
            if(IsDisposed)return;
            devices=found;
            list.Rows.Clear();
            foreach(var item in found)
            {
                int i=list.Rows.Add(item.Name,item.Connection,
                    item.BatteryDisplay,item.Detail);
                list.Rows[i].Tag=item;
                foreach(DataGridViewCell cell in list.Rows[i].Cells)
                    cell.ToolTipText=item.BatterySource;
            }
            info.Text=UiLanguage.English
                ?$"{found.Count} devices. Unsupported vendor battery protocols remain unknown."
                :$"{found.Count} cihaz bulundu. Desteklenmeyen özel pil protokolleri bilinmiyor.";
            UpdateSelected();
        }
        catch(Exception ex)
        {
            if(!IsDisposed)info.Text=(UiLanguage.English
                ?"Scan could not finish: ":"Tarama tamamlanamadı: ")+
                ex.GetType().Name;
        }
        finally
        {
            busy=false;
            if(!IsDisposed)UpdateSelected();
        }
    }

    private void UpdateSelected()
    {
        if(busy||IsDisposed)return;
        var item=list.CurrentRow?.Tag as ConnectedPeripheral;
        assign.Enabled=item is {Percent: >=0 and <=100,BatteryKind: not null};
        rename.Enabled=assign.Enabled;
        nickname.Enabled=assign.Enabled;
        if(item?.BatteryKind is string kind)
            nickname.Text=BatteryDeviceAliases.Name(kind)??item.Name;
        if(item is {Percent:null})
            info.Text=UiLanguage.English
                ?"The device is connected, but no trusted battery level is available."
                :"Cihaz bağlı; fakat doğrulanmış güncel pil yüzdesi yok.";
    }

    private void AssignSelected()
    {
        if(list.CurrentRow?.Tag is not ConnectedPeripheral device ||
           device.Percent is not >=0 and <=100 ||
           string.IsNullOrEmpty(device.BatteryKind))return;
        var kind=device.BatteryKind;
        // Stable provider keys already bound to the discovered reading.
        // Save only this user's priority list, never any vendor-specific ID.
        preferences.PriorityKinds=BatteryPriority.Sanitize(
            preferences.PriorityKinds.Prepend(kind));
        preferences.Save();
        info.Text=UiLanguage.English
            ?"Added to your OLED priority. Other preferences remain unchanged."
            :"OLED önceliğine eklendi. Diğer ayarlar korunuyor.";
    }
}

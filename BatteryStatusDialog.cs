using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

internal sealed class BatteryPreferences
{
    public string[] PriorityKinds {get;set;}=BatteryPriority.DefaultKinds.ToArray();
    public bool AutoDualSense {get;set;}=true;
    public bool ExperimentalControllerHid {get;set;}=false;
    public bool UseAdbPhoneBattery {get;set;}=false;
    public bool EnableLowBatteryWarnings {get;set;}=true;
    public int LowBatteryWarningPercent {get;set;}=20;
    public int CriticalBatteryWarningPercent {get;set;}=10;
    public int WarningRepeatMinutes {get;set;}=30;
    public Dictionary<string,int> LowBatteryThresholds {get;set;}=new();
    public bool AutoMinimizePhoneLink {get;set;}=false;
    // User-controlled recognition/tracking of connected peripherals.
    // No fake percentages or fixed model defaults shipped.
    public string[] FollowedDevices {get;set;}=[];
    public string[] HiddenDevices {get;set;}=[];
    public Dictionary<string,string> FollowedNames {get;set;}=new();
    public static string FilePath=>Path.Combine(
        Beta5Sandbox.SettingsDirectory,"batteries-v098.json");

    public static BatteryPreferences Load()
    {
        try
        {
            if(File.Exists(FilePath))
            {
                var pref=JsonSerializer.Deserialize<BatteryPreferences>(File.ReadAllText(FilePath));
                if(pref!=null){pref.PriorityKinds=BatteryPriority.Sanitize(pref.PriorityKinds);
                    pref.LowBatteryThresholds??=new();
                    pref.FollowedDevices??=[];
                    pref.HiddenDevices??=[];
                    pref.FollowedNames??=new();
                    return pref;}
            }
            // Carry over the old experimental-device flag, but apply the new
            // default priority instead of the former hard-coded 3 devices.
            string old=Path.Combine(Path.GetDirectoryName(FilePath)!,"batteries-v09.json");
            if(File.Exists(old))
            {
                using var data=JsonDocument.Parse(File.ReadAllText(old));
                var r=data.RootElement;
                return new BatteryPreferences{
                    ExperimentalControllerHid=ReadFlag(r,"ExperimentalControllerHid"),
                    UseAdbPhoneBattery=ReadFlag(r,"UseAdbPhoneBattery")
                };
            }
        }
        catch{}
        return new BatteryPreferences();
    }
    private static bool ReadFlag(JsonElement root,string field)=>
        root.TryGetProperty(field,out var value)&&value.ValueKind==JsonValueKind.True;

    public void Save()
    {
        PriorityKinds=BatteryPriority.Sanitize(PriorityKinds);
        LowBatteryThresholds??=new();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath,JsonSerializer.Serialize(this,
            new JsonSerializerOptions{WriteIndented=true}));
    }
}

internal sealed class BatteryStatusDialog : Form
{
    private readonly BatteryPreferences prefs;
    private IReadOnlyList<ConnectedPeripheral> discovered=[];
    private bool discoveryBusy;
    private sealed record DisconnectedTracked(string Id,string Name);
    private readonly Func<BatterySnapshot> snapshotGetter;
    private readonly Func<Task> refresh;
    private readonly Action onApply;
    private readonly DataGridView grid=new()
    {
        Left=16,Top=18,Width=840,Height=210,ReadOnly=true,
        AllowUserToAddRows=false,AllowUserToDeleteRows=false,
        MultiSelect=false,RowHeadersVisible=false,
        SelectionMode=DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor=Color.White
    };
    private readonly ListBox priority=new(){
        Left=17,Top=292,Width=355,Height=164,
        DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=24
    };
    private readonly Label active=new(){Left=402,Top=329,Width=450,Height=73};
    private readonly Label lastUpdated=new(){Left=150,Top=234,Width=700,Height=27};
    private readonly Label summary=new(){
        Left=18,Top=48,Width=832,Height=15,
        ForeColor=Color.FromArgb(143,188,203),
        Font=new Font("Segoe UI",8.4f)};
    // Sources are selected by verified connections automatically.
    // No redundant HID/ADB/priority-override checkboxes.
    private readonly DarkCheckBox lowWarning=new(){
        Left=17,Top=517,Width=400,Height=28,
        Text="Düşük pil uyarıları"};
    private readonly DarkNumberBox warningPercent=new(){
        Left=500,Top=517,Width=100,Minimum=1,Maximum=99};
    private readonly DarkNumberBox criticalPercent=new(){
        Left=720,Top=517,Width=100,Minimum=1,Maximum=99};
    private readonly DarkNumberBox warningRepeat=new(){
        Left=500,Top=555,Width=100,Minimum=5,Maximum=240};
    private readonly DarkCheckBox phoneLinkRecovery=new(){
        Left=17,Top=584,Width=790,Height=31,AutoSize=false,
        Text="Phone Link erişilemiyorsa otomatik açıp küçült (isteğe bağlı)"};
    // Never launch Phone Link without an explicit enabled preference.

    public BatteryStatusDialog(BatteryPreferences preferences,
        Func<BatterySnapshot> getSnapshot,Func<Task> update,Action apply)
    {
        UiPolish.Register(this);
        prefs=preferences;snapshotGetter=getSnapshot;refresh=update;onApply=apply;
        Text=UiLanguage.English
            ?"Battery devices and priority":"Pil cihazları ve öncelik";
        StartPosition=FormStartPosition.CenterParent;
        ClientSize=new Size(900,760);
        MinimumSize=new Size(760,640);
        AutoScroll=true;
        AutoScrollMinSize=new Size(875,730);
        Font=new Font("Segoe UI",9.5f);
        Controls.Add(grid);
        var discoveredMenu=new ContextMenuStrip();
        UiPolish.StyleMenu(discoveredMenu);
        var followAction=new ToolStripMenuItem(
            UiLanguage.English?"Track device":"Cihazı takibe al");
        var hideAction=new ToolStripMenuItem(
            UiLanguage.English?"Hide from list":"Listeden gizle");
        var showHidden=new ToolStripMenuItem(UiLanguage.English
            ?"Show hidden devices again":"Gizlenen cihazları tekrar göster");
        var changeIcon=new ToolStripMenuItem(UiLanguage.English
            ?"Customize device icon...":"Cihaz ikonunu özelleştir...");
        discoveredMenu.Items.AddRange([
            followAction,changeIcon,hideAction,showHidden]);
        discoveredMenu.Opening+=(_,_)=>{
            object? selected=grid.CurrentRow?.Tag;
            string id=selected switch{
                ConnectedPeripheral d=>d.Id,
                DisconnectedTracked d=>d.Id,
                _=>""};
            bool followed=(prefs.FollowedDevices??[]).Contains(id,
                StringComparer.OrdinalIgnoreCase);
            followAction.Enabled=id.Length>0;
            changeIcon.Enabled=selected is ConnectedPeripheral;
            hideAction.Enabled=selected is ConnectedPeripheral;
            followAction.Text=followed
                ?(UiLanguage.English?"Stop tracking":"Takibi bırak")
                :(UiLanguage.English?"Track device":"Cihazı takibe al");
            showHidden.Enabled=(prefs.HiddenDevices??[]).Length>0;
        };
        followAction.Click+=(_,_)=>{
            object? selection=grid.CurrentRow?.Tag;
            string id=selection switch{
                ConnectedPeripheral d=>d.Id,
                DisconnectedTracked d=>d.Id,
                _=>""};
            if(id.Length==0)return;
            bool followed=(prefs.FollowedDevices??[]).Contains(id,
                StringComparer.OrdinalIgnoreCase);
            if(followed)
            {
                prefs.FollowedDevices=(prefs.FollowedDevices??[])
                    .Where(x=>!x.Equals(id,
                        StringComparison.OrdinalIgnoreCase)).ToArray();
                prefs.FollowedNames.Remove(id);
            }
            else if(selection is ConnectedPeripheral device)
            {
                prefs.FollowedDevices=(prefs.FollowedDevices??[]).Append(id)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                prefs.FollowedNames[id]=device.Name;
                prefs.HiddenDevices=(prefs.HiddenDevices??[])
                    .Where(x=>!x.Equals(id,
                        StringComparison.OrdinalIgnoreCase)).ToArray();
                if(device.Percent.HasValue &&
                   device.BatteryKind is string kind)
                    prefs.PriorityKinds=BatteryPriority.Sanitize(
                        prefs.PriorityKinds.Prepend(kind));
            }
            prefs.Save();onApply();UpdateRows();
        };
        hideAction.Click+=(_,_)=>{
            if(grid.CurrentRow?.Tag is not ConnectedPeripheral device)return;
            prefs.HiddenDevices=(prefs.HiddenDevices??[]).Append(device.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            prefs.FollowedDevices=(prefs.FollowedDevices??[])
                .Where(x=>!x.Equals(device.Id,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
            prefs.FollowedNames.Remove(device.Id);
            prefs.Save();UpdateRows();
        };
        changeIcon.Click+=(_,_)=>{
            if(grid.CurrentRow?.Tag is not ConnectedPeripheral device)return;
            using var picker=new BatteryDeviceIconPickerDialog(
                device.BatteryKind,device.Name,device.IconKind);
            if(picker.ShowDialog(this)==DialogResult.OK)
                UpdateRows();
        };
        showHidden.Click+=(_,_)=>{
            prefs.HiddenDevices=[];
            prefs.Save();UpdateRows();
        };
        grid.ContextMenuStrip=discoveredMenu;
        grid.MouseDown+=(_,e)=>{
            if(e.Button!=MouseButtons.Right)return;
            var hit=grid.HitTest(e.X,e.Y);
            if(hit.RowIndex>=0)grid.CurrentCell=
                grid.Rows[hit.RowIndex].Cells[0];
        };
        grid.Columns.Add("device","Cihaz");
        grid.Columns.Add("pct","Pil");
        grid.Columns.Add("source","Doğrulanan kaynak");
        grid.Columns.Add("status","Durum");
        grid.Columns[0].FillWeight=90;
        grid.Columns[1].FillWeight=40;
        grid.Columns[2].FillWeight=115;
        grid.Columns[3].FillWeight=190;
        foreach(var kind in BatteryPriority.AllKinds)
            grid.Rows.Add(BatteryPriority.DisplayName(kind,snapshotGetter()),"--%","","");

        var now=new DarkButton{Left=17,Top=232,Width=125,Height=31,Text="Şimdi yenile"};
        now.Click+=async (_,_)=>{
            now.Enabled=false;
            try
            {
                await refresh();
                await RefreshFoundDevicesAsync();
                UpdateRows();
            }
            finally{now.Enabled=true;}
        };
        Controls.Add(now);Controls.Add(lastUpdated);
        lastUpdated.Width=400;
        var connectedDevices=new DarkButton{
            Left=604,Top=232,Width=252,Height=32,
            Text=UiLanguage.English?"Connected devices":"Bağlı cihazlar"
        };
        connectedDevices.Click+=(_,_)=>{
            using var inventory=new ConnectedDevicesDialog(snapshotGetter);
            inventory.ShowDialog(this);
        };
        Controls.Add(connectedDevices);
        var deviceSetup=new DarkButton{
            Left=459,Top=232,Width=140,Height=32,
            Text=UiLanguage.English?"Device setup":"Cihaz kurulumu"};
        deviceSetup.Click+=(_,_)=>{
            using var wizard=new DeviceSetupDialog(snapshotGetter,prefs,refresh);
            wizard.ShowDialog(this);
            onApply();
            UpdateRows();
        };
        Controls.Add(deviceSetup);
        var brandSources=new DarkButton{
            Left=151,Top=232,Width=300,Height=32,
            Text=UiLanguage.English?"Wireless device library":
                "Kablosuz cihaz kütüphanesi"};
        brandSources.Click+=(_,_)=>{
            using var info=new WirelessCompatibilityDialog();
            info.ShowDialog(this);
        };
        Controls.Add(brandSources);
        Controls.Add(new Label{
            Left=17,Top=271,Width=610,Height=23,
            Text=UiLanguage.English
                ?"OLED battery order — use Up/Down to arrange":
                "OLED pil sırası — Yukarı/Aşağı ile sıralayabilirsin",
            ForeColor=Color.FromArgb(183,207,221)
        });
        Controls.Add(priority);
        priority.DrawItem+=(_,e)=>{
            if(e.Index<0)return;
            e.DrawBackground();
            var kind=priority.Items[e.Index]?.ToString()??"";
            TextRenderer.DrawText(e.Graphics,BatteryPriority.DisplayName(kind,snapshotGetter()),
                priority.Font,new Rectangle(e.Bounds.Left+8,e.Bounds.Top,
                    e.Bounds.Width-12,e.Bounds.Height),
                e.State.HasFlag(DrawItemState.Selected)
                    ?Color.FromArgb(72,194,238):Color.FromArgb(235,242,248),
                TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        };
        foreach(var kind in BatteryPriority.Sanitize(prefs.PriorityKinds))
            priority.Items.Add(kind);
        priority.SelectedIndex=0;
        var priorityMenu=new ContextMenuStrip();
        UiPolish.StyleMenu(priorityMenu);
        var thresholdItem=new ToolStripMenuItem(UiLanguage.T("Cihaz pil uyarısı eşiği..."));
        thresholdItem.Click+=(_,_)=>
        {
            if(priority.SelectedItem is not string kind)return;
            int current=prefs.LowBatteryThresholds.TryGetValue(kind,out var set)
                ?set:prefs.LowBatteryWarningPercent;
            using var editor=new BatteryAlertThresholdDialog(kind,current,snapshotGetter());
            if(editor.ShowDialog(this)!=DialogResult.OK)return;
            prefs.LowBatteryThresholds[kind]=editor.Percent;
        };
        priorityMenu.Items.Add(thresholdItem);
        priority.ContextMenuStrip=priorityMenu;
        priority.MouseDown+=(_,e)=>
        {
            if(e.Button!=MouseButtons.Right)return;
            int index=priority.IndexFromPoint(e.Location);
            if(index>=0)priority.SelectedIndex=index;
        };
        var up=new DarkButton{Left=382,Top=294,Width=76,Height=32,Text="Yukarı"};
        var down=new DarkButton{Left=382,Top=334,Width=76,Height=32,Text="Aşağı"};
        // Keep the priority controls separate from other device editing.
        up.Click+=(_,_)=>MovePriority(-1);
        down.Click+=(_,_)=>MovePriority(1);
        Controls.Add(up);Controls.Add(down);
        active.Left=470;active.Top=290;active.Width=384;
        Controls.Add(active);
        Controls.Add(new Label{
            Left=17,Top=456,Width=835,Height=48,
            Text=UiLanguage.English
                ?"Sources: SteelSeries GG, connected Bluetooth, "+
                  "standard HID, optional SDKs and Phone Link. "+
                  "Windows-reported Bluetooth levels may lag."
                :"Veri kaynakları: GG, bağlı Bluetooth, standart HID, "+
                  "isteğe bağlı SDK'lar ve Phone Link. "+
                  "Windows Bluetooth yüzdeleri gecikmeli güncellenebilir.",
            ForeColor=Color.FromArgb(143,171,188),
            Font=new Font("Segoe UI",9.1f)
        });
        phoneLinkRecovery.Checked=prefs.AutoMinimizePhoneLink;
        phoneLinkRecovery.Text=UiLanguage.English
            ?"Open Phone Link automatically if unavailable (off by default)"
            :"Phone Link kapalıysa otomatik aç (varsayılan: kapalı)";
        var hints=new ToolTip{
            AutomaticDelay=400,AutoPopDelay=12500,
            InitialDelay=500,ReshowDelay=150};
        hints.SetToolTip(phoneLinkRecovery,UiLanguage.English
            ?"This may open Windows Phone Link for phone battery recovery. "+
              "Leave off if you do not use phone battery."
            :"Yalnızca telefon pili için Phone Link kurtarmasına izin verir. "+
              "Telefon pili kullanmıyorsan kapalı bırak.");
        Controls.Add(phoneLinkRecovery);
        lowWarning.Checked=prefs.EnableLowBatteryWarnings;
        warningPercent.Value=Math.Clamp(prefs.LowBatteryWarningPercent,1,99);
        criticalPercent.Value=Math.Clamp(prefs.CriticalBatteryWarningPercent,1,99);
        warningRepeat.Value=Math.Clamp(prefs.WarningRepeatMinutes,5,240);
        Controls.Add(lowWarning);Controls.Add(warningPercent);
        Controls.Add(criticalPercent);
        Controls.Add(warningRepeat);
        Controls.Add(new Label{
            Text="Uyarı tekrarı (dakika)",Left=265,Top=558,Width=215,Height=24});
        Controls.Add(new Label{Text="Uyarı %",Left=420,Top=520,Width=75,Height=21});
        Controls.Add(new Label{Text="Kritik %",Left=630,Top=520,Width=75,Height=21});
        var applyButton=new DarkButton{Left=620,Top=627,Width=236,Height=37,
            Text=UiLanguage.English?"Save battery settings":"Pil ayarlarını kaydet"};
        applyButton.Click+=(_,_)=>{
            prefs.PriorityKinds=priority.Items.Cast<string>().ToArray();
            // Older source toggles are deliberately not exposed: the app
            // chooses already-authorized, connected providers itself.
            prefs.EnableLowBatteryWarnings=lowWarning.Checked;
            prefs.LowBatteryWarningPercent=(int)warningPercent.Value;
            prefs.CriticalBatteryWarningPercent=(int)criticalPercent.Value;
            prefs.WarningRepeatMinutes=(int)warningRepeat.Value;
            prefs.AutoMinimizePhoneLink=phoneLinkRecovery.Checked;
            prefs.Save();
            onApply();
            if(TopLevel)Close();
            else lastUpdated.Text="Ayarlar kaydedildi · Pil verileri yenileniyor";
        };
        Controls.Add(applyButton);
        priority.SelectedIndexChanged+=(_,_)=>ShowActive();
        // Priority order alone decides which devices occupy the OLED.
        UpdateRows();

        // Embedded Settings page: maintain the studio's dark visual language.
        BackColor=Color.FromArgb(13,18,26);
        ForeColor=Color.FromArgb(235,242,248);
        foreach(Control control in Controls)
        {
            control.Top+=45;
            if(control is Label label)
            {
                label.ForeColor=Color.FromArgb(151,169,187);
                label.BackColor=BackColor;
            }
            else if(control is Button button)
            {
                button.FlatStyle=FlatStyle.Flat;
                button.FlatAppearance.BorderSize=0;
                bool isPrimary=ReferenceEquals(button,applyButton);
                button.BackColor=isPrimary
                    ?Color.FromArgb(72,194,238):Color.FromArgb(35,47,62);
                button.ForeColor=isPrimary
                    ?Color.FromArgb(13,18,26):Color.FromArgb(235,242,248);
                button.Cursor=Cursors.Hand;
            }
            else if(control is CheckBox box)
            {
                box.ForeColor=Color.FromArgb(235,242,248);
                box.BackColor=BackColor;
            }
        }
        grid.BackgroundColor=Color.FromArgb(23,31,43);
        grid.BorderStyle=BorderStyle.None;
        grid.GridColor=Color.FromArgb(45,59,74);
        grid.EnableHeadersVisualStyles=false;
        grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(35,47,62);
        grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.FromArgb(235,242,248);
        grid.DefaultCellStyle.BackColor=Color.FromArgb(23,31,43);
        grid.DefaultCellStyle.ForeColor=Color.FromArgb(235,242,248);
        grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(38,68,82);
        grid.DefaultCellStyle.SelectionForeColor=Color.White;
        grid.RowsDefaultCellStyle.BackColor=Color.FromArgb(23,31,43);
        grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(28,38,51);
        priority.BackColor=Color.FromArgb(23,31,43);
        priority.ForeColor=Color.FromArgb(235,242,248);
        priority.BorderStyle=BorderStyle.None;
        Controls.Add(new Label{
            Text=UiLanguage.English?"BATTERY & DEVICES":"PİL VE CİHAZLAR",
            Left=18,Top=13,Width=850,Height=31,
            Font=new Font("Segoe UI Semibold",18f,FontStyle.Bold),
            ForeColor=Color.FromArgb(235,242,248),BackColor=BackColor
        });
        Controls.Add(summary);
        var alertHeading=new Label{
            Text=UiLanguage.English?"LOW BATTERY ALERTS":"DÜŞÜK PİL UYARILARI",
            Left=17,Top=537,Width=640,Height=22,
            ForeColor=Color.FromArgb(138,199,224),
            Font=new Font("Segoe UI Semibold",9f,FontStyle.Bold)};
        Controls.Add(alertHeading);

        // In Settings this form is a dock-filled child: its absolute
        // 900px content must NOT cover the bottom Save button at 125/150%
        // scale or when the user has enlarged Windows text. A scrolling
        // body and a permanently visible right-aligned footer keep every
        // action accessible without enlarging the settings window.
        var body=new Panel{
            Dock=DockStyle.Fill,AutoScroll=true,BackColor=BackColor,
            AutoScrollMinSize=new Size(875,680)};
        var footer=new Panel{
            Dock=DockStyle.Bottom,Height=58,BackColor=BackColor};
        var existing=Controls.Cast<Control>()
            .Where(c=>!ReferenceEquals(c,applyButton)).ToArray();
        foreach(var element in existing)
        {
            Controls.Remove(element);
            body.Controls.Add(element);
        }
        Controls.Remove(applyButton);
        footer.Controls.Add(applyButton);
        applyButton.Top=10;
        applyButton.Left=Math.Max(14,footer.Width-applyButton.Width-22);
        applyButton.Anchor=AnchorStyles.Top|AnchorStyles.Right;
        footer.Resize+=(_,_)=>applyButton.Left=Math.Max(
            14,footer.ClientSize.Width-applyButton.Width-22);
        var frame=new TableLayoutPanel{
            Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,
            Margin=Padding.Empty,Padding=Padding.Empty,
            BackColor=BackColor};
        frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        frame.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
        body.Dock=DockStyle.Fill;
        footer.Dock=DockStyle.Fill;
        frame.Controls.Add(body,0,0);
        frame.Controls.Add(footer,0,1);
        Controls.Add(frame);
        AutoScroll=false;

        ApplyLanguage();
        Shown+=async (_,_)=>await RefreshFoundDevicesAsync();
    }

    private async Task RefreshFoundDevicesAsync()
    {
        if(discoveryBusy||IsDisposed)return;
        discoveryBusy=true;
        try
        {
            discovered=await ConnectedPeripheralInventory.DiscoverAsync(
                snapshotGetter());
            if(!IsDisposed)UpdateRows();
        }
        catch{/* No USB/Bluetooth enumeration permission is not zero devices. */}
        finally{discoveryBusy=false;}
    }

    public void ApplyLanguage()
    {
        UiLanguage.ApplyTree(this);
        string[] headers=["Cihaz","Pil","Doğrulanan kaynak","Durum"];
        for(int i=0;i<headers.Length;i++)
            grid.Columns[i].HeaderText=UiLanguage.T(headers[i]);
        for(int i=0;i<grid.Rows.Count;i++)
            if(grid.Rows[i].Tag is string kind)
                grid.Rows[i].Cells[0].Value=BatteryPriority.DisplayName(kind,snapshotGetter());
        UpdateRows();
    }

    private void MovePriority(int delta)
    {
        int before=priority.SelectedIndex,after=before+delta;
        if(before<0||after<0||after>=priority.Items.Count)return;
        var item=priority.Items[before];
        priority.Items.RemoveAt(before);
        priority.Items.Insert(after,item);
        priority.SelectedIndex=after;
        ShowActive();
    }
    private void ShowActive()
    {
        var local=new BatteryPreferences{
            PriorityKinds=priority.Items.Cast<string>().ToArray()
        };
        string[] display=BatteryPriority.SelectThree(snapshotGetter(),local);
        active.Text=UiLanguage.T("Şu anda seçilenler:\n")+string.Join("  ·  ",
            display.Select(k=>BatteryPriority.DisplayName(k,snapshotGetter())));
    }
    internal void SetFoundForCapture(
        IReadOnlyList<ConnectedPeripheral> sample)
    {
        discovered=sample;
        UpdateRows();
    }

    internal static bool IsBatteryCandidate(ConnectedPeripheral device)
    {
        if(device.Percent.HasValue)return true;
        if(device.Connection.Contains("Bluetooth",
            StringComparison.OrdinalIgnoreCase))return true;
        if(device.BatteryKind is "Headset" or "Mouse" or "Phone" or
            "SpareBattery" or "DualSense")return true;
        if(WirelessCompatibilityCatalog.Recognize(
            device.Name,device.Manufacturer,device.Connection)!=null)
            return true;
        var match=System.Text.RegularExpressions.Regex.Match(
            device.HardwareSummary,@"VID_([0-9A-F]{4}) PID_([0-9A-F]{4})");
        return match.Success&&
            CompxWirelessBatteryProvider.FindReceiver(
                Convert.ToInt32(match.Groups[1].Value,16),
                Convert.ToInt32(match.Groups[2].Value,16))!=null;
    }

    public void UpdateRows()
    {
        if(IsDisposed)return;
        var snap=snapshotGetter();
        var kinds=BatteryPriority.DefaultKinds.Concat(snap.Values
            .Where(pair=>pair.Value.Available &&
                BatteryPriority.IsCurrent(pair.Value,DateTimeOffset.Now))
            .Select(pair=>pair.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var blocked=(prefs.HiddenDevices??[])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trackedIds=(prefs.FollowedDevices??[])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var linkedDevices=discovered.Where(d=>
            !blocked.Contains(d.Id)&&IsBatteryCandidate(d) &&
            // A linked live reading already has a normal battery slot.
            !(d.BatteryKind is string kind &&
                kinds.Contains(kind,StringComparer.OrdinalIgnoreCase)) &&
            !kinds.Any(k=>string.Equals(
                BatteryPriority.DisplayName(k,snap),d.Name,
                StringComparison.OrdinalIgnoreCase)))
            .Take(60).ToArray();
        var tracked=linkedDevices.Where(d=>trackedIds.Contains(d.Id)).ToArray();
        var found=linkedDevices.Where(d=>!trackedIds.Contains(d.Id)).ToArray();
        grid.Rows.Clear();
        foreach(var kind in kinds)
        {
            int row=grid.Rows.Add(
                BatteryPriority.DisplayName(kind,snap),"--%","","");
            grid.Rows[row].Tag=kind;
        }
        foreach(var device in tracked)
        {
            int row=grid.Rows.Add("✓ "+device.Name,device.BatteryDisplay,
                device.BatterySource,
                (UiLanguage.English?"Tracking · ":"Takipte · ")+device.Detail);
            grid.Rows[row].Tag=device;
        }
        var activeIds=discovered.Select(d=>d.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(string id in trackedIds.Where(id=>!activeIds.Contains(id)))
        {
            string savedName=prefs.FollowedNames.TryGetValue(id,out var name)
                ?name:(UiLanguage.English?"Previously tracked device":
                    "Önceden takip edilen cihaz");
            int row=grid.Rows.Add(
                savedName,"--%",
                UiLanguage.English?"Disconnected":"Bağlantı yok",
                UiLanguage.English
                    ?"Device not connected; no battery percent cached."
                    :"Cihaz bağlı değil; eski pil yüzdesi gösterilmiyor.");
            grid.Rows[row].Tag=new DisconnectedTracked(id,savedName);
        }
        if(found.Length>0)
        {
            int section=grid.Rows.Add(
                UiLanguage.English
                    ?$"FOUND DEVICES ({found.Length})"
                    :$"BULUNAN CİHAZLAR ({found.Length})",
                "",UiLanguage.English?"Connected / detected":
                "Bağlı / tespit edildi",
                UiLanguage.English
                    ?"Right click: track / icon / hide"
                    :"Sağ tık: takip / ikon / gizle");
            grid.Rows[section].ReadOnly=true;
            grid.Rows[section].DefaultCellStyle.BackColor=
                Color.FromArgb(36,57,71);
            grid.Rows[section].DefaultCellStyle.ForeColor=
                Color.FromArgb(117,214,220);
        }
        foreach(var device in found)
        {
            int row=grid.Rows.Add(device.Name,
                device.BatteryDisplay,device.BatterySource,
                device.Detail);
            grid.Rows[row].Tag=device;
        }
        foreach(var kind in kinds)
            if(!priority.Items.Cast<string>().Any(k=>
                k.Equals(kind,StringComparison.OrdinalIgnoreCase)))
                priority.Items.Add(kind);
        for(int i=0;i<kinds.Length;i++)
        {
            string kind=kinds[i];
            var reading=snap[kind];
            grid.Rows[i].Cells[0].Value=BatteryPriority.DisplayName(kind,snap);
            grid.Rows[i].Cells[1].Value=reading.Display;
            grid.Rows[i].Cells[2].Value=UiLanguage.T(reading.Source);
            grid.Rows[i].Cells[3].Value=UiLanguage.T(reading.Detail);
        }
        lastUpdated.Text=snap.CheckedAt==DateTimeOffset.MinValue
            ?UiLanguage.T("Tarama bekleniyor"):
            UiLanguage.T("Son okuma: ")+snap.CheckedAt.ToLocalTime().ToString("HH:mm:ss");
        ShowActive();
        int verified=kinds.Count(k=>BatteryPriority.IsCurrent(snap[k],
            DateTimeOffset.Now));
        int foundCount=grid.Rows.Cast<DataGridViewRow>()
            .Count(r=>r.Tag is ConnectedPeripheral);
        summary.Text=UiLanguage.English
            ?$"{verified} verified live · "+
                $"{kinds.Length+trackedIds.Count} tracked · "+
                $"{found.Length} discovered"
            :$"{verified} canlı pil · "+
                $"{kinds.Length+trackedIds.Count} takipte · "+
                $"{found.Length} bulunan cihaz";
        summary.ForeColor=verified>0
            ?Color.FromArgb(108,211,197)
            :Color.FromArgb(152,182,200);
        foreach(DataGridViewRow row in grid.Rows)
            foreach(DataGridViewCell cell in row.Cells)
                cell.ToolTipText=cell.Value?.ToString()??"";
        priority.Invalidate(); // redraw current PC device names after each scan
    }
}

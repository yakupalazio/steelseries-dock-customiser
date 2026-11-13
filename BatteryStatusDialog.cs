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
                    pref.LowBatteryThresholds??=new();return pref;}
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
            try{await refresh();UpdateRows();}finally{now.Enabled=true;}
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
                ?"Sources: SteelSeries GG, connected Bluetooth GATT, "+
                  "standard HID, optional vendor SDKs and Phone Link. "+
                  "Only verified live values appear on the OLED."
                :"Veri kaynakları: GG, bağlı Bluetooth, standart HID, "+
                  "isteğe bağlı üretici SDK'ları ve Phone Link. "+
                  "OLED'de yalnızca doğrulanmış güncel yüzdeler gösterilir.",
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
        ApplyLanguage();
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
    public void UpdateRows()
    {
        if(IsDisposed)return;
        var snap=snapshotGetter();
        var kinds=BatteryPriority.DefaultKinds.Concat(snap.Values
            .Where(pair=>pair.Value.Available &&
                BatteryPriority.IsCurrent(pair.Value,DateTimeOffset.Now))
            .Select(pair=>pair.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(grid.Rows.Count!=kinds.Length ||
           kinds.Where((kind,i)=>i<grid.Rows.Count &&
               !string.Equals(grid.Rows[i].Tag as string,kind,
                   StringComparison.OrdinalIgnoreCase)).Any())
        {
            grid.Rows.Clear();
            foreach(var kind in kinds)
            {
                int row=grid.Rows.Add(BatteryPriority.DisplayName(kind,snap),"--%","","");
                grid.Rows[row].Tag=kind;
            }
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
        summary.Text=UiLanguage.English
            ?$"{verified} verified live battery sources · {kinds.Length} tracked slots"
            :$"{verified} doğrulanmış canlı pil · {kinds.Length} takip edilen yuva";
        summary.ForeColor=verified>0
            ?Color.FromArgb(108,211,197)
            :Color.FromArgb(152,182,200);
        foreach(DataGridViewRow row in grid.Rows)
            foreach(DataGridViewCell cell in row.Cells)
                cell.ToolTipText=cell.Value?.ToString()??"";
        priority.Invalidate(); // redraw current PC device names after each scan
    }
}

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
    public bool AutoMinimizePhoneLink {get;set;}=false;
    public static string FilePath=>Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","batteries-v098.json");

    public static BatteryPreferences Load()
    {
        try
        {
            if(File.Exists(FilePath))
            {
                var pref=JsonSerializer.Deserialize<BatteryPreferences>(File.ReadAllText(FilePath));
                if(pref!=null){pref.PriorityKinds=BatteryPriority.Sanitize(pref.PriorityKinds);return pref;}
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
    private readonly ListBox priority=new(){Left=17,Top=292,Width=355,Height=164};
    private readonly Label active=new(){Left=402,Top=329,Width=450,Height=73};
    private readonly Label lastUpdated=new(){Left=150,Top=234,Width=700,Height=27};
    private readonly CheckBox dualSense=new()
    {
        Left=403,Top=412,Width=450,Text="DualSense bağlandığında otomatik öne çıkar"
    };
    private readonly CheckBox experimental=new()
    {
        Left=17,Top=482,Width=790,Text="Deneysel DualSense HID pil okuması (salt-okunur)"
    };
    private readonly CheckBox adb=new()
    {
        Left=17,Top=509,Width=790,Text="Android ADB pil kaynağı (isteğe bağlı)"
    };
    private readonly CheckBox minimizePhoneLink=new()
    {
        Left=17,Top=536,Width=790,
        Text="Phone Link'i gerekirse açıp küçült (pencereyi kapalı tutmaya alternatif)"
    };

    public BatteryStatusDialog(BatteryPreferences preferences,
        Func<BatterySnapshot> getSnapshot,Func<Task> update,Action apply)
    {
        prefs=preferences;snapshotGetter=getSnapshot;refresh=update;onApply=apply;
        Text="Pil cihazları — öncelik sıralaması";
        StartPosition=FormStartPosition.CenterParent;
        ClientSize=new Size(900,728);
        MinimumSize=new Size(900,728);
        AutoScroll=true;
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
            grid.Rows.Add(BatteryPriority.DisplayName(kind),"--%","","");

        var now=new Button{Left=17,Top=232,Width=125,Height=31,Text="Şimdi yenile"};
        now.Click+=async (_,_)=>{
            now.Enabled=false;
            try{await refresh();UpdateRows();}finally{now.Enabled=true;}
        };
        Controls.Add(now);Controls.Add(lastUpdated);
        Controls.Add(new Label{
            Left=17,Top=271,Width=360,Height=23,
            Text="Öncelik sırası (yukarıdaki üç uygun cihaz):"
        });
        Controls.Add(priority);
        foreach(var kind in BatteryPriority.Sanitize(prefs.PriorityKinds))
            priority.Items.Add(kind);
        priority.SelectedIndex=0;
        var up=new Button{Left=382,Top=294,Width=76,Height=32,Text="Yukarı"};
        var down=new Button{Left=382,Top=334,Width=76,Height=32,Text="Aşağı"};
        // Keep the priority controls separate from other device editing.
        up.Click+=(_,_)=>MovePriority(-1);
        down.Click+=(_,_)=>MovePriority(1);
        Controls.Add(up);Controls.Add(down);
        active.Left=470;active.Top=290;active.Width=384;
        Controls.Add(active);
        dualSense.Checked=prefs.AutoDualSense;
        dualSense.Left=403;dualSense.Top=403;
        Controls.Add(dualSense);
        Controls.Add(new Label{
            Left=17,Top=459,Width=834,Height=22,
            Text="Telefon ve DualSense okumaları 60 saniyede bir; GG/laptop her saniye.",
            ForeColor=Color.DarkSlateGray
        });
        experimental.Checked=prefs.ExperimentalControllerHid;
        adb.Checked=prefs.UseAdbPhoneBattery;
        minimizePhoneLink.Checked=prefs.AutoMinimizePhoneLink;
        Controls.Add(experimental);Controls.Add(adb);Controls.Add(minimizePhoneLink);
        Controls.Add(new Label{
            Left=17,Top=569,Width=832,Height=42,
            Text="Phone Link veri kaynağı: yalnızca pil durumunun erişilebilirlik metni. "+
                "Arka plan servisi doğrudan pil API'si sunmuyor. Açıp küçültme isteğe bağlıdır; "+
                "telefon verilerine veya mesajlara erişilmez.",
            ForeColor=Color.DarkSlateGray
        });
        var applyButton=new Button{Left=649,Top=617,Width=207,Height=32,
            Text="Öncelikleri uygula"};
        applyButton.Click+=(_,_)=>{
            prefs.PriorityKinds=priority.Items.Cast<string>().ToArray();
            prefs.AutoDualSense=dualSense.Checked;
            prefs.ExperimentalControllerHid=experimental.Checked;
            prefs.UseAdbPhoneBattery=adb.Checked;
            prefs.AutoMinimizePhoneLink=minimizePhoneLink.Checked;
            prefs.Save();
            onApply();
            if(TopLevel)Close();
            else lastUpdated.Text="Ayarlar kaydedildi · Pil verileri yenileniyor";
        };
        Controls.Add(applyButton);
        priority.SelectedIndexChanged+=(_,_)=>ShowActive();
        dualSense.CheckedChanged+=(_,_)=>ShowActive();
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
                button.BackColor=button.Text=="Öncelikleri uygula"
                    ?Color.FromArgb(72,194,238):Color.FromArgb(35,47,62);
                button.ForeColor=button.Text=="Öncelikleri uygula"
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
            Text="PİL VE ÖNCELİK",Left=18,Top=13,Width=850,Height=31,
            Font=new Font("Segoe UI Semibold",18f,FontStyle.Bold),
            ForeColor=Color.FromArgb(235,242,248),BackColor=BackColor
        });
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
            PriorityKinds=priority.Items.Cast<string>().ToArray(),
            AutoDualSense=dualSense.Checked
        };
        string[] display=BatteryPriority.SelectThree(snapshotGetter(),local);
        active.Text="Şu anda seçilenler:\n"+string.Join("  ·  ",
            display.Select(BatteryPriority.DisplayName));
    }
    public void UpdateRows()
    {
        if(IsDisposed)return;
        var snap=snapshotGetter();
        for(int i=0;i<BatteryPriority.AllKinds.Length;i++)
        {
            string kind=BatteryPriority.AllKinds[i];
            var reading=snap[kind];
            grid.Rows[i].Cells[1].Value=reading.Display;
            grid.Rows[i].Cells[2].Value=reading.Source;
            grid.Rows[i].Cells[3].Value=reading.Detail;
        }
        lastUpdated.Text=snap.CheckedAt==DateTimeOffset.MinValue
            ?"Tarama bekleniyor":"Son okuma: "+snap.CheckedAt.ToLocalTime().ToString("HH:mm:ss");
        ShowActive();
    }
}

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

internal sealed class BatteryPreferences
{
    public string[] DisplayDevices {get;set;}=["Mouse","Phone","DualSense"];
    public bool ExperimentalControllerHid {get;set;}=false;
    public bool UseAdbPhoneBattery {get;set;}=false;
    public static string FilePath=>Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","batteries-v09.json");

    public static BatteryPreferences Load()
    {
        try
        {
            var pref=JsonSerializer.Deserialize<BatteryPreferences>(File.ReadAllText(FilePath));
            if(pref is not null && pref.DisplayDevices.Length==3 &&
                pref.DisplayDevices.All(x=>BatterySources.Kinds.Contains(x)))
                return pref;
        }
        catch {}
        return new BatteryPreferences();
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
    }
}
internal sealed class BatteryStatusDialog : Form
{
    private readonly BatteryPreferences prefs;
    private readonly Func<BatterySnapshot> snapshotGetter;
    private readonly Func<System.Threading.Tasks.Task> forceRefresh;
    private readonly Action<string[]> apply;
    private readonly DataGridView grid = new()
    {
        Dock=DockStyle.Top,Height=252,ReadOnly=true,AllowUserToAddRows=false,
        AllowUserToDeleteRows=false,AllowUserToResizeRows=false,MultiSelect=false,
        SelectionMode=DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible=false,BackgroundColor=Color.White
    };
    private readonly ComboBox[] choices = [new(),new(),new()];
    private readonly Label lastUpdate=new(){AutoSize=true,Text="Henüz tarama yapılmadı."};
    private readonly CheckBox experimental=new(){AutoSize=true,
        Text="Deneysel: DualSense Bluetooth HID'yi salt-okunur sorgula"};
    private readonly CheckBox adbPhone=new(){AutoSize=true,
        Text="Android ADB ile telefon pilini oku (yalnızca kullanıcının eşlediği cihaz)"};
    public BatteryStatusDialog(BatteryPreferences preferences,Func<BatterySnapshot> getSnapshot,
        Func<System.Threading.Tasks.Task> update,Action<string[]> applyChoices)
    {
        prefs=preferences;snapshotGetter=getSnapshot;forceRefresh=update;apply=applyChoices;
        Text="Cihaz bataryaları — yalnızca doğrulanmış değerler";
        StartPosition=FormStartPosition.CenterParent;
        ClientSize=new Size(820,580);
        MinimumSize=new Size(820,615);
        Font=new Font("Segoe UI",10);
        Controls.Add(grid);
        grid.Columns.Add("kind","Cihaz");
        grid.Columns.Add("battery","Pil");
        grid.Columns.Add("source","Gerçek kaynak");
        grid.Columns.Add("detail","Durum");
        grid.Columns[0].FillWeight=68;
        grid.Columns[1].FillWeight=45;
        grid.Columns[2].FillWeight=100;
        grid.Columns[3].FillWeight=165;
        foreach(string kind in BatterySources.Kinds)grid.Rows.Add(kind,"--%","Taranıyor","...");
        var controls=new FlowLayoutPanel { Left=16,Top=266,Width=780,Height=45 };
        var refresh=new Button{Text="Şimdi yenile",Width=135,Height=32};
        refresh.Click+=async (_,_)=>{refresh.Enabled=false;try{await forceRefresh();UpdateRows();}finally{refresh.Enabled=true;}};
        controls.Controls.Add(refresh);controls.Controls.Add(lastUpdate);
        Controls.Add(controls);

        Controls.Add(new Label{Left=16,Top=316,Width=780,Height=24,
            Text="Dock üzerinde göstermek istediğin 3 cihazı seç:"});
        var selectors=new FlowLayoutPanel {Left=16,Top=344,Width=780,Height=43};
        for(int i=0;i<choices.Length;i++)
        {
            var choice=choices[i];choice.Width=178;choice.DropDownStyle=ComboBoxStyle.DropDownList;
            choice.Items.AddRange(BatterySources.Kinds);
            choice.SelectedItem=prefs.DisplayDevices[i];
            selectors.Controls.Add(choice);
        }
        Controls.Add(selectors);
        experimental.Left=16;experimental.Top=391;experimental.Width=730;
        experimental.Checked=prefs.ExperimentalControllerHid;
        experimental.Enabled=true;
        experimental.CheckedChanged+=async (_,_)=>{
            prefs.ExperimentalControllerHid=experimental.Checked;
            prefs.Save();
            await forceRefresh();
            UpdateRows();
        };
        Controls.Add(experimental);
        adbPhone.Left=16;adbPhone.Top=420;adbPhone.Width=776;
        adbPhone.Checked=prefs.UseAdbPhoneBattery;
        adbPhone.CheckedChanged+=async (_,_)=>
        {
            prefs.UseAdbPhoneBattery=adbPhone.Checked;
            prefs.Save();
            await forceRefresh();
            UpdateRows();
        };
        Controls.Add(adbPhone);
        var instructions=new Label{Left=16,Top=450,Width=780,Height=67,
            Text="ADB: S24+ üzerinde Kablosuz hata ayıklama ve eşleştirme açık olmalı. "+
                 "Google Platform-Tools dosyasını kurup yerel olarak eşleştir. "+
                 "Eşleme yoksa Phone Link kullanılabilir. ADB sadece pil bilgisi okur. "+
                 "Eski Bluetooth pil önbelleği hiçbir zaman canlı kaynak sayılmaz.",
            ForeColor=Color.DarkSlateGray};
        Controls.Add(instructions);
        var applyButton=new Button { Left=585,Top=525,Width=208,Height=34,Text="3 cihazı uygula" };
        applyButton.Click+=(_,_)=>{
            var selected=choices.Select(c=>c.SelectedItem?.ToString()??"").ToArray();
            if(selected.Distinct().Count()!=3){MessageBox.Show("Üç farklı cihaz seç.","Geçersiz seçim");return;}
            prefs.DisplayDevices=selected;
            prefs.Save();
            apply(selected);
            Close();
        };
        Controls.Add(applyButton);
        UpdateRows();
    }

    public void UpdateRows()
    {
        if(IsDisposed)return;
        var s=snapshotGetter();
        for(int i=0;i<BatterySources.Kinds.Length;i++)
        {
            var r=s[BatterySources.Kinds[i]];
            grid.Rows[i].Cells[1].Value=r.Display;
            grid.Rows[i].Cells[2].Value=r.Source;
            grid.Rows[i].Cells[3].Value=r.Detail;
        }
        lastUpdate.Text=s.CheckedAt==DateTimeOffset.MinValue
            ? "Tarama bekleniyor" : "Son okuma: "+s.CheckedAt.ToLocalTime().ToString("HH:mm:ss");
    }
}

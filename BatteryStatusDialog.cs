using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using System.Collections.Generic;

namespace SteelseriesDockCustomiser;

internal sealed class BatteryPreferences
{
    // Legacy array retained for old settings. v0.9.8 uses PriorityOrder instead.
    public string[] DisplayDevices {get;set;}=["Mouse","Laptop","Phone"];
    public string[] PriorityOrder {get;set;}=BatteryPriority.Defaults.ToArray();
    public bool DualSenseReplacesLaptop {get;set;}=true;
    public bool ExperimentalControllerHid {get;set;}=false;
    public bool UseAdbPhoneBattery {get;set;}=false; // kept off; debugging not required.
    public static string FilePath=>Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","batteries-v09.json");

    public static BatteryPreferences Load()
    {
        try
        {
            var prefs=JsonSerializer.Deserialize<BatteryPreferences>(File.ReadAllText(FilePath));
            if(prefs!=null)
            {
                prefs.PriorityOrder=BatteryPriority.Normalize(prefs.PriorityOrder);
                return prefs;
            }
        }catch{}
        return new BatteryPreferences();
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        PriorityOrder=BatteryPriority.Normalize(PriorityOrder);
        File.WriteAllText(FilePath,JsonSerializer.Serialize(this,
            new JsonSerializerOptions{WriteIndented=true}));
    }
}

internal sealed class BatteryStatusDialog : Form
{
    private readonly BatteryPreferences prefs;
    private readonly Func<BatterySnapshot> snapshot;
    private readonly Func<System.Threading.Tasks.Task> refresh;
    private readonly Action<string[]> apply;
    private readonly DataGridView grid=new(){
        Left=16,Top=42,Width=810,Height=220,ReadOnly=true,
        AllowUserToAddRows=false,AllowUserToDeleteRows=false,
        RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor=Color.White
    };
    private readonly ListBox priorities=new(){Left=18,Top=317,Width=375,Height=174};
    private readonly CheckBox special=new(){
        Left=18,Top=503,Width=800,Height=27,
        Text="DualSense bağlanınca laptop yerine göster (diğer cihazlarda geçerli değil)"};
    private readonly CheckBox hid=new(){
        Left=18,Top=538,Width=800,Height=27,
        Text="DualSense için deneysel salt-okunur HID raporunu kullan"};
    private readonly Label updated=new(){Left=18,Top=566,Width=800,Height=42,
        Text="Phone Link: yalnız doğrulanmış erişilebilir pil değeri kullanılır. " +
             "ADB ve USB hata ayıklama gerekmez. Telefon ve DualSense dakikada bir güncellenir.",
        ForeColor=Color.DarkSlateGray};
    private bool updating;

    public BatteryStatusDialog(BatteryPreferences preferences,
        Func<BatterySnapshot> getSnapshot,Func<System.Threading.Tasks.Task> refreshNow,
        Action<string[]> applyChoices)
    {
        prefs=preferences;snapshot=getSnapshot;refresh=refreshNow;apply=applyChoices;
        Text="Cihaz pilleri — öncelik sırası ve canlı kaynaklar";
        StartPosition=FormStartPosition.CenterParent;
        ClientSize=new Size(844,660);
        MinimumSize=new Size(862,700);
        Font=new Font("Segoe UI",10);
        Controls.Add(new Label{
            Left=18,Top=12,Width=790,Height=24,
            Text="En yüksek öncelikli 3 uygun cihaz dock ekranında gösterilir."
        });
        grid.Columns.Add("device","Cihaz");
        grid.Columns.Add("charge","Pil");
        grid.Columns.Add("source","Veri kaynağı");
        grid.Columns.Add("detail","Durum");
        grid.Columns[0].FillWeight=65;
        grid.Columns[1].FillWeight=38;
        grid.Columns[2].FillWeight=135;
        grid.Columns[3].FillWeight=185;
        foreach(var kind in BatterySources.Kinds)
            grid.Rows.Add(kind,"--%","Yok","...");
        Controls.Add(grid);
        Controls.Add(new Label{Left=18,Top=276,Width=670,Height=27,
            Text="Öncelik sırası (yukarı taşıyarak önceliği artır):"});
        Controls.Add(priorities);
        var up=new Button{Text="Yukarı ↑",Left=415,Top=325,Width=116,Height=36};
        var down=new Button{Text="Aşağı ↓",Left=415,Top=369,Width=116,Height=36};
        var reset=new Button{Text="Varsayılan sıra",Left=415,Top=418,Width=165,Height=35};
        up.Click+=(_,_)=>Shift(-1);
        down.Click+=(_,_)=>Shift(1);
        reset.Click+=(_,_)=>FillPriority(BatteryPriority.Defaults);
        Controls.AddRange([up,down,reset,special,hid,updated]);
        special.Checked=prefs.DualSenseReplacesLaptop;
        hid.Checked=prefs.ExperimentalControllerHid;
        var liveOrder=new Label{Left=583,Top=318,Width=236,Height=155,
            Text="Gösterilecek üç cihaz:",Font=new Font("Segoe UI",10,FontStyle.Bold)};
        Controls.Add(liveOrder);
        special.CheckedChanged+=(_,_)=>PreviewOrder();
        hid.CheckedChanged+=(_,_)=>PreviewOrder();
        void PreviewOrder()
        {
            var order=BatteryPriority.Resolve(snapshot(),ListPriority(),special.Checked);
            liveOrder.Text="Seçilen üç cihaz:\n\n"+string.Join("\n",order.Select((k,i)=>$"{i+1}. {k}"));
        }
        priorities.SelectedIndexChanged+=(_,_)=>PreviewOrder();
        var refreshButton=new Button{Left=18,Top=615,Width=138,Height=32,
            Text="Şimdi yenile"};
        refreshButton.Click+=async (_,_)=>{
            if(updating)return;
            updating=true;refreshButton.Enabled=false;
            try{await refresh();UpdateRows();PreviewOrder();}
            finally{updating=false;refreshButton.Enabled=true;}
        };
        var phoneButton=new Button{Left=170,Top=615,Width=322,Height=32,
            Text="Phone Link'i aç ve küçült (ADB gerekmez)"};
        phoneButton.Click+=async (_,_)=>{
            phoneButton.Enabled=false;
            try{
                updated.Text=await PhoneLinkLauncher.OpenMinimizedAsync();
                await refresh();UpdateRows();PreviewOrder();
            }
            finally{phoneButton.Enabled=true;}
        };
        Controls.Add(phoneButton);
        var saveButton=new Button{Left=623,Top=615,Width=197,Height=32,
            Text="Öncelikleri uygula"};
        saveButton.Click+=(_,_)=>{
            prefs.PriorityOrder=BatteryPriority.Normalize(ListPriority());
            prefs.DualSenseReplacesLaptop=special.Checked;
            prefs.ExperimentalControllerHid=hid.Checked;
            // ADB source is intentionally disabled by default unless already enabled.
            prefs.Save();
            apply(BatteryPriority.Resolve(snapshot(),prefs.PriorityOrder,
                prefs.DualSenseReplacesLaptop));
            Close();
        };
        Controls.AddRange([refreshButton,saveButton]);
        FillPriority(prefs.PriorityOrder);
        UpdateRows();
        PreviewOrder();
    }

    private void FillPriority(IEnumerable<string> order)
    {
        priorities.Items.Clear();
        foreach(var key in BatteryPriority.Normalize(order))priorities.Items.Add(key);
        if(priorities.Items.Count>0)priorities.SelectedIndex=0;
    }
    private string[] ListPriority()=>priorities.Items.Cast<string>().ToArray();
    private void Shift(int offset)
    {
        int i=priorities.SelectedIndex,j=i+offset;
        if(i<0||j<0||j>=priorities.Items.Count)return;
        object item=priorities.Items[i];
        priorities.Items.RemoveAt(i);
        priorities.Items.Insert(j,item);
        priorities.SelectedIndex=j;
    }

    public void UpdateRows()
    {
        if(IsDisposed)return;
        var result=snapshot();
        for(int i=0;i<BatterySources.Kinds.Length;i++)
        {
            var b=result[BatterySources.Kinds[i]];
            grid.Rows[i].Cells[1].Value=b.Display;
            grid.Rows[i].Cells[2].Value=b.Source;
            grid.Rows[i].Cells[3].Value=b.Detail;
        }
    }
}

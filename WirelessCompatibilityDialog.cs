using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Searchable, offline wireless product-name recognition catalog.
/// It explains what identification means, and never claims that every
/// recognized model exposes its battery percentage to Dock Customiser.
/// </summary>
internal sealed class WirelessCompatibilityDialog:Form
{
    private readonly DataGridView grid=new();
    private readonly TextBox search=new();
    private readonly Label status=new();
    private readonly ComboBox type=new();

    internal WirelessCompatibilityDialog()
    {
        UiPolish.Register(this);
        bool english=UiLanguage.English;
        Text=english?"Wireless device library":
            "Kablosuz cihaz kütüphanesi";
        ClientSize=new Size(1010,680);
        MinimumSize=new Size(770,540);
        StartPosition=FormStartPosition.CenterParent;
        BackColor=Color.FromArgb(13,18,26);
        ForeColor=Color.FromArgb(229,240,248);
        Font=new Font("Segoe UI",9.5f);

        var header=new Panel{
            Dock=DockStyle.Top,Height=158,BackColor=BackColor};
        var title=new Label{
            Left=22,Top=18,Width=955,Height=43,
            Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top,
            Font=new Font("Segoe UI Semibold",19,FontStyle.Bold),
            ForeColor=ForeColor,
            Text=english?"Wireless compatibility library":
                "Kablosuz cihaz uyumluluk kütüphanesi"};
        var explain=new Label{
            Left=23,Top=65,Width=955,Height=54,
            Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top,
            ForeColor=Color.FromArgb(157,179,196),
            Text=english
                ?"The catalog identifies known wireless models; it is not a list "+
                 "of guaranteed battery readers. Current percentage appears "+
                 "only through verified GG, Bluetooth GATT, standard HID or "+
                 "official SDK read-only providers."
                :"Kütüphane kablosuz cihaz modellerini tanır; listedeki her "+
                 "cihazın pilini okuyacağımız anlamına gelmez. Yüzdeler "+
                 "yalnızca GG, Bluetooth GATT, standart HID veya resmî "+
                 "SDK'dan canlı ve doğrulanmışsa gösterilir."};
        search.SetBounds(23,124,680,30);
        search.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;
        search.BackColor=Color.FromArgb(32,44,60);
        search.ForeColor=ForeColor;
        search.BorderStyle=BorderStyle.FixedSingle;
        search.PlaceholderText=english
            ?"Search brand or model (ATK, Beast X, Logitech...)":
            "Marka veya model ara (ATK, Beast X, Logitech...)";
        type.DropDownStyle=ComboBoxStyle.DropDownList;
        type.SetBounds(721,124,260,30);
        type.Anchor=AnchorStyles.Top|AnchorStyles.Right;
        type.BackColor=Color.FromArgb(32,44,60);
        type.ForeColor=ForeColor;
        type.Items.AddRange(english
            ?["All devices","Mouse","Keyboard","Headset","Controller"]
            :["Tüm cihazlar","Fare","Klavye","Kulaklık","Kontrolcü"]);
        type.SelectedIndex=0;
        header.Controls.AddRange([title,explain,search,type]);

        grid.Dock=DockStyle.Fill;
        grid.ReadOnly=true;
        grid.MultiSelect=false;
        grid.RowHeadersVisible=false;
        grid.AllowUserToAddRows=false;
        grid.AllowUserToDeleteRows=false;
        grid.AllowUserToResizeRows=false;
        grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        grid.BorderStyle=BorderStyle.None;
        grid.BackgroundColor=Color.FromArgb(22,30,42);
        grid.GridColor=Color.FromArgb(49,64,81);
        grid.EnableHeadersVisualStyles=false;
        grid.ColumnHeadersHeight=37;
        grid.RowTemplate.Height=33;
        grid.ColumnHeadersDefaultCellStyle.BackColor=
            Color.FromArgb(35,48,64);
        grid.ColumnHeadersDefaultCellStyle.ForeColor=ForeColor;
        grid.DefaultCellStyle.BackColor=Color.FromArgb(24,34,47);
        grid.DefaultCellStyle.ForeColor=ForeColor;
        grid.DefaultCellStyle.SelectionBackColor=
            Color.FromArgb(39,77,101);
        grid.DefaultCellStyle.SelectionForeColor=Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor=
            Color.FromArgb(29,41,56);
        grid.Columns.Add("brand",english?"Brand":"Marka");
        grid.Columns.Add("model",english?"Model":"Model");
        grid.Columns.Add("type",english?"Type":"Tür");
        grid.Columns.Add("reader",english?"Battery availability":"Pil okuma durumu");
        grid.Columns[0].FillWeight=67;
        grid.Columns[1].FillWeight=129;
        grid.Columns[2].FillWeight=59;
        grid.Columns[3].FillWeight=172;

        var footer=new Panel{
            Dock=DockStyle.Bottom,Height=94,BackColor=BackColor};
        status.Left=23;status.Top=14;status.Width=940;status.Height=28;
        status.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        status.ForeColor=Color.FromArgb(154,179,195);
        footer.Controls.Add(status);
        var source=new DarkButton{
            Text=english?"Battery sources & API status":
                "Pil kaynakları ve API durumları",
            Left=23,Top=49,Width=282,Height=36};
        source.Click+=(_,_)=>{
            using var dialog=new VendorBatterySourcesDialog();
            dialog.ShowDialog(this);
        };
        footer.Controls.Add(source);
        var close=new DarkButton{
            Text=english?"Close":"Kapat",
            Top=49,Width=125,Height=36,
            Anchor=AnchorStyles.Top|AnchorStyles.Right};
        footer.Resize+=(_,_)=>close.Left=Math.Max(320,footer.Width-149);
        close.Left=ClientSize.Width-149;
        close.Click+=(_,_)=>Close();
        footer.Controls.Add(close);
        Controls.Add(grid);
        Controls.Add(footer);
        Controls.Add(header);
        search.TextChanged+=(_,_)=>RefreshRows();
        type.SelectedIndexChanged+=(_,_)=>RefreshRows();
        RefreshRows();
    }

    private void RefreshRows()
    {
        string phrase=WirelessCompatibilityCatalog.Normalize(search.Text);
        int filter=type.SelectedIndex;
        bool en=UiLanguage.English;
        grid.SuspendLayout();
        grid.Rows.Clear();
        var matches=WirelessCompatibilityCatalog.All.Where(item=>
            (filter==0||item.Category==new[]{
                "","Mouse","Keyboard","Headset","Controller"}[filter])&&
            (phrase.Length==0||
                WirelessCompatibilityCatalog.Normalize(
                    item.Brand+" "+item.Model).Contains(phrase,
                        StringComparison.Ordinal)))
            .ToArray();
        foreach(var item in matches)
        {
            string statusText=item.Brand switch{
                "SteelSeries"=>en
                    ?"GG / Bluetooth: depends on verified device":
                    "GG / Bluetooth: doğrulanan cihaza bağlı",
                "Corsair"=>en
                    ?"iCUE SDK: supported models only":
                    "iCUE SDK: yalnızca desteklenen modeller",
                _=>en
                    ?"Recognition only — battery reader not verified":
                    "Model tanıma; pil okuyucusu henüz doğrulanmadı"
            };
            string typeName=item.Category switch{
                "Mouse"=>en?"Mouse":"Fare",
                "Keyboard"=>en?"Keyboard":"Klavye",
                "Headset"=>en?"Headset":"Kulaklık",
                "Controller"=>en?"Controller":"Kontrolcü",
                _=>item.Category
            };
            grid.Rows.Add(item.Brand,item.Model,typeName,statusText);
        }
        grid.ResumeLayout();
        status.Text=en
            ?$"{matches.Length} shown · "+
             $"{WirelessCompatibilityCatalog.ModelCount} wireless models · "+
             $"{WirelessCompatibilityCatalog.BrandCount} brands · offline data"
            :$"{matches.Length} gösteriliyor · "+
             $"{WirelessCompatibilityCatalog.ModelCount} kablosuz model · "+
             $"{WirelessCompatibilityCatalog.BrandCount} marka · çevrimdışı kütüphane";
    }
}

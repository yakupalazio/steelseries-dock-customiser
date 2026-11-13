using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Read-only, automatic discovery: Bluetooth classic, Bluetooth LE, USB/dock.
/// An undisclosed battery protocol is not a connection failure. Only verified
/// battery readings can enter the separate OLED priority system.
/// </summary>
internal sealed class ConnectedDevicesDialog:Form
{
    private readonly Func<BatterySnapshot> getSnapshot;
    private readonly DataGridView table=new();
    private readonly TextBox filter=new();
    private readonly Label status=new();
    private readonly Button refreshButton=new();
    private readonly System.Windows.Forms.Timer timer=new(){Interval=20000};
    private IReadOnlyList<ConnectedPeripheral> detected=[];
    private bool busy;
    internal IReadOnlyList<ConnectedPeripheral> Latest=>detected;

    public ConnectedDevicesDialog(Func<BatterySnapshot> snapshotGetter)
    {
        UiPolish.Register(this);
        getSnapshot=snapshotGetter;
        Text=UiLanguage.English?"Connected peripherals":"Bağlı cihazlar";
        StartPosition=FormStartPosition.CenterParent;
        ClientSize=new Size(1020,670);
        MinimumSize=new Size(800,520);
        Font=new Font("Segoe UI",9.5f);
        BackColor=Color.FromArgb(13,18,26);
        ForeColor=Color.FromArgb(230,240,248);
        var header=new Label{
            Dock=DockStyle.Top,Height=60,Padding=new Padding(20,13,0,0),
            ForeColor=ForeColor,
            Font=new Font("Segoe UI Semibold",17,FontStyle.Bold),
            Text=UiLanguage.English?"Connected peripherals / USB & Bluetooth":
                "Bağlı cihazlar / USB ve Bluetooth"
        };
        var note=new Label{
            Dock=DockStyle.Top,Height=52,Padding=new Padding(20,0,20,0),
            ForeColor=Color.FromArgb(163,179,194),
            Text=UiLanguage.English
                ?"All connected devices are listed. Only proven, fresh battery data is shown on OLED."
                :"Bağlı cihazlar otomatik keşfedilir. OLED'e yalnızca doğrulanmış güncel pil değeri eklenir."
        };
        var toolbar=new Panel{Dock=DockStyle.Top,Height=55,
            Padding=new Padding(18,7,18,7)};
        filter.PlaceholderText=UiLanguage.English?
            "Search by name, brand, type...":"Ad, marka veya bağlantı türüne göre ara...";
        var searchLabel=new Label{
            Text=UiLanguage.English?"Search:":"Ara:",
            Left=19,Top=15,Width=54,Height=27,
            ForeColor=Color.FromArgb(180,196,210)
        };
        filter.Left=76;filter.Top=9;filter.Width=554;
        filter.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;
        filter.BackColor=Color.FromArgb(32,43,59);
        filter.ForeColor=Color.White;
        filter.BorderStyle=BorderStyle.FixedSingle;
        filter.TextChanged+=(_,_)=>DrawRows();
        refreshButton.Text=UiLanguage.English?"Refresh":"Yenile";
        refreshButton.SetBounds(800,7,130,36);
        refreshButton.Anchor=AnchorStyles.Top|AnchorStyles.Right;
        refreshButton.FlatStyle=FlatStyle.Flat;
        refreshButton.FlatAppearance.BorderSize=0;
        refreshButton.BackColor=Color.FromArgb(49,119,139);
        refreshButton.ForeColor=Color.White;
        refreshButton.Click+=async (_,_)=>await RefreshAsync();
        toolbar.Controls.Add(searchLabel);
        toolbar.Controls.Add(filter);toolbar.Controls.Add(refreshButton);
        toolbar.Resize+=(_,_)=>{
            filter.Width=Math.Max(160,toolbar.ClientSize.Width-265);
            refreshButton.Left=Math.Max(182,toolbar.ClientSize.Width-148);
        };

        table.Dock=DockStyle.Fill;
        table.ReadOnly=true;
        table.MultiSelect=false;
        table.AllowUserToAddRows=false;
        table.AllowUserToDeleteRows=false;
        table.AllowUserToResizeRows=false;
        table.RowHeadersVisible=false;
        table.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
        table.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        table.BackgroundColor=Color.FromArgb(21,29,40);
        table.GridColor=Color.FromArgb(49,61,78);
        table.EnableHeadersVisualStyles=false;
        table.ColumnHeadersHeight=41;
        table.RowTemplate.Height=34;
        table.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(31,43,59);
        table.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
        table.DefaultCellStyle.BackColor=Color.FromArgb(22,32,46);
        table.DefaultCellStyle.ForeColor=Color.FromArgb(228,238,248);
        table.DefaultCellStyle.SelectionBackColor=Color.FromArgb(50,83,104);
        table.DefaultCellStyle.SelectionForeColor=Color.White;
        table.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(28,38,52);
        table.Columns.Add("name",UiLanguage.English?"Device":"Cihaz");
        table.Columns.Add("connection",UiLanguage.English?"Connection":"Bağlantı");
        table.Columns.Add("battery",UiLanguage.English?"Battery":"Pil");
        table.Columns.Add("source",UiLanguage.English?"Battery source":"Pil kaynağı");
        table.Columns.Add("status",UiLanguage.English?"Status":"Durum");
        table.Columns[0].FillWeight=150;
        table.Columns[1].FillWeight=72;
        table.Columns[2].FillWeight=42;
        table.Columns[3].FillWeight=120;
        table.Columns[4].FillWeight=160;

        var bottom=new Panel{Dock=DockStyle.Bottom,Height=58};
        status.Dock=DockStyle.Fill;status.Padding=new Padding(18,12,14,0);
        status.ForeColor=Color.FromArgb(158,179,197);
        status.AutoEllipsis=true;
        bottom.Controls.Add(status);
        Controls.Add(table);
        Controls.Add(bottom);
        Controls.Add(toolbar);
        Controls.Add(note);
        Controls.Add(header);
        Shown+=async (_,_)=>await RefreshAsync();
        timer.Tick+=async (_,_)=>await RefreshAsync();
        timer.Start();
        FormClosed+=(_,_)=>{timer.Stop();timer.Dispose();};
    }

    private async Task RefreshAsync()
    {
        if(busy||IsDisposed)return;
        busy=true;
        refreshButton.Enabled=false;
        status.Text=UiLanguage.English?
            "Checking Windows Bluetooth and USB devices...":
            "Windows Bluetooth ve USB cihazları taranıyor...";
        try
        {
            var newest=await ConnectedPeripheralInventory.DiscoverAsync(getSnapshot());
            if(IsDisposed)return;
            detected=newest;
            DrawRows();
        }
        catch(Exception ex)
        {
            if(!IsDisposed)status.Text=UiLanguage.English?
                "Device scan failed: "+ex.GetType().Name:
                "Cihaz taraması başarısız: "+ex.GetType().Name;
        }
        finally
        {
            busy=false;
            if(!IsDisposed)refreshButton.Enabled=true;
        }
    }

    private void DrawRows()
    {
        if(IsDisposed)return;
        var q=filter.Text.Trim();
        var selected=detected.Where(d=>q.Length==0 ||
            d.Name.Contains(q,StringComparison.OrdinalIgnoreCase) ||
            d.Connection.Contains(q,StringComparison.OrdinalIgnoreCase) ||
            d.Manufacturer.Contains(q,StringComparison.OrdinalIgnoreCase));
        table.Rows.Clear();
        foreach(var device in selected)
        {
            int idx=table.Rows.Add(device.Name,device.Connection,
                device.BatteryDisplay,device.BatterySource,
                device.Detail);
            var row=table.Rows[idx];
            row.Tag=device.Id;
            foreach(DataGridViewCell cell in row.Cells)
                cell.ToolTipText=device.Manufacturer.Length>0
                    ?device.Manufacturer+" | "+device.Detail:device.Detail;
            if(device.Percent is int)
                row.Cells[2].Style.ForeColor=Color.FromArgb(90,216,160);
        }
        int known=detected.Count(d=>d.Percent.HasValue);
        status.Text=UiLanguage.English
            ?$"{detected.Count} connected peripherals · {known} verified battery readings · Auto-refresh: 20 s"
            :$"{detected.Count} bağlı cihaz · {known} doğrulanmış pil değeri · Otomatik yenileme: 20 sn";
    }
}

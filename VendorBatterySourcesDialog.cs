using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Truthful integration inventory: an RGB SDK is not a battery API.
/// This is documentation and diagnostics, not a list of pretend providers.
/// </summary>
internal sealed class VendorBatterySourcesDialog:Form
{
    internal sealed record Vendor(string Name,string Method,
        string State,string Details);

    internal static IReadOnlyList<Vendor> Entries(bool english)
    {
        bool corsair=CorsairIcueBatteryProvider.HasOfficialSdkFile;
        if(english)
            return [
                new("SteelSeries GG","Local Engine API",
                    "Built in","Nova Pro headset/spare status from verified GG"),
                new("Windows game controllers","Windows.Gaming.Input BatteryReport",
                    "Built in; device support varies",
                    "Read-only capacity for connected controllers and headsets "+
                    "when the Windows driver provides a valid live report"),
                new("ATK / VXE / Pulsar","CompX 0x04 battery-status HID",
                    "Built in for exact receiver IDs; hardware varies",
                    "27 positive-listed USB IDs, FF02:0002; mouse must be "+
                    "awake. No generic vendor-ID probing or configuration."),
                new("Corsair iCUE","Official SDK BatteryLevel",
                    corsair?"SDK library found":"Optional SDK file required",
                    "iCUE 4.31+; official x64 SDK, read-only, supported devices"),
                new("Logitech G HUB","HID++ / BLE / standard HID",
                    "Standard sources; model protocol pending",
                    "Public G HUB integrations cover lighting, keys and wheels, "+
                    "not a general battery API"),
                new("Razer Synapse","BLE / standard HID",
                    "Model protocol pending",
                    "Chroma SDK is for lighting; no verified public battery API"),
                new("ASUS Armoury Crate","BLE / standard HID",
                    "Model protocol pending",
                    "Aura SDK is for lighting; no verified battery API"),
                new("HyperX NGENUITY","BLE / standard HID",
                    "Model protocol pending",
                    "No public generic battery API confirmed; some HID "+
                    "devices have documented model-specific protocols"),
                new("WLMOUSE","Read-only receiver 0x83 / BLE / HID",
                    "Built in for exact allowlisted receivers",
                    "Only documented firmware report shapes; model-specific "+
                    "support varies and missing percent stays unknown"),
                new("Other brands","BLE GATT / USB HID Battery",
                    "Automatically checked",
                    "A live, verified percentage is required; no cached guess")
            ];
        return [
            new("SteelSeries GG","Yerel Engine API",
                "Yerleşik","Nova Pro ana ve yedek pil: doğrulanmış GG durumu"),
            new("Windows oyun kontrolcüleri","Windows.Gaming.Input pil raporu",
                "Yerleşik; cihaz sürücüsüne bağlı",
                "Windows'un bağlı kontrolcü veya kulaklık için verdiği "+
                "gerçek kapasite okunur; yoksa yüzde gösterilmez"),
            new("ATK / VXE / Pulsar","CompX 0x04 pil durumu HID",
                "Kesin alıcı kimliklerinde yerleşik; cihaz testi gerekli",
                "27 izinli USB kimliği, FF02:0002; fare uyanık olmalı. "+
                "Bilinmeyen cihaza HID komutu veya yapılandırma gönderilmez."),
            new("Corsair iCUE","Resmî SDK BatteryLevel",
                corsair?"SDK dosyası bulundu":"İsteğe bağlı SDK dosyası gerekli",
                "iCUE 4.31+; resmî x64 SDK, salt-okunur, uygun cihazlar"),
            new("Logitech G HUB","HID++ / BLE / standart HID",
                "Standart kaynaklar; model protokolü bekleniyor",
                "G HUB SDK'ları aydınlatma, tuş ve direksiyon odaklı; "+
                "genel pil API'si doğrulanmadı"),
            new("Razer Synapse","BLE / standart HID",
                "Model protokolü bekleniyor",
                "Chroma SDK aydınlatma içindir; doğrulanmış açık pil API'si yok"),
            new("ASUS Armoury Crate","BLE / standart HID",
                "Model protokolü bekleniyor",
                "Aura SDK aydınlatma içindir; doğrulanmış pil API'si yok"),
            new("HyperX NGENUITY","BLE / standart HID",
                "Model protokolü bekleniyor",
                "Genel pil API'si doğrulanmadı; bazı HID modelleri "+
                "için belgelenmiş özel protokoller mevcut"),
            new("WLMOUSE","Salt-okunur 0x83 / BLE / HID",
                "Kesin doğrulanmış alıcılarda yerleşik",
                "Yalnızca belgelenen firmware raporları; destek modele göre "+
                "değişir, bilinmeyen değerler tahmin edilmez"),
            new("Diğer markalar","BLE GATT / USB HID pil",
                "Otomatik kontrol ediliyor",
                "Yalnızca canlı, doğrulanmış yüzde gösterilir; önbellek tahmini yok")
        ];
    }

    public VendorBatterySourcesDialog()
    {
        UiPolish.Register(this);
        bool en=UiLanguage.English;
        Text=en?"Battery providers and brand compatibility":
            "Pil sağlayıcıları ve marka uyumluluğu";
        ClientSize=new Size(1040,650);
        MinimumSize=new Size(820,540);
        StartPosition=FormStartPosition.CenterParent;
        BackColor=Color.FromArgb(13,18,26);
        ForeColor=Color.FromArgb(235,243,249);
        Font=new Font("Segoe UI",9.5f);

        var title=new Label{
            Dock=DockStyle.Top,Height=66,
            Padding=new Padding(22,17,0,0),
            Font=new Font("Segoe UI Semibold",18,FontStyle.Bold),
            ForeColor=ForeColor,
            Text=en?"Battery sources by brand":
                "Markalara göre pil kaynakları"};
        var subtitle=new Label{
            Dock=DockStyle.Top,Height=79,
            Padding=new Padding(22,0,22,8),
            ForeColor=Color.FromArgb(153,179,194),
            Text=en
                ?"Only verified, live percentages are shown. An RGB SDK "+
                 "does not necessarily expose battery information. "+
                 "The app will not start vendor software, install drivers, "+
                 "or send configuration commands.\n"+
                 "SDK status means adapter readiness, not a guarantee "+
                 "that the device has a readable battery."
                :"Yalnızca canlı ve doğrulanmış yüzdeler gösterilir. "+
                 "RGB SDK'sı pil API'si demek değildir. Uygulama üretici "+
                 "yazılımını başlatmaz, sürücü kurmaz, yapılandırma komutu göndermez.\n"+
                 "SDK durumu, okuyucunun hazır olduğunu belirtir; "+
                 "her cihazda pil okunacağı garantisi değildir."};
        var table=new DataGridView{
            Dock=DockStyle.Fill,
            ReadOnly=true,
            AllowUserToAddRows=false,AllowUserToDeleteRows=false,
            MultiSelect=false,RowHeadersVisible=false,
            AllowUserToResizeRows=false,
            SelectionMode=DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor=Color.FromArgb(23,31,43),
            GridColor=Color.FromArgb(49,61,75),
            BorderStyle=BorderStyle.None,
            EnableHeadersVisualStyles=false,
            ColumnHeadersHeight=39,
            RowTemplate={Height=53}
        };
        table.ColumnHeadersDefaultCellStyle.BackColor=
            Color.FromArgb(34,47,62);
        table.ColumnHeadersDefaultCellStyle.ForeColor=ForeColor;
        table.DefaultCellStyle.BackColor=Color.FromArgb(24,34,47);
        table.DefaultCellStyle.ForeColor=ForeColor;
        table.DefaultCellStyle.SelectionBackColor=
            Color.FromArgb(42,76,95);
        table.DefaultCellStyle.SelectionForeColor=Color.White;
        table.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
        table.AlternatingRowsDefaultCellStyle.BackColor=
            Color.FromArgb(28,40,54);
        table.Columns.Add("brand",en?"Brand":"Marka");
        table.Columns.Add("provider",en?"Data source":"Veri kaynağı");
        table.Columns.Add("status",en?"Support state":"Destek durumu");
        table.Columns.Add("detail",en?"Details":"Açıklama");
        table.Columns[0].FillWeight=86;
        table.Columns[1].FillWeight=140;
        table.Columns[2].FillWeight=151;
        table.Columns[3].FillWeight=205;
        foreach(var row in Entries(en))
            table.Rows.Add(row.Name,row.Method,row.State,row.Details);

        var footer=new Panel{Dock=DockStyle.Bottom,Height=86,
            BackColor=BackColor};
        var note=new Label{
            Left=21,Top=5,Width=990,Height=30,
            ForeColor=Color.FromArgb(155,179,194),
            Text=en
                ?"Corsair SDK file (not included): VendorSDK/Corsair/iCUESDK.x64_2019.dll"
                :"Corsair SDK dosyası (pakete dahil değil): VendorSDK/Corsair/iCUESDK.x64_2019.dll"};
        var official=new DarkButton{
            Text=en?"Official Corsair SDK page":"Resmî Corsair SDK sayfası",
            Left=21,Top=41,Width=260,Height=35};
        official.Click+=(_,_)=>{
            try{Process.Start(new ProcessStartInfo(
                "https://github.com/CorsairOfficial/cue-sdk/releases")
                {UseShellExecute=true})?.Dispose();}
            catch{}
        };
        var close=new DarkButton{
            Text=en?"Close":"Kapat",
            Left=296,Top=41,Width=120,Height=35};
        close.Click+=(_,_)=>Close();
        footer.Controls.AddRange([note,official,close]);
        Controls.Add(table);
        Controls.Add(footer);
        Controls.Add(subtitle);
        Controls.Add(title);
    }
}

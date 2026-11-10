using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Persistent Turkish/English UI choice. Internal widget keys, JSON files and
/// GG/HID protocol strings remain language-neutral and never get translated.
/// </summary>
internal static class UiLanguage
{
    private sealed class Preference { public bool English {get;set;} }
    private sealed class Original { public string Text {get;set;}=""; }
    private static readonly ConditionalWeakTable<Control,Original> Origins=new();
    private static readonly string PathName=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","language.json");
    public static bool English {get;private set;}=ReadPreference();
    private static bool ReadPreference()
    {
        try{
            return File.Exists(PathName) &&
                JsonSerializer.Deserialize<Preference>(File.ReadAllText(PathName))?.English==true;
        }catch{return false;}
    }
    public static void Select(bool english)
    {
        English=english;
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
        File.WriteAllText(PathName,JsonSerializer.Serialize(new Preference{English=english}));
    }
    internal static void PreviewOnly(bool english)=>English=english;

    private static readonly Dictionary<string,string> Texts=new(StringComparer.Ordinal)
    {
        ["Ayarlar"]="Settings",["AYARLAR"]="SETTINGS",
        ["ⓘ   Hakkında"]="ⓘ   About",["HAKKINDA"]="ABOUT",
        ["◫   Pil ve öncelik"]="◫   Battery & priority",
        ["◉   Dil / Language"]="◉   Language",
        ["⚙   Ayarlar"]="⚙   Settings",["Kapat"]="Close",
        ["Ayarlar | Dock Customiser"]="Settings | Dock Customiser",
        ["DİL / LANGUAGE"]="LANGUAGE",["Dil seçimi"]="Language selection",
        ["Türkçe"]="Turkish",["İngilizce"]="English",
        ["Uygulama dili"]="App language",
        ["Değişiklik anında uygulanır ve sonraki açılışlarda korunur."]=
            "Changes apply immediately and persist across restarts.",
        ["SteelSeries Nova Pro Wireless için kişisel OLED stüdyosu"]=
            "Personal OLED studio for SteelSeries Nova Pro Wireless",
        ["SÜRÜM   1.1.0\nPLATFORM   Windows 11  ·  .NET 10\nEKRAN   128 × 64  ·  Monokrom OLED"]=
            "VERSION   1.1.0\nPLATFORM   Windows 11  ·  .NET 10\nDISPLAY   128 × 64  ·  Monochrome OLED",
        ["Tasarlayan ve geliştiren"]="Designed and developed by",
        ["© 2026 Yakup Can Palaz. Bağımsız bir proje; SteelSeries ile bağlantılı değildir."]=
            "© 2026 Yakup Can Palaz. Independent project; not affiliated with SteelSeries.",
        ["ÇALIŞMA MODU"]="DISPLAY MODE",["♫    Medya çalarken"]="♫    Media playing",
        ["▣    Oyun açıkken"]="▣    In game",["◷    Boştayken"]="◷    Idle",
        ["DÜZENLEME ARAÇLARI"]="EDITING TOOLS",
        ["♢   İkon atölyesi"]="♢   Icon workshop",
        ["◷   Saat ve tarih"]="◷   Clock & date",
        ["DURUM"]="STATUS",["Otomatik medya"]="Auto-detect media",
        ["Medya otomatik algılansın"]="Auto-detect media",
        ["WIDGET YÖNETİMİ"]="WIDGET MANAGER",
        ["Öğe ekle"]="Add widget",["+ Öğe ekle"]="+ Add widget",
        ["Seçileni sil"]="Remove selected",
        ["EKRANDAKİ ÖĞELER"]="ON-SCREEN WIDGETS",
        ["ÖĞE AYARLARI"]="WIDGET PROPERTIES",
        ["Seçtiğin öğenin özellikleri"]="Selected widget settings",
        ["Göster"]="Visible",["Görünür"]="Visible",
        ["Metin"]="Text",["Font"]="Font",
        ["Kalınlık"]="Stroke",["X konumu"]="X position",
        ["Y konumu"]="Y position",["Widget eni"]="Widget width",
        ["Gösterge tipi"]="Display style",["Dairesel tekerlek"]="Radial wheel",
        ["Düz çubuk"]="Flat bar",
        ["EKRAN KORUMASI"]="SCREEN PROTECTION",
        ["Orijinal üst 10 pikseli koru"]="Preserve top 10 OEM pixels",
        ["Kapalıysa tam 128×64 OLED alanını düzenlersin. Dock'a göndermeden önce onay istenir."]=
            "Off: edit the full 128×64 OLED. Confirmation is required before sending.",
        ["DOCK  /  CUSTOMISER"]="DOCK  /  CUSTOMISER",
        ["Kaydet"]="Save",["Tasarımı aç"]="Open layout",
        ["Özel tasarım"]="Custom layout",["Orijinal"]="Original",
        ["✓ Uygula"]="✓ Apply",["▶ Canlı"]="▶ Live",
        ["■ Durdur"]="■ Stop",["↻ Dock Reset"]="↻ Dock reset",
        ["PİKSEL EDİTÖRÜ  ·  128 × 64"]="PIXEL EDITOR  ·  128 × 64",
        ["Piksel ızgarası"]="Pixel grid",
        ["↶ Geri al"]="↶ Undo",["↷ İleri al"]="↷ Redo",
        ["Modu sıfırla"]="Reset mode",["Çizimi sil"]="Clear drawing",
        ["Medya tasarımı"]="Media layout",["Oyun tasarımı"]="Game layout",
        ["Boşta ekranı"]="Idle layout",
        ["Seçili moddaki her değişiklik dock üzerinde anında görünür."]=
            "Each change appears instantly on the dock preview.",
        ["Kalem"]="Pencil",["Silgi"]="Eraser",
        ["Taşı / Seç"]="Move / Select",
        ["Piller: "]="Batteries: ",
        ["BOŞTA"]="IDLE",["OYUN AKTİF"]="GAME ACTIVE",
        ["MEDYA AKTİF"]="MEDIA ACTIVE",
        ["Bilgisayarın durumu: "]="Computer state: ",
        ["Oyun: "]="Game: ",["Medya: "]="Media: ",
        ["Oyun veya medya bulunmadı."]="No game or media detected.",
        ["○ Dock canlı modu kapalı"]="○ Dock live mode off",
        ["● Dock üzerinde canlı mod açık"]="● Dock live mode on",
        ["üst 10 piksel korunuyor"]="top 10 pixels protected",
        ["tam ekran düzenleme"]="full-screen editing",
        ["SEÇİLİ  ·  "]="SELECTED  ·  ",
        ["Saat"]="Clock",["Tarih"]="Date",
        ["Kaynak ikonu"]="Source icon",["Şarkı adı"]="Track title",
        ["Sanatçı"]="Artist",["İlerleme çubuğu"]="Progress bar",
        ["Küçük medya saati"]="Small media clock",
        ["Oyun başlığı"]="Game heading",["Oyun adı"]="Game title",
        ["Mini Player (Oyun)"]="Mini player (Game)",
        ["Özel metin"]="Custom text",
        ["Batarya 1 · Otomatik"]="Battery 1 · Auto",
        ["Batarya 2 · Otomatik"]="Battery 2 · Auto",
        ["Batarya 3 · Otomatik"]="Battery 3 · Auto",
        ["Fare bataryası"]="Mouse battery",
        ["Telefon bataryası"]="Phone battery",
        ["Laptop bataryası"]="Laptop battery",
        ["Kulaklık bataryası"]="Headset battery",
        ["Yedek batarya"]="Spare battery",
        ["DualSense bataryası"]="DualSense battery",
        ["Ses / ChatMix tekerleği"]="Volume / ChatMix wheel",
        ["Nova Pro Kulaklık"]="Nova Pro Headset",
        ["Nova Pro Yedek Pil"]="Nova Pro Spare Battery",
        ["Pil cihazları — öncelik sıralaması"]="Battery devices — priority order",
        ["Cihaz"]="Device",["Pil"]="Battery",
        ["Doğrulanan kaynak"]="Verified source",["Durum"]="Status",
        ["Şimdi yenile"]="Refresh now",
        ["Öncelik sırası (yukarıdaki üç uygun cihaz):"]=
            "Priority order (first three eligible devices):",
        ["Yukarı"]="Up",["Aşağı"]="Down",
        ["DualSense bağlandığında otomatik öne çıkar"]=
            "Prioritize DualSense when connected",
        ["Telefon ve DualSense okumaları 60 saniyede bir; GG/laptop her saniye."]=
            "Phone and DualSense update every 60 s; GG/laptop every second.",
        ["Deneysel DualSense HID pil okuması (salt-okunur)"]=
            "Experimental DualSense HID battery reading (read-only)",
        ["Android ADB pil kaynağı (isteğe bağlı)"]=
            "Android ADB battery source (optional)",
        ["Phone Link'i gerekirse açıp küçült (pencereyi kapalı tutmaya alternatif)"]=
            "Launch and minimize Phone Link if needed (instead of keeping it closed)",
        ["Phone Link veri kaynağı: yalnızca pil durumunun erişilebilirlik metni. Arka plan servisi doğrudan pil API'si sunmuyor. Açıp küçültme isteğe bağlıdır; telefon verilerine veya mesajlara erişilmez."]=
            "Phone Link data source: accessible battery-status text only. Its background service has no direct battery API. Launch/minimize is optional; no messages or phone files are accessed.",
        ["Öncelikleri uygula"]="Apply priorities",
        ["PİL VE ÖNCELİK"]="BATTERY & PRIORITY",
        ["Şu anda seçilenler:\n"]="Currently selected:\n",
        ["Tarama bekleniyor"]="Waiting for scan",
        ["Son okuma: "]="Last read: ",
        ["Ayarlar kaydedildi · Pil verileri yenileniyor"]=
            "Settings saved · Refreshing battery data",
        ["Saat ve tarih — OLED kişiselleştirme"]="Clock and date — OLED customization",
        ["Saat ve tarihi bağımsız olarak düzenle"]=
            "Customize clock and date independently",
        ["Görünüm"]="Format",["Piksel kalınlığı"]="Pixel stroke",
        ["OLED'in doğal 128×64 görüntüsü (5× büyütülmüş) — saat/tarih modu"]=
            "Native 128×64 OLED preview (5× enlarged) — clock/date mode",
        ["Konum ve boyutu ana piksel editöründen değiştirebilirsin. Kalınlık 1, 2 veya 3 gerçek OLED pikselidir."]=
            "Adjust position/size in the pixel editor. Stroke thickness is 1, 2 or 3 actual OLED pixels.",
        ["İptal"]="Cancel",["Uygula"]="Apply",
        ["İkonları tek tek piksel piksel düzenle"]="Edit icons pixel by pixel",
        ["İkon Düzenleyici — Steelseries Dock Customiser v0.9"]=
            "Icon Editor — SteelSeries Dock Customiser",
        ["1× / 8× önizleme"]="1× / 8× preview",
        ["Genişlik:"]="Width:",["Yükseklik:"]="Height:",
        ["Kaydettiğinde ikonu hem editör hem de Dock Preview kullanır.\n\nDeğişiklikler yalnızca bilgisayarında tutulur."]=
            "Once saved, the icon is used in both the editor and dock preview.\n\nChanges are stored locally only.",
        ["Temizle"]="Clear",["Kopyala"]="Copy",
        ["Yapıştır"]="Paste",["Geri al"]="Undo",["İleri al"]="Redo",
        ["Sol tık: piksel aç  |  Sağ tık: sil  |  Ctrl+Z / Ctrl+Y: geri al / ileri al"]=
            "Left click: add pixel  |  Right click: erase  |  Ctrl+Z / Ctrl+Y: undo / redo",
        ["DOCK EKRANINA BAK 👀"]="LOOK AT YOUR DOCK 👀",
        ["▶ DOCK'TA CANLI OYNA"]="▶ PLAY ON DOCK",
        ["■ DURDUR / OLED RESET"]="■ STOP / OLED RESET",
        ["Oyun görüntüsü yalnızca dock üzerinde görünür · ESC güvenli çıkış"]=
            "Gameplay appears only on the dock · ESC exits safely",
        ["W/S hareket  •  A/D / oklar dön  •  SPACE ateş  •  R sıfırla  •  ESC çıkış"]=
            "W/S move  •  A/D / arrows turn  •  SPACE fire  •  R reset  •  ESC exit",
        ["DOCK CANLI · W/S hareket  A/D dön  SPACE ateş  R yeniden başla  ESC çıkış"]=
            "DOCK LIVE · W/S move  A/D turn  SPACE fire  R restart  ESC exit",
        ["Kaydedilmemiş ikon değişiklikleri var. Çıkılsın mı?"]=
            "There are unsaved icon changes. Exit anyway?",
        ["Kaydedilmemiş çizimi atlayıp başka ikona geçilsin mi?"]=
            "Discard unsaved pixels and edit another icon?",
        ["Orijinal sprite yüklendi. Kaydet ile değişikliği uygula."]=
            "Original sprite loaded. Save to apply the change.",
        ["Yapıştırıldı. Kaydet ile uygula."]=
            "Pasted. Save to apply.",
        ["Geçersiz 0/1 sprite matrisi."]="Invalid 0/1 sprite matrix.",
        ["Yapıştırma başarısız"]="Paste failed",
        ["Kaydetme hatası"]="Save error",
        ["İkon editörü"]="Icon editor",
        ["İkon düzenleyici"]="Icon editor",
        ["Oyun yalnızca Nova Pro Wireless OLED ekranında görünecek.\n"]=
            "The game appears only on the Nova Pro Wireless OLED.\n",
    };

    // Short dynamic prefixes are kept separate so real device percentages,
    // timestamps and hardware error details are never altered.
    private static readonly (string Turkish,string English)[] Fragments =
    [
        ("Canlı dock modu etkin","Live dock mode active"),
        ("Canlı mod durdu","Live mode stopped"),
        ("SteelSeries ekranı geri yükleniyor","Restoring SteelSeries display"),
        ("Tasarım dock'a uygulandı","Design applied to dock"),
        ("Tasarım dock'a gönderiliyor","Sending design to dock"),
        ("Orijinal ekrana dönmek için Reset'i kullan","Use Reset to restore OEM screen"),
        ("Dock üzerinde canlı mod açık","Dock live mode on"),
        ("Dock canlı modu kapalı","Dock live mode off"),
        ("Salt-okunur tekerlek:","Read-only wheel:"),
        ("Tekerlek bekleniyor","Waiting for wheel input"),
        ("Veri yok","No data"),
        ("Mod, ses veya ChatMix hareketi algılandığında değişir.",
            "Updates when mode, volume or ChatMix changes."),
        ("Üç mod kaydedildi:","Saved all three modes:"),
        ("Son işlem geri alındı.","Undid last action."),
        ("İşlem yeniden uygulandı.","Redid last action."),
        ("En fazla 70 widget.","Up to 70 widgets."),
        ("Önce", "First"),
        ("Tarama bekleniyor", "Waiting for scan"),
        ("Son okuma:","Last read:"),
        ("Şu anda seçilenler:","Currently selected:"),
        ("Piller:","Batteries:"),
        ("Bilgisayarın durumu:","Computer state:"),
        ("OYUN AKTİF","GAME ACTIVE"),
        ("MEDYA AKTİF","MEDIA ACTIVE"),
        ("BOŞTA","IDLE"),
        ("Oyun: ","Game: "),
        ("Medya: ","Media: "),
        (" ikonu kaydedildi ve önizlemeye uygulandı.",
            " icon saved and applied to preview."),
        ("OLED aktarım hatası:","OLED streaming error:"),
        ("ESC: çıkış","ESC: exit"),
        ("Uygulama hatası:","Apply error:"),
        ("Pil kaynağı okunamadı:","Could not read battery source:"),
        ("Kayıt hatası:","Save error:"),
        ("Kaydedilmiş üç modlu tasarım bulunamadı.","No saved three-mode layout found."),
        ("Önizleme simülasyondur.","The preview is a simulation."),
        ("Uygula sabit görüntüyü gönderir; Reset orijinal ekranı geri yükler.",
            "Apply sends a static image; Reset restores the original display."),
        ("Her modun düzeni bağımsızdır. Donanım için Dock Preview'da onay ver.",
            "Each mode has its own layout. Confirm before sending to hardware."),
        ("Şarj olmuyor","Not charging"),
        ("şarj oluyor","charging"),
        ("Şarj oluyor","Charging"),
        ("Bağlı değil","Not connected"),
        ("Bağlı","Connected"),
        ("Prize bağlı","Plugged in"),
        ("Batarya","Battery"),
        ("Yedek pil takılı","Spare battery docked"),
        ("Son onaylı veri 10 saniyeyi aştı.","Last verified data is over 10 seconds old."),
        ("doğrulanmamış","unverified"),
        ("CANLI DEĞİL","NOT LIVE"),
        ("canlı","live"),
        ("Kaynak bulunamadı","Source unavailable"),
        ("bağlantı yok","disconnected"),
        ("erişilemiyor","inaccessible"),
        ("Phone Link ekranı (senkronizasyon güncelliği doğrulanmadı)",
            "Phone Link UI (sync freshness not verified)"),
        ("Telefon Bağlantısı'nın canlı değeri ayrı.",
            "Phone Link has a separate live battery reading."),
        ("Phone Link açık değil veya pil göstergesi erişilebilir değil.",
            "Phone Link is closed or its battery label is not accessible."),
        ("Bağlı değil veya canlı pil raporu alınamadı.",
            "Disconnected or no live battery report."),
        ("PhoneExperienceHost çalışmıyor.",
            "PhoneExperienceHost is not running."),
        ("Henüz sorgulanmadı","Not queried yet"),
        ("Oyun veya medya bulunmadı.","No game or media detected."),
        ("üst 10 piksel korunuyor","top 10 pixels protected"),
        ("tam ekran düzenleme","full-screen editing"),
    ];
    public static string T(string? text)
    {
        if(!English || string.IsNullOrEmpty(text))return text??"";
        if(Texts.TryGetValue(text,out string? translated))return translated;
        string result=text;
        foreach(var (tr,en) in Fragments)
            result=result.Replace(tr,en,StringComparison.Ordinal);
        return result;
    }

    public static void ApplyTree(Control root)
    {
        if(root is not (ComboBox or TextBoxBase or NumericUpDown or
            DarkNumberBox or ListBox))
        {
            var source=Origins.GetValue(root,c=>new Original{Text=c.Text});
            if(source.Text.Length>0)root.Text=English?T(source.Text):source.Text;
        }
        foreach(Control child in root.Controls)ApplyTree(child);
    }
}

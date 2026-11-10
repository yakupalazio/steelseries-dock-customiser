namespace SteelseriesDockCustomiser;

internal static class WidgetLabels
{
    public static string Display(string kind)=>kind switch
    {
        "Source"=>"Kaynak ikonu",
        "Track"=>"Şarkı adı",
        "Artist"=>"Sanatçı",
        "Progress"=>"İlerleme çubuğu",
        "MediaClock"=>"Küçük medya saati",
        "GameLabel"=>"Oyun başlığı",
        "GameTitle"=>"Oyun adı",
        "GameMiniPlayer"=>"Mini Player (Oyun)",
        "Custom text"=>"Özel metin",
        "Clock"=>"Saat",
        "Date"=>"Tarih",
        "BatterySlot1"=>"Batarya 1 · Otomatik",
        "BatterySlot2"=>"Batarya 2 · Otomatik",
        "BatterySlot3"=>"Batarya 3 · Otomatik",
        "Mouse"=>"Fare bataryası",
        "Phone"=>"Telefon bataryası",
        "Laptop"=>"Laptop bataryası",
        "Headset"=>"Kulaklık bataryası",
        "SpareBattery"=>"Yedek batarya",
        "DualSense"=>"DualSense bataryası",
        "VolumeChatMix"=>"Ses / ChatMix tekerleği",
        _=>kind
    };
}

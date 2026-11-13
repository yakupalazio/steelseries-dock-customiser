using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// The single source of truth for addable widget types, category ordering,
/// per-screen visibility and the four intentional multi-instance exceptions.
/// Applied by the picker, quick-add, importer and saved-layout sanitizer.
/// </summary>
internal static class WidgetCatalog
{
    internal sealed record Category(string Key,string Title,
        string Subtitle,string EnglishTitle,string EnglishSubtitle)
    {
        public override string ToString()=>
            UiLanguage.English?EnglishTitle:Title;
        public string Description=>UiLanguage.English?EnglishSubtitle:Subtitle;
    }

    internal static readonly IReadOnlyList<Category> Categories=[
        new("Media","Müzik ve şarkı sözleri",
            "Şarkı, sanatçı, söz, ilerleme ve ses görselleştirme",
            "Music & lyrics","Track, artist, lyrics, progress, audio visualizer"),
        new("Battery","Pil ve cihazlar",
            "Bağlı cihazların güncel pil bilgileri",
            "Battery & devices","Verified batteries of locally connected devices"),
        new("Time","Saat ve tarih",
            "Saat, tarih ve kişisel metin",
            "Clock & text","Clock, date and custom text"),
        new("Visual","Görsel öğeler",
            "GIF animasyonları ve özel grafikler",
            "Visuals","GIF animations and custom visuals"),
        new("System","Bilgisayar ve ağ",
            "İşlemci, bellek ve bağlantı bilgileri",
            "System & network","CPU, memory and network telemetry"),
        new("Game","Oyun ekranı",
            "Oyun bilgisi ve oyuna özgü göstergeler",
            "Gaming","Game title and dedicated game-mode widgets"),
        new("Controls","Ses kontrolleri",
            "Ses, ChatMix ve donanım tekerleği",
            "Sound controls","Volume, ChatMix and the dock wheel")
    ];

    private static readonly string[] Media=[
        "Source","Track","Artist","Lyrics","Progress","MediaClock",
        "AudioVisualizer"];
    private static readonly string[] Battery=[
        "BatterySlot1","BatterySlot2","BatterySlot3",
        "Headset","SpareBattery","Mouse","Phone","Laptop","DualSense"];
    private static readonly string[] Time=["Clock","Date","Custom text"];
    private static readonly string[] Visual=["AnimatedGif"];
    private static readonly string[] System=["SystemCpu","SystemRam","NetworkPing"];
    private static readonly string[] Game=[
        "GameLabel","GameTitle","GameLogo","GameMiniPlayer"];
    private static readonly string[] Controls=["VolumeChatMix"];

    internal static IReadOnlyList<string> ForCategory(string category)=>category switch
    {
        "Media"=>Media,
        "Battery"=>Battery,
        "Time"=>Time,
        "Visual"=>Visual,
        "System"=>System,
        "Game"=>Game,
        "Controls"=>Controls,
        _=>[]
    };

    internal static bool IsMediaKind(string kind)=>Media.Contains(kind,
        StringComparer.Ordinal);
    internal static bool AllowsMultiple(string kind)=>kind is
        "AnimatedGif" or "Custom text" or "AudioVisualizer" or "Clock";

    internal static bool CanAdd(ScreenMode mode,string kind,
        IEnumerable<DockWidget> existing)=>
        StudioProject.IsAllowed(mode,kind)&&
        (AllowsMultiple(kind)||!existing.Any(w=>w.Kind==kind));

    internal static IReadOnlyList<string> Available(ScreenMode mode,
        string category,IEnumerable<DockWidget> existing)=>
        ForCategory(category).Where(kind=>CanAdd(mode,kind,existing))
            .ToArray();

    internal static IReadOnlyList<Category> AvailableCategories(
        ScreenMode mode,IEnumerable<DockWidget> existing)=>
        Categories.Where(c=>Available(mode,c.Key,existing).Count>0).ToArray();

    internal static string Description(string kind)=>
        UiLanguage.English?EnglishDescription(kind):TurkishDescription(kind);

    private static string EnglishDescription(string kind)=>kind switch
    {
        "Lyrics"=>"One-line scroll with smooth upward line transitions",
        "AudioVisualizer"=>"Live stereo bars or circular sound spectrum",
        "AnimatedGif"=>"Convert your own animated GIF to 1-bit OLED",
        "Clock"=>"Add as many clocks as you like",
        "Custom text"=>"Customize your text and its font",
        "Phone"=>"Battery status of the phone connected to this PC",
        "Headset"=>"Battery status of your connected headset",
        "BatterySlot1"=>"First device from your battery priority list",
        "Source"=>"Icon of the current music application",
        "Track"=>"Currently playing song title",
        "Artist"=>"Artist of the current song",
        "Progress"=>"Playback progress bar",
        "VolumeChatMix"=>"Volume / game / chat controls",
        "GameMiniPlayer"=>"Game-specific compact media widget",
        "GameLogo"=>"Running game's logo from a local EXE icon or custom image",
        _=>"Place in this screen and drag to reposition"
    };

    private static string TurkishDescription(string kind)=>kind switch
    {
        "Lyrics"=>"Tek satır akış, soldan kayan sözler ve yumuşak satır geçişi",
        "AudioVisualizer"=>"Stereo, çubuk veya dairesel ses görseli",
        "AnimatedGif"=>"Kendi GIF dosyanı siyah-beyaz OLED'e dönüştür",
        "Clock"=>"İstediğin kadar saat yerleştir",
        "Custom text"=>"Kendi metnini ve yazı tipini düzenle",
        "Phone"=>"Bu PC'ye bağlı telefonun pil durumu",
        "Headset"=>"Bağlı kulaklığın pil seviyesi",
        "BatterySlot1"=>"Öncelik sırasındaki ilk bağlı pil kaynağı",
        "Source"=>"Aktif müzik uygulamasının küçük simgesi",
        "Track"=>"Çalan parçanın adı",
        "Artist"=>"Çalan parçanın sanatçısı",
        "Progress"=>"Çalan şarkının ilerleme çubuğu",
        "VolumeChatMix"=>"Ses seviyesi ve sohbet/oyun dengesi",
        "GameMiniPlayer"=>"Oynarken mini medya bilgisi",
        "GameLogo"=>"Aktif oyunun EXE simgesi veya seçtiğin özel logosu",
        _=>"Seçili ekrana yerleştir, sürükleyerek konumlandır"
    };
}

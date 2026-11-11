# Steelseries Dock Customiser 1.1 — Türkçe Kılavuz

SteelSeries Arctis Nova Pro Wireless baz istasyonunun 128×64 OLED ekranı için bağımsız Windows 11 tasarım uygulaması. SteelSeries'in resmî yazılımı değildir.

## Kurulum

1. `SteelseriesDockCustomiser-v1.1.0-win-x64-portable.zip` dosyasını güvenilir bir klasöre çıkar.
2. `SteelseriesDockCustomiser.exe` dosyasını aç. Yönetici yetkisi normal tasarım ve önizleme için gerekli değildir.
3. Soldan **Medya / Oyun / Boşta** modunu seç. Ortadaki dock önizlemesi ve altındaki piksel editörü tek ekrandadır.
4. Sağdan widget ekle, konumunu değiştir, pil kaynaklarını seç ve **Kaydet** ile sakla. Sağdaki ayarlar seçili widget'ın türüne göre görünür.

Bu paket .NET 10 çalışma zamanını içinde taşır; ayrıca .NET yüklemen gerekmez. Geliştirme bilgisayarındaki **yerel pakette** dock'un orijinal ürün fotoğrafı korunur; GitHub kaynak kodunda bu dosya yoktur. Fotoğrafı yükleme ve hizalama kontrolleri kaldırıldı. Fotoğraf dosyası bulunmayan kaynak derlemelerde koyu vektör dock görünür.

## Gerçek OLED'e yazma

- Normal düzenleme ve fotoğraf üzerindeki önizleme **yalnızca bilgisayarda** çalışır.
- **Uygula** tasarımı dock'a bir kez gönderir ve Reset'e kadar bırakır. **Canlı** düzeni aktif verilerle günceller. İkisi de açık kullanıcı onayı gerektirir.
- Bu yollar için proje belgelerinde belirtilen, SHA-256 değeri doğrulanmış ayrı `ggoled v0.3.0` aracı gerekir. Yardımcı aracı bilinmeyen sitelerden indirme.
- Varsayılan olarak OLED'in üst 10 satırı korunur. Bu korumayı kapatmak tam ekranı geçici değiştirebilir.
- OLED yanığını azaltmak için uzun süre aynı yüksek parlaklıklı kareleri açık bırakma. **Dock Reset** ile SteelSeries ekranına dön. Uygulama çökerse yeniden açıp **Dock Reset**'e basabilirsin; otomatik kurtarma bir işletim sistemi çökmesinde garanti edilemez.

## Pil ve öncelik

**Ayarlar → Pil ve öncelik** sekmesinde bağlı cihazların son okumalarını, kaynaklarını ve gösterim sırasını görüp değiştirebilirsin. Galaxy S24+ için Phone Link açık/minimize durumdayken erişilebilir pil etiketi salt-okunur kullanılır; ADB zorunlu değildir.

## İsteğe bağlı Ses / ChatMix tekerleği widget'ı

**Boşta** ve **Oyun** modlarında dairesel widget varsayılan olarak eklidir. Sağdan widget'ı seçip **Gösterge tipi → Dairesel tekerlek / Düz çubuk** arasında geçebilirsin. İstemiyorsan öğeyi silebilirsin; kaydettikten sonra kendiliğinden geri gelmez. Ses modunda yüzdeyi, ChatMix modunda Game/Chat dengesini gösterir; Windows sesini veya kulaklık ayarını değiştirmez. Fiziksel tekerlek hareketinden gelen salt-okunur HID bildirimlerini dinler. İlk gerçek tekerlek hareketine kadar `--` görünmesi normaldir. Bazı cihazlarda tuşa basmak yeni bir HID bildirimi üretmeyebilir; görünüm bir sonraki tekerlek hareketiyle güncellenir.

Bu gösterge SteelSeries'in resmî görselinden **esinlenilmiş, 45×42 gerçek OLED pikseliyle yeniden çizilmiş** bir tasarımdır; firmware veya GG içinden alınmış hazır sprite içermez. Ortadaki beyaz disk, siyah `VOL` / `MIX` yazısı ve halkası dışında yüzde metni yoktur. Görseli birebir kendi cihazına uyarlamak için dairesel widget seçiliyken **Tekerleği piksel piksel düzenle** düğmesine bas. Beyaz/Siyah/Silgi fırçalarını ve sese göre dolan mavi katmanı kullanıp 0–100 örnek sürgüsüyle dene. İstersen otomatik orta yazıyı kapatıp onu da elle çizebilirsin. Değişiklikler widget'a kaydedilir; **Orijinal tasarıma dön** ile şablona dönebilirsin. Aynı menüde **Düz çubuğa geç** seçeneği de bulunur.

**Fiziksel tekerlek sorunu için düzeltme:** Canlı mod artık doğrudan OLED çizim oturumunu yeniden kullanır, tekerlek hareketinden kısa süre sonra tasarımı tekrar gösterir ve GG olayı saklarsa iki saniyede bir ekranı geri alır. Tekerlek döndüğünde orijinal SteelSeries göstergesi çok kısa görünebilir; bağlantı veya HID bildirimlerinin GG tarafından tüketilmesi hâlinde görünümün hareketi gerçek donanımda doğrulanmalıdır. Ses seviyesini ayarlayan hiçbir komut gönderilmez. OLED'in orijinal üst 10 pikseli değiştirilmez.

## Dil

**Ayarlar → Dil / Language** bölümünden Türkçe veya İngilizce seçebilirsin. Seçim anında etkinleşir ve sonraki açılışlarda korunur. **Hakkında** sekmesi ayarlar menüsünün en altındadır.

## Sorun giderme

- Pil görünmüyor: SteelSeries GG'yi çalıştır, cihaz bağlantılarını kontrol et. Telefon için Phone Link durumu her Windows sürümünde erişilebilir olmayabilir.
- Fiziksel OLED değişmiyor: önce doğrulanmış `ggoled` aracının kurulumunu ve dock USB bağlantısını kontrol et. Uygulama donanımın yoksa gerçeği taklit ederek sahte pil değeri göstermez.
- Önizleme fotoğrafsız: `dock-photo-local.png` dosyasının EXE yanında bulunduğunu kontrol et. GitHub kaynak derlemeleri bu dosyayı içermediği için vektör dock gösterebilir.

Kaynak kod ve güncellemeler: https://github.com/yakupalazio/steelseries-dock-customiser

Geliştiren: **Yakup Can Palaz**

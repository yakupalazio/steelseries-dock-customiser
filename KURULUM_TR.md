# Steelseries Dock Customiser 1.1 — Türkçe Kılavuz

SteelSeries Arctis Nova Pro Wireless baz istasyonunun 128×64 OLED ekranı için bağımsız Windows 11 tasarım uygulaması. SteelSeries'in resmî yazılımı değildir.

## Kurulum

1. `SteelseriesDockCustomiser-v1.1.0-win-x64-portable.zip` dosyasını güvenilir bir klasöre çıkar.
2. `SteelseriesDockCustomiser.exe` dosyasını aç. Yönetici yetkisi normal tasarım ve önizleme için gerekli değildir.
3. Soldan **Medya / Oyun / Boşta** modunu seç. Ortadaki dock önizlemesi ve altındaki piksel editörü tek ekrandadır.
4. Sağdan widget ekle, konumunu değiştir, pil kaynaklarını seç ve **Kaydet** ile sakla.

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

Sağdaki **Öğe ekle** listesinden **Ses / ChatMix tekerleği** seç. Widget, dock'un fiziksel tekerleğini çevirdiğinde gelen salt-okunur HID bildirimlerini izler. Ses modunda yüzdeyi, ChatMix modunda Game/Chat dengesini gösterir; Windows sesini veya kulaklık ayarını değiştirmez. İlk gerçek tekerlek hareketine kadar `--` görünmesi normaldir. Salt tuşa basmak her cihazda ayrı bir okunabilir bildirim üretmeyebilir; görünüm bir sonraki tekerlek hareketiyle güncellenir. SteelSeries GG ile eşzamanlı test cihaz üzerinde henüz tamamlanmadı.

## Gizli DOOM MODE

1. Soldan **Ayarlar → Hakkında** bölümüne gir.
2. `↑ ↑ ↓ ↓ ← → ← → B A` tuşlarına sırayla bas.
3. Bilgisayarda yalnızca **Dock ekranına bak** mesajını görürsün. **DOCK'TA CANLI OYNA** tuşuna basıp onay ver; oyun görüntüsü sadece dock'a aktarılır. `W/S` ileri/geri; `A/D` dön; `Space` ateş; `R` oyunu sıfırla; `ESC` çıkış.

Bu oyun DOOM'un kendisi değildir; telifli oyun varlıkları kullanılmadan hazırlanmış özgün bir retro FPS Easter egg'idir.

**DOCK'TA CANLI OYNA** isteğe bağlı deneysel moddur: yalnızca Nova Pro Wireless OLED koleksiyonu ve güvenli geri-dönüş aracı doğrulandıktan sonra ayrıca onay ister. Üst pil satırları dâhil 128×64 ekranı değiştirir; **süre sınırı yoktur**. Durdur, `ESC`, pencereyi kapat veya odağı değiştirerek aktarımı sonlandırıp varsayılan ekrana dönersin. OLED yanığına karşı sabit görüntüyü uzun süre bırakma. Donanımda hızlı akış testi henüz tamamlanmadı.

## Sorun giderme

- Pil görünmüyor: SteelSeries GG'yi çalıştır, cihaz bağlantılarını kontrol et. Telefon için Phone Link durumu her Windows sürümünde erişilebilir olmayabilir.
- Fiziksel OLED değişmiyor: önce doğrulanmış `ggoled` aracının kurulumunu ve dock USB bağlantısını kontrol et. Uygulama donanımın yoksa gerçeği taklit ederek sahte pil değeri göstermez.
- Önizleme fotoğrafsız: `dock-photo-local.png` dosyasının EXE yanında bulunduğunu kontrol et. GitHub kaynak derlemeleri bu dosyayı içermediği için vektör dock gösterebilir.
- Oyun modundan çıkmak: `ESC`. Gerçek OLED aktarımı sonlandırılırken cihazın varsayılan ekranına geri dönme komutu gönderilir.

Kaynak kod ve güncellemeler: https://github.com/yakupalazio/steelseries-dock-customiser

Geliştiren: **Yakup Can Palaz**

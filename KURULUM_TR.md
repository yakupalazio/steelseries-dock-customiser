# Steelseries Dock Customiser 1.0 — Türkçe Kılavuz

SteelSeries Arctis Nova Pro Wireless baz istasyonunun 128×64 OLED ekranı için bağımsız Windows 11 tasarım uygulaması. SteelSeries'in resmî yazılımı değildir.

## Kurulum

1. `SteelseriesDockCustomiser-v1.0.0-win-x64-portable.zip` dosyasını güvenilir bir klasöre çıkar.
2. `SteelseriesDockCustomiser.exe` dosyasını aç. Yönetici yetkisi normal tasarım ve önizleme için gerekli değildir.
3. Soldan **Medya / Oyun / Boşta** modunu seç. Ortadaki dock önizlemesi ve altındaki piksel editörü tek ekrandadır.
4. Sağdan widget ekle, konumunu değiştir, pil kaynaklarını seç ve **Kaydet** ile sakla.

Bu paket .NET 10 çalışma zamanını içinde taşır; ayrıca .NET yüklemen gerekmez. Ürün fotoğrafı lisans nedeniyle pakette bulunmaz. Yerine koyu renkli, uygulama içinde çizilmiş bir dock görünümü sunulur. İstersen **Fotoğraf** ve **Hizala** ile kendi yerel fotoğrafını kullanabilirsin.

## Gerçek OLED'e yazma

- Normal düzenleme ve fotoğraf üzerindeki önizleme **yalnızca bilgisayarda** çalışır.
- **10 sn test** veya **Canlı** düğmeleri fiziksel OLED'e yazmayı ancak açık kullanıcı onayıyla başlatır.
- Bu yollar için proje belgelerinde belirtilen, SHA-256 değeri doğrulanmış ayrı `ggoled v0.3.0` aracı gerekir. Yardımcı aracı bilinmeyen sitelerden indirme.
- Varsayılan olarak OLED'in üst 10 satırı korunur. Bu korumayı kapatmak tam ekranı geçici değiştirebilir.
- OLED yanığını azaltmak için uzun süre aynı yüksek parlaklıklı kareleri açık bırakma. Görünüm bozulursa **Geri dön** ile SteelSeries ekranını yüklemeyi dene.

## Gizli DOOM MODE

1. Soldan **Ayarlar → Hakkında** bölümüne gir.
2. `↑ ↑ ↓ ↓ ← → ← → B A` tuşlarına sırayla bas.
3. Gizli 128×64 mini FPS açılır. `W/S` ileri/geri; `A/D` dön; `Space` ateş; `R` sıfırla; `ESC` çıkış.

Bu oyun DOOM'un kendisi değildir; telifli oyun varlıkları kullanılmadan hazırlanmış özgün bir retro FPS Easter egg'idir.

**Gerçek OLED'de dene (20 sn)** isteğe bağlı deneysel moddur: yalnızca Nova Pro Wireless OLED koleksiyonu ve güvenli geri-dönüş aracı doğrulandıktan sonra ayrıca onay ister. Üst pil satırları dâhil 128×64 ekranı en fazla 20 saniye değiştirir, sonra varsayılan ekrana döndürmeye çalışır. Donanım üzerinde yüksek FPS testi henüz tamamlanmadığı için sorunla karşılaşırsan kullanmayı durdur.

## Sorun giderme

- Pil görünmüyor: SteelSeries GG'yi çalıştır, cihaz bağlantılarını kontrol et. Telefon için Phone Link durumu her Windows sürümünde erişilebilir olmayabilir.
- Fiziksel OLED değişmiyor: önce doğrulanmış `ggoled` aracının kurulumunu ve dock USB bağlantısını kontrol et. Uygulama donanımın yoksa gerçeği taklit ederek sahte pil değeri göstermez.
- Önizleme fotoğrafsız: koyu vektör dock görünümü normaldir. Kendi fotoğrafını seçip dört köşeyi hizalayabilirsin.
- Oyun modundan çıkmak: `ESC`. Gerçek OLED aktarımı sonlandırılırken cihazın varsayılan ekranına geri dönme komutu gönderilir.

Kaynak kod ve güncellemeler: https://github.com/yakupalazio/steelseries-dock-customiser

Geliştiren: **Yakup Can Palaz**

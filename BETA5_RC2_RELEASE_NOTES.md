# Steelseries Dock Customiser — Beta 5 RC2

**Tarih:** 12 Kasım 2026
**Hazırlayan:** Yakup Can Palaz
**Dağıtım:** Yalnızca bu bilgisayarda özel test paketi; genel GitHub sürümü değil.

## Bu güncellemede düzeltilenler

- Mini dairesel tekerleğin gerçek **31 × 29** OLED piksel tuvali eklendi.
  Büyük tekerleğin 45 × 42 boyutlu tuvali artık küçültülerek kullanılmıyor.
  Dış ve iç halka için kesintisiz tek bitlik raster yaylar oluşturuldu.
- Mini tekerleğe beyaz/siyah/dinamik piksel atölyesi, yüzde önizlemesi,
  ayrı Game/Chat X–Y ofsetleri, sürükleme modu, **Geri al / İleri al**
  ve `Ctrl+Z / Ctrl+Y` eklendi. Mini ve büyük tekerleklerin çizimleri
  bağımsız saklanır; eski 45×42 mini çizimler ilk yüklemede yeni boyuta
  dönüştürülür. Kullanıcının büyük tekerlek çizimi değiştirilmez.
- Game ve Chat simgeleri yalnızca kendi dönen halka bölümlerinde yer
  alır; halka veya birbirleriyle çakışamaz. X/Y değişiklikleri ve
  mini ikonların farklı konumlara taşınması otomatik testlerden geçti.
- Uygulama ilk açılışta **Boşta** düzenini gösterir. Canlı OLED'de
  otomatik Oyun / Medya / Boşta önceliği korunur.
- Şarj başlangıcı doğrulandığında **4 saniyelik tam ekran ŞARJ OLUYOR /
  CHARGING** ekranı eklendi. Cihazın özel ikonu, yüzde ve büyük yıldırım
  görünür. Başlangıçta zaten şarjda olan cihaz için sahte bir
  başlangıç uyarısı üretilmez; çıkarıp yeniden takıldığında bildirilir.
- Önceki düşük (%20) ve kritik (%10) pil uyarıları korunur. Kritik
  ekran 1 sn yanar / 0,5 sn söner, 5,5 sn devam eder. Uyarı bitiminde
  tek seferlik OEM başlık geri verme işlemi uygulanır.
- Galaxy S24+ için Windows'un **yerleşik Telefon Bağlantısı / Phone Link**
  arayüzünden yüzde okunması iyileştirildi. Arka plandaki COM sunucusu
  pencere oluşturmuyorsa sınırlı, en fazla 10 dakikada bir yeniden açma
  ve küçültme denemesi yapılır; kullanımda olan pencere kapatılmaz.
  Bilgisayardaki gerçek testte **%87 ve daha sonra %84** okunmuştur.
  Ek BQC veya özel bir telefon uygulaması kurulmaz; Windows Phone Link'in
  bağlantısı ve erişilebilirlik alanları yine gereklidir.
- Ana pencerenin küçük boyutunu, pil önceliklerini, ikon atölyesini,
  tarih/saat penceresini ve profilleri ekran görüntüsü üzerinden
  yeniden denetledik.

## Tamamlanan testler

- `dotnet build -c Release`: 0 hata, 0 uyarı.
- `--self-test`: mini ve büyük piksel geçmişi, halkaanahatları,
  Game/Chat hareketi, 4 sn şarj, kritik yanıp sönme, BLE/GG pil
  kaynakları, widget öncelikleri, tarih/saat okunabilirliği.
- `--battery-scan-file`: Galaxy S24+ %84 (Phone Link), Aerox %64
  (şarj oluyor), Nova Pro %100, yedek batarya %75 (şarj oluyor).
  Bu değerler **yalnızca test anına aittir**.
- `--oled-live-ui-smoke`: WinForms Canlı aç/kapat, orijinal dock'a dönüş.
- `--oled-alert-ui-smoke`: düşük, kritik, şarj ve normal ekran geçişi,
  gerçek Nova Pro OLED'e komut göndermesi ve OEM'e güvenli dönüş.
- Widget, mini/büyük tekerlek, pil, ikon, saat ve profil
  pencerelerinin gerçek Windows ekran görüntüleri gözden geçirildi.

## Devam eden sınırlamalar

- DualSense ilk kez eşleştirilip canlı şarj yüzdesi ile sınanmadı.
- Windows Phone Link üçüncü taraflar için resmî bir pil API'si değildir;
  bağlantı, senkronizasyon veya erişilebilirlik yapısı değişirse
  telefon yüzdesi hâlâ `--%` olabilir. Eski SetupAPI önbelleği
  hiçbir zaman canlı veri gibi kullanılmaz.
- Ses / ChatMix modu değiştirildiğinde cihazın yerel ekranında yaklaşık
  5 saniyelik bekleme halen inceleme konusudur.
- Orijinal üst 10 piksellik OEM pil göstergesinin her batarya değişiminden
  hemen sonra yenilenmesi firmware davranışı nedeniyle garanti edilmez.
- Tüm özel ikonların her açı ve DPI ölçeğinde **fiziksel kullanıcı kabulü**
  tamamlanmadı; otomatik testler sınırlıdır.
- Dock fotoğrafının herkese açık yeniden dağıtım hakkı ve eski GitHub
  sürümlerinin lisans geçmişi kesinleşmeden public release oluşturulmaz.

## Kurulum ve gizlilik

Beta 5 RC2 `%LOCALAPPDATA%\SteelseriesDockCustomiser-Beta5-Development`
ayarlarını kullanır. Beta 4'ün kurulu EXE'sini ve profil verilerini
değiştirmez. ZIP içerisindeki EXE kendi .NET runtime'ını taşır.
Dışarıdan BQC kurulmaz. Çizimler ve cihaz tanılama ayarları yereldir.
Telefonun mesajları, fotoğrafları veya kişisel dosyaları okunmaz.

Copyright (c) 2026 Yakup Can Palaz. Third-party licenses are preserved.

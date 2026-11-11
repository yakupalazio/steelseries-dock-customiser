# 1.1.1-beta.3 — yerel donanım test adayı

**Henüz GitHub Release değil.** Kullanıcının fiziksel dock doğrulaması bekleniyor. 1.1.1-beta.2 olduğu gibi korunuyor.

## Eklendi / değişti
- ChatMix halkası çevrilirken Game ve Chat ikonları halkanın başlangıç/bitiş noktalarıyla birlikte konum değiştirir. İkon Atölyesi pikselleri korunur. Her iki ikon için ayrı X/Y piksel kaydırması vardır; istenirse halkanın hareketinden bağımsız konum seçilebilir.
- Phone Link pil okuması tamamen pasif: uygulama pencere başlatmaz, öne getirmez, restore/minimize etmez. Telefon yüzdesi yalnızca eldeki erişilebilir veri veya **kullanıcının etkinleştirdiği** Android ADB varsa gösterilir.
- Oyun profili başlığı dilden bağımsız depolanan eski GAME:/NOW PLAYING: metinleri için TR “Oyun:” / EN “Game:” olarak çizilir. Kullanıcının özel başlıkları değiştirilmez.
- Dock temalı .ico ve Windows tepsi menüsü: Aç, canlı OLED aç/durdur, medyayı dinle/dinleme, kayıtlı profillerden seç, yeniden başlat, uygulamadan çık. X = tepsiye küçült. Profiller yerel profiles-v1.json üzerinden dinamik oluşturulur.
- Normal başlatmada Canlı OLED otomatik başlar (daha önceki donanım doğrulama/bağlantı güvenlik kontrolleri korunur). Ekran görüntüsü testlerinde HID açılmaz.

## Bilinen sorunlar (çözüldü iddiası yok)
1. **Telefon:** Phone Link UI Automation ağacı, test edilen bilgisayarda 69 erişilebilir öğede hiçbir battery/pil etiketi vermedi. Bu nedenle GG/Phone Link çalışsa bile --% görünebilir. Pencereyi öne getirmeyen güvenilir alternatif, önceden izin verilmiş Android ADB'dir. Kullanıcı izni olmadan debugging veya telefon ayarı değiştirilmez.
2. **Fiziksel Volume ↔ ChatMix mod düğmesi:** Daha önceki kontrollü testlerde düğmeye basıştan ayrı HID bildirimi bulunamadı. Bu yüzden yalnız mod değiştirme hâlâ OEM OLED'i 4–5 saniye öne getirebilir. Yeni bir öncelik/firmware komutu bulunmadı; 50–60 FPS yazmak bunu çözmedi.
3. **Tekerlek ile ses ayarı:** 450 ms sessizlik + tek 0x95/0x93 geri dönüşü Beta 2'de gerçek dock üzerinde yaklaşık 1 saniyede gözlendi; bu mekanizma değiştirilmedi.
4. GG/Sonar ve özel OLED ekran sahibi aynı anda çalışınca çakışma olabilir. GG/Sonar'ı uygulama otomatik kapatmaz.
5. Düzenleme için kullanıcıya ait ikonlar ve özel profiller %LOCALAPPDATA%\SteelseriesDockCustomiser altında kalır. Kaynak kodda veya herkese açık pakette kişisel pil verisi, özel fotoğraf, profil bulunmamalı.

## Donanım kabul testi (kullanıcı eşzamanlı)
- Önce eski uygulama kapatılsın; GG'yi kullanıcı kendisi kapatsın veya açık tutmayı tercih ederse çakışma not edilsin.
- Beta3 EXE açılınca 1) Canlı OLED'in kendiliğinden başlaması, 2) X ile tepsiye geçip ekranın devam etmesi, 3) ses tekerleği ile 1 saniyelik dönüş ve flicker olmaması, 4) ChatMix basışı ve dönüşü, 5) ikonların 90° döndüğünde takip etmesi, 6) tepsi menüsü ve yeniden başlatma, 7) Phone Link'in hiçbir zaman öne gelmemesi gözlemlensin.
- Yalnızca kullanıcı test ve gözlemini doğruladıktan sonra GitHub'da bir sonraki prerelease yayımlansın.

**Firmware'e yazma / flaşlama işlemi yok.** Ağır 5 Hz 0x95 müdahalesi ve 50 FPS yarış mekanizması canlı akışa eklenmedi.
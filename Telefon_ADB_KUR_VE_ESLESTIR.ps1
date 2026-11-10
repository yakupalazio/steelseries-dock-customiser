$ErrorActionPreference='Stop'
$folder=Join-Path $env:LOCALAPPDATA 'SteelseriesDockCustomiser\platform-tools'
$adb=Join-Path $folder 'adb.exe'
if(!(Test-Path $adb))
{
    $zip=Join-Path (Join-Path $env:USERPROFILE 'Downloads') 'platform-tools-latest-windows.zip'
    if(!(Test-Path $zip))
    {
        Write-Host 'Google SDK Platform-Tools arsivi Downloads klasorunde bulunamadi.' -ForegroundColor Yellow
        Write-Host 'Resmi sayfa: https://developer.android.com/tools/releases/platform-tools'
        Write-Host 'Windows icin Platform-Tools indirin, lisans kosullarini okuyup kabul edin.'
        Start-Process 'https://developer.android.com/tools/releases/platform-tools'
        Read-Host 'Dosyayi indirdikten sonra bu araci yeniden acin. Cikmak icin Enter'
        exit 1
    }

    $target=Join-Path $env:LOCALAPPDATA 'SteelseriesDockCustomiser'
    New-Item -Path $target -ItemType Directory -Force|Out-Null
    $extract=Join-Path $target 'adb-unpack-temp'
    if(Test-Path $extract){Remove-Item $extract -Recurse -Force}
    New-Item -Path $extract -ItemType Directory -Force|Out-Null
    Expand-Archive -LiteralPath $zip -DestinationPath $extract -Force
    $source=Join-Path $extract 'platform-tools'
    if(!(Test-Path (Join-Path $source 'adb.exe'))){throw 'Arsiv Google platform-tools yapisinda degil.'}
    Copy-Item -LiteralPath $source -Destination $target -Recurse -Force
    Remove-Item -LiteralPath $extract -Recurse -Force
}

Write-Host ''
Write-Host 'Google ADB hazir:' -ForegroundColor Green
& $adb version | Select-Object -First 2
Write-Host ''
Write-Host 'Telefon ve PC ayni Wi-Fi aginda olmali.' -ForegroundColor Yellow
Write-Host 'Galaxy S24+: Ayarlar > Gelistirici secenekleri > Kablosuz hata ayiklama'
Write-Host 'Eslestirme koduyla cihaz eslestir ekranina gir.'
Write-Host ''
$pair=Read-Host 'Eslestirme ekranindaki IP:PORT (ornegin 192.168.1.20:37123)'
if($pair -notmatch '^\d{1,3}(\.\d{1,3}){3}:\d{2,5}$'){
    Write-Host 'Gecersiz IP:PORT formati.' -ForegroundColor Red
    Read-Host 'Enter ile cik';exit 2
}
& $adb pair $pair
if($LASTEXITCODE -ne 0){Write-Host 'Eslestirme basarisiz.' -ForegroundColor Red;Read-Host 'Enter ile cik';exit 3}
Write-Host ''
Write-Host 'Eslestirildi! Kablosuz hata ayiklama ana ekranindaki IP adresi ve PORTU gir.'
Write-Host 'Bu port, eslestirme kodu portundan farkli olabilir.'
$connect=Read-Host 'Baglanti IP:PORT'
if($connect -notmatch '^\d{1,3}(\.\d{1,3}){3}:\d{2,5}$'){
    Write-Host 'Gecersiz IP:PORT.' -ForegroundColor Red;Read-Host 'Enter ile cik';exit 4
}
& $adb connect $connect
Write-Host ''
Write-Host 'Yetkilendirilmis cihazlar:' -ForegroundColor Cyan
& $adb devices
Write-Host ''
Write-Host 'Yalnizca pil bilgisi (dumpsys battery):' -ForegroundColor Cyan
& $adb shell dumpsys battery | Select-String 'level:|scale:|status:'
Write-Host ''
Write-Host 'Steelseries Dock Customiser > Cihaz pilleri > Android ADB ile telefon pilini oku'
Read-Host 'Kapatmak icin Enter'

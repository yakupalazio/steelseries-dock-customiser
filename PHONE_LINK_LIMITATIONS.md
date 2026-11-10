# Phone Link battery research — v1.1.0 update

11 Kasım 2026'da Galaxy S24+ bağlıyken Phone Link UI Automation üzerinde pil grup etiketi **`Piliniz şu anda %54`** olarak doğrulandı. Eski ayrıştırıcı yalnızca bağımsız `pil` kelimesini kabul ettiğinden Türkçe `Piliniz` ekini reddediyordu. Yeni sürüm bu kısa, doğrulanmış durum etiketini okuyarak **54%** döndürdü; mesaj içeriklerine erişilmez. Tamamen kapalı Phone Link uygulamasından veri alma hâlâ garanti edilmez.

## Historical v0.9.8 findings

## What works

- Microsoft Phone Link's own **battery status accessibility Group** can be read via Windows UI Automation when its app window is open **or minimized**. This is read-only and does not access messages, contacts or files.
- The v0.9.8 reader now identifies owned windows by the PhoneExperienceHost process instead of requiring one particular WinUI window class name, making it more resilient to UI version changes.
- The battery dialog includes a manually triggered **Phone Link'i aç ve küçült (ADB gerekmez)** button. It opens the official app, minimizes its window and refreshes the reading. This may briefly show the app as it opens and does not guarantee the last phone sync is current.

## What remains unsupported

When the Phone Link window is completely closed, `PhoneExperienceHost` and `CrossDeviceService` remain running and Windows 11 **Start** can still display phone battery. That does **not** imply a public third-party battery API.

We inspected CrossDevice settings metadata and identified internal types named `ISettingsDeviceStatus` and fields `BatteryPercent`, `BatteryCharging`, and `BatteryPowerSave`.

Ordinary and developer-mode **MSIX-packaged** read-only activation of `CrossDevice.Settings.Utilities.SettingsUXHostServer` each returned HRESULT **0x80040154** (`class not registered`). The temporary MSIX test package was uninstalled. No permission bypass, custom Windows service, registry mutation, process injection or private database extraction was attempted.

Microsoft's published Phone Link task-continuity integration is a **Limited Access Feature** and is not a general battery-reading interface: https://learn.microsoft.com/en-us/windows/cross-device/phonelink/

## User preference

The user does not want Android developer debugging or ADB kept enabled whenever Wi-Fi changes. v0.9.8 leaves ADB **disabled**, does not ship the ADB pairing helper in the new project, and does not request Android debugging permissions. Prior development v0.9.7 remains unchanged.

If Phone Link is not opened/minimized and no other independently verified source exists, the phone shows `--%` rather than a cached/fabricated percentage. The optional minimized-window button is a workaround, not a guarantee that the closed-window background data source was unlocked.

No Windows toggle currently known can be guaranteed to enable direct access to those undocumented CrossDevice components from an independent desktop application.

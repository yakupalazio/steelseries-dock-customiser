# Steelseries Dock Customiser v0.9.7 — 1 Hz OLED + Android ADB Option

Independent Windows .NET 10 128×64 OLED designer for the SteelSeries Arctis Nova Pro Wireless base station.

## Screen refresh: once per second

- **Live dock mode checks the display once every 1,000 milliseconds**, not every four seconds.
- Marquee text moves by **3 native OLED pixels per second** instead of skipping one full character. Text is clipped at the left boundary so it doesn't overwrite the source icon.
- Media playback information is sampled from Windows GSMTC **every second**. Track title, artist, source logo and timeline progress are refreshed as Windows reports new data.
- Windows laptop battery and SteelSeries GG (Aerox 3 Gen 2 / Nova Pro headset and spare battery) are queried on a lightweight **1-second cadence**.
- Phone Link, optionally authorized Android ADB and experimental DualSense HID are sampled about **every 5 seconds**, avoiding frequent costly background UI scans and Bluetooth enumerations.
- The **full OLED frame is rendered and compared every second**, but the app transmits USB updates **only if actual lower-screen pixels changed**, to reduce CPU, I/O and unnecessary OLED refreshing. It does not manufacture changes when a physical percentage has not changed.
- The original SteelSeries factory indicator strip **Y=0..9 is never sent**. The custom area is Y=10..63, written as two partial patches (128×48 at Y=10; 128×8 at Y=56).

### Live test controls

Use **Dock Preview → Canlı dock modunu başlat**. This is opt-in and off by default. It tries to return to the normal SteelSeries interface when stopped. Disable SteelSeries GG's own Now Playing overlay while using it, to avoid simultaneous screen writers. For safety, only the separately installed SHA-256-verified ggoled v0.3.0 executable is supported; it is not distributed with this project.

## Android phone: optional direct ADB battery source

The user wants a Samsung **Galaxy S24+** battery reading even when the Phone Link window is closed. Microsoft's internal CrossDevice interface remained unavailable to a packaged full-trust test app (HRESULT 0x80040154).

v0.9.7 adds a strictly opt-in, read-only **ADB battery adapter**:
- It detects exactly one authorized ADB device and verifies the expected S24+ model ID (`SM-S926*` or S24+).
- It runs only `adb devices`, `adb shell getprop ro.product.model` and `adb shell dumpsys battery`.
- It parses `level`, `scale`, `status` and charging information; invalid numbers are rejected. **No hard-coded percent.**
- It does not access files, photos, contacts, messages or other phone data, does not install apps or write settings.
- If ADB is not available / authorized, Phone Link UI Automation remains a fallback, and its absence results in `--%`.

**ADB grants broad debugging access to the authorized computer beyond what this app uses. Only enable it with a trusted PC and network.**

### Pairing with Wi-Fi (no USB cable needed)

1. On Galaxy S24+, enable Developer Options: **Settings → About phone → Software information → Build number**, tap seven times (if not already enabled).
2. Open **Settings → Developer options → Wireless debugging**, enable it. Ensure phone and PC are on the same trusted Wi-Fi.
3. Open **Pair device with pairing code**.
4. Download the official **SDK Platform-Tools for Windows** from https://developer.android.com/tools/releases/platform-tools and accept Google's license in the browser. Save the Windows ZIP in **Downloads** as `platform-tools-latest-windows.zip`.
5. Run `Telefon_ADB_KUR_VE_ESLESTIR.cmd` inside this project. It extracts only your already-downloaded ZIP to `%LOCALAPPDATA%\SteelseriesDockCustomiser\platform-tools\`, asks for the temporary pairing IP:port, and invokes the official interactive pairing prompt locally. Do not post the pairing code in chat.
6. When prompted, provide the **connection** IP:port shown on the Wireless debugging main screen (which may differ from the pairing port).
7. In **Tasarım & Piksel Editörü → Cihaz pilleri**, enable **Android ADB ile telefon pilini oku**.

The helper never stores the pairing code. Windows ADB's own trusted keys are managed by Google's adb implementation. Wi-Fi debugging may require reconnection after a reboot or Wi-Fi change. Android 17 and recent ADB versions may improve reconnection behavior, but uninterrupted connection isn't guaranteed.

### Cable alternative

Enable **USB debugging** under Developer options, connect by a USB data cable, unlock the phone and explicitly approve the computer's RSA debugging permission prompt. The same ADB battery reader works, with no Wi-Fi pairing required. You may disable debugging after testing; live ADB readings will then stop.

## Other functionality

- Local native OLED pixel editor and icons, photo/perspective preview, 3 selectable external battery widgets, undo/redo, JSON layouts and PNG export
- Automatic media source detection (Spotify logo for Spotify) and track scrolling
- Independent clock/date formats and 1–3 pixel stroke thickness
- GG device batteries from current read-only local status
- Experimental DualSense read-only input decoder, off by default

## Build

```powershell
dotnet build -c Release
.\bin\Release\net10.0-windows10.0.19041.0\SteelseriesDockCustomiser.exe --self-test
```

Run `ONIZLEMEYI_AC.cmd` to open the local application.

This independent personal prototype is not affiliated with SteelSeries, Microsoft or Google. The official SteelSeries dock product photo and third-party ggoled executable are not redistributed.

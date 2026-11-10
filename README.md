# Steelseries Dock Customiser — v0.9.4 (Experimental Physical OLED Test)

Windows .NET 10 desktop project for designing **128×64 monochrome OLED** layouts, previewing them on a dock photo, and reading live battery statuses from supported sources.

## New: send a test image to the physical dock

**Dock Preview → Gerçek dock'a gönder (10 sn)**

This is a deliberately limited experiment: select Custom and manually click the button. A confirmation explains that the full 128×64 physical display, including factory battery indicators, may be replaced for 10 seconds. The software then calls **return to SteelSeries UI**. You can click **Dock ekranını geri yükle** earlier.

**This is not a persistent wallpaper or a firmware change.** The physical OLED is not read back, so a successful CLI return code is not independent proof that the visual appeared; the owner must confirm by looking at the base station. **Factory battery indicators are not guaranteed to remain on the screen during these temporary tests.**

The experiment uses the separately installed open-source tool [ggoled v0.3.0](https://github.com/JerwuQu/ggoled/releases/tag/v0.3.0), which explicitly supports Arctis Nova Pro Wireless and its 128×64 OLED. It delegates only to the tool's **img** and **return** commands. It never invokes brightness, firmware updates, headset settings, or arbitrary USB packets.

- A ggoled CLI downloaded from the official release is required locally at `%USERPROFILE%\Documents\Codex\DockHardwareResearch\ggoled-v0.3.0.exe` for this private experimental build.
- The application checks the exact SHA-256 for the tested official v0.3.0 executable before invoking it. If absent or different, sending is disabled.
- The third-party executable is **not redistributed** in this repository.
- A successful temporary test followed by `return` is not a substitute for user visual confirmation.
- ggoled is a third-party project, not an official SteelSeries driver. The software is provided for testing only.
- Do not leave static high-contrast OLED images on screen for extended periods, to reduce burn-in risk.

**Existing functions are preserved:** icon editor, personal photo calibration, undo/redo, 1-bit text, real SteelSeries GG batteries, phone battery via Phone Link UI Automation (when the UI is open/minimized), and opt-in experimental DualSense read-only HID.

## Why not GameSense?

The documented [SteelSeries GameSense screen list](https://github.com/SteelSeries/gamesense-sdk/blob/master/doc/api/standard-zones.md) does not identify Nova Pro Wireless's 128×64 display. A successful GameSense API request therefore does not establish hardware OLED compatibility. The independently tested ggoled device-specific path is used for this small experiment instead.

## Phone background + MSIX research

A dedicated isolated `PhoneBatteryResearch` folder includes a temporary **Windows package-identity probe**. We built a full-trust app manifest and attempted developer loose-file registration without changing machine settings:

- Windows returned **0x80073CFF**: Developer Mode or sideloading must be enabled.
- No package was installed.
- The installed CrossDevice package contains `ISettingsDeviceStatus.BatteryPercent`, but unpackaged COM/WinRT activation returned **0x80040154**.
- **No supported background battery provider has yet been validated** for use without the Phone Link UI. The existing UI Automation reader remains in place; no stale Bluetooth values are used.

Developer Mode is a machine-wide security policy. This experiment **did not enable it** or install a locally trusted developer certificate without authorization. Whether packaged identity would provide access to Microsoft's private interface remains **unverified**.

## Commands

```powershell
dotnet build -c Release
dotnet run -c Release
.\bin\Release\net10.0-windows\SteelseriesDockCustomiser.exe --self-test
```

## Safety and distribution

The local official dock product photo is ignored by Git and may not be redistributed without permission. Third-party ggoled is downloaded separately and is not bundled.

This prototype is **not affiliated with or endorsed by SteelSeries or Microsoft**.

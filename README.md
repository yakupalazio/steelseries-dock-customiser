# Steelseries Dock Customiser v0.9.6 — Live Media and Personalized Clock

A lightweight Windows **.NET 10** app to design and preview a 128×64 monochrome OLED screen for the Arctis Nova Pro Wireless base station. Physical display writing is experimental and opt-in.

## What's new in v0.9.6

### Preserved factory battery indicator strip: 10 rows

The header exclusion is now exactly **Y = 0..9**, leaving **Y = 10..63** for custom OLED content. Both the editor and the physical writer use the same boundary. The hardware writer never transmits pixels from the reserved area and never uses full-screen clear (`-C`).

The external ggoled CLI uses two partially overlapping, byte-block-compatible updates:
- First image: **128×48 pixels at (0, 10)** — physical rows 10–57.
- Second image: **128×8 pixels at (0, 56)** — physical rows 56–63.

The same source pixel positions are projected into Dock Preview. These writes can coexist with the dock's factory indicator line *provided SteelSeries does not concurrently overwrite that region*. The user should visually verify the final 10-row boundary on the actual dock.

### Automatic media player

Reads active media session metadata from Windows **GlobalSystemMediaTransportControlsSessionManager** (GSMTC). Tested with the actual Spotify session on the developer PC. The renderer switches automatically:
- **Playing:** source icon (Spotify logo for Spotify; generic note for other players), track title, artist and timeline-based progress bar.
- **Paused / stopped / no active playback:** customizable clock and date.
- Song names that exceed available OLED width scroll horizontally.
- The existing three battery widgets remain available in both modes.

The layout follows the familiar **Now Playing** icon / song / artist / progress visual pattern, but uses independently implemented 1-bit glyphs and sprites; no SteelSeries Now Playing program assets or artwork have been extracted or copied. The app never reads browser history or the user's media files and sends no media-control commands.

**Otomatik medya** is enabled by default. Turn it off to switch to the existing manual demo mode.

### Independent clock and date customization

Click **Saat / Tarih** from the Editor tab to choose:
- Clock format: `HH:mm`, `HH:mm:ss`, or `hh:mm`.
- Date format: `dd/MM/yyyy`, `dd.MM.yyyy`, `yyyy-MM-dd`, `dd.MM.yy`, or `MM/dd/yyyy`.
- **Clock stroke thickness**: 1, 2, or 3 native pixels.
- **Date stroke thickness**: independently 1, 2, or 3 native pixels.

A native-resolution OLED preview updates instantly. Press **Uygula** then **Kaydet** in the main editor to save the layout. Existing widget dragging and positioning are preserved. Formatting and stroke settings are stored with the layout's other widget properties.

### Physical OLED: one-shot and optional live mode

In Dock Preview:
- **Gerçek dock'a gönder (10 sn)** sends the current 128×64 framebuffer as bounded partial updates and returns to the normal SteelSeries screen after ten seconds.
- **Canlı dock modunu başlat** starts a **user-confirmed**, reversible, live feed. The display updates approximately every four seconds **only if its native OLED pixels have changed**. The current media session is refreshed every three seconds; time/date updates when needed.
- **Canlı modu durdur** or **Dock ekranını geri yükle** sends the ggoled `return` command. App shutdown also attempts to return the SteelSeries display.

**Important:** Disable SteelSeries GG's own Now Playing screen app while using live mode; otherwise both programs may compete for the OLED. Live mode is **off by default**. A crash or USB disconnect can prevent automatic restoration; `Dock ekranını geri yükle` or the official SteelSeries GG UI should be used if necessary. Avoid very long static OLED content to mitigate burn-in.

Physical writes use the separately installed, SHA-256 pinned [ggoled v0.3.0](https://github.com/JerwuQu/ggoled/releases/tag/v0.3.0) CLI, **not bundled with this repository**. Only `img` and `return` are used; no firmware change, brightness alteration or user-pairing change is performed. Do not distribute the locally retained official SteelSeries product photograph without appropriate permission.

## Battery providers retained

- Laptop: Windows PowerStatus
- Aerox 3 Gen 2 + Nova Pro headset & spare battery: local read-only SteelSeries GG Engine status (undocumented vendor API)
- Samsung phone: Phone Link UI accessibility reader (requires an open or minimized Phone Link UI)
- DualSense: optional experimental read-only HID input battery reader (off by default)
- Windows cached Bluetooth battery data is **not** displayed as live.

## Phone Link background research update

The user's Developer Mode is now on. A temporary **MSIX-loose registered full-trust test app** was successfully registered and launched. CrossDevice's undocumented `SettingsUXHostServer` still returned **0x80040154 (class not registered)** from the packaged process. The temporary package was then **uninstalled successfully**.

Thus developer mode alone does **not** make the private CrossDevice battery status accessible. The normal Phone Link reader is unchanged, and phone battery remains unknown (`--%`) when no accessible status UI is available.

## Build / verify

```powershell
dotnet build -c Release
.\bin\Release\net10.0-windows10.0.19041.0\SteelseriesDockCustomiser.exe --self-test
```

Run `ONIZLEMEYI_AC.cmd` in this directory. All tests, timestamps, photos and UI observations are local to the development PC.

**Independent personal prototype; not sponsored or endorsed by SteelSeries or Microsoft.**

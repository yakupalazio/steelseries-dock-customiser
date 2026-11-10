# Steelseries Dock Customiser v0.9.8

Windows .NET 10 desktop app for designing a 128×64 monochrome OLED for the Arctis Nova Pro Wireless base station. Preserves a photographic preview and optional, explicitly confirmed hardware writing through a separately installed **ggoled v0.3.0** CLI. This independent personal project is not endorsed by SteelSeries.

## Three independent screen designs

The editor now contains **Medya çalarken**, **Oyun açıkken**, and **Idle** tabs. Each has its own widget positions, 1-bit painted pixels and saveable layout.

Runtime priority: **game detected → Game**, otherwise **playing media → Media**, otherwise **Idle**. Merely alt-tabbing from an active game does not switch away from Game mode; it stays until the game process exits.

**Media** defaults to source icon, song title, artist, playback progress bar, a small media clock and three dynamic battery slots. Source, track, artist and progress widgets cannot be deleted or hidden in this tab. The small clock can be positioned and customized independently through **Boyut** and **Kalınlık** (1–3 native pixels), with X/Y placement. It belongs only to Media mode.

**Game** defaults to a mandatory "NOW PLAYING:" label and detected game title (plus optional clock and battery slots). Those two functional widgets cannot be deleted or hidden; position/font thickness are editable. Media source widgets cannot be added here. With no running game, the *editor preview* shows VALORANT as a placeholder, not a claim that VALORANT is active.

**Idle** has no mandatory functional widgets. It defaults to clock/date and battery slots, but all can be deleted or repositioned; any number of optional **Custom text** widgets can be added. For clock/date format configuration, use **Saat / Tarih**; for any text or small clock use size/weight controls.

Layouts and factory-header preference save to:
`%LOCALAPPDATA%\SteelseriesDockCustomiser\studio-v098.json`.

The older `studio-v05.json` and previous v0.9.x folders are retained for rollback.

## Factory header protection switch

The editor includes **Üst 10 pikseli koru** (default ON). With protection ON, the upper Y=0..9 area is *not sent to the physical device*, preserving its factory headset/spare battery indicators. Two hardware patches update only Y=10..57 (48 rows) and Y=56..63 (8 rows). The original display is restored when live/test mode stops.

When protection is OFF, the entire 128×64 framebuffer is editable. The next **explicitly confirmed** hardware test/live mode may overwrite original factory battery indicators. Switching back to protection ON never deletes the artwork stored in the first ten rows, but masks it from the actual display. Protection changes automatically end live output so accidental full-screen writes cannot occur without a new confirmation.

Photographic Dock Preview follows the setting: ON preserves the original photo's upper factory row; OFF overlays the custom framebuffer across the whole screen. **The physical screen cannot be read back**, so verify visually.

## Real device battery priorities

Default order:
1. Mouse (Aerox)
2. Phone (Galaxy S24+)
3. DualSense
4. Laptop
5. Nova Pro headset
6. Nova Pro spare battery

The OLED shows the three highest-priority **eligible** devices. Defaults: Mouse + Phone + Laptop; when a **truly connected** DualSense is detected over Bluetooth, it takes the Laptop slot. Unconnected DualSense is skipped; its pairing record alone is not treated as connection. Without a verified DualSense battery report it may display `--%`. In the future, the same priority system can support more devices.

**Cihaz pilleri** allows moving all six sources up/down. Nova Pro's spare battery is now available as a separate real GG source, using the backup battery percentage already reported by GG Engine. Individual battery slot widgets can be added, moved or removed per mode.

Phone Link, ADB (optional) and experimental DualSense HID battery probes run **once every 60 seconds**, not five seconds. GG and laptop battery values still update approximately once per second. DualSense Bluetooth *connection state* (not battery level) is checked separately about every 10 seconds for responsive slot substitution.

## Game detection: read-only SteelSeries GG library

- Uses the **installed GG application's** SQLite `library_scan` table, in read-only mode, to retrieve names and registered game executable paths.
- Compares installed game executable basenames to currently running Windows processes.
- Checks the running-process state every three seconds and refreshes the GG catalog periodically.
- Never injects into games, reads game memory, sends commands to GG, alters profiles, or accesses game account data.
- On this development PC the GG library had **14 installed game entries**, including VALORANT.

This is an independent process matcher using GG's catalog, **not** an official GG "active game" API. Some games using shared launcher executables, or not registered in GG Library, may not be detected. To add a game, register it in GG's game library.

## Phone Link without Android developer options

Direct battery access to Windows' internal CrossDevice service still does not have a verified third-party API. The previous packaged MSIX probe (after enabling Developer Mode) remained unable to activate the private service (HRESULT `0x80040154`).

A practical, transparently labeled workaround now exists:

- Existing **Microsoft Phone Link UI Automation** reader extracts only the phone battery percentage/charging status from its accessibility battery group.
- The reader now distinguishes **Phone Link background with no window** from **splash-screen loading** and waits until the real battery UI is available.
- In **Cihaz pilleri**, optional **Phone Link'i gerekirse açıp küçült** lets the app launch the already-installed official Phone Link and minimize it, *only when necessary*, rather than asking the user to open it manually.
- On the development PC, a cold window-close test initially returned `--%`; after waiting for the official Phone Link UI and minimizing it, the reader returned a real displayed value of **57%**. This is a *test observation*, never a hardcoded percent.
- The Phone Link window's battery reading may still be stale if the phone stopped syncing. Freshness cannot be independently verified.
- **USB debugging, wireless debugging and ADB are not needed for this option.** ADB remains disabled by default; it is retained as an optional development/test provider.

The recovery is a **minimized app window workaround**, not an exposed background service API. It can be switched off.

## Running and tests

Open `ONIZLEMEYI_AC.cmd` inside the v0.9.8 folder, or:

```powershell
dotnet build -c Release
.\bin\Release\net10.0-windows10.0.19041.0\SteelseriesDockCustomiser.exe --self-test
```

Read-only diagnostic commands:
```powershell
.\bin\Release\net10.0-windows10.0.19041.0\SteelseriesDockCustomiser.exe --game-scan-file .\game-scan.txt
.\bin\Release\net10.0-windows10.0.19041.0\SteelseriesDockCustomiser.exe --battery-scan-file .\battery-scan.txt
```

Phone recovery diagnostic (opens/minimizes Phone Link if the user intentionally runs it):
```powershell
.\bin\Release\net10.0-windows10.0.19041.0\SteelseriesDockCustomiser.exe --phone-recovery-test-file .\phone-recovery.txt
```

No automatic physical OLED writes occur just by launching the app. The live dock mode remains **off** until confirmed in Dock Preview. Third-party binaries and the official SteelSeries product photo are excluded from Git.

## Technical limitations

- GG internal status and local game library schemas are unofficial and can change after GG updates.
- Battery percentages are never fabricated and Windows cached Bluetooth values are not accepted as live.
- Bluetooth DualSense detection must be verified with a real paired controller; no controller was connected during the v0.9.8 development checks.
- Physical full-screen mode can temporarily overwrite the built-in battery indicators, so use it intentionally.
- A game was not launched during the isolated game-detection test; the initial source scan, model logic, and process matching were verified.

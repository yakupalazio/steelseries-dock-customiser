# Steelseries Dock Customiser v0.9.8 — Three OLED Modes & Battery Priority

A Windows/.NET 10 OLED pixel studio for the SteelSeries Arctis Nova Pro Wireless base station, with optional physical OLED updates through a separately installed, verified ggoled v0.3.0 CLI.

## What's new

### Three independent modes

The editor now has **Medya çalarken**, **Oyun açıkken**, and **Idle** tabs. Each mode has independent editable pixels, widget positions, formatting and visibility. Old v0.9.7 single-layout data are imported non-destructively if no v0.9.8 file exists. New layouts are stored in `%LOCALAPPDATA%\SteelseriesDockCustomiser\studio-v098.json` and do **not** overwrite the old file.

| Mode | Must remain | Optional and movable |
|---|---|---|
| Media playing | Source icon, track, artist, progress | Small clock, clock, date, custom text, battery slots and device widgets |
| Game running | NOW PLAYING: label and detected game name | All other non-media widgets |
| Idle | Nothing required | Clock, date, custom text, three battery slots and device widgets |

Use the **Öğe** picker, **Ekle**, **Sil**, 128×64 canvas drag and coordinate controls to arrange elements. Mandatory widgets cannot be removed or hidden. Widgets and pixels are independent per mode; Undo/Redo applies to the currently selected mode.

**Mode switching priority: Game > Media > Idle.** A game detected from GG Moments' installed-game catalog stays on screen while its game process is active, even if Spotify plays in the background. Once the game closes, the OLED falls back to active media or idle. GG's own [Moments game detection](https://support.steelseries.com/hc/en-us/articles/360060113892-Moments-isn-t-detecting-your-game) should list the game. If it doesn't, use GG's **Settings → Game Detection → Scan now / Add game**. This reader matches the GG game library to running Windows processes every five seconds. It does not alter profiles, install hooks or inspect game memory. The GG local database is opened **read-only**, and results are checked against actual process executable names/paths.

**Not yet physically validated:** A specific active game must be launched to test a live automatic transition. Synthetic game detection and rendering tests pass.

### Custom clocks and text

A smaller **SmallClock** widget is part of the default media layout, positioned above the song title. Choose it in the list and independently edit its pixel X/Y, **font size**, and **1–3-pixel thickness**. The same controls apply to date and optional custom text. The existing Saat/Tarih dialog retains format options.

When media stops and no game runs, the Idle profile appears. Add **Custom text** to show your own editable line without forcing a message into the default layout.

### Protect 10 pixels: optional

The **Üst 10 px koru** checkbox is ON by default. When ON, the physical OLED writer sends only rows **10–63**, in two safe partial updates (128×48 at y=10 and 128×8 at y=56), leaving the original SteelSeries status row untouched.

When OFF, the entire **128×64** can be drawn. The physical writer sends a full-height image at y=0, and the photo preview overlays the top status bar too. **This hides/replaces the original SteelSeries battery indicators on the physical screen while active**. The temporary and live modes request confirmation; changing this setting alone does not write to hardware.

### Battery priority and spare Nova Pro battery

There are six available battery sources: **Mouse, Laptop, Phone, DualSense, Headset, SpareBattery** (dock's removable headset battery).

The GG Engine reader now gives `SpareBattery` its **own percentage and charging status**, not merely a mention in the headset detail.

Select **Cihaz pilleri** to reorder them with **Yukarı** and **Aşağı**. The three most appropriate priority slots are shown in each mode only if that mode has battery-slot widgets.

Default: **Mouse, Laptop, Phone**. **DualSense replaces Laptop** when connected (rule ON by default), and the default order is restored after disconnect. The connected DualSense status can be detected without an exact battery percentage. Optional experimental **read-only HID** is needed for an estimated percentage (in ~10% bands); enabling it can conflict with other controller software.

Phone and DualSense are sampled about **once per minute**. GG mouse, headset, spare battery and Windows laptop battery are refreshed on the lightweight per-second loop. Pixels are compared each second and unchanged physical frames are skipped.

### Phone Link: no Android debugging needed

The user does not want persistent USB/Wi-Fi debugging. **ADB is not required or automatically used** in v0.9.8.

Phone Link can expose its current displayed battery status through Windows UI Automation while the **Phone Link window is open or minimized**. The reader now identifies windows by owning process rather than a single hard-coded WinUI class, which helps after Phone Link updates.

If Phone Link is fully closed, Windows can still show battery in the Start menu, **but no supported third-party background battery API was found**. Attempts to activate Microsoft's internal CrossDevice class from both ordinary and MSIX-packaged apps returned `0x80040154`. Developer Mode alone does not grant access. No Windows setting can be guaranteed to unlock this undocumented API.

To avoid manually opening Phone Link, the battery dialog offers **Phone Link'i aç ve küçült (ADB gerekmez)**. Clicking it explicitly opens the official Phone Link UI, minimizes it and refreshes the battery. The window remains minimized; this is a workaround, *not* direct background-service access, and the last-synchronization age cannot be independently verified. No private app database, contacts, messages or photos are read. When unavailable, display `--%` instead of stale Bluetooth cache.

### Safety

Physical OLED updates are **opt-in** from Dock Preview; do not run GG's own Now Playing in parallel. Only verified ggoled `img` and `return` commands are executed. No firmware changes, USB settings writes or global TLS exceptions. The official SteelSeries photo and separate ggoled utility are not included in the Git project.

The GG integration reads undocumented local vendor status and game databases and may need updating if the vendor changes formats. The app is independent of SteelSeries, Microsoft and Google.

## Build and run

```powershell
dotnet build -c Release
.\bin\Release\net10.0-windows10.0.19041.0\SteelseriesDockCustomiser.exe --self-test
```

Double-click `ONIZLEMEYI_AC.cmd` to start the program. All previous v0.9.x folders are retained; this v0.9.8 build is a local test version until you verify the new screen layouts.

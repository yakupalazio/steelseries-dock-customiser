# Steelseries Dock Customiser 1.0 — OLED Studio

Independent Windows 11 desktop designer for the SteelSeries Arctis Nova Pro Wireless base station's **128×64 monochrome OLED**. This is a personal project and is not an official SteelSeries product.

## New in 1.0: one integrated, responsive workspace

A completely redesigned GG-inspired dark UI replaces the old Editor and Dock Preview tabs with **one wide window**:

- **Left sidebar:** Media / Game / Idle layout selection; Battery Priority, Icon Studio and Clock/Date shortcuts; detected runtime game/media status.
- **Middle, upper area:** Large, clean dock hardware photograph with **no photographic background** (the included development photo has transparency, automatically cropped to the device outline); the live simulated OLED is aligned to the physical screen.
- **Middle, lower area:** Pixel-perfect **native 128×64** editing canvas, selection, pen/eraser, optional grid and undo/redo. The canvas automatically adjusts integer pixel zoom to available screen size and keeps pixel boundaries sharp.
- **Right sidebar:** Human-friendly widget catalog and hierarchy, Add/Delete, and selected-widget properties (text, visibility, font size, stroke weight, X/Y and optional Mini Player width).
- **Top toolbar:** Save, Load, export PNG.
- **Bottom status bar:** Current data and header protection state.

**Preview updates immediately with edits** to the selected design, whether or not that design is active on the physical device. Real live dock mode continues to follow runtime priority **Game > Playing media > Idle**. The "Custom / Original / 10-sec test / Live / Restore / Photo / Align" actions are directly beneath the dock photograph, not in a separate tab.

The photo is a **simulation**, not a capture from the hardware. Real device output only occurs after pressing the appropriate test/live control and confirming the dialog.

### Modes and widgets preserved

- **Media:** Spotify/generic source icon, song, artist, progress bar and customizable small clock.
- **Game:** A required "NOW PLAYING:" label and game title, plus an optional Mini Player that shows the playing song while gaming and stays black when paused.
- **Idle:** Completely customizable clock/date, optional text and battery widgets.
- Three dynamic prioritized battery slots, Nova Pro spare battery source, optional DualSense override, pixel icon editor, per-mode saved layouts, image/photo calibration.

### Charging lightning symbol

When a device's **validated live source explicitly reports it is charging**, a **3×7 native-pixel lightning bolt** appears immediately next to the percentage. Sources currently capable of reporting this include:
- SteelSeries GG: supported Aerox mouse, Nova Pro headset and dock spare battery.
- Windows PowerStatus: laptop only when Windows reports actual **Charging**, not merely plugged in.
- Microsoft Phone Link: when its accessible battery state explicitly reports charging.
- Android ADB (optional): Android's battery status 2.

A known percentage without charging status shows **only the percentage**. A missing reading shows **--%**, never a fabricated number. A headset detail mentioning a charging *spare* battery does not incorrectly cause a headset charging symbol.

## Saved settings and rollback

New layouts are saved to:
`%LOCALAPPDATA%\SteelseriesDockCustomiser\studio-v100.json`

On first launch, 1.0 reads the existing `studio-v099.json` (or `studio-v098.json`) if available. Clicking Save writes **only the new v1.0 file**. Old layout data and source folders are preserved, without automatic conversion in-place.

Battery priority preferences, icon sprites and dock photo calibration remain in their existing user-local settings.

## How to run

On the development PC, open `ONIZLEMEYI_AC.cmd` in this project folder. Or run:

```powershell
dotnet build -c Release
.\bin\Release\net10.0-windows10.0.19041.0\SteelseriesDockCustomiser.exe --self-test
```

The third-party [ggoled 0.3.0](https://github.com/JerwuQu/ggoled/releases/tag/v0.3.0) is kept **separately installed** for the experimental physical OLED writer, SHA-256-verified before output, and is never bundled into Git. The device's original top ten rows remain protected by default; the header protection switch can allow full OLED editing, only with explicit confirmation on physical writes.

## SteelSeries GG integration — compatibility and next stage

The official [GameSense SDK](https://github.com/SteelSeries/gamesense-sdk) allows a developer's app to **register and appear in Engine's Apps list** without submitting it for approval. GameSense apps communicate with the Engine's REST API, but this does **not** embed an arbitrary Windows desktop GUI as a native GG page.

Our **full graphical editor remains standalone in 1.0**. For the physical Nova Pro Wireless OLED we currently use the separately tested ggoled path because official GameSense screen zones do not unambiguously document the newer Nova Pro Wireless **128×64** OLED:

- https://github.com/SteelSeries/gamesense-sdk/blob/master/doc/api/standard-zones.md

A later opt-in GG GameSense registration could add an Apps entry / supported events **after checking Nova Pro Wireless zone compatibility and conflict behavior**. No undocumented GG app files are patched or injected, and no registration is performed without user consent.

## Privacy and safety

- GG battery status and Library catalog are inspected read-only; no game process injection.
- Windows Media sessions provide playing song title/artist and timeline; no Spotify account token.
- Phone Link battery reading uses its accessibility status while open/minimized, with an optional user-enabled open-and-minimize recovery. It does not read messages, photos or contact data.
- No direct firmware modification or automatic full-screen OLED writes.
- User-specific official product photo stays **local and excluded from Git**. If this project is built on another PC without the photo, the Dock Preview will ask for a locally selected reference photo.

## Verification status

- .NET 10 Release compilation and headless self-tests (renderer, photo alignment, mode priority, Game Mini Player, battery priority, charging indicators, integrated v1.0 workspace).
- The photo-based preview and native edit canvas have been visually inspected in Windows.
- **Physical dock appearance of this 1.0 build has not been independently confirmed yet**. Use the same on-demand test/restore buttons to validate on the user's own device before publishing a public release.

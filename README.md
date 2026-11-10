# Steelseries Dock Customiser 1.1 — OLED Studio

## New in 1.1: reliability, Phone Link and the dock wheel

- Fixed the integrated **Live / 10-second Test / Return** buttons: they now invoke the real guarded hardware actions instead of clicking invisible legacy controls.
- Removed **Photo** and **Align** controls. The original product photo stays in the user's **local Windows build**, while the GitHub source still excludes that user-provided binary asset. A vector fallback is shown on fresh source builds without it.
- Moved **Battery & Priority** into the dark-styled **Settings → Pil ve öncelik** page; the separate left-sidebar shortcut is gone.
- Fixed the Turkish Microsoft Phone Link accessibility label **“Piliniz şu anda %54”** by recognizing localized suffixes. Verified on the connected Galaxy S24+ without ADB. A phone's displayed status may still lag real battery sync.
- Added an **optional Volume / ChatMix wheel widget** in each design mode. It decodes the Nova Pro Wireless HID **read-only** notifications: `07 25 raw` for hardware volume, `07 45 game chat` for ChatMix balance. It never writes mixer settings. It shows `--` until a wheel event and labels an inaccessible device rather than guessing data. A button press itself does not supply a guaranteed standalone event; the view changes with the first volume/ChatMix dial movement.
- Wheel HID detection and read-only open/close succeeded on the actual Windows PC while SteelSeries GG was running. **Physical wheel-turn event behavior still needs on-device acceptance testing.**

Independent Windows 11 desktop designer for the SteelSeries Arctis Nova Pro Wireless base station's **128×64 monochrome OLED**. This is a personal project and is not an official SteelSeries product.

## New in 1.0: one integrated, responsive workspace

A completely redesigned GG-inspired dark UI replaces the old Editor and Dock Preview tabs with **one wide window**:

- **Left sidebar:** Media / Game / Idle layout selection, Icon Studio and Clock/Date shortcuts, runtime game/media status, and Settings (including Battery Priority).
- **Middle, upper area:** Large, clean dock hardware photograph with **no photographic background** (the included development photo has transparency, automatically cropped to the device outline); the live simulated OLED is aligned to the physical screen.
- **Middle, lower area:** Pixel-perfect **native 128×64** editing canvas, selection, pen/eraser, optional grid and undo/redo. The canvas automatically adjusts integer pixel zoom to available screen size and keeps pixel boundaries sharp.
- **Right sidebar:** Human-friendly widget catalog and hierarchy, Add/Delete, and selected-widget properties (text, visibility, font size, stroke weight, X/Y and optional Mini Player width).
- **Top toolbar:** Save, Load, export PNG.
- **Bottom status bar:** Current data and header protection state.

**Preview updates immediately with edits** to the selected design, whether or not that design is active on the physical device. Real live dock mode continues to follow runtime priority **Game > Playing media > Idle**. The "Custom / Original / 10-sec test / Live / Restore" actions are directly beneath the dock photograph, not in a separate tab.

The photo is a **simulation**, not a capture from the hardware. Real device output only occurs after pressing the appropriate test/live control and confirming the dialog.

### Modes and widgets preserved

- **Media:** Spotify/generic source icon, song, artist, progress bar and customizable small clock.
- **Game:** A required "NOW PLAYING:" label and game title, plus an optional Mini Player that shows the playing song while gaming and stays black when paused.
- **Idle:** Completely customizable clock/date, optional text and battery widgets.
- Three dynamic prioritized battery slots, Nova Pro spare battery source, optional DualSense override, an optional read-only volume/ChatMix wheel widget, pixel icon editor and per-mode saved layouts.

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

Battery priority preferences and icon sprites remain in their existing user-local settings. Legacy photo calibration data is preserved but no longer used in the v1.1 interface.

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

## Settings, About and hidden OLED mini FPS

Open **Ayarlar → Hakkında** for the app information and the **Yakup Can Palaz** signature. While the About panel has keyboard focus, enter the classic Konami sequence:

`↑ ↑ ↓ ↓ ← → ← → B A`

This launches **DOOM MODE**, an original monochrome 128×64 ray-casting mini FPS with walls, an enemy, health, shooting and a pixel gun. It is an Easter egg **inspired by retro FPS games**, not the copyrighted id Software DOOM game or engine.

- `W/S` or `↑/↓`: forward/back, `A/D` or `←/→`: turn, `Space`: fire, `R`: reset.
- `ESC`: close the game and return to the normal OLED Studio.
- The game first runs **only in the Windows preview**. No hardware command is sent by unlocking it.
- **Gerçek OLED'de dene (20 sn)** is an optional, explicitly confirmed *experimental* test on the Nova Pro Wireless base station. It replaces the entire 128×64 OLED image for at most 20 seconds, including the factory battery strip. The user can stop it at any time and the SHA-verified separate `ggoled` utility sends `return` afterward.
- Fast output uses the restricted OLED collection only, with HidSharp feature reports; no firmware modifications, brightness changes, device settings edits or unattended automatic starts.
- It refuses to start while the in-app normal live OLED writer is active, if the USB OLED collection cannot be verified, or if the validated restore utility is unavailable.
- **High-frame-rate physical streaming has not yet been visually verified on the user's dock.** Check on the actual device before marking the Easter egg hardware path release-ready.

Developer read-only diagnostics: `--oled-hid-check` / `--oled-hid-open-test` check the OLED interface without sending a frame; `--dial-hid-check` / `--dial-hid-open-test` check the separate knob notification interface without changing audio. Neither writes a device command.

See `THIRD_PARTY_NOTICES.md` and `THIRD_PARTY_LICENSES/` for HidSharp (Apache 2.0) and external ggoled notices.

## Privacy and safety

- GG battery status and Library catalog are inspected read-only; no game process injection.
- Windows Media sessions provide playing song title/artist and timeline; no Spotify account token.
- Phone Link battery reading uses its accessibility status while open/minimized, with an optional user-enabled open-and-minimize recovery. It does not read messages, photos or contact data.
- No direct firmware modification or automatic full-screen OLED writes.
- The user-provided SteelSeries product photograph stays **excluded from Git**. Local Windows packages built on the development PC contain the photo for personal use or direct review by SteelSeries; fresh source builds without the asset draw a vector dock. Redistribution permission for someone else's product photograph is not implied by owning the device.

## Verification status

- .NET 10 Release compilation and headless self-tests (renderer, photo alignment, mode priority, Game Mini Player, battery priority, charging indicators, integrated v1.0 workspace).
- The photo-based preview and native edit canvas have been visually inspected in Windows.
- **Physical OLED appearance, 1.1 wheel-event delivery and live/test interactions still need hands-on confirmation.** The read-only wheel interface could be opened while GG was running, but no actual wheel-turn event was captured during automated validation. Use on-demand test/restore with your own device before publishing a stable public release.

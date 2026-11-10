# Changelog

## 1.1.0 — Wheel widget and integration fixes

- **In-place refinement (no version bump):** replaced the timed 10-second test with persistent, separately confirmed **Uygula**; relabeled restore to **Dock Reset**. Static output is restored on normal shutdown or explicit Reset, and Reset is available after relaunch if the app crashed.
- Removed the DOOM 20-second cutoff; after explicit confirmation, the PC shows a dock-facing instruction rather than the gameplay. The physical OLED alone receives game frames until Stop/ESC/close/focus loss. Restore and OLED burn-in warnings remain.

- Corrected Live, 10-second physical test and Return buttons: old hidden controls no longer swallow clicks; separate device confirmation is unchanged.
- Removed Photo and Align buttons and stopped loading photo overrides; the original product photograph can be included in the private Windows package but is excluded from Git.
- Moved Battery & Priority into a GG-style dark Settings page next to About; preserved author signature and About-only Konami unlock.
- Fixed Phone Link Turkish `Piliniz şu anda %54` accessibility label; actual connected device returned 54% without ADB.
- Added an optional read-only Volume/ChatMix OLED widget for Media/Game/Idle, decoding `07 25` and `07 45` HID events; no audio/device configuration writes.
- Added synthetic HID decode/render and interface discovery tests. Read-only interface open/close passed while GG was running. Physical wheel event streaming requires acceptance testing.

## 1.0.0 — GG-inspired widescreen OLED Studio

- Rebuilt the app as a responsive, resizable **single-page editor and dock preview**. No separate preview tab.
- Dark GG-inspired original UX: left navigation, middle dock + pixel canvas, right widget tree/properties, persistent save/export and status.
- Embedded large transparent dock device photograph, automatically removing alpha padding **without changing photo source files or screen calibration coordinates**.
- Changes to selected Media/Game/Idle layout reflect on the **photo immediately**, while physical live mode independently selects Game > Media > Idle.
- Pixel edit canvas changes integer zoom on window resizing, retaining actual 128×64 pixel coordinates and custom icon support.
- Localized widget catalog and tree names (battery slots, song, artist, game, Mini Player, spare battery).
- Added a 3×7 native-pixel charging lightning icon next to battery percentages, only for explicit, verified charging values.
- Extended charging state across SteelSeries GG, Phone Link, optional Android ADB and Windows PowerStatus.
- Independent 1.0 layout store `studio-v100.json`, loading earlier v0.9.9 / v0.9.8 files only as read-only migration sources.
- Preserved opt-in physical 10-second frame, live 1Hz, return, photo import and four-point calibration.
- Added regression tests for the new one-page workspace and charging-flag behavior; removed obsolete two-tab assertions.
- Matched the dark off-pixel OLED photo composite to the surrounding black dock bezel; removed the grey preview rectangle.
- Added **Settings → About**, the Yakup Can Palaz signature and hidden Konami-code entry.
- Added an original playable 128×64 monochrome DOOM-style mini FPS (not the licensed DOOM game/engine); ESC returns to the normal application.
- Added optional 20-second, fully user-confirmed physical OLED gameplay streaming via a restricted Nova Pro Wireless HID collection. It is off by default, stops on ESC, checks for competing in-app live output and restores via the SHA-verified ggoled CLI.
- Added read-only HID collection detection, successful open/close without drawing, and report-packing/self-test coverage. The physical fast-stream mode still requires on-device visual acceptance testing.
- Added HidSharp 2.6.4 attribution, Apache 2.0 license and NOTICE files.
- No unattended hardware output, firmware writes or SteelSeries GG registration.

## 0.9.9 — Game Mini Player

- Optional mini song display when game and music run together; black if paused.

## 0.9.8 — Independent mode layouts

- Separate Media/Game/Idle layouts, GG game matching, ranked battery sources and optional 10px header.

## 0.9.7 — One-second physical OLED update

- Native-pixel scrolling, battery polling refinements and Android ADB optional provider.

## 0.9.6 — Windows media session integration

- Spotify detection, game/music fallback, configurable clock and date.

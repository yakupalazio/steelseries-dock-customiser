# Changelog

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
- No automatic hardware output, GG app registration, firmware changes or public GitHub push.

## 0.9.9 — Game Mini Player

- Optional mini song display when game and music run together; black if paused.

## 0.9.8 — Independent mode layouts

- Separate Media/Game/Idle layouts, GG game matching, ranked battery sources and optional 10px header.

## 0.9.7 — One-second physical OLED update

- Native-pixel scrolling, battery polling refinements and Android ADB optional provider.

## 0.9.6 — Windows media session integration

- Spotify detection, game/music fallback, configurable clock and date.

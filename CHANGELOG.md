# Changelog

## 0.9.8 — Three OLED modes, read-only GG game detection, battery priorities

- Added three independent editable profiles (**Medya çalarken / Oyun açıkken / Idle**) with mandatory media-player and game-title widgets, optional battery/clock/date/text.
- Added **SmallClock** with independent size, pixel thickness, placement and clock format in media mode.
- Added persistent v0.9.8 workspace JSON with non-destructive migration from the older single-layout save file.
- Added priority-based mode selection: active GG-detected game process > Windows media > idle; game mode persists while process is open.
- Reads the GG Moments detected-game library with read-only Microsoft.Data.Sqlite; never modifies GG settings or game processes.
- Added optional full-screen 128×64 OLED mode, with a default-ON 10-row factory indicator protection switch and matching photographic preview.
- Added GG **SpareBattery** as a separate live device source, and a six-device, user-sortable battery priority list.
- Connected DualSense automatically replaces laptop in the default three battery positions; battery remains unknown without a trustworthy HID input report.
- Changed Phone Link and DualSense polling to once every 60 seconds, leaving GG and laptop on the lightweight 1-second loop.
- Improved Phone Link UI Automation window detection, and added an explicit user-triggered **Open and minimize Phone Link** workaround; no ADB is required.
- Replaced stale Bluetooth percentages with unknown values, never fabricated numbers.
- Added self-tests for all three layouts, GG battery parsing, controller priority, game matching and OLED header state.
- Local build and automated tests passed. Real game/connected DualSense/full-header physical screen behavior still require owner verification.

## 0.9.7 — 1 Hz screen and ADB experiment
- Smooth 3-pixel-per-second scrolling and opt-in Android ADB (no longer used automatically).

## 0.9.6 — Live media
- Dynamic Windows GSMTC playback and optional live physical OLED.

## 0.9.5 — Protected battery area
- Two overlapping lower-area updates preserving the original SteelSeries OLED indicators.

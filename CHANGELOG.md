# Changelog

## 0.9.9 — Optional Game Mini Player

- Added a **GameMiniPlayer** widget, displayed as **Mini Player (Oyun)** in the editing UI.
- Available only in the Game tab; not mandatory, not part of its default layout.
- Game still wins over active music; when playing simultaneously, Mini Player displays the active song name and Spotify/generic source icon.
- When media stops or pauses, the reserved region becomes **solid black** — no lingering song name or fake playback metadata.
- Added adjustable X/Y position, 35–128px box width, font size and 1–3px pixel weight.
- All Mini Player pixels, including 4–20px user-defined icon sprites, are confined to its rectangle; custom icons are resampled to a compact 14×14 badge.
- Long text scrolls by existing 3 pixels/second; no extra media scans or USB writes are introduced.
- Added regression tests for mode restrictions, default absence, paused black region, clipping, pixel-header protection, and JSON settings persistence.
- Store layouts in **studio-v099.json**, migrating from but never overwriting v0.9.8's studio-v098.json.
- Previous v0.9.8 source folder unchanged; no GitHub push or automatic physical display modification.

## 0.9.8 — Separate Media / Game / Idle layouts

- Per-mode designs, process-based GG game detection, battery priorities and 10px header toggle.

## 0.9.7 — Faster OLED and optional phone debugging

- 1-second OLED checking and pixel-based marquee; Phone Link and read-only ADB battery methods.

## 0.9.6 — Windows live music

- Spotify and generic Windows media information plus independent clock/date.

# Changelog

## 0.9.7 — One-second OLED refresh and optional Android battery reader

- Changed physical live OLED timer from 4,000 ms to **1,000 ms**.
- Replaced character-at-a-time song marquee with **3 native pixels per second**, clipped to the display region.
- Refreshed Windows media session every second rather than every three seconds.
- Added 1-second, lightweight laptop + SteelSeries GG battery sampling. Full phone/controller battery readings happen every five seconds.
- Removed cached Windows PnP enumeration from normal live polling; old Bluetooth values are diagnostic only.
- Kept hash-based skip-if-unchanged and in-flight USB write protection.
- Added optional read-only Galaxy S24+ **ADB battery adapter**, with explicit user opt-in and Phone Link fallback.
- Added Google Platform-Tools pairing assistant which does not store pairing codes or auto-accept any SDK license.
- Added regression tests for per-second pixel scrolling and Android battery response parsing.
- Previous versions remain untouched; v0.9.7 is a separate local development checkpoint.

## 0.9.6 — Live media and 10-row OLED header

- Automatically switched Windows media playback / clock-date modes, with Spotify source logo and progress.
- Added opt-in continuous physical dock output and 10-row factory indicator preservation.
- Added independent clock/date stroke thickness controls.

## 0.9.5 — First partial physical OLED updates
- Split custom frame into safe lower-screen segments to avoid original factory bar.

## 0.9.4 — First physical display experiment
- Added temporary ten-second physical OLED write and restore.

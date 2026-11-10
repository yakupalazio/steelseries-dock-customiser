# Changelog

## 0.9.6 — Live Windows media and 10-row OLED header

- Reduced the factory indicator reservation to **10 rows (Y=0..9)**, with only Y=10..63 writable in the physical dock.
- Two patch updates: 128×48 at Y=10, then 128×8 at Y=56, never clear full screen.
- Added Windows GSMTC active media status, Spotify source detection, actual song title/artist and progress.
- Automatic media → clock/date fallback; refreshes metadata every three seconds.
- Added a dedicated clock/date editor with independent 1–3 pixel stroke thickness and configurable formats.
- Added an explicitly opt-in physical **live dock mode** that sends only changed OLED frames and restores the SteelSeries interface on stop.
- Tested MSIX developer-mode package registration and launch; CrossDevice internal activation remained blocked (0x80040154). Temporary package removed.
- Expanded self-tests for media source, track metadata, progress, separate clock/date thickness and precise 10-row protection.
- Previous v0.9.5 source retained and public v0.9.3 GitHub baseline unchanged.

## 0.9.5 — Factory upper indicator preservation
- Corrected reserved rows to 12; added cropped, reversible physical OLED updates.

## 0.9.4 — Experimental dock output
- Added manual, ten-second physical dock test using pinned external ggoled and automatic restore.

## 0.9.3 — Phone Link status
- Read phone status from the accessible UI when Phone Link was open or minimized.

## 0.9.2 — SteelSeries GG battery status
- Read connected Aerox / Nova Pro battery values via local GG Engine status.

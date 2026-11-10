# Changelog

## 0.9.8 — Media / Game / Idle independent layout modes

- Added three independently editable mode tabs with separate layouts and a shared factory-header switch.
- Added priority routing: **Game running > Media playing > Idle**. Running games remain active across Alt+Tab.
- Connected read-only GG installed-game catalog (SQLite library_scan) to Windows process detection.
- Added a smaller, independently positioned/sized/weighted **MediaClock**; game mode mandatory "NOW PLAYING" label/title; optional Idle custom textbox.
- Media widgets only in Media and game widgets only in Game. All nonessential widget groups are optional.
- Factory OLED header can be protected (Y=0..9 excluded from physical writes) or fully edited; changing protection stops live output and requires renewed manual approval.
- Added **SpareBattery** as a separate, current GG source, and ordered six-device battery priority menu.
- Default battery trio Mouse / Phone / Laptop; connected Bluetooth DualSense automatically takes Laptop's slot (battery reading may remain unavailable).
- Phone Link and DualSense battery reads changed from 5 seconds to **60 seconds**. GG/laptop reads remain fast; DualSense link-state checks run separately.
- Improved Phone Link UI reader diagnostic states and optional minimized-window recovery. Confirmed recovery after a closed-window test (57% displayed via Phone Link UI).
- Expanded source regression tests for modes, priorities, y=0..9 protection, spare batteries, game executable matching.
- Retained previous versions and did not send any unsolicited commands to the physical dock.

## 0.9.7 — Faster OLED and optional ADB
- 1-second OLED refresh, pixel scrolling, enhanced battery scheduling.

## 0.9.6 — Spotify, Now Playing and small clock formatting
- Windows GSMTC read-only media integration; automatic media/clock switch and 10-row header.

## 0.9.5 — Partial OLED writes
- User-verified upper factory indicator preservation by partial writes.

## 0.9.4 — Experimental dock hardware output
- Explicit 10-second test and restore via ggoled v0.3.0.

# Changelog

## 1.1.1-beta.1 — Experimental single-shot OLED reclaim (2026-11-11)

- In the user-authorized **Canlı / Live** mode, schedule one `0x95` native release followed by `0x93` custom patches after **450 ms** without a decoded physical wheel report; apply a **900 ms** minimum cooldown. No constant `0x95` traffic, firmware writes, or audio setting modifications.
- Keep the factory top 10 OLED rows untouched in the normal protected layout; fully redraw only when full-screen ownership was separately authorized.
- Prior 450 ms one-shot hardware diagnosis brought the custom screen back after about **0.5–1 seconds** following volume rotation without visible flicker in that run. **This integrated beta path has not yet passed a separate live hardware acceptance test**; the physical mode button may not emit an independent HID event and can still leave the factory UI visible.
- Document the unsuccessful 28/50/60 FPS drawing-only attempts, flicker and lost ChatMix mode during repeated 5 Hz `0x95→0x93`, firmware-timer uncertainty, and safe contributor investigations in [OLED_KNOWN_ISSUES_BETA.md](OLED_KNOWN_ISSUES_BETA.md).
- Add deterministic in-memory regression checks for the one-shot 450 ms scheduling and cooldown. The app remains an unofficial SteelSeries project.



## 1.1.0 — Wheel widget and integration fixes

- **Latest in-place update:** configurable radial wheel gap angle (−180° to +180°, fixed upright VOL/MIX lettering), signed hardware-volume steps, compact Game/Chat badges and center-originating ChatMix arc. Physical wheel recovery uses a bounded rapid reassert burst plus a 500 ms idle fallback, without changing firmware or mixer settings.
- Added a local **Profile Manager**: blank profiles, current-design copies, rename/delete, one-click apply and data-only JSON import/export. Starter widgets can now be removed from Media/Game as well as Idle without silently reappearing after Save.
- Added opt-in CPU, RAM and network ping OLED widgets using lightweight native counters and on-demand ICMP, optional 45-second one-pixel burn-in drift for live output, and automatic OLED reconnect/backoff after USB interruptions.
- Rechecked connected Nova Pro Wireless HID input/output, a successful bounded OLED report write and original-screen restoration, Windows self-tests, and English layout screenshots. The native overlay may still briefly take priority during physical knob rotation; that exact behavior needs manual observation.
- **Physical wheel hotfix (in place, version remains 1.1.0):** replaced slow per-frame CLI process startup with a persistent, opt-in OLED-only HID draw session (existing ggoled patch fallback remains). On every decoded physical dial report, a short delay reasserts the custom display after the dock's native overlay; a half-second fallback also recovers from native overrides when GG consumes the input event. Stopping Live or pressing Dock Reset releases the OLED session.
- Rebuilt the 45×42 1-bit radial wheel with a white filled centre, black `VOL` / `MIX` lettering, and no redundant right-hand label or percentage. The narrower footprint leaves more room for the clock, date and battery widgets.
- Added a dedicated **pixel-by-pixel wheel designer**, with permanent white, dynamic percentage-controlled ring, black and eraser layers, optional auto VOL/MIX centre lettering, live sample-fill slider, factory reset, per-widget JSON persistence and English UI. Switch to Flat Bar from either the editor or the inspector.
- Added tests for native protected 10–63 row frame patches, exact 45×42 layout bounds, per-widget pixel-art persistence, and English editor labels. Hardware wheel interaction still requires real-device acceptance testing.
- **In-place visual and behavior update (still 1.1.0):** rebuilt the right inspector with non-overlapping rows, relevant-property visibility, 1-bit OLED previews, native Windows 11 dark mode, consistent dark input controls and custom stepper/button painting.
- Added a radial, OEM-inspired **Volume / ChatMix wheel** alternative to the flat bar. Native monochrome pixels recreate the segmented arc, center mode badge, and percentage. The wheel is present by default in Idle and Game; it can be edited or removed, and a one-time non-destructive migration upgrades familiar legacy layouts without re-adding an intentionally removed wheel.
- Repacked defaults around the wheel; kept all three bottom battery slots and the protected OEM header. Repaired compact-clock colon and shifted old media/game elements away from overlaps. Charging bolt still appears only when a provider positively reports charging.
- Moved **About** to the bottom of Settings, added a persistent Turkish / English language selector, and localized the main workspace, battery pane, clock, icons and settings dialogs.
- Added regression checks for radial drawing, intact date/header/battery bounds, one-time layout migration, language strings and English/Turkish UI.
- **In-place refinement (no version bump):** replaced the timed 10-second test with persistent, separately confirmed **Uygula**; relabeled restore to **Dock Reset**. Static output is restored on normal shutdown or explicit Reset, and Reset is available after relaunch if the app crashed.

- Corrected Live, 10-second physical test and Return buttons: old inactive controls no longer swallow clicks; separate device confirmation is unchanged.
- Removed Photo and Align buttons and stopped loading photo overrides; the original product photograph can be included in the private Windows package but is excluded from Git.
- Moved Battery & Priority into a GG-style dark Settings page next to About; preserved the About page and author signature.
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
- Added **Settings → About** and the Yakup Can Palaz author signature.
- Added read-only HID device discovery, open/close diagnostics and packet-format regression tests.
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

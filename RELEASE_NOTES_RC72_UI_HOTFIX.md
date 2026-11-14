# RC7.2 — UI, Phone Battery and Lyrics Hotfix (PRIVATE TEST)

Standalone private Win x64 beta preview; not a public 1.0 GitHub release. Original RC7.1 ZIP and saved personal profiles/preferences untouched.

## Changes
- Original compact 680x555 Profile Manager, genuine two-row fixed footer: Close fully visible, overlapping action hint moved, saved profile shown in new static 128x64 miniature OLED preview.
- Battery Priority Settings Save button sits in permanently visible 58px bottom row while other contents scroll, without growing Settings window.
- One uniquely identified connected BLE/Windows Bluetooth phone merges into Phone slot and receives Phone icon; Phone Link percentage wins if present; ambiguous multiple phones not assigned arbitrarily.
- Connected Devices > Change icon: choose 11 built-in sprites, import an image as 11x11 1-bit locally stored pixels (no original path), or restore automatic; renders in OLED battery widget.
- Removed the sample PALE TORTURED / DRACONIAN: inactive song reads No media playing, empty artist.
- Lyric style reset fixed at actual main editor transfer. 1–4 lyric rows and Slide/Fade/Drop/Wipe/Instant retained; serialization regression added.
- Quick Guide: live target control rectangles, scroll-to-target, refresh snapshot, crop full WinForms DrawToBitmap to client area to avoid ~30–45px title-bar offset. Verified Profiles and Live highlighted correctly on screenshots.
- Top protected OEM ten rows: clean hard-pixel 128x10 editor-only simulated header with readable USB-1, rather than broken photographic threshold. Firmware's real OEM rows stay untouched.

## Reality of battery coverage
On this real PC, ATK 373B:101A CompX battery status was observed live at 90%; the user also confirmed success. Other brands and the remaining 27 allowlisted Compx identifiers need beta testers; 364 model names recognized does not imply 364 verified battery readers.

## Safety / tests
New tests cover non-Slide lyric persistence, phone dedup and custom icon bitmap, clean USB-1 header and fixed control footer. Private settings and original ZIPs are preserved; no AGPL driver code or telemetry.

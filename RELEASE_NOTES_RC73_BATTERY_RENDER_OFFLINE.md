# RC7.3 — Battery + Render Stabilization (PRIVATE TEST)

**Private Windows x64 test build · 1.0.0-beta.2-rc7.3.**
The earlier public GitHub v1.0.0-beta.1 and its downloadable ZIP remain unchanged.

## Offline lyrics — EXACT requested behavior

- Timecoded lyrics successfully fetched for an exact song are now persisted locally to a protected application-settings folder (SHA-256 key derived from title, artist and duration, 150 tracks/12 MB cap, atomic JSON update and strict timecode validation).
- After restarting, a previously saved song displays its synced lines even without internet access. The cache is not shipped in any installer or uploaded to GitHub.
- In Lyrics widget settings, **Yerel .LRC içe aktar** imports a user's own timecoded file for the currently playing song, preserving millisecond timing and instrumental gaps.
- **When lyrics cannot be found, cached or imported, the OLED lyrics widget draws absolutely nothing.** There is no "lyrics unavailable" message and no placeholder icon; other media widgets continue normally.
- A temporary failed online lookup does not permanently prevent retrying that track later.

## USB/Bluetooth battery discovery

- Battery Settings shows a distinct **Found devices / Bulunan cihazlar** section for recognized wireless USB/Bluetooth peripherals, including devices for which no verified battery percentage is available (`--%`). Wired USB hubs and coolers are excluded from the battery candidates.
- Right-click a found peripheral to **Track**, **Hide**, **Stop tracking**, or restore hidden items. Tracked names are stored in the user's existing private local preferences, and an offline tracked device remains visible as disconnected (`--%`), never with a stale percent.
- The default follow/priority list STILL contains only **Headset and Spare Battery**. A discovered device is not automatically promoted into the default priority list.
- Exact matching of a single physical USB VID/PID receiver to a single live vendor reading handles the case where Windows calls the mouse “USB Dongle” but the protocol provider names the actual model, without matching two identical receivers to a single reported percent.
- Existing custom icon picker remains in **Connected devices**. No external telemetry, firmware commands or third-party driver installation is introduced.
- **WLMOUSE experimental** battery reader implements independently documented 64/65-byte `0x83` status protocol and passive heartbeat on a limited exact 36A7 allowlist; unsupported Pro variants use passive-only observation until active HID command is independently verified. No real WLMOUSE on the development PC: the beta tester must validate a live percentage and report their VID/PID before classifying support as confirmed.
- Existing ATK 373B:101A CompX read-only battery reader remains intact.

## Game image/rendering

- Fixed 1-bit PNG/JPEG/ICO rasterizer for black-on-white art (no large solid white rectangle), white-on-black art, and transparent black silhouettes.
- Locally imported game art now remains associated with the executable even if SteelSeries GG changes or localizes the display name. Old overrides have a legacy compatibility read path.
- Running foreground games under Steam, Epic Games, Riot, XboxGames and GOG Galaxy installed folders can be detected even if missing from GG's library; GG remains a primary identity source. Only known game install paths are considered, not arbitrary foreground windows/process injection.
- Read-only registered EXE icon fallback remains when there is no custom PNG.

## Dock preview / top OLED header

- The local **original dock photograph** (`dock-photo-local.png`) is preserved and shipped **in this private tester package**. The preview overlays the same 128x64 OLED buffer on that photo. Any missing-asset vector fallback is not the default private appearance.
- The separate 128x10 *simulated* OEM header was refined from the local photo as a cleaner open-earcup headset silhouette and slim charge bolt (with the user's USB-1 label). The physical dock firmware still owns those ten rows; app does not overwrite or reflash native battery icons. **Pixel-perfect physical header parity needs visual acceptance on actual hardware.**
- Photo redistribution license has **not** been independently established: this private test package is not a new public GitHub release.

## Acceptance and limitations

- Release build: 0 errors and 0 warnings; all previously existing and new automated self-tests pass.
- Added regression coverage for cache preservation, local LRC importing, empty-cache completely blank OLED, rendering 32x32 transparent/white/black PNG, exact vendor receiver matching, 64/65-byte WLmouse inquiry and no unauthorized HID probe.
- Offscreen screenshots include realistic photo preview, game logo and media editor, Lyrics settings and a synthetic cross-brand Battery Found devices list. Synthetic previews do **not** prove the external test user's real hardware will return a battery percentage.
- All changes have a Git checkpoint on the private development branch; no current C# source, local profiles, private settings, cached lyrics or user screenshots are included in the public GitHub repository or uploaded.
- Existing v1.0.0-beta.1 remains available on GitHub; this RC7.3 is a separate private validation ZIP until the requested hardware acceptance is completed.

### Protocol references
- WLMOUSE public protocol notes and observed receiver models: https://github.com/HeyOkay/HaloBattery/blob/main/providers/wlmouse.py
- Reference documentation and MIT license: https://github.com/HeyOkay/HaloBattery/blob/main/docs/protocols.md and https://github.com/HeyOkay/HaloBattery/blob/main/LICENSE
- No HaloBattery or OpenMouse source code is bundled; the C# adapter is independently implemented.

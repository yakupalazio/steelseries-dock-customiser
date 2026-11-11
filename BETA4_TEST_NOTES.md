# Steelseries Dock Customiser 1.1.1-beta.4 — local acceptance candidate

**NOT A PUBLIC GITHUB RELEASE.** Hardware acceptance on the user's Nova Pro Wireless dock and first-time DualSense pairing are still pending. Stable and beta.3 releases remain intact.

## Improvements implemented
- Battery bar: selects zero to three devices strictly by the saved priority order and **verified, fresh numeric readings**. Unavailable/stale devices are excluded; higher-priority devices automatically return on recovery. No fake 0%, 100% or `--%` slots. Readings younger than 90 seconds for slow sources, 12 seconds for per-second GG/Windows.
- Phone battery: Phone Link polling never launches, focuses or minimizes a window. The existing inaccessible UIA label cannot be treated as data. Added a guided **opt-in** ADB diagnostic/settings page with official Platform Tools reference; Android SDK path detection improved. Android debugging/pairing is **never enabled automatically**.
- Wheel 45x42 pixel source remains compatible with personal designs. Rendered icon area extends beyond the source bounds without overwriting the rest of the screen. Added an adaptive Game/Chat external-endpoint placement solver; icons stay upright and track ring rotation, fit within rows 10–52, avoid the ring, and honor manual X/Y offsets.
- Game and Chat icons can be hidden separately from the Wheel Workshop. Added full 128x64 OLED overview preview and adjustable ring thickness (1–7 pixels) that grows **inward** without changing the outer radius. Optional solid end caps close both arc endpoints.
- One X click minimizes to the tray; tray restore and quit verified with `--tray-smoke` (no USB/HID). Retains themed tray menu, media toggle, profile selection and auto-live behavior.
- Current icon and layout files were SHA-verified and backed up. Separate personal default snapshots stored under LocalAppData; subsequent layout/icon saves also update those personal default snapshots. Reset uses these user defaults instead of erasing their design. Game/Idle wheel position and appearance align once on first load and remain independently customizable afterward.
- OLED owner recovery unchanged: one `0x95 → 0x93` pulse after 450 ms physical dial inactivity, 900 ms minimum interval. No firmware changes, 5 Hz forced reset loop or repeated pulse spam.

## Still unresolved / constraints
- **Galaxy S24+ actual percentage:** Microsoft's Phone Link accessibility tree on the tested computer does not expose a battery control. Reliable percentage requires an existing accessible status group or user-authorized ADB connection; ADB was neither installed nor enabled in the saved preferences at development time. User must install official Platform Tools and pair/authorize USB or wireless debugging if they choose this route. Until then Phone is automatically omitted from visible battery slots.
- **Press-only Volume / ChatMix mode switch:** still no separate verified HID input report. The original OEM screen may remain for ~5 seconds after press, until a physical dial rotation triggers the proven 1-second recovery. No unsafe firmware modifications.
- **DualSense:** never paired with this Windows computer. First-time Windows Bluetooth or USB pairing is a user action. The optional read-only HID battery reader is approximate (10%-step bands) and may fail if another program owns the HID input. Enable Experimental DualSense HID in Settings and observe actual live reading before claiming automatic battery display.
- This unofficial beta may conflict with SteelSeries GG's own OLED drawing; user controls GG/Sonar.

## Acceptance test before GitHub release
1. Confirm App Beta 4 launches and OLED Live starts without manual click.
2. Close once (X) and confirm the window stays hidden to tray; reopen/quit via tray. Restart command should not leave duplicate instances.
3. While editing Game and Idle wheel, validate same initial coordinates and independent edits; confirm last saved custom icon pixels survive.
4. Rotate ring -180, -90, 0, 90, 180 degrees and check Game/Chat logos remain outside arc and adjacent to endpoints; toggle either logo off, adjust thickness and end caps, then save/reopen.
5. Review battery slot priority with only valid readings, after a source disappears and comes back.
6. Rotate physical Volume wheel and confirm custom OLED restores in ~1 second without flicker. Mode press-only issue is disclosed, not considered fixed.
7. Pair DualSense over Bluetooth or USB, enable optional read-only battery source and check for verified battery reading. If no live report, the device must NOT be displayed with a fabricated value.
8. Only after these hardware observations, consider a new GitHub prerelease.

This document contains no device serials or personal pixels/photos. User data stays local.
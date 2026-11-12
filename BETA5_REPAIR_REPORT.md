# Steelseries Dock Customiser — Beta 5 Repair Acceptance

**Date:** 12 November 2026
**Status:** Local private release-candidate; **not** an authorized public GitHub release.
**Branch:** `feat/beta5-resume-safe` (separate from the working Beta 4 branch).

## Repairs completed

1. Restored the original real dock photograph, SHA-256 matched to the protected Beta 4 portable build. The photo stays local and must **not** be distributed publicly without rights clearance.
2. Fixed Live mode failing to start: removed a development-only authorization gate that also blocked user-confirmed UI operation. Added a built-in, device-limited OLED HID path; an independently verified ggoled utility is now an optional compatibility fallback, **not a requirement**.
3. Rebuilt the MiniWheel editor to use the **same pixel artwork workshop** as the large wheel. Saved compact artwork is independent of the existing large-wheel pixel design; compact size remains separately adjustable.
4. Corrected the mini circular control selector label, added constrained Game/Chat drag target selection, allowed manual per-logo X/Y movement, and enforced ring-sector and collision limits.
5. Replaced visible “DualSense” labels in user-facing battery and icon lists with **Kontrolcü / Controller**. Removed redundant controller priority override and experimental HID/ADB checkboxes; the battery reader selects previously authorized connected sources without installing anything.
6. Read the dock's **spare battery with the headset off** by checking the dock's independent presence. During a real Windows scan, live SteelSeries GG reported the spare battery at 100% while the headset value was unavailable.
7. Added 128×64 whole-OLED low-battery warning at <=20% (4 s), and critical warning at <=10% (5.5 s), with 1.0 s lit / 0.5 s black flashing, user-configured device icon, percentage, localized caption and one-time firmware-header restoration after the alert.
8. Repaired clipped text and overlapping layout in the icon workshop, battery priority list and small-window layout; restored readable proper device names and included font/metadata improvements. Updated legal notes and private build instructions.
9. Preserved Beta 4's personal layout and settings, live OLED recovery logic, and separate installation path.

## Completed verification

- Release compilation: no errors or warnings.
- OLED studio self-tests: passed, including bar ChatMix neutrality, radial icon sector constraints, manual offsets, wheel scale, Bluetooth deduplication, low-battery threshold timing, and blink-frame rendering.
- Real Nova Pro USB OLED protected feature reports accepted; normal OEM screen restored.
- Real Nova Pro full-screen low/critical/black reports accepted; normal OEM screen restored.
- Real WinForms Live UI flow activated, drew a framebuffer, and restored OEM output.
- Real WinForms Live alert scheduler displayed low and critical overlays, terminated them, reclaimed the OEM header, and retained Live mode until explicit stop.
- Windows battery scan confirmed spare dock battery without the headset being available.
- Offscreen screenshots inspected for media/game/idle, large/mini wheel, battery settings, controller icon, clock/date, About, and minimum-window size.
- Third-party GPL project BQC was **not** installed, embedded, or used as a dependency.

## Not certified by these tests

- Initial Bluetooth pairing of a real DualSense and its USB/HID battery values; some reports are only approximate.
- A phone Bluetooth GATT 180F service, if any, from Galaxy S24+; Windows cached SetupAPI values are intentionally not shown as live percentages.
- Every physical rotation/drag placement and every user-created icon at every DPI scale.
- The approximately 5-second native dock overlay after changing Volume/ChatMix modes (unchanged; requires firmware-event research).
- Immediate refresh of the firmware-owned top 10 pixels after every battery swap (remains under investigation).
- All past GitHub releases' legal rights and whether the reference photo may be redistributed. **Do not create a public GitHub release until cleared.**

## Private test procedure

1. Leave the stable Beta 4 installation untouched.
2. Open the private Beta 5 publish folder and launch `SteelseriesDockCustomiser.exe`.
3. Confirm the original dock photo appears and your copied personal default layout is intact.
4. Click **Özel tasarım > Canlı** and explicitly approve the hardware prompt. Verify a short wheel turn and return to the design.
5. Select Mini dairesel tekerlek, open its pixel workshop, move Game and Chat icons in Mix mode, save and reopen.
6. Open **Ayarlar > Pil ve öncelik**: ensure six battery slots and any verified BLE devices appear in correct preference order. Check the independent spare battery.
7. Exercise warnings only with an actual verified low battery or the explicit safe test command; don't falsify a battery percentage.
8. Stop Live / Dock Reset and verify the native OLED is back.

**Copyright (c) 2026 Yakup Can Palaz.** Third-party components retain their own licenses.

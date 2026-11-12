# Beta 5 acceptance checklist — safe engineering build

Status: **LOCAL DEVELOPMENT / NOT RELEASED** — 12 November 2026.
Release is blocked until real hardware acceptance, ownership audit and
explicit review of the isolated branch. Never replace a working Beta 4
installation during acceptance.

## 0. Preserve Beta 4 (COMPLETED)

- [x] Save independent copy of user settings, icons, layout and personal
  reset baseline to `_backups/SteelseriesDockCustomiser-Beta4-protected-20261112`.
- [x] Beta 5 reads and writes only to
  `%LOCALAPPDATA%\SteelseriesDockCustomiser-Beta5-Development`.
- [x] The separate directory initially contains a COPY of the protected
  personal layout; no Beta 4 application settings were modified.
- [x] UI requires explicit user confirmation before physical Apply/Live.
  Read-only and visual tests do not write to the hardware. Test-only CLI
  smoke commands send transient frames and restore OEM ownership.
- [x] Do not merge into the Beta 4 branch, alter current EXE or publish.

## 1. ChatMix appearance / wheel (CODE + SELF-TEST COMPLETE)

- [x] Game left, Chat right in split bar; neutral G=100/C=100 leaves both
  fills empty with always-present central divider.
- [x] Game and Chat icon sprites are user-authored 1-bit assets, not G/C text.
- [x] Large and mini wheel icon offsets adjustable by X/Y and by dragging
  selected Game or Chat icon in an OLED-scale preview.
- [x] Valid placements restricted to respective rotating arc sectors; reject
  overlap with ring or the other icon. If no valid position exists, omit.
- [x] Independent compact dial at 70–100% size and 1–5 ring thickness,
  V/X centre glyphs, independent saved styling; retain original full-wheel
  45×42 user sprite unchanged.
- [x] Regression tests for 0/±90/±180 rotations and maximum offsets.
- [x] Flat bar border/fill thickness adjustable.
- [x] Bold date/time three-level stroke retains readable digit counters.
- [x] Off-screen dialog screenshots reviewed and footer overlap corrected.
- [ ] Confirm user-customized Game/Chat icon placement visually on
  the real Nova Pro OLED after multiple changes and physical dial turns.
- [x] Confirm protected live OLED framebuffer and factory handback on device.
- [x] Confirm app's Live-mode WinForms activation and clean OEM restoration.

## 2. Battery discovery / notification (CODE + SELF-TEST COMPLETE)

- [x] 10-second slower phone/HID/BLE polling; per-second light GG/laptop
  polling; event-driven battery refresh requests.
- [x] Read-only paired BLE GATT 0x180F/0x2A19 *uncached* if supported.
- [x] SetupAPI cached battery percent remains diagnostic-only.
- [x] Correctly recognized known GG/Phone/GATT readings deduplicate instead
  of duplicating an OLED battery widget; other BLE devices retain unique IDs.
- [x] Automatic 1-bit icons for earbuds, headset, mouse, keyboard, speaker,
  phone, controller, USB, generic wireless/Bluetooth are present.
- [x] Missing/stale/nonverified battery percent never becomes a fake OLED %.
- [x] Default low/critical warnings 20%/10%; per-device thresholds and
  adjustable 5–240-minute repeat interval.
- [x] Labels use Kontrolcü (TR) / Controller (EN).
- [ ] Pair and test first-time DualSense via both Bluetooth and USB; confirm
  real battery level, disconnect, reconnect, icon and priority. HID may only
  provide coarse approximate bands.
- [ ] Test Galaxy S24+ GATT availability and verify Phone Link reads without
  stale SetupAPI percentage overwriting. Some phones do not expose GATT 180F.
- [x] Read the dock's spare battery even while the headset is disconnected:
  live GG check returned 100% spare while the headset slot was unavailable.
- [ ] Confirm new physical battery swaps, GG Aerox and priority/slot 3
  in all OLED modes.
- [ ] Verify reconnect, duplicate-model, unsupported wireless/USB handling.

## 3. Native header and dial handoff (RESEARCH ONLY)

- [x] Leave successful Beta 4 one-shot ~1-second recovery after dial movement
  **unchanged**. No rapid write/release loops or flicker attempts.
- [x] Document mode-only button ~5-second OEM overlay without decoded event.
- [x] Investigate independent source documentation for vendor battery status
  and passive reports, with native rows 0–9 firmware-managed by default.
- [ ] Test firmware header change after a verified physical battery pack
  replacement under user-supervised acceptance.
- [ ] Only consider header redraw/query with direct device and protocol
  compatibility proof, no unrequested firmware commands. Otherwise evaluate
  optional custom drawn header with verified readings.
- [ ] Do not promise mode-only 5-second hold fixed or OEM battery header live.

## 4. Copyright and packaging (DRAFT ONLY)

- [x] Draft proprietary-freeware `LICENSE`, Turkish `TERMS.md`, notices,
  and owner name in About / .NET package metadata.
- [x] Inspect locally available historical beta.1/.2 Git tags and HidSharp
  Apache 2.0 license/NOTICE files.
- [x] Include user license and third-party notices in *local staging publish*.
- [ ] Complete full contributor and legacy release/license review, including
  all remote release assets and historical rights.
- [ ] Confirm ownership or redistribution license for any photo/font/binary
  included in the actual downloadable package.
- [ ] Decide public-source/private-binary distribution; no retroactive
  revocation of already valid permissions.
- [ ] Manually approve final launch/public GitHub release; none created.

## Tests and reproducibility

Run from `SteelseriesDockCustomiser-beta5-continuation`:

```powershell
dotnet build -c Release
dotnet run -c Release --no-build -- --self-test
```

Test-only visual capture uses `--ui-screenshot <file> --capture-mini-wheel`
or `--ui-screenshot <file> --capture-wheel --capture-wheel-mix`.
No HID writes, firmware changes or remote release occur in these tests.

Beta 4 personal backups are never overwritten by the Beta 5 smoke suite.

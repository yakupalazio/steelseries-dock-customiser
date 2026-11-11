# Beta 5 continuation checkpoint — 12 November 2026

This branch is a **private/local engineering checkpoint** after the previous
ChatGPT conversation reached its context limit. It is **not** a public
release, does not update the user's installed app, and does not change the
SteelSeries dock firmware.

## Recovery and isolation

- Recovered branch: `feat/beta5-local-validation` in the original worktree.
- Preserved its interrupted, compiled changes at local commit `58496dc`.
- Continued in a separate Git worktree: `feat/beta5-resume-safe`.
- No original layouts, icons, ADB authorizations or GitHub releases were replaced.
- The cancelled external BQC bridge is **not** merged, installed or required.

## Already implemented in the recovered checkpoint

- Split flat ChatMix: Game icon and fill on the left, Chat icon and fill on
  the right; neutral center leaves both bars empty but preserves divider.
- Radial ChatMix endpoint labels corrected to follow native G/C dominance;
  optional compact MiniWheel.
- Real Windows Bluetooth LE GATT Battery Service (0x180F), Battery Level
  characteristic (0x2A19) **uncached** read for paired, compatible devices.
- DeviceWatcher and Windows arrival/removal notifications request refresh.
- Ten-second slow-source polling; per-second GG and laptop monitoring remain.
- New Bluetooth device icons, low/critical battery warning preferences,
  per-device thresholds and stale source suppression.
- Bold date/time glyph readability improvements.
- Existing 450 ms one-shot OLED reassert remains unchanged and firmware-safe.

## Completed during this continuation

- Verified clean Release build and all existing headless OLED Studio self-tests.
- Added deterministic tests for raw G=100/C=0 (Game-left),
  G=0/C=100 (Chat-right), neutral center, icon suppression, and bold glyph
  counters.
- Added tests for warning thresholds, critical transitions, duplicate
  suppression, charging and stale readings.
- Added dynamic BLE device/automatic OLED battery slot tests and phone/earbuds
  icon classification.
- Integrated a **self-contained, read-only SetupAPI fallback diagnostic**
  with a 60-second throttle and re-scan on device arrival/removal.
  Windows Bluetooth cached percentage is never falsely presented as live.
- Added regression fixtures for Galaxy S24+: cached Windows 40% vs verified
  Phone Link 84% must preserve the verified phone reading.

## Important remaining work

1. **Hardware acceptance**: test a newly paired BLE accessory, a Galaxy S24+,
   and DualSense on the actual Windows machine. Many smartphones do not expose
   GATT 0x180F to Windows; do not promise live phone percentage.
2. **SetupAPI limitation**: it exposes cached battery metadata, sometimes stale
   even while paired. It is diagnostics-only until freshness is independently
   corroborated; there is no fabricated OLED percentage.
3. **OLED mode-only button**: the OEM overlay may still persist ~5 s without
   a decoded dial event. The proven low-risk one-shot recovery after a rotation
   must not be replaced with flickery forced write loops.
4. **Mini wheel pixel editor**: compact preset works, but the current large
   wheel pixel editor can show 45×42 artwork not used by the separate 31×29
   mini preset. Dedicated compact editor / clearer UX remains to do.
5. Confirm small clock/date stroke on the physical 128×64 OLED and inspect
   Game/Chat icon placement at extreme rotations.
6. Licensing PRs for SteelSeries Dock Customiser, FOX, StudentNo_Calculator
   are still **draft** and require ownership/third-party reviews. Do not
   publicly release until licensing is resolved.
7. Do not push/merge/tag a public Beta 5 build before the hardware acceptance
   list and review of this isolated branch.

## Validation

Run in this worktree:

```powershell
dotnet build -c Release
dotnet run -c Release --no-build -- --self-test
```

Only headless tests were performed here; no HID writes, OLED takeovers,
firmware updates, pairing actions, or changes to SteelSeries GG were made.

## Continuation after receiving the complete Beta 5 plan (02:00 local)

- Retrieved the separate unfinished original-tree changes, created safety
  checkpoint `4bb00bf` in `feat/beta5-local-validation`, then cherry-picked
  them into `feat/beta5-resume-safe` as `661dd4b`.
- Resolved the sole conflict in `Program.cs`, preserving the broader dynamic
  BLE discovery report. The original recovery code and the old two test
  patches were not deleted.
- Recovered the compact MiniWheel settings dialog, ring/bar thickness controls,
  preview output and accumulated font/ChatMix tests.
- Further tightened the compact wheel's Game/Chat icon positions so each
  icon remains strictly inside its own *rotating arc sector*, with no ring
  bitmap or other-icon overlap. Added rotation fixtures at −90/0/+90.
- Deduplicated GATT readings for **exactly identified** Galaxy S24+, Aerox 3,
  Nova Pro Wireless and DualSense models with existing primary-source slots;
  this deliberately does not merge unrelated generic phones or mice based
  on device type alone. Verified Phone Link 84% is not overridden by a 40%
  GATT fixture and that a missing Aerox source may use verified GATT.
- Latest isolated build: 0 warnings, 0 errors. Headless self-tests: PASS.
- Event-triggered BLE refresh, ten-second slow polling and fast GG monitoring
  remain in place. Beta 4's proven dial-triggered recovery was not changed.
- **Do not publish** until real dock/dual-sense/phone acceptance and third-party
  distribution review.

### Licensing research checkpoint

The Git-tracked beta.1 and beta.2 tags do not include a top-level LICENSE,
COPYING or TERMS file. This is **not** by itself proof that no other prior
release terms, contributor grants or distribution rights exist. The current
repo includes HidSharp 2.6.4 (Apache-2.0) and documents an externally
downloaded ggoled utility; packaging/licensing terms need separate verification.
Source files and user-provided photos should not be included in a claimed
all-rights-reserved redistributed binary without a rights audit. No automatic
relicensing, GitHub privacy changes, tags, pushes or public releases performed.

### Remaining acceptance checks

1. Verify OLED output and the original 1s post-volume-wheel handback on the
   physical dock; observe without changing the firmware or HID flow.
2. Confirm Game/Chat icon placements, mini wheel and split bar at extreme
   ChatMix values and all configured rotations.
3. Test pairing, disconnect/reconnect and GATT battery availability with
   Galaxy S24+, Aerox, and a newly paired DualSense; document when Windows
   does not expose a live BLE battery service.
4. Check small bold date/time visibility on the real 128×64 panel.
5. Treat OEM top ten rows and mode-only button delay as isolated research.
6. Perform licensing audit and decide whether future releases publish source.

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

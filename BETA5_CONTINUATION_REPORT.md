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

## Implementation against the final user-approved Beta 5 requirements

Date: 12 November 2026, local Windows engineering branch.
This checkpoint is limited to an isolated development worktree.
See [BETA5_ACCEPTANCE_CHECKLIST.md](BETA5_ACCEPTANCE_CHECKLIST.md)
for complete completed/pending acceptance criteria.

**Stage 1 — Data and hardware safety.**
`Beta5Sandbox` changes all Beta 5 app configuration reads/writes to its
own AppData development folder, cloned from the preserved user default.
A guarded `DockHardwareTest.Validate()` denies real HID/OLED writes unless
an explicit command-line hardware-test flag is passed. All new smoke tests
were run WITHOUT that flag. A SHA-256 comparison of the original Beta 4
studio/profile/icon/battery files with the independent protected snapshot
showed exact matches at the end of the engineering session.

**Stage 2 — ChatMix and compact wheel.**
Independent icon drag handles now move Game/Chat desired positions in the
native 128x64 preview. Nearest-legal placement strictly enforces own
rotating audio sector and zero overlap with ring/other icon. X/Y values
persist in widget layout; miniature wheel at 70–100% size uses original
V/X glyphs and independently adjustable ring thickness. Larger user's
45x42 painted ring art is neither replaced nor auto-rescaled.
Split ChatMix bar and bold date/time regression tests pass.

**Stage 3 — battery infrastructure and alerts.**
Existing Windows GATT read-only polling, SetupAPI diagnostic-only fallback,
GG/Phone Link/HID providers, 10s refresh and event watcher retained.
Low/critical battery warnings provide 20%/10% defaults, device overrides
and 5–240-minute user-configurable repeats. Unknown/old Windows battery
values are never advertised as current; Android S24+ and first-time DualSense
live-GATT support still require hardware acceptance. No BQC helper installed.

**Stage 4 — original OEM top ten rows.**
Research was documented in
[NATIVE_HEADER_RESEARCH_BETA5.md](NATIVE_HEADER_RESEARCH_BETA5.md).
The firmware-managed native battery display was not forcibly redrawn and
its underlying update route is still unverified. The proven Beta 4 450ms
one-shot reclaim and the unresolved approximately five-second mode-button
overlay were left unchanged; no firmware modifications were made.

**Stage 5 — draft owner licensing and distribution rights.**
Created proposed proprietary-freeware [LICENSE](LICENSE), [TERMS.md](TERMS.md),
[COPYRIGHT_AUDIT_BETA5.md](COPYRIGHT_AUDIT_BETA5.md) and expanded
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Hakkında/About and .NET
metadata include owner copyright. Local staging publish copies license,
terms and HidSharp Apache2 NOTICE/LICENSE; no GitHub tags, previous versions,
public licensing, branch privacy or release assets were changed.

**Stage 6 — automated acceptance / local staging.**
Off-screen UI smoke captures of both mini and full wheel editors were
visually reviewed. A footer overlap and narrow-size numeric clipping
were found and fixed; re-capture shows controls readable and inside the
window. Build: 0 errors and 0 warnings. Existing self-tests and new
Beta 5 constraints, size serialization, source deduplication, warning
interval and safety tests: PASS.
A local staging publish was created in `artifacts/beta5-staging`, not
installed or publicly distributed. Further physical device acceptance
is necessary before any release.

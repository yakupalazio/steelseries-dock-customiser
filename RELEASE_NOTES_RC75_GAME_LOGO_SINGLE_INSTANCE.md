# RC7.5 — Game Logo Sources and Single-Instance OLED Ownership

**Private Windows x64 QA build · 1.0.0-beta.2-rc7.5.** Existing RC7.4 archives and public GitHub releases remain untouched.

## Changes

- Normal Studio startup now uses a named per-Windows-session mutex before creating the UI or OLED transport. Starting another copy (including from another release folder) sends a local, same-user named-pipe signal to the existing instance and exits without obtaining OLED ownership.
- An existing Studio window hidden in the system tray is restored on a second launch. Tray **Restart** now closes the old owner and releases its mutex **before** starting the replacement, avoiding an Application.Restart race.
- Game logos now try verified, local MSIX/UWP package artwork (ICO or PNG, including app manifest assets) and fall back to the EXE icon or Windows Shell icon. No internet lookups, game-file modifications, or profile resets are involved.
- Corrected opaque multi-color corner classification, adaptive black/white contrast selection for dark textured icons, and dense-rectangle cropping that previously transformed filled icons into an almost solid block. The public custom-logo import path uses the same 1-bit conversion.
- Verified optional direct process-executable fallback for recognized GG titles installed under WindowsApps. Unrecognized processes are not promoted to games.

## Regression acceptance

- Full built-in self-test suite passed for each of these **installed executable** fixtures:
  - `C:\Games\Sekiro - Shadows Die Twice\sekiro.exe`: 65 lit pixels; EXE icon.
  - Minecraft Bedrock `Minecraft.Windows.exe`: 539 lit pixels; package logo.
  - Minecraft Launcher `Minecraft.exe`: 596 lit pixels; package logo.
- Automated named-mutex duplicate rejection and local pipe-to-form restore signal passed, along with synthetic dark icon and existing RC7.4 inverted/transparent artwork tests.
- No real OLED hardware was driven as part of automated tests. On-device visual quality and live duplicate-launch acceptance remain to be verified by the tester.
- Original source game icons and derived OLED QA previews are never included in the release ZIP.

## Safety

- No firmware write, game modification, account access, driver installation, user layout/profile deletion, or change to the public GitHub release.
- Exit all **old RC7.4 and earlier running copies** from the tray before opening RC7.5 once; old releases cannot retroactively participate in the new singleton mutex.
- This RC is private QA, not public stable release.

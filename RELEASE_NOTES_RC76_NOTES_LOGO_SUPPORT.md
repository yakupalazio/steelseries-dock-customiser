# RC7.6 — Instrumental Music Note, Saved Logo Recovery & Support Me

**Private Windows x64 QA build · 1.0.0-beta.2-rc7.6.** Builds on the tested RC7.5 single-instance/Windows game logo changes. Previous binaries, ZIPs, Git checkpoints, personal profiles and private logo records remain unchanged.

## 1. Instrumental lyrics indicator
- Replaced the barely readable, left-aligned 7×7 dot-like note with a centered 15×12 double-eighth-note pixel sprite.
- The icon is drawn only during timecoded instrumental (empty lyric) sections; real lyric lines and timings are not modified. It stays inside the lyrics viewport and does not touch the protected firmware header.
- Added a shape/legibility regression and updated the older timecoded-lyrics test to verify the note at its new position.

## 2. Saved white-rectangle logos
- Read-only investigation found an old Sekiro user override with 775 active pixels forming a completely solid 31×25 block. Previous block detection rejected only more than 900 active pixels, so that override incorrectly won over the fixed game EXE icon.
- The validator now rejects large, near-uniform rectangular silhouettes based on bounding-box occupancy, not pixel count alone. This automatically ignores invalid stored override data **without deleting or editing private files**, allowing the RC7.5 EXE/Shell image fallback to run.
- The test suite reproduces the exact 31×25 / 775-pixel malformed pattern synthetically and confirms detailed artwork is not rejected. User-specific JSON and game art are not included.
- SteelSeries GG's Sekiro registered path was found to differ from the actual installed folder; RC7.5's verified live-process executable fallback remains available. Live game/OLED confirmation is still required.

## 3. PNG toolbar
- Confirmed the existing button already calls `ExportPng()` with a SaveFileDialog to save the selected 128×64 OLED mode as a PNG. It was **not removed**.
- Renamed the label to `PNG dışa aktar` / `Export PNG` with an accessible description and preserved the existing action.

## 4. About / Support Me
- Added a visible **Beni Destekle / Support Me** card under the author's signature and copyright text. The GitHub profile button opens the verified project owner's profile: `https://github.com/yakupalazio`.
- No payment, tracking, hidden network call or contact form is involved. Automated tests do not open the browser.
- Updated the window title and About version display to derive the actual application version instead of a stale RC7.4 literal, with localized English About text.
- Both Turkish and English About page layouts and the main Studio toolbar were captured offscreen and visually checked for overflow.

## Acceptance
- `dotnet build -c Release`: 0 errors, 0 warnings.
- Complete no-hardware `--self-test`: PASS, including existing RC7.5 and RC7.4 tests and new RC7.6 coverage.
- UI screenshot smoke: About TR/EN and main Studio toolbar: PASS.
- Real-time Nova Pro OLED note/legacy Sekiro logo validation by the user: **pending**.

## Release safeguards
- Private QA only, not a public GitHub stable release.
- No actual OLED writing, firmware change, automatic game launch, user profile reset, old asset deletion or GitHub publication during this work.
- If an older RC is still running, quit it from the system tray before opening RC7.6; RC7.6 retains RC7.5's one-instance guard.

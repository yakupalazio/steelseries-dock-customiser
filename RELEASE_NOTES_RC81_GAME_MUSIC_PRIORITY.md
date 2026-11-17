# RC8.1 — Simultaneous Game / Music OLED Priority

**Independent private QA build · 1.0.0-beta.2-rc8.1 · Windows x64.**

## User request
When a game and music are both active, previous versions always prioritize the game OLED layout. Users should be able to choose whether the game screen or music screen takes precedence without interfering with the other two modes.

## Implementation
- New setting: **Ayarlar → OLED ekran → Oyun ve müzik aynı anda açıkken** (English: **Settings → OLED display → When music and a game run together**).
- Two mutually exclusive choices:
  - **Oyun ekranı öncelikli / Prioritize game screen** (default, preserves prior releases)
  - **Müzik ekranı öncelikli / Prioritize music screen**
- Persisted in the **existing** per-user `display-v1.json` through `DisplayPreferences.ConcurrentModePriority`. No migration overwrites the user's existing file, layout, custom logo or profile.
- A saved change immediately invalidates the photographic preview and requests a live OLED refresh **through the existing serialized hardware writer**. On-screen mode transitions remain enabled.
- Mode chooser applies the preference only if **both** a real game and active media are detected. When only a game is detected, show Game. When only music is detected, show Media. When neither is detected, show Idle.
- Language selection updates the new control in both Turkish and English without changing the selected value.
- Existing visual widget designs remain independent; only the automatic mode-selection decision changes.

## Acceptance
- Compile: 0 warnings, 0 errors.
- Full legacy and new self-tests pass. RC8.1 tests cover all 4 activity combinations under Game, Media, lowercase Media and invalid preference; JSON round-tripping, legacy default behavior, no profile mutation; and both localizations plus UI positioning.
- Off-screen screenshots reviewed: `artifacts/_QA_RC81_settings-tr.png`, `artifacts/_QA_RC81_settings-en.png`. No overlaps, photo preserved.
- Published EXE `--self-test` and ZIP validation should run before delivery.
- **No physical simultaneous gaming+music test performed** and **no public GitHub release requested**. The code is committed locally; previous RC8 downloads remain unchanged.

## Tester instructions
1. Fully exit the previous version from system tray before opening RC8.1.
2. Open Settings → OLED display. Under the simultaneous game+music section, choose Music priority.
3. Play an actual track and open a recognized game. Verify Media OLED appears while both are active.
4. Pause or stop music: Game OLED should return. Close game while music plays: Media OLED should appear. Stop both: Idle OLED should appear.
5. Set Game priority; with both active, verify Game OLED appears.
6. Restart the program and confirm the previous selection persists. Use an official previous RC8 ZIP if you need to roll back.

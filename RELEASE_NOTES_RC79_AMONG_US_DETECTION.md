# RC7.9 — Among Us game detection with stale GG records

**Private Windows x64 QA release · 1.0.0-beta.2-rc7.9.**

## User-verified field results
- RC7.8 fixes were tested with VALORANT: user reports successful game icon rendering.
- Among Us did not activate Game mode.

## Verified root cause
- The real installed game is at `C:\Games\AmongUs\Among Us.exe` (Epic installation).
- SteelSeries GG's local read-only `library_scan` entry is **`uninstalled=1`** despite the game still existing.
- GG's recorded executable `c:/games/amongus/amongus.exe` omits spaces from the real `Among Us.exe` basename.
- RC7.8 ignored GG rows marked uninstalled and its fallback accepted only a few common library-directory patterns, excluding this directly installed Epic game.

## RC7.9 fix
- Read GG's `uninstalled` column without modifying its SQLite data.
- Keep active GG registrations and the existing safe Steam/Riot/EA/Epic/GOG paths as before.
- Put stale GG entries in a **separate** read-only cache. Consider them only when an actual currently running Windows process has the corresponding normalized basename AND its accessible true executable path exists inside the exact same full directory registered by GG. Only executable whitespace/casing may differ. No name-only fallback, no user-custom folder scanning, and no cached or launcher-only detection.
- Index stale entries by normalized basename to avoid expensive nested scanning across all Windows processes every poll.
- When a live process is confirmed, preserve Game screen selection and the normal game-icon provider, including GG's local logo fallback.
- Do not change GG's install/uninstall state, launch games, touch profiles, change OLED firmware, request permissions or perform network calls.

## Tests
- Full self-test suite, including RC7.4–RC7.8 and new stale-registration tests.
- Positive: a real existing executable and same-directory recorded basename with omitted spaces.
- Negative: unrelated executable, different directory, nonexistent executable, a DLL and an absent path.
- Optional real fixture `DOCK_AMONG_US_EXE=C:\Games\AmongUs\Among Us.exe` confirms the live GG record is recovered as **Among Us** and its image returns 725 lit 1-bit pixels.
- Actual Among Us icon is dense and resembles a mostly filled figure; display recognizability is dependent on original artwork.
- Optional 32×32 OLED preview generated only in ignored local `artifacts/_QA_RC79`.
- **Physical OLED Game-mode switching while Among Us is actually running remains unverified.**

## Release safeguards
- Previous RC7.8 and older ZIPs/EXEs preserved. RC7.8 may still be running while QA package is built.
- Independent private release, not a public GitHub publication.

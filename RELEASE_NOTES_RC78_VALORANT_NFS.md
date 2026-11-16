# RC7.8 — Valorant logo and EA App/NFS Unbound recognition

**Private Windows x64 QA version `1.0.0-beta.2-rc7.8`.**

## Field test evidence
The user confirmed correctly rendered logos in RC7.7 for League of Legends, Riot Client, Half-Life, Buckshot Roulette, R.E.P.O. and Crysis 3 (though Crysis' intrinsic art is difficult to read at 32px). Valorant appeared blank and Need for Speed Unbound never activated Game mode.

## Diagnosed causes
- VALORANT's installed Win64 shipping EXE extracts an extremely sparse dark/white icon. SteelSeries GG already keeps a locally registered high-contrast official-looking VALORANT image inside GG's own `externalAppIcons` folder.
- GG has an installed EA App **Need for Speed Unbound** library entry with a populated installation directory but an **empty `exe_path`**. The previous detector skipped entries without a nonempty executable path.
- The user already has a verified on-disk `NeedForSpeedUnbound.exe`, plus a separate `NeedForSpeedUnboundTrial.exe`, under the registered EA Games directory.

## Fixes
- Extend read-only GG SQLite metadata to consume `name`, `exe_path`, `directory`, `image_path`, without changing the GG database.
- Retain normal executable matching; for missing-EXE registrations, recognize only a running Windows process with (a) matching normalized game title/executable basename (optionally suffix `Trial`), (b) actual verified executable path under a recognized installed-game location, and (c) parent directory matching the registered GG installation folder. Normalize folder whitespace for GG metadata that strips it. Unrelated launchers/background processes are rejected.
- Windows' read-only `QueryFullProcessImageName` via `PROCESS_QUERY_LIMITED_INFORMATION` can fetch a process path when `.NET Process.MainModule` is denied. No process memory reads/injection. Before querying any missing-registration process path, filter by normalized registered game name; this avoids heavy process scans.
- For low-detail/blank icons, use the game's **GG-registered local image** when it exists under the strictly allowlisted `C:\ProgramData\SteelSeries\GG\shared\externalAppIcons` directory; no web requests. Preserve existing high-detail EXE art and Store icons for games already rendering correctly.
- Ignore legacy user overrides that contain fewer than 12 lit pixels instead of treating an all-black image as a valid override. **Never delete, rewrite, or upload saved override art.**
- NFS Unbound also resolves its locally registered GG logo even when the GG executable path is empty.

## Verification
- Full .NET Release build: 0 warnings and 0 errors.
- All application self-tests pass, including previous RC7.4–RC7.7 checks.
- Against installed real files and GG's real local database:
  - VALORANT resolves its GG local artwork (89 white OLED pixels);
  - Need for Speed Unbound resolves its GG local artwork (391 white OLED pixels);
  - Verified full and trial NFS executable naming and folder matching;
  - Rejects unrelated executables outside the recognized game directory;
  - Read-only process path API works for current process.
- 32×32 OLED preview QA PNGs kept only under ignored local `artifacts/_QA_RC78`, not packaged.
- **Real running Valorant/NFS game + physical OLED acceptance is not yet verified.** The implementation uses no live game launches, process injection, firmware changes, or automatic display updates during QA.

## Release safety
- Private QA only. Previous RC7.7 and older ZIPs and original personal profile/game icon files remain intact.
- To test RC7.8, quit the prior Studio completely via its system tray, then launch RC7.8. The cross-release single-instance mutex is retained.

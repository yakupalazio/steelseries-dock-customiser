# Steelseries Dock Customiser — RC2 Text & Motion Preview

Private test build, 13 November 2026. Base: RC1 Hotfix 1 (commit `02ecb45`). No public release, settings migration, user layout overwrite or physical OLED test is performed during packaging.

## Text widget changes
- Artist, track, game name, game label, custom text, dates/clocks, system CPU/RAM/ping and compact game mini-player expose width and thickness in the same inspector; Game Mini Player uses its existing saved mini width.
- Text width defaults to legacy automatic behavior if the property was never changed. Setting a width clips overflowing text, keeping neighbouring OLED widgets intact.
- Long text marquees use millisecond time and 28 pixels per second, refreshed at the established 60 ms visual timer when visible text is too long. Former rate: 3 px/s. Text fitting the area stays stationary.
- Existing size, coordinates, font thickness and per-mode personalization are retained.

## Synchronized lyrics
- Lyrics can wrap onto 1, 2, or 3 8-pixel rows at the user's choice. Higher row counts need enough lower OLED space; the top 10 pixels are protected as before.
- Lyrics widget width and font thickness can be adjusted; the media glyph remains at the left, with no track-duration prefix.
- Instrumental breaks show a tiny music note, rather than repeating the preceding text. Explicit empty LRC timestamps and common instrumental/music markers are preserved as non-spoken sections. Long unmarked gaps/outros are inferred conservatively; timestamps for later lyrics are never shifted.
- The per-widget offset is applied to actual SMTC-interpolated position before line selection and transition: negative values advance words, positive values delay words. Tests verify ±400 ms changes result in different rendered words. 10 ms settings precision does NOT imply 10 ms physical OLED timing.

## UI
- Round-corner DarkButton surfaces with ~16 ms hover/press easing and subtle borders, preserving ordinary click/keyboard semantics.
- Main Media/Game/Idle page changes and Settings navigation use a ~145 ms crossfade/vertical movement. Changes are instantaneous underneath; transition is cosmetic. Capture/smoke modes skip transitions.

## Validation
- .NET 10 Release build, full automated self-test suite, new deterministic tests for text clipping/weight persistence, millisecond marquee movement, 2/3 lyric rows, timecode offset and instrumental notes.
- Offscreen UI captures for Media, compact Idle and About are generated for review without real HID or Sonar capture.
- Physical Nova Pro OLED performance and actual third-party LRCLIB transcription quality still need user acceptance. No public release, code signing or installation was attempted.

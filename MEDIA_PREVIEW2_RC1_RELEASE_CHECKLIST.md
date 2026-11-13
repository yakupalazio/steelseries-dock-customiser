# Media Preview 2 — RC1 release checklist
**Build:** 1.1.1-rc.1 (private Windows x64, 13 November 2026)
**Code branch:** feature/release-candidate-ui-lyrics-motion

## Reference animation (user-provided 19-second 1920x1080 video)

The upper Dynamic Island has three stable / animated regions:
1. Total track duration stays pinned at left.
2. A small square album-art region remains pinned immediately to its right.
3. The lyric text occupies a clipped single-line strip on the right.
   Long LRC lines glide smoothly **right-to-left** as the current line
   plays. When the timestamped line advances, the OLD line slides UP
   and dims, and the NEXT line rises from BELOW into the same viewport,
   briefly creating a two-tier vertical crossfade.

Our 128x64 one-bit OLED implementation has a 16-pixel height lyric
region with a fixed left-side duration plus 7x7 original media glyph
instead of a full-color actual album image; the lyric strip uses a
340ms vertical transition with ordered monochrome pixel dimming, 1-pixel
horizontal movement interpolated from LRC line timestamps, 60ms
preview timer and the existing hardware's independent OLED limit.
Video screenshots/lyrics/album artwork are **not bundled or copied**.

## Widget-library decisions

- Categorized selection: Music & lyrics, Battery & devices, Clock & text,
  Visuals, System & network, Gaming, Sound controls.
- Choice list ONLY contains widgets allowed in the active display mode
  and not already present in the current layout if they are singletons.
- EXCEPTIONS that may repeat: Animated GIF, Custom text,
  Audio visualizer, Clock. Every other widget is limited to one
  per display mode, including Lyrics and individual battery slots.
- Music-specific Source / Track / Artist / Lyrics / Progress /
  MediaClock / AudioVisualizer are **only addable in the Media mode**.
  The legacy **GameMiniPlayer** remains explicitly game-only because
  it is a dedicated, previously approved gameplay widget.
- Loading older profiles retains the first VISIBLE copy of each
  singleton, falling back to the first copy if all are hidden.
  Original on-disk files are never overwritten merely by loading;
  the user can save the migrated layout when satisfied.
- Categories can disappear automatically when their final singleton
  widget is added. The four repeatable kinds always stay available.
- Category names, add/remove labels, help and advanced OLED settings
  are bilingual.
- OLED hardware controls are grouped in a collapsed Advanced section.
- The reset button requires explicit confirmation and remains undoable.
- Widget names in list expose full text on hover.

## Safety, compatibility and previous features

- Working GIF decoder/importer/dithering remains unchanged.
- Verified Windows SMTC timebase fix and SteelSeries Sonar-aware audio
  capture from prior Media Preview 2 remain unchanged.
- OLED factory-protected top 10 rows, original GameDAC artwork and
  safe restore flow remain unchanged.
- Battery priorities / per-computer device-name privacy unchanged.
- User's %LOCALAPPDATA% development studio/icon/GIF settings preserved.
- Beta 4 and other project directories remain untouched.
- Do not run two live OLED writers simultaneously.
- No downloaded lyric text, uploaded video, source GIF assets,
  Bluetooth IDs, local studio JSON or personal device information
  are included in distributed ZIP.

## Validation run

- Release build (Release, win-x64, self-contained, single-file):
  zero compiler errors and zero warnings.
- `--self-test`: checks older battery/GG/ChatMix/DualSense,
  private installation paths, GIF LRC parser, media time
  interpolation, all audio visualizers and original OEM top-row
  behavior; adds RC1-specific tests of all 27 widget types,
  uniqueness, mode filters, legacy visible-duplicate migration,
  multi-widget exceptions, lyric leftward scroll and 340ms
  bottom-to-top transitions.
- Offscreen UI captures reviewed in Turkish and English,
  full-size and minimum 1120x730 size, including Media/Game/Idle,
  settings and contextual media dialogs.
- Eight synthetic lyric frame captures generated from a locally
  authored in-memory test text. No copyrighted song lines shipped.
- No hardware writes or Bluetooth pairing changes performed by QA.

## What still needs physical acceptance before public release

1. With Spotify playing and the Dynamic Island reference enabled,
   verify the RC1 lyrics both auto-advance and move left/up naturally;
   adjust +/-10ms offset if necessary. This is timecode-driven; actual
   OLED hardware cannot guarantee a 10ms physical refresh.
2. Verify Bar/Stereo/Circular visually respond to music routed to
   Sonar Media; manually select the Spotify output if automatic
   endpoint selection misses it.
3. Leave the custom OLED live over an extended session to inspect
   thermal/load and SteelSeries firmware priority takeovers. Test
   restore OEM and 10px protection after several physical ChatMix
   wheel turns.
4. Test imported animated GIF playback on real GameDAC, and its
   expected unchanged behavior after GIF settings edits.
5. Before general release, perform code signing / installer upgrades,
   clean-Windows PC smoke tests, and review third-party lyric/API
   rights and distribution license terms.

**Status:** Private release candidate suitable for user acceptance,
not a claim of a fully certified public final release.

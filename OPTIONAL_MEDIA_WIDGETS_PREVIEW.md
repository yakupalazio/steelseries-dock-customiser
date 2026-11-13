# Optional Lyrics, Audio Visualizer and GIF Widgets
Private Windows 11 / .NET 10 OLED Studio preview — 13 November 2026

## Enable/disable

None of the new widgets is included in existing or shipped default
layouts. From the editor select the appropriate widget and click Add.
Each widget has a context-sensitive **Widget ayarları...** button, and
each can be moved, hidden, duplicated, removed, undone and saved in
the relevant Media/Game/Idle layout.

### 1. Lyrics (any screen, visible only when music is playing)

- Metadata and precise position come from Windows SMTC: works with Spotify
  **and other media players** exposing a playing media session.
- The public LRCLIB API is asked once for each new title / artist /
  duration, returning externally provided timecoded LRC lines. This is not
  Spotify's in-app lyrics API, which is not officially available through
  Spotify Web API.
- Display-only in the optional Lyrics widget, which users may add to
  Media, Game or Idle layouts. Never include lyrics in
  release assets, logs or personal layout JSON. RAM cache is bounded.
- Synchronization offset -10000..+10000 milliseconds, configurable in
  **10 ms steps**; negative means earlier. Playback seek / skip is
  calculated from actual current SMTC position, not count of timer ticks.
- This 10 ms **control precision does not guarantee actual physical
  10 ms response**: Windows session timestamps, source latency and OLED
  hardware refresh determine observed timing.
- If a track does not have synchronized lyrics, the area stays blank,
  rather than showing incorrect lyrics or pretending plain text is timed.
- LRCLIB: https://lrclib.net/docs ; identify HTTP User-Agent; respect
  Retry-After on 429, exact track metadata checks, network timeout.
- Lyrics service sends a title, artist and approximate duration to
  LRCLIB **only after the user adds the widget**.

### 2. Audio visualizer (any screen)

- Opt-in WASAPI loopback capture from the **default Windows output**
  device, via NAudio 3.3 MIT. No microphone capture, raw audio storage,
  processing of unrelated apps' private content or audio transfer.
- Style: Bars / Stereo / Circular. Stereo has actual left-channel bars
  on the left and right-channel bars on the right; circular uses two
  channel groups as radial segments.
- Width, height and gain configurable in the widget settings.
- An automatically started shared-mode device loopback is active
  **only while at least one visualizer is visible in the live or currently
  edited screen**.
  Removing/hiding the last visualizer stops the capture.
- Audio routing via SteelSeries Sonar or per-app output devices can mean
  the default Windows output is not the desired stream. Planned later:
  per-output endpoint selector and device-change listener.
- Visuals are calculated from energy in 12 logarithmic bands/channel;
  they are a compact frequency-energy visualization, not a studio-grade
  calibrated FFT. No fake activity when muted or capture fails.

### 3. Animated GIF (any screen)

- Choose a local .gif, safely copy it into per-user
  %LOCALAPPDATA%/SteelseriesDockCustomiser-Beta5-Development/AnimatedGifs.
  Widgets save only randomized relative GIF IDs. No personal absolute
  paths and no private GIFs are ever bundled into distributed EXEs.
- Limit: 12 MB source; up to 160 frames and 70 million source decoded
  pixels per import pass. Conversion runs asynchronously; bounded cache.
- Resize Contain, Cover, Stretch. 1-bit monochrome conversion via
  grayscale and Threshold, Bayer ordered or Floyd-Steinberg dithering.
- Playback honors GIF frame durations within safe limits (60–1000 ms);
  playback speed 25–400%, width 16–128px, height 8–54px.
- Actual full OLED rows are 128x64 but the top 10 rows remain protected
  by factory firmware when this protection is enabled.
- The preview uses existing live OLED transport; physical frame rates
  depend on USB/HID and OEM wheel-screen takeovers.
- MIT reference for principles only (no source code copied):
  https://github.com/D4rkTT/GGIF-Studio

## QA / privacy / limitations

- Synthetic tests check LRC seek/offset, audio PCM spectrum and three
  waveform styles, GIF dithering, real two-frame GIF animation decoding,
  GIF frame time selection and asset path safety.
- Offscreen QA captures are generated without audio recording, network
  access, Bluetooth pairing or any HID frame sending.
- Tests for private settings, personal device labels, Battery/ChatMix
  and earlier regressions all run as part of --self-test.
- A real synchronized song, real multi-frame GIF and actual physical
  OLED refresh still need on-device acceptance before public release.
- The user retains sole responsibility for GIF rights and lyric display
  rights; the developer ships no third-party lyric text or user GIFs.

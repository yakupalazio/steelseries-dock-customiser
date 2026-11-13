# Media Preview 2 — Live lyrics / Audio Visualizer hotfix
Private test release: 1.1.1-beta.5-media.2 — 13 November 2026

## User acceptance issues addressed

1. GIF playback was reported to work. Its decoder, converted frames,
   imported assets and previous settings remain unchanged.
2. Lyrics were fetched correctly but sometimes stayed on the same line.
   Windows SMTC (including Spotify) can provide a sampled timeline position
   every few seconds rather than an updated position on every app poll.
   Previously the program replaced the timeline timestamp with the app's
   current polling time every second, effectively restarting the progress
   estimate. The program now uses timeline.LastUpdatedTime to interpolate
   elapsed playing time between genuine SMTC position updates, respecting
   track end and the per-widget -/+ 10ms lyric offset.
3. All three audio visualizer styles were invisible when no fresh PCM
   arrived at the original Windows default output. On Sonar setups Spotify
   may use the Sonar Media endpoint while Windows defaults to Sonar Gaming.
   Auto now samples up to six read-only WASAPI render endpoints with active
   meter priority, including Sonar Media/Gaming/Chat/Aux and other outputs.
   The strongest fresh source is used; the Audio Visualizer settings panel
   also offers a specific output device selector. This avoids requiring
   Windows users to change their default audio routing.
4. A silent/disconnected output no longer makes the widget disappear:
   Bars and Stereo show narrow baseline bars, Circular shows a baseline
   circle without fake animated levels.
5. Optional preview refresh is now 60ms, matching the pre-existing
   live OLED 60ms timer. USB/HID/OEM update latency is independent.
6. In Auto mode, only up to six endpoints are captured, even if many
   virtual audio devices exist. All raw PCM stays in memory for
   approximately 2K frame frequency envelopes, never recorded to disk.

## Test steps

- Close the old Media Preview via tray to avoid two dock controllers.
- Run the new private Media Preview 2 EXE.
- Play the same Spotify track in Dynamic Island for Windows, add the
  **Canlı şarkı sözleri** widget in the Media layout. Lyrics should now
  advance with the song using the most recent real timeline timestamp.
- Add **Ses görselleştirici**, choose Bars/Stereo/Circular.
- Leave **Ses çıkışı** = **Otomatik (Sonar / Windows / Voicemeeter)**.
- If bars stay at baseline while music plays, set **Ses çıkışı** to
  **SteelSeries Sonar - Media** or the actual output selected for Spotify
  in Windows / SteelSeries Sonar.
- The bars should never vanish completely, even if output is silent.
- GIF widget should behave exactly as in the previous working preview.
- The renderer only displays audio energy, not a simulated sound waveform.
  If a selected endpoint has no audio signal, baseline-only is expected.

## Diagnostics and safety

- A diagnostic command enumerates active Windows render endpoints,
  reporting connection/PCM callback counts without logging audio samples:
  SteelseriesDockCustomiser.exe --media-pipeline-diagnostics
- A simpler WASAPI probe reports the number of active output captures,
  presence/absence of fresh bands, and levels:
  SteelseriesDockCustomiser.exe --audio-loopback-probe
- On the author's Windows computer, default device was Sonar Gaming while
  Sonar Media (8 channels) and multiple Voicemeeter endpoints were present.
  The new Auto WASAPI probe successfully started six capture endpoints;
  no actual music was playing during the probe, so levels stayed zero.
- Synthetic LRC interpolation tests, all three visualizer rendering
  tests, silent-mode visibility tests, GIF animation tests and the whole
  regression suite pass.
- Real Spotify timed lyrics advancing on the dock, and physical audio
  responsiveness, still require acceptance by the user before a public
  release.
- This private package does not contain any user-specific GIFs, Bluetooth
  identities, device preferences or local profile files. Existing Beta 4
  and current user settings are preserved. No physical dock write is made
  by the tests or diagnostics.

# Dock Customiser — RC4 release preparation (private)

Release candidate based on RC3 commit `0f73205`. This is a developer preview, **not** an official SteelSeries application, a public release, or a statement that a cloud report service is already live.

## Implemented

- **Original visual identity:** New round OLED-inspired D/pixel logo, dedicated multi-size Windows EXE icon, and clean top-left brand in place of the old awkward `DOCK / CUSTOMISER` heading. The splash uses the same vector mark.
- **Startup:** Bounded startup overlay with a lightweight animated progress ring. Existing auto-live logic and initial GG, media and battery polls start concurrently rather than serially. A timeout of approximately 4.4 seconds prevents an unavailable optional device from blocking the whole UI. Unavailable sources continue refreshing normally after the main window opens. This is not a false guarantee that every Bluetooth/Phone Link source is connected.
- **First-run tutorial:** Seven contextual steps with Next and Skip, overlay-based control highlighting, a one-time local completion preference, and a replay action from Settings. No account/network requirements.
- **Windows animations:** More perceptible ~235ms page transition with fade and movement. Rapid repeated page changes cancel stale overlays. Dark button/selector polish and smooth wheel scrolling retained from RC3.
- **Physical OLED mode transitions:** `Slide` (default), `Fade`, `Wipe`, `Pixel Dissolve`, `Zoom`, `Instant`; user-selectable enabled toggle and 25–200% speed, normal=100%, typical Slide=350ms. Transition preview available in Settings > OLED display. Media, Game, Idle **active live** mode changes use the selected deterministic 1-bit effect. Never animate OEM-owned top 10 rows when protected. A new mode interrupts stale transition frames without stacking timers. Device firmware control overlays and connection recovery keep precedence.
- **Saved profiles:** Overwrite the currently selected profile with the active mode's design; confirmation required, preserve stable profile ID/mode, deep-clone and validate the layout. The previous full profile library gets a best-effort `.bak` copy before atomic file replacement. New/create/import/export unchanged.
- **In-app Bug Report:** Bug category, title, reproduction notes, optional user-approved Windows version and optional image. Local filename and embedded EXIF/GPS metadata are stripped, image re-encoded as clean PNG. Without a real HTTPS endpoint, the form explicitly stores an **unsent local draft**, and does NOT claim it reached GitHub. The app contains no GitHub token. A separate private Cloudflare Worker + D1 rate limit + private R2 screenshot storage + private GitHub Issues template exists under `ServerTemplates/`, but requires actual cloud credentials, account configuration, abuse controls and deployment before public submission works.
- **Workshop architecture only:** Versioned `WorkshopProfileContract` with data-only sanitized layouts and asset placeholders. Saved local profiles, device IDs and GIF paths are not uploaded. No gallery, cloud API, account system or publishing workflow exists yet; those remain the second plan.
- **Performance:** Replaced 8,192 individual GDI+ `Bitmap.SetPixel` calls per OLED render with one memory-copy rasterization path. Animation and frame resources are disposed when done; stale interface overlays are cancelled. No changes to factory battery header ownership or HID command contracts. Previous tests continue passing.

## Validation

- Existing single-page design, battery providers, ChatMix/wheel, GIF, custom icons, lyrics/SMTC synchronization, text marquee, Game logo, per-widget geometry, localization, and all prior release tests.
- Added tests for all six 128x64 OLED transitions, 25/100/200% speed, exact protected 10-row battery header, profile overwrite mode/clone protections, new screenshot privacy handling, opt-in bug metadata, Workshop draft redaction.
- `--perf-smoke` renders 400 OLED frames and 120 transition frames entirely offscreen; on the connected PC's RC4 dev build, the initial measurement was **0.56 ms average per OLED raster frame** and **4.76 ms per effect frame**. These are software-only timings, not measured physical dock write FPS or a promise about other PCs.
- UI offscreen screenshot smoke checks for Main, Splash, seven-step guide's first screen, Settings OLED transition page, Bug Report and Profile manager.
- No automatic hardware write or cloud upload as part of tests. Private user beta settings and old RC2/RC3 ZIPs remain unchanged.

## Items that require user/production acceptance

1. Real Nova Pro Wireless: test all transition effects, speed controls, protected OEM battery area, dial input, USB unplug/reconnect, original screen restoration and startup performance.
2. First-run tutorial: interactively verify each highlighted target on high DPI/small window layouts and finish/skip/replay persistence.
3. Report backend: operator must create private GitHub issue repository, Cloudflare Worker/D1/private R2 with token and WAF; submit and monitor a real report. Until then, the form produces local drafts only.
4. Code signing, clean installation, security review, privacy policy and public download packaging before a public 1.0 launch.
5. Workshop gallery, moderation, user accounts and hosted uploads are intentionally deferred.

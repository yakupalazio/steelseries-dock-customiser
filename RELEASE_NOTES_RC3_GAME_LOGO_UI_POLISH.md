# SteelSeries Dock Customiser — RC3 Game Logo & UI Polish

Private Windows x64 preview. Base commit: `c9205ce` (RC2). This is not a public release.

## 1. Consistent, softer interface
- The common DarkButton retains keyboard, focus, click, and hover behavior while presenting rounded corners and eased mouse interactions.
- Native selector boxes, text inputs, numeric editors, and list controls have matching restrained corner clipping and dark selection colors. Non-custom buttons also receive lightweight hover animation.
- Added round-corner menu highlighting to the tray menu and device-priority context menu.
- All main settings and editor dialogs share this styling, including icon workshop, clock/date, profile manager, optional media widgets, battery dialogs, and wheel configuration.
- Lyrics/visualizer/GIF settings use the dark stepper control instead of the previous hard-edged system NumericUpDown. Existing increments are preserved.
- Long scrollable menus and settings panes now respond to the mouse wheel with small, approximately 16 ms interpolation frames rather than jumping directly to a new scroll position. If a combo dropdown is open, it keeps native wheel input. Existing quick crossfades between Media/Game/Idle and Settings pages are preserved.
- Controls remain WinForms/native; OS popup menu edges and native window title bars cannot be made pixel-perfect replicas of a custom design without a major UI rewrite.

## 2. Adjustable song progress bar
- Media-only `Progress` widget: `ProgressWidth` 12–128 pixels; `ProgressThickness` 3–14 pixels; defaults 98×5 for existing user layouts.
- Both controls are visible only when Progress is selected in the Studio inspector.
- Geometry, click/drag hitbox, progress outline, and actual fill all use the saved dimensions, clipped to the 128×64 OLED.
- Independent settings for each Media layout; no old profile layout rewritten during this update.

## 3. Dynamic Game logo widget (Game-only)
- `GameLogo` is an optional, single-instance widget in the Gaming category. It cannot be added to Media/Idle and will be removed if imported incorrectly into those modes.
- Game detection stays read-only: SteelSeries GG `library_scan` registered executable name is matched with a real Windows process, just like the earlier game-title widget. GG supplies **game identity**, not a logo.
- The initial image is extracted from the registered game's local Windows EXE icon if available. This may be a launcher/app icon rather than official game key art; no web/logo catalog is invented.
- While that game is running, the user can import a custom PNG/JPG/BMP/ICO. It is converted locally to a private, 32×32, 1-bit JSON artwork in the Beta5 settings folder, indexed by exact GG game name and executable. The original image is not uploaded or bundled.
- The OLED widget can be resized from 12 to 54 pixels. Its initial 16-pixel size at x=96,y=37 is intentionally conservative to avoid overlapping the default Game title and protected battery rows. The user can move/resize other game widgets for larger artwork.
- When no known game is running, or the executable/selected artwork cannot be verified, no fake logo is drawn. A different game immediately stops using the previous game's icon; async loading does not delay drawing other widgets.
- User overrides are **local only** and not included in exported layout JSON or distribution ZIP; community gallery integration remains a future feature.

## Verification
- `dotnet build -c Release` and all `--self-test` checks.
- New acceptance checks: 61×11 progress geometry and boundary clipping, Game-only singleton and mode sanitization, 32→28 synthetic logo rasterization and mismatch/no-game suppression, local image conversion, and a functional off-screen mouse-wheel glide smoke test.
- UI screenshots reviewed for main Media/Game modes, compact layout, Settings, Icon Workshop, Clock/Date, Profiles, Wheel editor, Lyrics and controls.
- A synthetic icon is used ONLY by a dedicated UI screenshot flag and does not appear in ordinary use or personal profile data.
- Physical Nova Pro OLED output and accuracy of logos obtained from actual local game EXEs still require on-device acceptance. No user files/profiles were overwritten; old ZIPs were retained.

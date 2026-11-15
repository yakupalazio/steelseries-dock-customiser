# Private RC7.3 Battery + Render Stabilization Preview

> This is a **private tester package**. **Public GitHub v1.0.0-beta.1 remains unchanged.** Added permanent offline LRC song cache and per-song local .lrc import; **if no lyrics are available, the widget is completely blank**. Battery Settings now discovers and lists wireless USB/Bluetooth accessories even with --% and lets users track/hide them. Added experimental WLMOUSE status-only battery reader; fixed game logo PNG rasterization and non-GG foreground game detection. The **original dock photo is included in the private ZIP**, not redistributed in public GitHub. The top ten firmware rows remain physically protected. See [RC7.3 notes](RELEASE_NOTES_RC73_BATTERY_RENDER_OFFLINE.md).

# SteelSeries Dock Customiser — Public Beta 1 (Windows x64)

> **New:** Beta 1 feedback hand-off opens public GitHub Issue forms (with a separate optional screenshot upload that the tester confirms). The app never silently uploads a report or embeds a GitHub credential. Windows x64 single-file beta is self-contained. Refer to [BETA_TESTING.md](BETA_TESTING.md) and [RELEASE_NOTES_BETA1_PUBLIC.md](RELEASE_NOTES_BETA1_PUBLIC.md). The bundled ZIP excludes the private dock photograph and any third-party SDK binary; all source remains in the developer's local working tree.

# SteelSeries Dock Customiser — RC7.2 UI/Phone/Lyrics Hotfix (PRIVATE TEST)

> RC7.2 keeps the compact Profile Manager with a fixed, fully visible Close footer and saved-layout mini OLED preview; fixes the separate Battery Settings Save footer, the lyric transition choice unexpectedly reverting to Slide, and the old PALE TORTURED / DRACONIAN demo fallback (now No media playing). It adds per-connected-device icon presets or local 11x11 import, safer unique phone deduplication and phone glyphs, correct Quick Guide highlight coordinates including the WinForms title-bar crop, plus a crisp simulated 128x10 USB-1 OLED reference (the real dock firmware-owned rows remain untouched). The user's ATK 373B:101A CompX battery was observed at 90% on the development PC. Other-brand device battery support still requires verification. See [RC7.2 hotfix notes](RELEASE_NOTES_RC72_UI_HOTFIX.md). No user settings, public release or previous private ZIP changed.

# SteelSeries Dock Customiser — RC7.1 ATK/CompX battery hotfix (PRIVATE TEST)

> The exact user's ATK Mouse 1K Dongle (USB 373B:101A) exposes the documented FF02:0002 17-byte HID control collection. RC7.1 adds a strict model-ID-allowlisted **CompX 0x08/0x04 power-status reader**, used by 27 specifically documented ATK/VXE/Pulsar/Compx receiver and cable identities. Requests are **status-only**; no firmware, DPI, USB configuration or Bluetooth pairing. A live battery percent is accepted only from a complete, checksum-valid, 0..100% reply. The first physical query to this user's receiver **timed out** (mouse may be asleep); do not claim its battery was read before a real awake-device retry. Only verified live values enter OLED. See [RC7.1 notes](RELEASE_NOTES_RC71_COMXP_MOUSE_BATTERY.md).

# SteelSeries Dock Customiser — RC7 Wireless Library (PRIVATE TEST)

> **RC7 private preview:** Independent, compact wireless catalog: **364 model names / 35 brands**, with ATK, WLMOUSE and Wraith priority. **Recognition is not a guarantee of battery reading.** Standard Bluetooth GATT was extended with a connected-device fallback; Windows Classic Bluetooth/HFP battery reports require verified connected status and may lag hardware. The user-visible Battery Settings and profile manager were polished. Lyrics support **1–4 lines** and five transition effects. Connected Devices has a privacy-conscious, opt-in "Copy report" function. No copied OpenMouse AGPL source code, no undocumented HID writes, no public Workshop/telemetry. [RC7 full notes](RELEASE_NOTES_RC7_WIRELESS_LIBRARY.md).

# SteelSeries Dock Customiser — RC6 Optional Vendor SDK (PRIVATE TEST)

> RC6 adds an optional official Corsair iCUE SDK BatteryLevel reader (0–100%, verified read-only property), preserving the existing SteelSeries GG and generic GATT/HID sources. The SDK DLL is not distributed, downloaded or automatically installed because third-party redistribution terms require review. See [RC6 Vendor API Notes](RELEASE_NOTES_RC6_VENDOR_API.md). The new Settings > Battery > Brand battery sources screen explains confirmed APIs vs proprietary protocols. This is **not** general Razer/Logitech/ASUS/HyperX/WLMOUSE app API support: no public battery APIs could be confirmed for those apps. Never invent percentages or run vendor software without consent.

# SteelSeries Dock Customiser — RC5 Cross-Machine Reliability (PRIVATE TEST)

> **RC5 cross-machine private preview:** New-install OLED defaults show only Nova Pro headset and spare battery slots; first-run device discovery can recognize and assign verified USB/Bluetooth battery sources; safe per-device nicknames; SteelSeries GG Nova Pro primary/spare battery reader supports alternative status layouts and offers visible diagnostics; Phone Link does not self-open unless separately enabled; LRCLIB timed lyrics get a validated metadata-search fallback; optional Bubbles and Pulse music visualizers. This build **does not claim to support every vendor-specific battery protocol**. Existing user's saved designs and workshop contract remain unchanged. See [RC5 Notes](RELEASE_NOTES_RC5_CROSS_MACHINE.md).

# Steelseries Dock Customiser — RC4.1 Windows Integration

> **RC4.1 private preview:** Windows oturumu açıldığında uygulamayı başlatma (varsayılan kapalı, yalnızca kullanıcı hesabı); isteğe bağlı High işlem önceliği (varsayılan kapalı, Realtime yok); yalnızca Windows izin vermiyorsa kullanıcı onayıyla UAC yardımcısı. Kaynak kodlar, profiller, OLED ve gelecekteki Workshop etkilenmez. Ayrıntılar: [RC4.1 Windows Integration Notes](RELEASE_NOTES_RC41_WINDOWS_INTEGRATION.md).

# Steelseries Dock Customiser — RC4 Release Preparation

> **RC4 private preview (13 November 2026):** New original app logo and icons; bounded startup splash; first-run Next/Skip guide and replay menu; clearer Windows animations; selectable physical OLED mode transitions (Slide/Fade/Wipe/Pixel/Zoom/Instant) with 25–200% speed and protected OEM battery rows; overwrite existing saved profile with backup; private, opt-in bug-report screen (local unsent queue until HTTPS backend is deployed); share-safe future Workshop draft schema (Workshop itself is **not** implemented). See [RC4 Release Notes](RELEASE_NOTES_RC4_PRE_RELEASE.md).

# Steelseries Dock Customiser — RC3 Game Logo & UI Polish

> **RC3 private preview (13 November 2026):** Daha yumuşak düğmeler, açılır seçimler ve ayar kutuları; mouse tekerleğinde hızlanmadan kısa, doğal atalet; Medya ekranında ayarlanabilir ilerleme çubuğu eni/kalınlığı; yalnızca Game ekranında GG tarafından tanınan aktif oyunun yerel EXE simgesi veya kullanıcının seçtiği özel logosunu gösteren widget. Geliştirme ayrıntıları: [RC3 Release Notes](RELEASE_NOTES_RC3_GAME_LOGO_UI_POLISH.md).

> **RC2 preview:** Metin genişliği/kalınlığı, 28 px/s akıcı kaydırma, 1/2/3 satır söz, enstrümantal nota, doğrulanmış ±ms senkronizasyonu ve yumuşak animasyonlu arayüz. Ayrıntılar: [RC2 Release Notes](RELEASE_NOTES_RC2_TEXT_AND_MOTION.md).

> **RC1 Hotfix 1 (13 November 2026):** Removed the song-duration prefix from synchronized lyrics, reclaimed the width for words, and made Circular audio visualization more responsive with a bass-reactive centre and thicker stereo rays. Bars, Stereo, GIF and existing user settings are unchanged.

> **Release candidate:** Dynamic Island benzeri senkron söz animasyonu (yatay akış ve alttan yukarı geçiş), kategorilere ayrılmış öğe kütüphanesi, ekran başına tekil widget kuralları ve dört çoklu widget istisnası, sadeleştirilmiş çift dilli düzenleme arayüzü. Önceki Media Preview 2 Sonar ve senkronizasyon düzeltmeleri korunmuştur. Ayrıntılar: [RC1 Release Checklist](MEDIA_PREVIEW2_RC1_RELEASE_CHECKLIST.md).

> **Lyrics / visualizer fix:** synced lyrics now follow Windows' actual last media-timeline update, rather than resetting their timebase every second. Audio visualizers support SteelSeries Sonar's separate Media/Gaming/Chat outputs, manual device selection, and a visible silent baseline. GIF rendering is unchanged. See [Media Preview 2 Hotfix](MEDIA_PREVIEW2_LYRICS_VISUALIZER_FIX.md).

> **New opt-in widgets:** synced lyrics (Windows media session + LRCLIB, 10 ms timing nudge), stereo audio visualizer (Bars / Stereo / Circular), and local GIF → 1-bit OLED animations (3 dithering modes). See [Optional Media Widgets Preview](OPTIONAL_MEDIA_WIDGETS_PREVIEW.md).

## Earlier Universal Device Discovery Preview

> **Device-name privacy fix:** device names in battery settings and warnings now come from the user's own connected devices. Clean installations show generic role names, never the original developer's phone/mouse/headset models. See [Device Name Privacy Fix](DEVICE_NAME_PRIVACY_FIX.md).

> **Universal Preview 2:** the Pixel Editor now shows the same native 128x10 OEM headphone/battery indicators as the calibrated dock photograph while top-ten-pixel protection is enabled. Unprotecting the header hides the guide and exposes editable pixels. The reference is display-only, never saved or written to the physical OLED. See [OEM Header Editor Preview](OEM_HEADER_EDITOR_PREVIEW.md).

> Experimental development branch. See [Universal Device Discovery Preview](UNIVERSAL_DEVICE_DISCOVERY_PREVIEW.md). Connected Bluetooth and USB/dock devices are now listed regardless of whether their battery protocol is readable. Standard BLE GATT and HID Battery Strength work without brand-specific setup; proprietary Logitech/ASUS/ATK battery protocols still require verified internal adapters.

## Previous Beta 5 RC2 acceptance build

# Steelseries Dock Customiser — Beta 5 RC2 (private acceptance build)

> **Local Beta 5 acceptance build, not a public GitHub release.** The
> application uses `%LOCALAPPDATA%\SteelseriesDockCustomiser-Beta5-Development`.
> Physical OLED writes are allowed only after the application's explicit
> Apply/Live confirmation. Automated tests never modify Beta 4 settings.
> Beta 4's personalized layouts and stable dock handoff remain untouched.

## Beta 5 changes under verification

- Drag Game/Chat logo targets in the large and mini radial previews, or use
  per-icon X/Y adjustments. Icons are constrained to their own rotated audio
  arc sector and are excluded from the dial ring and each other.
- Split ChatMix bar: Game left / Chat right, empty at the equal-gain midpoint,
  independent frames and icons; a single bar still represents Volume.
- Read-only Windows BLE/GATT 0x180F/0x2A19 and SetupAPI diagnostics, automatic
  discovery, dynamic verified battery devices, stable device labels and low
  battery notifications with configurable repeat intervals. No BQC dependency.
- Compact wheel and revised bold clock/date glyphs; 10-second slow battery
  polling with faster supported event-driven refreshes.
- The broken Live start authorization was repaired; direct Nova Pro HID
  output, protected frame, full-screen battery alert and OEM handback passed
  Windows hardware smoke tests. Beta 5 uses its built-in HID writer first;
  the verified ggoled CLI is an optional legacy fallback, not a required app.
- The original dock photograph was restored from the protected local Beta 4
  build and is included in **local testing packages only**.
- Mini wheel now uses the same full pixel workshop as the large wheel,
  with an independent 70–100% compact scale and separately persisted pixels.
- Optional experimental HID/ADB and forced controller priority controls are
  hidden; read-only, previously authorized sources are selected automatically.
- The initial Studio view is **Idle / Boşta**, while the Live dock keeps`r`n  its existing Game > Media > Idle automatic priority.`r`n- Verified charging-start events show a **4-second full-screen OLED charging`r`n  alert** with device icon, battery percentage, large lightning symbol and`r`n  Turkish/English caption. First-seen already charging devices do not alert.`r`n- Galaxy S24+ battery was read via built-in Windows Phone Link during local`r`n  testing (87%, later 84%). When Phone Link has no accessible window,`r`n  the app attempts a rate-limited (10 minute) launch/minimize recovery.`r`n  Windows Phone Link does not promise a stable public battery API.`r`n- See [Beta 5 RC2 release notes](BETA5_RC2_RELEASE_NOTES.md).`r`n- Real user-guided first-time DualSense pairing, repeated battery swaps and
  complete image redistribution review remain open. No GitHub release yet.

**Copyright © 2026 Yakup Can Palaz. All rights reserved.**
Official unmodified releases are free to use. Modification and
redistribution require permission under [LICENSE](LICENSE). See
[TERMS.md](TERMS.md) and [third-party notices](THIRD_PARTY_NOTICES.md).
Earlier valid license grants, if any, are not retroactively revoked.

## Previous versions and background

> **Beta / experimental OLED handoff:** While Live mode is authorized, the app tries one `0x95 → 0x93` recovery after 450 ms of silence following a decoded physical dial event, with a 900 ms cooldown. This is **not** a firmware-level priority override. Physical mode-button presses can remain undetected, so the factory overlay may still persist for around 5 seconds. Repeated 5 Hz release/draw pulses are **not** used in normal Live mode because hardware testing showed partial flicker and native-screen takeover. For reproducible results, unsuccessful tests, risks and contributor ideas, see [OLED known issues](OLED_KNOWN_ISSUES_BETA.md).



## New in 1.1: dark OLED Studio and radial controls

- **Latest 1.1 update:** Rotate the wheel gap with the angle slider (the centre text remains upright). Hardware-volume steps can display signed `+` / `−` changes; ChatMix has G/C icons and fills from a neutral centre instead of from one end.
- **Profiles:** Left sidebar → Profiles. Create a truly blank canvas, save/duplicate a design, rename/delete a profile, apply it and import/export safe JSON. Default Media/Game widgets are now removable; saving never silently re-adds them.
- **Additional optional widgets:** CPU usage, RAM usage and ping, refreshed only while present on a layout. Settings → OLED display provides optional 45-second one-pixel live-output drift to reduce static-image persistence; a USB reconnect retry keeps an authorized Live session armed after temporary disconnection.
- **Physical wheel fix, same 1.1.0 version:** Live mode now reasserts the custom OLED after hardware wheel events and periodically recovers from the dock's native overlay. A persistent OLED-only HID sender reduces redraw latency; the verified ggoled sender remains a fallback. This never changes hardware audio, wheel mode, or firmware configuration. The factory overlay may still appear briefly on turning the physical knob.
- **Smaller native-style wheel:** 45×42 with filled white `VOL` / `MIX` centre and no right-hand duplicate text/percentage. Exact factory firmware pixel data was not extracted. To match a particular dock revision, select the wheel and open **Edit wheel pixels** to paint a custom 1-bit design, including a dynamic segment layer, optional centre text, instant sample-percentage preview and factory reset. **Flat bar** is accessible from both the pixel editor and Display style. Custom wheel art saves per widget and can be undone.
- Rebuilt the right-side property inspector using properly spaced, context-sensitive rows and dark native controls. Pixel editor and dock preview remain on one responsive page.
- Added a **radial wheel** option for hardware **Volume / ChatMix** (the earlier flat bar remains available). The black-and-white 1-bit design is an original, pixel-addressed recreation inspired by the factory ring. Default **Idle** and **Game** layouts now include a removable radial wheel, while existing layouts migrate only once and remember intentional removal.
- Added **Settings → Language** with persistent English/Turkish selection. **About** now sits at the bottom of the Settings navigation. UI labels, menus, dialogs, settings and editor actions support English.
- Polished compact clock rendering, original top-ten-pixel protection and charging bolts; added radial layout/migration/language regression checks.

### Earlier 1.1 reliability fixes

- Repaired the integrated hardware actions. **Uygula** now applies a static design without an automatic timeout; **Canlı** streams updates; **Dock Reset** restores the original screen, including after reopening the app following a crash.
- Removed **Photo** and **Align** controls. The original product photo stays in the user's **local Windows build**, while the GitHub source still excludes that user-provided binary asset. A vector fallback is shown on fresh source builds without it.
- Moved **Battery & Priority** into the dark-styled **Settings → Pil ve öncelik** page; the separate left-sidebar shortcut is gone.
- Fixed the Turkish Microsoft Phone Link accessibility label **“Piliniz şu anda %54”** by recognizing localized suffixes. Verified on the connected Galaxy S24+ without ADB. A phone's displayed status may still lag real battery sync.
- The wheel widget decodes Nova Pro Wireless HID **read-only** notifications: `07 25 raw` for hardware volume, `07 45 game chat` for ChatMix balance. It never writes mixer settings. It shows `--` until a wheel event and labels an inaccessible device rather than guessing data. A button press itself does not supply a guaranteed standalone event; the view changes with the first volume/ChatMix dial movement.
- Wheel HID detection and read-only open/close succeeded on the actual Windows PC while SteelSeries GG was running. The user reported a regression where the native OLED wheel took over and the old custom wheel did not update; this release addresses the display-ownership race, but event delivery under GG and the visual response still need hands-on verification.

Independent Windows 11 desktop designer for the SteelSeries Arctis Nova Pro Wireless base station's **128×64 monochrome OLED**. This is a personal project and is not an official SteelSeries product.

## New in 1.0: one integrated, responsive workspace

A completely redesigned GG-inspired dark UI replaces the old Editor and Dock Preview tabs with **one wide window**:

- **Left sidebar:** Media / Game / Idle layout selection, Icon Studio and Clock/Date shortcuts, runtime game/media status, and Settings (including Battery Priority).
- **Middle, upper area:** Large, clean dock hardware photograph with **no photographic background** (the included development photo has transparency, automatically cropped to the device outline); the live simulated OLED is aligned to the physical screen.
- **Middle, lower area:** Pixel-perfect **native 128×64** editing canvas, selection, pen/eraser, optional grid and undo/redo. The canvas automatically adjusts integer pixel zoom to available screen size and keeps pixel boundaries sharp.
- **Right sidebar:** Human-friendly widget catalog, Add/Delete and context-sensitive properties (text, visibility, font size, stroke, X/Y, optional Mini Player width, and Bar/Wheel dial style).
- **Top toolbar:** Save, Load, export PNG.
- **Bottom status bar:** Current data and header protection state.

**Preview updates immediately with edits** to the selected design, whether or not that design is active on the physical device. Real live dock mode continues to follow runtime priority **Game > Playing media > Idle**. The "Özel tasarım / Orijinal / Uygula / Canlı / Dock Reset" actions are directly beneath the dock photograph, not in a separate tab.

The photo is a **simulation**, not a capture from the hardware. Real OLED writes require pressing **Uygula** or **Canlı** and explicitly confirming. An applied static design remains until **Dock Reset** or normal app exit; if the app crashes, reopen it and use **Dock Reset**. Avoid leaving fixed white graphics on an OLED for long periods.

### Modes and widgets preserved

- **Media:** Spotify/generic source icon, song, artist, progress bar and customizable small clock.
- **Game:** A starter game heading/title, a default removable radial volume/ChatMix wheel, plus an optional Mini Player that shows the playing song while gaming and stays black when paused. All visible widgets can be removed or rearranged.
- **Idle:** Completely customizable clock/date, a default radial wheel, optional text and battery widgets.
- Three dynamic prioritized battery slots, Nova Pro spare battery source, optional DualSense override, the removable read-only volume/ChatMix wheel, pixel icon editor and per-mode saved layouts.

### Charging lightning symbol

When a device's **validated live source explicitly reports it is charging**, a **3×7 native-pixel lightning bolt** appears immediately next to the percentage. Sources currently capable of reporting this include:
- SteelSeries GG: supported Aerox mouse, Nova Pro headset and dock spare battery.
- Windows PowerStatus: laptop only when Windows reports actual **Charging**, not merely plugged in.
- Microsoft Phone Link: when its accessible battery state explicitly reports charging.
- Android ADB (optional): Android's battery status 2.

A known percentage without charging status shows **only the percentage**. A missing reading shows **--%**, never a fabricated number. A headset detail mentioning a charging *spare* battery does not incorrectly cause a headset charging symbol.

## Saved settings and rollback

New layouts are saved to:
`%LOCALAPPDATA%\SteelseriesDockCustomiser\studio-v100.json`

On first launch, 1.0 reads the existing `studio-v099.json` (or `studio-v098.json`) if available. Clicking Save writes **only the new v1.0 file**. Old layout data and source folders are preserved, without automatic conversion in-place.

Battery priority preferences and icon sprites remain in their existing user-local settings. Legacy photo calibration data is preserved but no longer used in the v1.1 interface.

Language preference is stored in `%LOCALAPPDATA%\SteelseriesDockCustomiser\language.json`. A one-time flag in the existing layout preserves explicit wheel removal. No prior layout is overwritten without Save.

## How to run

On the development PC, open `ONIZLEMEYI_AC.cmd` in this project folder. Or run:

```powershell
dotnet build -c Release
.\bin\Release\net10.0-windows10.0.19041.0\SteelseriesDockCustomiser.exe --self-test
```

The third-party [ggoled 0.3.0](https://github.com/JerwuQu/ggoled/releases/tag/v0.3.0) is kept **separately installed** for the experimental physical OLED writer, SHA-256-verified before output, and is never bundled into Git. The device's original top ten rows remain protected by default; the header protection switch can allow full OLED editing, only with explicit confirmation on physical writes.

## SteelSeries GG integration — compatibility and next stage

The official [GameSense SDK](https://github.com/SteelSeries/gamesense-sdk) allows a developer's app to **register and appear in Engine's Apps list** without submitting it for approval. GameSense apps communicate with the Engine's REST API, but this does **not** embed an arbitrary Windows desktop GUI as a native GG page.

Our **full graphical editor remains standalone in 1.0**. For the physical Nova Pro Wireless OLED we currently use the separately tested ggoled path because official GameSense screen zones do not unambiguously document the newer Nova Pro Wireless **128×64** OLED:

- https://github.com/SteelSeries/gamesense-sdk/blob/master/doc/api/standard-zones.md

A later opt-in GG GameSense registration could add an Apps entry / supported events **after checking Nova Pro Wireless zone compatibility and conflict behavior**. No undocumented GG app files are patched or injected, and no registration is performed without user consent.

## Settings and About

**Settings → About** contains application details and the **Yakup Can Palaz** author signature.

Developer read-only diagnostics: `--oled-hid-check` / `--oled-hid-open-test` check the OLED interface without sending a frame; `--dial-hid-check` / `--dial-hid-open-test` check the separate knob notification interface without changing audio. Neither writes a device command.

See `THIRD_PARTY_NOTICES.md` and `THIRD_PARTY_LICENSES/` for HidSharp (Apache 2.0) and external ggoled notices.

## Privacy and safety

- GG battery status and Library catalog are inspected read-only; no game process injection.
- Windows Media sessions provide playing song title/artist and timeline; no Spotify account token.
- Phone Link battery reading uses its accessibility status while open/minimized, with an optional user-enabled open-and-minimize recovery. It does not read messages, photos or contact data.
- No direct firmware modification. Physical OLED output requires the application's normal on-screen confirmation or Live controls.
- The user-provided SteelSeries product photograph stays **excluded from Git**. Local Windows packages built on the development PC contain the photo for personal use or direct review by SteelSeries; fresh source builds without the asset draw a vector dock. Redistribution permission for someone else's product photograph is not implied by owning the device.

## Verification status

- .NET 10 Release compilation and headless self-tests (renderer, factory-header protection, photo alignment, mode priority, charging bolt, radial Volume/ChatMix rendering, layout migration and language).
- Windows off-screen UI captures of Media, Idle, Game and English Settings were inspected; native inputs and context-specific rows were adjusted after visual QA.

# RC7.7 — Game Icon Contrast Engine and Wireless Battery Provider Expansion

**Private Windows x64 QA release · 1.0.0-beta.2-rc7.7.** Built atop verified RC7.6 Git commit 735a1a9. Earlier releases, saved projects, private game-logo overrides and device preferences were not altered.

## Universal contrast-aware game-logo conversion

- New `ContrastIconRasterizer` is the **single** image-to-32×32 OLED path for executable icons, Store application assets, and manually imported game artwork. Removed the obsolete legacy converter.
- Explicit alpha compositing, perceptual color brightness, adaptive Otsu/histogram threshold, original background-color recognition, and fallback threshold scoring.
- Unlike alpha-only conversion, dark pixels of multi-colored transparent icons remain black; bright accents become white. This fixes the reported League of Legends solid-white silhouette **without a title-specific special case**.
- Handles low-contrast/opaque textures, dark/bright backgrounds, non-square icons, and pure-black silhouette artwork with intentional readability fallback.
- Rejects blank/near-solid rectangles and scores alternate thresholds instead of saving an unreadable blob. OLED view and editor use identical produced pixel data.
- Real local EXE tests, source game image not included in release:
  - League of Legends: 377/1024 on pixels, recognizable dark interior/bright L;
  - Sekiro: 45/1024, EXE art;
  - Minecraft Bedrock: 526/1024, local package image;
  - Minecraft Launcher: 98/1024, local Creeper symbol.
- Synthetic transparency/dark-blue/gold, textured game tile, black-white icon and full frame parity tests pass.

## Wireless battery support

- Added **WindowsGameControllerBatteryProvider** using built-in `Windows.Gaming.Input.RawGameController.TryGetBatteryReport()` for *connected* game controllers with real OS-provided remaining/full capacity.
- No new third-party DLL is needed: the API is part of the existing Windows SDK. Unsupported report or invalid capacity => **unknown**, never a guessed percentage. No HID feature/output writes, controller pairing, RGB control, USB driver installations or gamepad input capture.
- Known Sony DualSense device may feed the existing controller battery slot when it has no better verified HID/GATT battery. Multiple identical controllers preserve separate identities instead of arbitrarily assigning a level to the default slot. Existing HID reading is not overwritten.
- Expanded the provider registry and truthful vendor source/compatibility view. Existing providers: GG, BLE GATT, connected Windows Bluetooth HFP/PnP, standard USB/Bluetooth HID, ATK/CompX verified receiver, WLmouse verified HID, optional official Corsair iCUE SDK, DualSense HID and Phone Link/ADB.
- Did **not** import unverified vendor DLLs or third-party SDKs just to increase package size. SDK and protocol existence does not guarantee every device exposes readable battery data.

## Actual on-PC audit

- Live scan found Aerox 3 mouse battery and dock spare battery. Windows still reported no verified primary Nova Pro headset level, no DualSense report and no Phone Link phone battery in the observed scan.
- Adding the controller source increases *possible* support but cannot override device/driver limitations. Missing readings remain "--%".
- Existing battery freshness and device identity checks are preserved; no cached disconnected percentages are promoted to live OLED state.

## Validation and safety

- `dotnet build -c Release --no-restore`: **0 warnings, 0 errors**.
- Full `--self-test`: PASS including 4 physical installed game executables, new color/contrast tests and controller capacity/dedup tests.
- No physical OLED/firmware tests were executed. Display legibility and actual unsupported-device hardware behavior remain pending user validation.
- GitHub public beta, saved user assets and previous private ZIP files remain unchanged. This is **private RC7.7 QA**, not stable 1.0.

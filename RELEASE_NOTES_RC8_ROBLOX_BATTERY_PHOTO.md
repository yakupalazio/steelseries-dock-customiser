# RC8 — Roblox detection, live headset battery fallback and dock photo restored

**Public beta candidate for Windows x64 · version `1.0.0-beta.2-rc8`.**

## Why this update exists
A tester of RC7.9 reported: (1) Roblox opened but the dock stayed in Idle and drew no game logo; (2) the battery screen recognized Huawei FreeBuds SE 2, WLMOUSE Beast X Pro 8K, and the SteelSeries Nova Pro dock, but did not show any live percentages; and (3) the GitHub download showed a simplified, drawn dock instead of the original full product photograph.

## Roblox
- Added read-only Roblox Player detection independent of SteelSeries GG's game library. Only genuine running Windows **RobloxPlayerBeta.exe** in the standard versioned Roblox installation, or the expected Roblox Microsoft Store package, qualifies. Roblox Studio and Roblox launcher are never treated as the game.
- If the game has no usable Windows executable icon, show an original **high-contrast 32×32 monochrome Roblox marker** instead of a blank OLED screen.
- Existing game detection (Among Us, NFS Unbound, Valorant, Minecraft, LoL and other GG registrations) is unchanged and covered by regression tests.

## Batteries
- Added optional, narrowly allowlisted **SteelSeries Nova Pro Wireless dock HID status-only `06 B0`** read for supported USB endpoints (`1038:12E0`, `1038:12E5`). This operates only when the hardware is present; it does not alter firmware, device settings, ChatMix or OLED output.
- The HID report represents the headset and spare battery in **12.5% increments**, so these readings are labeled **approximate**, not precise measurements. Existing validated, finer-grained SteelSeries GG percentages remain preferred; missing values are never invented.
- The status reader was tested on the developer's actual Nova Pro dock and returned **87%** headset / **100%** spare, matching the GG reading during that test.
- Added a read-only Windows connected Bluetooth device property check as another possible source for earbuds/headsets, in addition to existing BLE GATT BAS, Windows HFP/PnP and HID sources.
- Added safe passive `GetFeature`/input handling for newer WLMOUSE devices, retaining the strict prohibition on unknown proprietary control commands.
- **Not yet verified:** FreeBuds SE 2 and Beast X Pro 8K battery percentages on the tester's machine. Those devices may still show `--%` when Windows/vendor firmware does not expose an interpretable live battery value. No guarantees of all wireless devices are made.
- Right-click the battery grid and select **Pil tanı raporunu kopyala** to share a diagnostics report without addresses, serial numbers, private tokens, or pairing secrets.

## Original SteelSeries dock preview photo
- The maintainer-requested **`dock-photo-local.png`** is now explicitly included in the source build and Windows download. The GitHub release build fails if the photo is missing, to prevent a regression to the generic drawn dock.
- Original native OLED-aligned image presentation is preserved; this photo is **not** transmitted to the physical dock.
- The included photo is used solely for product identification; SteelSeries trademarks and third-party photo rights remain with their respective owners. This project is not endorsed by SteelSeries. Private/user-supplied custom images remain excluded.

## Packaging and QA
- `dotnet build -c Release`: 0 warnings / 0 errors.
- All existing and RC8 self-tests passed. The RC8 tests cover game identity, no false studio/launcher recognition, monochrome Roblox fallback, HID status report parsing/allowlist, GATT/HID source priority and original dock photo presence.
- Tested a real Nova Pro dock's HID report. **Actual Roblox gameplay and Huawei/WLMOUSE tester hardware still require tester acceptance.**
- Portable, self-contained Windows x64 package. Extract the whole ZIP, fully quit previous Dock Customiser from the tray, and run `SteelseriesDockCustomiser.exe`.
- **This is a beta prerelease, not stable 1.0.** All previous releases remain available.

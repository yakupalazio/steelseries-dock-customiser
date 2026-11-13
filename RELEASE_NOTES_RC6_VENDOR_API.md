# RC6 — Official Vendor Battery API Integration (Private Test)

## Scope
One **verified public battery API** added: **Corsair iCUE SDK 4.31+**, CDPI_BatteryLevel = 9, CorsairReadDeviceProperty, CorsairGetDevicePropertyInfo, CorsairGetDevices and shared CorsairConnect. Status rows expose manufacturer capabilities, not fictitious battery readers.

## Corsair iCUE
- Read-only calls to the documented, official SDK; no lighting changes, key interception, device writes, driver installation, elevated helper, third-party process launching, polling of private iCUE data files, or network access.
- Each property must advertise CanRead + Int32, return a true 0..100 integer, and come from a connected SDK device. Every reading has a source, actual model name, vendor-stable hashed identity and timestamp.
- A native call timeout cannot block the ongoing OLED refresh. Unresponsive native SDK calls remain isolated with a one-at-a-time gate.
- **Not shipped by us**: Corsair EULA covers the redistributable, so before public 1.0 do not embed or redistribute a manufacturer DLL without verifying license rights. The application's own C# reader contains no Corsair SDK binary.

### Optional manual Corsair SDK installation for testers
1. Install the latest supported **Corsair iCUE** from Corsair, and ensure it is running; third-party SDK access must be allowed if the installed iCUE offers that setting.
2. Download the genuine SDK from CorsairOfficial: https://github.com/CorsairOfficial/cue-sdk/releases
3. Copy only the **x64** SDK DLL `iCUESDK.x64_2019.dll` to `VendorSDK\Corsair\iCUESDK.x64_2019.dll` next to the extracted `SteelseriesDockCustomiser.exe`.
4. Restart Dock Customiser; visit **Ayarlar > Pil ve öncelik > Markalara göre pil kaynakları** to see whether the SDK was found. Live Corsair device batteries appear as regular verified sources only when iCUE exposes their BatteryLevel property.
5. Remove the DLL to disable the optional integration completely. No manufacturer software is installed or started automatically.

## Other brands: not invented APIs
- Logitech G HUB: documented public SDK family provides lighting/GamePanel/G-Keys/steering-wheel integrations; not a general battery endpoint. Model-specific Logitech HID++ protocol work is a separate future adapter (reference: Solaar).
- Razer Synapse: public Chroma SDK controls lighting; no confirmed general battery endpoint. Model-specific OpenRazer-style HID readers are separate, and would need strict VID/PID validation and opt-in hardware acceptance.
- ASUS Armoury Crate/Aura: public Aura SDK exposes lighting, not a general peripheral battery endpoint.
- HyperX NGENUITY, WLMOUSE: no broadly documented public companion-app battery API confirmed; some model-specific HID reports exist in third-party open source. Do not copy code with incompatible licensing or guess proprietary report IDs.
- Standard BLE GATT Battery Service (0x180F/0x2A19), standard USB HID Battery Strength (0x06/0x20), and SteelSeries GG remain active.
- The new **Brand battery sources** page describes each source's actual state and links to Corsair's official SDK.

## Regression/test obligations
- Compile with 0 warnings/errors, run all existing RC5 self-tests, new SDK ABI/format/validation/priority/staleness/deduplication unit tests, and performance smoke.
- Capture a screenshot of Brand battery sources with no hardware enumeration or SDK launch.
- The test computer has no compatible Corsair SDK/hardware; a real Corsair iCUE user must verify actual live percent/SDK handshake before public release.
- Keep all stored profiles, battery priority overrides and user's existing factory profile unchanged; add an independent RC6 private ZIP, and retain original RC5 ZIP.
- No public GitHub release or guaranteed support for Logitech/Razer/ASUS/HyperX/WLMOUSE is claimed.

## Sources used
- Corsair Official iCUE SDK: https://corsairofficial.github.io/cue-sdk/
- Corsair Official SDK header: https://github.com/CorsairOfficial/cue-sdk/blob/master/src/include/iCUESDK.h
- Logitech official SDK description: https://www.logitechg.com/en-us/programs/developer-alliance
- Razer Chroma SDK: https://assets.razerzone.com/dev_portal/docs/index.html
- ASUS Aura developer API: https://www.asus.com/microsite/aurareadygamesdk/game_sdk_restful.html

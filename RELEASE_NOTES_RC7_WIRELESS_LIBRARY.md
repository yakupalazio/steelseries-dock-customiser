# RC7 — Wireless Compatibility Library & OLED Polish

Private Windows x64 tester preview, not a public 1.0 beta.

## Wireless library
- 364 independently curated wireless product names from 35 brands. Priority: WLMOUSE Beast X Pro, ATK family (including undisclosed blind-test mouse), Wraith W1, VAXEE, Logitech, Razer, ASUS, HyperX, Corsair, SteelSeries, Pulsar, Lamzu and many others.
- Compact searchable Battery Settings > Wireless Device Library with brand, model, type and honest battery support status. Product-name recognition does NOT mean the actual battery percentage is readable.
- Public model-specific USB VID/PID mappings include ATK A9 Plus (373B:10C9), selected WLMouse receiver IDs from vendor 36A7, VAXEE vendor 3057 with published model PIDs, Logitech 046D:C094 and C095. Shared receiver IDs never imply unique mouse model.
- OpenMouse AGPL-3.0 protocol source was NOT copied or bundled. Product names and public hardware-identity facts were independently curated. Wraith W1 is a requested model in OpenMouse, not a verified working driver.
- The model database uses no hidden proprietary access, HID configuration writes, cloud telemetry or self-installed third-party apps.

## Read-only Bluetooth
- Windows Bluetooth LE GATT Battery Service 180F, Level 2A19, uncached: existing already paired services plus newly connected paired LE device fallback, multi-BAS instances, stable per-service key and one scan at a time.
- Windows Classic Bluetooth HFP / PnP battery: accept the Windows OS-reported battery property only when the exact Bluetooth address is confirmed CONNECTED by WinRT and the PnP node is present. No address is exported; local battery keys are hashed.
- Windows itself may update HFP battery status late after reconnect. UI and release notes indicate OS-reported values can lag hardware. No disconnected or conflicting profile reading is accepted.
- Bluetooth, generic HID and vendor adapter polls run in parallel with bounded timeouts, protecting other OLED tasks from stalls.

## UI and media
- Lyrics now support 1, 2, 3 or 4 lines and five timecode-locked transitions: Slide (default), Fade, Drop, Wipe, Instant. OEM first 10 pixel rows protected and layouts are preserved.
- Profile manager is resizable/scrollable and the formerly clipped bottom Close button has a permanently visible footer.
- Battery Settings have a clearer live battery count, priority controls, compatible device search, setup, source description, low-battery alert grouping, TR/EN labels and Save action. Phone Link automatic launching remains off by default.
- Connected Devices > Copy report is an opt-in local-only diagnostics action: friendly device names, public USB VID/PID, transport and battery-source status. Does not export Bluetooth MAC, USB serial, account IDs or raw Windows device paths. Review names before sharing.

## Verified on development computer
- Build Release: zero errors and warnings.
- Full regression suites for BLE 0-100, conflicting Classic Bluetooth data, stale/disconnected suppression, live HID priority, catalog 364/35, ATK blind-test recognition and 4-line lyric clipping.
- Offscreen UI screenshots for profile, lyrics, wireless library, connected devices and Battery Settings (Turkish/English), plus performance smoke.
- A real connected Windows Bluetooth device's OS HFP battery property was successfully read, without promoting cached disconnected diagnostic values.
- No physical tester-WLMOUSE, blind ATK mouse or Wraith hardware acceptance. Generic recognition is NOT complete proprietary battery protocol support. Manufacturer-specific USB receiver batteries still require documented, independently implemented read-only adapters and tester validation.

## Tester workflow
1. Check Battery & priority > Wireless Device Library.
2. Open Connected devices > Copy report. Review and share only what you want. Record official vendor battery level vs Windows reading and whether connected via Bluetooth or USB receiver.
3. Check first-run profile/locale, 4-line lyrics, transitions, BLE vs Classic Bluetooth and hardware disconnect/reconnect. Report inaccurate percentages.
4. Do not publish RC7 as stable 1.0. Old RC6 ZIP, user profiles, local settings and future Workshop data format remain untouched.

## Independent references
- https://openmouse.app/supported
- https://www.bluetooth.com/wp-content/uploads/Files/Specification/HTML/BAS_v1.1/out/en/index-en.html
- https://learn.microsoft.com/en-us/windows-hardware/design/accessory-guidelines/bluetooth-accessory-guidelines/bluetooth-accessory-guidelines-battery-profiles-services

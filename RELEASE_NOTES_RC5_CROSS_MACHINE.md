# RC5 — Cross-machine Battery & Media Reliability (Private Tester Preview)

Base: RC4.1 `4bbb765`. Not a public 1.0 beta or a guarantee that every manufacturer's proprietary battery protocol works.

## Confirmed problems from a second Nova Pro Wireless PC
- The OS enumerated Arctis Nova Pro Wireless, Huawei FreeBuds, a ROG keyboard, a WLMOUSE receiver and other USB peripherals. But every listed device had no validated live battery source. Device enumeration alone is not proof of battery-percent support.
- The previous GG reader required a particular active loadout and precisely nested Engine JSON, and could silently return no data on another GG installation. The generic Bluetooth/HID readers cannot decode proprietary vendor-only formats.
- Lyrics required one exact LRCLIB song signature and did not search alternate synced matches.
- Background polling called a Phone Link recovery method with automatic launching enabled, despite a false default setting.

## RC5 improvements
1. **Cross-PC clean-install defaults:** OLED media, game and idle templates contain **two** battery slots (Headset and SpareBattery) instead of three hard-coded generic slots. The new user's initial priority list defaults to these two; existing personal layouts and previous priority settings are not migrated or overwritten. Newly measured standard BLE GATT/HID/Windows-compatible batteries automatically fill additional eligible slots if the user adds another battery widget.
2. **One-time device setup:** After the first-run UI guide, an optional USB/Bluetooth inventory walks the new user through detected equipment; only verified current-percent sources can be assigned to OLED priority. Unsupported devices are still listed but display unknown, never fabricated numbers. A separate "Device setup" button in Battery settings reopens the wizard. Stable provider-key nicknames are saved **locally only** and can be edited.
3. **GG Nova Pro:** The reader now accepts verified `batteryLevels` in a device's own status fields or inside a loadout, handles a missing/changed active loadout and accepts a Nova Pro transmitter variant. Both primary and spare-battery data require an explicit verified status, subject to the reported schema. GG failures are shown as truthful diagnostics rather than a generic "No provider". **Real tester confirmation is required** because different GG versions and device firmware may use additional proprietary schemas.
4. **Phone Link:** Automatic window opening/minimization is now genuinely **off by default**. Existing Phone Link windows may be read without changing focus. If the owner enables the separate recovery checkbox in Battery settings, the bounded minimized-launch workaround is allowed again. ADB phone access also stays opt-in.
5. **Lyrics:** Keeps exact LRCLIB first, then tries a limited, sequential search for timecoded lyrics if exact metadata differs. Verification checks normalized track title and principal artist, rejects differences greater than 7s and respects LRCLIB `429 Retry-After` rather than querying continuously. No lyrics are invented when LRCLIB has no usable synced record.
6. **Audio visualizer:** Added **Bubbles** (independent, 1-bit bubbles flowing upward faster with actual bass) and **Pulse** (dynamic concentric rings); no extra audio storage or upload. Existing Bars/Stereo/Circular retained. Bubbles use per-widget monotonic animation state so bass changes do not cause teleportation.

## Remaining compatibility limitations
- Some gaming keyboard/mouse receivers and Bluetooth headsets advertise no standard battery characteristic. Full support requires a vendor-specific, legally usable protocol adapter and actual device testing; do not guess a percentage or import a stale PnP cache.
- Nova Pro Wireless primary/spare can be read only if GG or another validated approved adapter exposes the state. Direct hardware `06 B0` vendor status commands were NOT silently enabled here; the app still does not send USB/HID device configuration commands when refreshing batteries.
- The operator's private settings, profiles, aliases and Workshop architecture are separate. No community Workshop service has been launched and no cloud bug-report endpoint has been deployed.

## Tester acceptance before a public beta
1. On the second PC, confirm whether *SteelSeries GG Engine itself* shows current primary/spare battery percentages. With GG open, check RC5 Battery settings > Headset and SpareBattery diagnostic/status rows. Capture these rows if numbers remain unknown.
2. Open Battery settings > Device setup; confirm real connected Bluetooth/USB names and verify all supported battery-source associations. A new device with only USB enumeration may truthfully display --%.
3. Verify that default first-run layout has exactly two generic battery widgets; previously saved user layout remains identical. Try opting into one discovered device, changing its nickname and restoring priority order.
4. Restart RC5 with Phone Link recovery disabled: Phone Link must NOT be opened by RC5. Optionally opt in and test the previous behavior separately.
5. Test synced lyrics with at least three tracks, including a remastered title and a track with multiple artists; note whether the Windows media session exposes the correct track/duration and whether LRCLIB actually has time-coded lyrics.
6. Test Bubbles and Pulse on Sonar/Windows outputs with music playing, muted and after switching the output device.
7. Test on real dock for startup, battery header ownership, disconnect/reconnect and long-running performance. All offline self-tests only simulate these behaviors.

No official GitHub/public beta release was created.

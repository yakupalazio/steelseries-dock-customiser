# RC7.1 — ATK & CompX Wireless Battery Reader (PRIVATE TEST)

This private Windows x64 build extends RC7. No public 1.0 beta release.

## User's real ATK receiver
- Windows hardware report: ATK Mouse 1K Dongle, USB `373B:101A`. This is a **receiver identity, not proof of the physical mouse's exact model**. The user's blind-model compatibility test remains intact.
- Read-only interface inspection on the authorized PC confirmed exactly one compatible HID top-level collection: usage page `FF02`, usage `0002`, **17-byte input and 17-byte output**. Seven other collections were rejected without sending any requests.
- Implemented dedicated **CompxWirelessBatteryProvider** (C#, independently implemented from public protocol descriptions). It sends only the documented 17-byte `0x08 / 0x04` **power-status query** to a strictly allowlisted VID/PID and the matching `FF02:0002` collection. It never sends DPI, configuration, lighting, pairing or firmware commands. The code does not use or bundle the OpenMouse AGPL driver.
- Reply is accepted only if report ID is `0x08`, command `0x04`, checksum matches `(0x55 - sum(bytes 0..15)) modulo 256`, and percent at byte 6 is within `0..100`. Byte 7 indicates plugged-in/on-cable state; bytes 8..9 are optional voltage metadata. Invalid/unrelated reports or silence produce **--%**, not fabricated percent.
- USB device path is locally hashed as `Vendor:Compx:<hash>`, not exposed to the user report. Live percent automatically participates in the Battery settings, device setup and OLED priority pipeline.
- **Hardware probe outcome:** the 17-byte status query was sent to the matching collection, but this session obtained **0 valid replies and a read timeout**. Many CompX receiver mice cease answering when asleep/idle. The mouse must be physically moved/woken before the user presses Refresh. We do **not** claim a live value was confirmed on this computer. The UI now names the Compx reader and explains what to do when no reply arrives.

## Same protocol family
The reader contains **27 exact VID/PID entries** documented for ATK/VXE, Pulsar, VGN, Redragon, Scyrox, Hitscan, G-Wolves, Corsair Sabre v2, Cougar and some CompX wireless keyboards. The same status format is used only when the device itself also advertises `FF02:0002` with the correct 17-byte report lengths. Other devices are refused without any USB output. Model support is confirmed per-device only by actual valid replies; documentation/reference-hardware status is separate.

## Safety, compatibility and licensing
- The receiver whitelist is **not** a blanket `373B` or `3554` match. A new unlisted model cannot be queried just because it shares a manufacturer VID.
- Tests never enumerate/modify USB hardware. The explicit `--compx-inspect` CLI only reads HID metadata (no USB writes); the explicit `--compx-power-probe` sends only the approved power query.
- A sleeping or disconnected mouse returns `--%`. Stale values are not promoted to live OLED data; existing 25-second TTL applies.
- The proprietary Dock Customiser source remains private. HaloBattery is MIT-licensed; we referenced public protocol facts and independently implemented C# rather than copying Python source or OpenMouse's AGPL code. The original attribution and reference URLs are recorded below.
- No user profiles, Phone Link preferences, charging rules, OLED default widgets, private settings or Workshop format were reset or modified.

## Test results
- `dotnet build -c Release`: zero errors/warnings.
- Entire existing RC7 regression suite + new strict output query/checksum, wrong command/report ID, malformed/truncated/101% reply, 17-byte collection validation, exact VID/PID allowlisting, duplicate source prevention, ATK blind model/connected-device association, OLED priority and stale expiry tests pass.
- Packaged Windows EXE `--self-test` and `--perf-smoke` were rerun independently. Screenshots of the Battery settings and vendor-source compatibility page captured.
- **Remaining acceptance:** with the physical ATK mouse awake, trigger a new battery refresh. Compare the returned live percent with ATK HUB if available. The tester hardware model has intentionally not been disclosed; dongle identity alone must not determine it.

## Independent references
- HaloBattery hardware-verified ATK X1 Pro 1K dongle `373B:101A` and Compx protocol: https://github.com/HeyOkay/HaloBattery/blob/main/docs/protocols.md
- HaloBattery MIT license: https://github.com/HeyOkay/HaloBattery/blob/main/LICENSE
- HaloBattery CompX device list: https://github.com/HeyOkay/HaloBattery/blob/main/providers/pulsar.py
- OpenMouse research catalog, not copied: https://openmouse.app/supported

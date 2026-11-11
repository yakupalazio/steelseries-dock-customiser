# Nova Pro Wireless OLED: Read-only firmware investigation

Date: 2026-11-11. Target: SteelSeries Nova Pro Wireless base station transmitter MCU.
**No firmware flashing, bootloader operations, device writes, or installed firmware changes were performed.**

## Empirical baseline

- The native Volume/ChatMix popup persisted about 5 seconds when drawing with 0x93 at 50 FPS (49.26 measured) or 60 FPS (58.25 measured).
- Single 0x95 then 0x93 after 450 ms of dial inactivity restored the custom image in about 1 second after volume rotation, without recurring flicker; mode-switch button presses were not reliably handled.
- More aggressive 0x95 then 0x93 bursts sometimes caused partial-screen flicker.
- Read-only 0xB0/0x20 polling received 338 responses with zero changed status bytes during supervised mode changes; other undocumented status transports are not excluded.
- Temporary 0x49 01 and 0x8D 01 commands enabled ChatMix with GG/Sonar closed; both were restored to zero afterward.

## Installed original firmware file

C:\Program Files\SteelSeries\GG\apps\engine\firmware\272110304\firmware_arctis_nova_pro_wireless_tx_mcu_v1.29.27.hex

SHA256: 203888eeedf29cd2de937156eff501fcd62c8e08805b25b452293b36b287ff0a

Intel HEX checksums and record lengths validated. Mapped regions:

- 0x00008000..0x0003AE33: 208,436 bytes, plausible ARM Cortex-M vector table (SP 0x20030000, Thumb reset entry 0x00008181).
- 0x00055800..0x000754D7: 130,264 bytes, starts with CA FE BA BE; apparently a structured data container. Arbitrary Thumb disassembly of this region is unreliable.

The little-endian integer 5000 occurs at 0x00056CE4 **within the data container**. A naive disassembler produces a spurious apparent LDR reference nearby. **This is not evidence of an OLED popup timeout.** No verified overlay-priority or popup-duration command was identified in this pass. The absence of a simple 0x93/0x95 immediate comparison in vector-backed code does not rule out indirect dispatch or packed code.

## Protocol facts and boundaries

- 0x93: OLED draw, 1024-byte HID feature reports (two 64×64 strips per 128×64 frame).
- 0x95: 64-byte output report that returns OLED ownership to the native station UI; subsequent 0x93 reports draw custom content.
- 0x89: simple versus detailed station screen mode, per third-party protocol notes; **not a verified display priority setting**.
- 0xB0 / 0x20: aggregate device status/settings queries, not documented mode-button state reports.

## Recommended next steps

1. Preserve single-reclaim after 450 ms of dial inactivity as the measured baseline, not a production default yet.
2. Under Yakup's direct supervision, optionally compare 50 FPS 0x93 drawing with sparse 0x95 pulses (5 Hz, at most 10 seconds); cap total writes and abort on flicker, audio issues, or HID latency. Restore OEM display in finally.
3. Research a verified notification for physical mode button transitions. Without one, a host cannot reliably trigger reclaim on button press with the currently known events.
4. For any deeper firmware analysis, prove code boundaries and call references starting from the vector-backed region. Do not modify or flash the HEX.
5. Build a bounded event-driven screen-arbitration state machine only after event detection and timing are verified.

## Repeatable read-only scan

Run FirmwareStaticAnalyzer.py with the original HEX path as its argument. It validates Intel HEX checksums, reconstructs mapped memory regions and candidate constants, and labels data-container hypotheses. Optional --focus and --span enable raw Thumb decoding with an explicit warning about false instructions.

Source references:
- https://github.com/JerwuQu/ggoled
- https://github.com/TimP4w/HaloDaemon-plugins/blob/main/steelseries_arctis/docs/protocol.md
- https://github.com/Piipperi/SS-OLED-Mac-Controller

This note is research evidence, not firmware modification guidance.

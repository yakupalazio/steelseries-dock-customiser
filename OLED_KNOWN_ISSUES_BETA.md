# OLED ownership and ChatMix: known issues (1.1.1-beta.1)

Status: **unresolved / reproducible**. This document separates actual observations from hypotheses. This project is unofficial and does not modify device firmware.

## What works and what does not

| Interaction or experiment | Observed result | Confidence |
| --- | --- | --- |
| Original 0x93-only live redraw (~28 FPS) | Native Volume/ChatMix overlay generally remains visible ~4–5 s after a physical action. | Observed |
| 50 FPS 0x93-only test | ~49 FPS achieved; native overlay still lasted ~5 s. | Observed |
| 60 FPS 0x93-only test | ~58 FPS achieved; native overlay still lasted ~5 s. | Observed |
| One 0x95 → 0x93 after 450 ms since last **decoded wheel turn** | In one supervised test the custom screen returned after ~0.5–1 s with no flicker. | Observed in diagnostic test; **beta UI integration not yet revalidated** |
| Repeated / aggressively timed 0x95 → 0x93 | Left half and other regions briefly flickered 1–3 times; sometimes OEM panel remained. | Observed |
| 50 FPS drawing + 5 Hz 0x95 → 0x93 (15 s cap) | 47.47 FPS, 74 pulses, no USB write error, but OEM interface stayed visible throughout and the physical ChatMix mode switch did not work in that run. | Observed; **failed** |
| Native Volume ↔ ChatMix mode button | A standalone press event was not reliably visible through the polled HID reports. Sometimes the OEM mode screen remains indefinitely until another wheel movement. | Observed |
| GG closed; temporary 0x49 / 0x8D ChatMix enable | Physical mode switching worked in some separate runs; actual Sonar game/chat audio mixing does not automatically exist when GG/Sonar is closed. | Partially observed; platform mixer support unresolved |

Do **not** treat successful HID writes or measured FPS as proof that a custom frame was displayed.

## Working theory (not proven firmware behavior)

- **0x93** carries bitmap regions, and **0x95** returns display control to the factory interface (as documented by the independently developed [ggoled](https://github.com/JerwuQu/ggoled)).
- A physical dial turn or mode change seems to start an OEM overlay, often visible for around five seconds. That duration and arbitration mechanism have **not** been located and verified in firmware.
- High-frequency 0x95 release commands likely repeatedly reactivate native ownership. A two-part OLED report can also visually tear if the factory renderer updates between the left and right columns. Both are **inferences**, not firmware proof.
- When GG is running it may consume an input report, and the firmware may emit no separate notification for a press-only mode switch. Distinguish button events, Volume changes and ChatMix-balance changes.

## Beta behavior

- Only in user-confirmed **Live** OLED mode, a decoded physical wheel event arms a **single** 0x95 → 0x93 recovery **450 ms after the last observed wheel event**. A 900 ms minimum interval prevents rapid repeated releases.
- Routine Live updates use the existing 0x93 drawing path; the failed 5 Hz release loop is **not** used by Live.
- If enabled, header protection confines application-drawn pixels to rows **10–63** so the factory's rows **0–9** are not addressed by 0x93. Full-screen mode still requires separate explicit authorization.
- The beta cannot promise 0.5–1 s restoration on every unit, for a press-only ChatMix mode change, or when no wheel event reaches the application. The beta integration has unit tests but **still needs an on-device acceptance test**.
- Stop Live or press Dock Reset to restore factory OLED ownership. After an app crash, reopen and use Dock Reset. The beta does not flash firmware or alter audio volume.

## Firmware exploration

Read-only static inspection of a local 338700-byte Intel HEX image found two address regions and an additional container with a `CA FE BA BE` marker. A literal `5000` occurred in the container, **not a verified five-second timeout constant**. No confirmed overlay priority register, timeout field, or safe persistent configuration instruction was found. See [FIRMWARE_OLED_FINDINGS.md](FIRMWARE_OLED_FINDINGS.md). Do not patch or flash an experimental firmware image to solve a cosmetic OLED issue.

## Reproduction and contribution ideas

1. Confirm exact model (Nova Pro Wireless revision), original firmware version and whether SteelSeries GG / Sonar is running. Do not publish device serial numbers.
2. Test **Volume up**, **Volume down**, **button → ChatMix**, **ChatMix balance turn**, **button → Volume**, separately. Record whether the factory OLED is visible for 0–1, 1–2 or ~5 seconds and any left/right partial-frame flicker.
3. Capture raw *read-only* input from every exposed HID collection while changing mode; compare 0x25/0x45 events. Do not infer a mode-toggle packet just from screen changes.
4. Compare actual USB read/write **ordering** (including report type Output vs Feature), not only command bytes. Report if a proven non-persistent ownership or native-overlay cancel command exists.
5. Prefer one-shot, event-driven refresh with backoff to high-rate release loops. Keep a restore path, timeout, device-specific VID/PID safeguards and explicit user consent.
6. If sharing logs/issues, remove user paths, account names, serials and any private assets.

### Related reverse engineering

- [ggoled — Nova Pro OLED 0x93 drawing and 0x95 return](https://github.com/JerwuQu/ggoled)
- [nova-chatmix-linux — ChatMix handshake and read events](https://github.com/Dymstro/nova-chatmix-linux)
- [OpenChatMix — Windows mixer and SteelSeries dial support](https://github.com/Swiftlyx/OpenChatMix)

Please open a GitHub issue or pull request with a minimal, reproducible log and precise device revision. Reports of a *failed* experiment are useful too.

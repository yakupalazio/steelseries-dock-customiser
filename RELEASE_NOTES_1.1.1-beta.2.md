# Steelseries Dock Customiser v1.1.1-beta.2

Experimental **Windows 11 x64 portable beta** for the Nova Pro Wireless 128×64 OLED. Unofficial; by **Yakup Can Palaz**.

## What's new / Yenilikler

- In confirmed Live mode only, a detected physical dial movement schedules **one** custom-screen recovery after **450 ms of inactivity** (minimum **900 ms** between attempts).
- The recovery sends a single `0x95` release and then custom `0x93` bitmap patches. It does **not** flash firmware or change audio configuration.
- Default mode preserves the factory top ten OLED rows (headphone and spare battery). The failed 5 Hz release experiment is **not enabled** in Live mode.
- Full test history, failure modes and reverse-engineering suggestions are published in [OLED known issues](OLED_KNOWN_ISSUES_BETA.md).

## IMPORTANT: open issues / Bilinen sorunlar

- The **0.5–1 s restoration** was observed in an earlier supervised **diagnostic** run. This beta UI implementation is newly integrated and still needs separate real-hardware acceptance.
- The physical **Volume ↔ ChatMix mode button** may not produce a distinct readable event. The factory display can remain visible around 5 s or until another dial movement.
- Repeated `0x95 → 0x93` at 5 Hz caused **flicker**, original-screen takeover, and loss of mode switching in a test. It has been rejected for normal operation.
- GG/Sonar can conflict with independent OLED ownership. Closing GG also closes Sonar; real ChatMix audio balance depends on compatible host mixer software.
- Native firmware's overlay duration and priority arbitration have not been conclusively reversed.

## Install / Kurulum

1. Extract `SteelseriesDockCustomiser-v1.1.1-beta.2-win-x64-portable.zip`.
2. Run `SteelseriesDockCustomiser.exe`. .NET 10 runtime is included; this binary is **not code signed**.
3. Preview/design works without hardware access. **Uygula / Canlı** requires explicit confirmation and the separately obtained SHA-verified `ggoled v0.3.0` helper documented in the Turkish guide.
4. Stop Live or use **Dock Reset** to return to the OEM screen.

This **public** asset deliberately does **not** contain the developer's local dock photograph; the app displays its vector fallback. Do not share device serials, personal paths, or private photographs in bug reports.

Please help investigate via GitHub issues or PRs with bounded, non-firmware experiments. The full technical history is in `OLED_KNOWN_ISSUES_BETA.md`.

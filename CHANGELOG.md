# Changelog

## 0.9.5 — 12-row factory-header preservation

- Corrected reserved header height from 16 rows to **12**, based on device photos and SteelSeries Now Playing behavior.
- Physical OLED updates now cover only **y=12..63**, split into two safe ggoled patches: `128×48 @ y=12` and `128×8 @ y=56`.
- Avoids `-C` (full-screen clear) and avoids hardware packet padding beyond 64 rows.
- Editor and photo preview use the same 12-row protection boundary.
- Added pixel-level test coverage to ensure no header pixels are transmitted.
- Verified both partial write operations and normal-screen restore succeed; the owner's physical visual confirmation is still needed.
- Keeps v0.9.4 source untouched as a rollback.

## 0.9.4 — Experimental physical OLED writing

- Added confirmation-gated physical dock 10-second image test with automatic return.
- Downloaded a separate verified ggoled v0.3.0 CLI for private use.
- Attempted an isolated MSIX package-identity test for CrossDevice: blocked by 0x80073CFF (Developer Mode disabled), so no package was installed.

## 0.9.3 — Phone Link status provider

- Read phone battery from an accessible Phone Link status Group when Phone Link is open/minimized.

## 0.9.2 — SteelSeries GG battery bridge

- Added live GG Engine battery readings for Aerox 3 Gen 2, Nova Pro headset and spare battery.

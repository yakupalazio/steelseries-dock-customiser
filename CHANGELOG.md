# Changelog

## 0.9.3 — Phone Link battery reader

- Read phone battery percent and charging state directly from Phone Link's accessible status Group via Windows UI Automation.
- Verified on a real linked Galaxy S24+: **100% and charging**, including while Phone Link was minimized.
- Added safe Turkish/English text parsing, bounds checks, ambiguous-value rejection and parser tests.
- Integrated the phone reading with the existing 45-second polling and Dock Preview.
- Do not open or focus Phone Link, read private messages, interact with its controls, or fall back to stale Bluetooth percentages.
- Added CLI `--battery-scan-file` for accurate offline diagnostics.
- Kept all previous tools and the physical dock preview-only.

## 0.9.2 — SteelSeries GG live battery integration

- Discovered the local GG Engine read-only battery status API for connected Aerox 3 Gen 2, Nova Pro headset and spare dock battery.
- Added automatic GG device discovery, connected-device checks and tests.

## 0.9.1 — Stale Bluetooth suppression

- Rejected old Windows Bluetooth battery records as current percentages.

## 0.9.0 — Battery selection, icon editor

- Added the device battery selection dialog, icon pixel editor and read-only Windows battery sources.

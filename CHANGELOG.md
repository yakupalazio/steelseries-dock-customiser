# Changelog

## 0.9.2 — Native GG live battery bridge
- Discovered actual local SteelSeries Engine loadout status API from the already-installed GG frontend (read-only).
- Added loopback-only HTTPS discovery, active loadout detection, and connected-device filtering.
- Integrated live Aerox 3 Wireless Gen 2, Nova Pro Wireless headset and spare-dock-battery status with automatic refresh.
- Added strict battery-status and GG URL parsing tests; reject unknown/unavailable status rather than guessing.
- Retained stale Bluetooth cache as diagnostics only, not a live value; phone remains unresolved.
- No HID output or firmware commands, no changes to GG settings, and no global TLS security changes.

## 0.9.1 — Cached Bluetooth suppression
- Corrected wrong mouse and phone percentages displayed from stale Windows Bluetooth cache.
- Unknown sources display --% until a verified live provider is available.

## 0.9.0 — Battery device selector, icon editor, initial adapters
- Added laptop battery, diagnostic PnP battery discovery, optional experimental DualSense input reader.
- Added pixel-by-pixel custom icon editor.

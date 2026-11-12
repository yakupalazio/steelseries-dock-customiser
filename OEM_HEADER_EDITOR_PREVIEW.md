# Steelseries Dock Customiser — OEM Header in Editor (Universal Preview 2)

Date: 13 November 2026
Status: private, local beta preview. Not a GitHub public release.

## Design behavior

- When **Orijinal üst 10 pikseli koru** (Protect original top ten
  pixels) is enabled, the 128x64 Pixel Editor now displays the same
  headphone, central mode and charging/battery icons visible at the
  top of the real dock photo.
- Reference pixels are extracted and thresholded to native 1-bit
  128x10 format from the original, calibrated dock photograph. Pixel
  scaling is nearest-neighbor to match the main OLED editor.
- The guide is **display-only**, never added to the saved profile,
  exported OLED bitmap, hardware report, or Live stream.
- Disabling **Orijinal üst 10 pikseli koru** removes the guide
  immediately and exposes the true editable top 10 rows. Re-enabling
  protection restores the guide without changing any user pixels.
- The reference is a **static photograph** of the original firmware
  symbols, not a live read of headphone battery percentages or
  firmware state. The dock's physical OLED top 10 rows remain
  firmware-owned under protection.
- Reference cache is discarded if dock reference photo or its screen
  calibration changes. Failure to load the photo gracefully omits
  the visual guide rather than modifying the saved design.

## Regression acceptance

- .NET 10 Windows Release build: no warnings/errors.
- Full OLED Studio self-tests including extracted top-10 icon groups.
- Offscreen real WinForms editor capture in protected and editable
  modes: 6755 pixels changed within the top 10 only; **zero pixels
  differed below that row**.
- Previous custom 3-slot battery design, modes, icons, saved layouts,
  live hardware protocol and Beta 4 profile remain unchanged.
- No real HID/OLED write is performed in these tests.

Copyright (c) 2026 Yakup Can Palaz. Respect third-party photo and
library rights. The reference image is supplied only with the
authorized local/private preview build.

# Beta 5 RC2.1 — DualSense Bluetooth Battery Hotfix

Copyright © 2026 Yakup Can Palaz. All rights reserved.

## Verified root cause

A Sony DualSense Wireless Controller paired over classic Bluetooth was visible
in Windows Plug and Play at HID device ID containing
`VID&0002054C_PID&0CE6` but our previous HID reader only accepted USB
device paths formatted `VID_054C&PID_0CE6`.

## Changes

- Accept the documented Sony DualSense HID identifiers in both Windows USB
  and Bluetooth device-interface paths. The reader opens only known Sony
  0x054C / DualSense model 0x0CE6 or 0x0DF2 devices.
- Continue to read existing HID *input* reports only. No controller vibration,
  pairing, writes, output or feature commands are sent.
- Store an informative diagnostic status when Windows recognizes the paired
  controller but Steam/other apps block the battery input report.
- No unverified/battery-less device is allowed into the OLED battery widgets.
- Keep the owner's existing Mouse → Phone → Controller priority and all
  existing widget positions intact.
- The Bluetooth report encodes approximate 10%-step battery bands, not an
  exact 1%-resolution value. Do not advertise 70% as a lab-calibrated reading.

## Actual hardware proof

On November 13, 2026, the authorized Windows computer presented:
- `DualSense Wireless Controller` in present Bluetooth devices.
- Sony Bluetooth HID controller `VID&0002054C_PID&0CE6`.
- Old RC2 read-only battery scan: `DualSense --%`.
- Updated RC2.1 code read-only battery scan: `DualSense 70%`
  from an existing Bluetooth HID input report.
- Release source build: 0 warnings and 0 errors.
- Self-tests: PASS (including alternate Windows path recognition).
- The user's running RC2 executable has NOT been terminated, overwritten,
  or installed over; the hotfix is packaged separately.

## Operator note

Close the previous RC2 application **fully** from its system tray before
launching RC2.1. Running both at once can compete for the Nova Pro OLED.
Use the separate local `%LOCALAPPDATA%\SteelseriesDockCustomiser-Beta5-Development`
settings directory, preserving every personal icon and widget location.

The first screen should show the existing user's first three *verified*
priority devices, including Controller in Battery Slot 3 when a current HID
report is available. If the controller is connected but no HID report is
available, the battery settings show its diagnostics, not an invented %.

No GitHub release or distribution rights change is made by this hotfix.

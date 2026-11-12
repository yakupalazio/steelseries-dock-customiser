# Device name privacy fix — Universal Preview 2

## Behavior
- Battery settings, priority ordering, tray warnings, CLI battery scan and
  threshold dialogs no longer use a developer's hardcoded product names.
- A clean installation without discovered hardware shows generic translated
  roles: Phone, Mouse, Controller, Laptop, Headset and Spare Battery.
- Connected Bluetooth Classic/LE devices are queried read-only on the
  current user's own Windows installation. If there is one unambiguous phone,
  its local Windows-friendly name is used. The app never pairs or disconnects
  Bluetooth devices to obtain labels.
- Known provider battery readings (GG, BLE GATT, standard HID) display the
  actual product name only after a confirmed live battery read on this PC.
- The connected-peripheral inventory contains only names from this PC's
  current devices; one phone reading is never assigned to two phones when
  multiple phones are attached.
- User preferences, Bluetooth identities and detected device names are never
  embedded into the distributed EXE, defaults or ZIP. Each user's preferences
  remain stored in their own per-user LocalAppData directory.
- The approved default OLED layout and custom pixel-art icons are preserved:
  they contain only layout coordinates, widget roles and custom 1-bit glyphs,
  not the original developer's connected device labels or Bluetooth IDs.

## Acceptance
- Clean-settings offscreen UI screenshot reviewed: only generic roles.
- Automated tests verify alternate phone names, ambiguously connected phones,
  and no developer-model-name fallback.
- Read-only device scan on a Windows PC confirms names resolve from that
  machine's connected phone and actual live GG / HID devices.
- No changes to real OLED/dock output, profile files or pairing settings.

This fix is a private pre-release build and must not be represented as
universal support for reading every vendor's device battery.

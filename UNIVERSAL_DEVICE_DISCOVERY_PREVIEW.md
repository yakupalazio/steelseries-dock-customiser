# Steelseries Dock Customiser — Universal Device Discovery Preview 1

Date: 13 November 2026
Branch: feature/universal-peripheral-discovery
Status: Local, experimental, private Windows x64 build. Not an official public release.

## What was implemented

1. A **Connected devices / Bağlı cihazlar** button in Settings -> Battery & priority.
   Displays physical, currently connected Bluetooth Classic, Bluetooth LE,
   and USB/dock PnP devices, even if there is no trustworthy battery API.
2. The inventory automatically updates every **20 seconds while the dialog
   is open**; manual Refresh and live name/brand/transport filtering work.
   Bluetooth devices are selected by Windows' Connected state; USB by
   currently-present PnP nodes. Device/container identity is used to
   consolidate multiple logical interfaces.
3. The inventory shows model, connection, current verified battery percentage
   if one exists, the source and diagnostics. Generic "USB Composite Device"
   labels are replaced with descriptive HID product names when matching USB
   vendor and product identifiers are available.
4. Built-in **brand-independent battery readers**:
   - Already-present BLE GATT Battery Service 0x180F / Level 0x2A19 (live
     uncached read, paired devices only).
   - New standard HID Usage Page 0x06, Usage 0x20 Battery Strength:
     descriptor-validated Input or **read-only GetFeature** query with
     logical range normalization. Only devices that advertise this
     standard battery field are queried, no proprietary commands are sent.
   - Existing verified SteelSeries GG, Phone Link and DualSense HID providers.
5. An internal `IPeripheralBatteryProvider` adapter contract and registry;
   no external app, BQC install, background service or user-written code.
   This allows future owner-developed, reviewed vendor-specific readers to
   be added inside the same executable.
6. Newly verified generic HID battery records (`Hid:` keys) are eligible for
   the existing three OLED priority slots, icons, freshness expiry and
   low-battery notifications, like existing GATT `Ble:` records.
   USB/Bluetooth devices without verified battery **never show invented
   percentages** or occupy the OLED slot.
7. No change to Beta 4 personal layouts, GG/dial handback timing, existing
   Beta 5 UI layout, per-device widget priority, or proprietary license.

## What this does NOT promise

Windows may identify USB and Bluetooth peripherals but cannot retrieve a
battery percentage they do not report over an available public protocol.
Many Logitech Lightspeed/Unifying/Bolt devices use proprietary HID++, and
some ASUS, ATK and other vendors use private receiver commands or dedicated
apps. Such devices appear in the connected-device inventory but may show
`--%` until a verified, user-safe adapter is added.

A USB receiver often represents multiple paired wireless peripherals;
listing each peer individually requires the receiver's own enumeration
protocol. The generic Windows inventory cannot infer all receiver peers
from the adapter's USB ID.

Generic HID Battery Strength can be input or feature-report based, but
devices using *only* vendor-defined HID fields are intentionally not guessed.
Never fabricate battery percentages from stale Windows SetupAPI caches.
No manufacturer support is claimed without actual physical battery tests.

## Acceptance

- .NET 10 Windows Release build and self-tests, including synthetic
  Logitech/ASUS/ATK-style inventory, duplicate USB container handling,
  standard HID descriptor Input/Feature decoding and priority selection.
- Real Windows scan discovered 8 currently connected peripherals, including
  a Bluetooth DualSense, Arctis Nova Pro USB dock, SteelSeries Aerox,
  Galaxy S24+ and a USB audio interface; verified battery availability
  depends on GG/Phone Link freshness at the moment of each scan.
- Screenshot review of the connected-devices form and existing battery
  settings panel: no blocking overlap or hidden new navigation button.
- Do not overwrite the user's currently running RC2.1 executable. New
  binaries are published to an independent private test directory.

## Follow-up vendor compatibility program

Research and test plug-in-free, internal read-only readers per protocol
family, not one per end-user device. Priorities include Logitech HID++ 2.0
(Bolt/Unifying/Lightspeed), validated ASUS / ATK receiver families,
standard Bluetooth Classic and any OEM-exposed Windows battery properties
with demonstrated freshness. Require manufacturer/product IDs,
proof of real battery readings on physical hardware and reconnect tests
before a model is called supported.

Copyright (c) 2026 Yakup Can Palaz. Third-party components retain their
respective license notices.

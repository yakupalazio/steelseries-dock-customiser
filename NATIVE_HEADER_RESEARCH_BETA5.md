# Native top-ten-pixel battery header — Beta 5 research

This is a research checkpoint, not permission to alter dock firmware or HID
settings. The prior successful Beta 4 dial handback is intentionally unchanged.

## Known observations

- On the user's real Nova Pro Wireless base station, the custom layout
  returned in approximately one second after dial rotation without flicker.
- A mode-only Volume/ChatMix button press can leave the OEM screen visible
  for approximately five seconds. The current read-only dial report listener
  does not guarantee a separate notification on that button press.
- When a headset battery pack was swapped, the firmware-managed top ten
  rows did not visibly refresh until the user's next physical interaction.
- The existing renderer protects rows 0–9 by default. Sending a custom
  frame to rows 10–63 alone cannot require firmware to redraw rows 0–9.

## Public protocol leads, not yet verified on this particular dock

- OpenChatMix protocol documentation describes a Nova Pro Wireless status
  request `06 B0` returning headset/spare battery bands 0–8 and a passive
  `07 B7 hh ss cs` battery/status event.
  Reference: https://github.com/Swiftlyx/OpenChatMix
- The same documentation describes `06 93` draw and `06 95` relinquish
  controls for the OLED. Their event/command behavior under SteelSeries GG
  and this exact hardware revision needs independent verification.
- An 8-step band value is **not** a verified 0–100% battery percentage and
  must not be represented as one. No active vendor HID status query or mode
  control command is added to this Beta 5 checkpoint.
- The current `OnBatteryPackChanged()` asks our renderer to refresh after
  a large verified GG change; that is not proof of an OEM header update.
  It does not request `06 B0` or change the physical knob mode.

## Safe acceptance approach

1. Capture already-observed, read-only GG and unsolicited battery events
   while the user explicitly tests a battery replacement.
2. Establish which reports reflect headset and spare battery independently
   and verify freshness and connection status on the target hardware.
3. Investigate a strictly bounded, user-authorized status query only after
   a source/target/firmware compatibility review. No rapid redraw loops.
4. If OEM refresh cannot be requested safely, offer a separately enabled
   full-screen **custom header**, using verified signals only, while keeping
   the default protected-top-ten layout intact.
5. Measure one-second dial return and five-second mode-only delay again on
   hardware before any Beta 5 release.

This is intentionally OPEN: the OEM firmware-owned header has not been
demonstrated to refresh automatically from this application.

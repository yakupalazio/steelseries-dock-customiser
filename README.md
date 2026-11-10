# Steelseries Dock Customiser — v0.9.3

A **Windows / .NET 10** 128×64 1-bit OLED layout editor with a live *photographic simulation* of the SteelSeries Arctis Nova Pro Wireless dock.

**No commands are sent to the physical dock.** The app does not change firmware or SteelSeries settings.

## Live device batteries

| Device | Provider | Notes |
|---|---|---|
| Laptop | Windows PowerStatus | Live PC battery percentage |
| Aerox 3 Gen 2 | Local SteelSeries GG Engine | Read-only local HTTPS GG status; requires running GG |
| Nova Pro Wireless headset | Local SteelSeries GG Engine | Headset and spare-battery status available in GG |
| Galaxy S24+ / current selected phone | **Microsoft Phone Link UI Automation** | New in v0.9.3; runs when Phone Link window is open or minimized |
| DualSense | Optional read-only HID input probe | Experimental and disabled by default; no output commands |

### Phone Link method (new in v0.9.3)

1. Finds the already-running `PhoneExperienceHost` process.
2. Reads only accessibility **Group names** belonging to Phone Link's own window via the Windows UI Automation API.
3. Looks for the phone-battery status phrase in Turkish or English and extracts an actual value from 0–100%.
4. Reports the charging state when the accessibility text provides it.

**A real test on this PC returned Phone: 100%, charging.** This was read directly from the Phone Link UI even when its window was minimized. The value is **not hardcoded** and differs from the stale Windows Bluetooth record. It will change when Phone Link updates.

This is an *unofficial, best-effort UI reader*, not an API endorsed by Microsoft. Phone Link may show a last-synced percentage when the phone is offline, so the application's source description explicitly warns that freshness is not independently validated. If Phone Link is closed, the phone panel is absent, multiple phone battery values are visible, or the accessibility label changes, the result is **--%** (unknown). We never fall back to a cached Bluetooth value.

**Privacy:** No messages, photos, contacts, call history, notifications or files are read or stored by this provider. It never clicks, focuses, maximizes or modifies Phone Link. It only reads battery-related accessibility **Group names** from the already-open program. No private Phone Link databases are inspected by the shipping app.

### Why not Bluetooth?

On this PC Windows Bluetooth PnP previously returned stale values (e.g., 40% for the phone when Phone Link showed 84%). It is not a reliable current source for the Galaxy S24+. A standard GATT Battery Service was not identified for the phone. For now, Phone Link is the preferred provider.

## UI features

- Edit individual 1-bit OLED pixels and add/move widgets.
- Undo/redo, save/load layouts, native 128×64 PNG export.
- Draw and save custom mouse, phone, DualSense, headset, laptop, Spotify and media icons with a sprite editor.
- Choose three battery devices to display; the app refreshes approximately every 45 seconds.
- Dock Preview places the generated OLED image on a real base-station photo, including custom photo import and perspective calibration.
- Custom/Default changes only the simulated photo, **not** the physical device.

## Build and test

```powershell
dotnet build -c Release
dotnet run -c Release
.\bin\Release\net10.0-windows\SteelseriesDockCustomiser.exe --self-test
.\bin\Release\net10.0-windows\SteelseriesDockCustomiser.exe --battery-scan
```

On a machine where Windows does not attach stdout to the WinExe process, use:

```powershell
.\bin\Release\net10.0-windows\SteelseriesDockCustomiser.exe --battery-scan-file .\diagnostic-batteries.txt
```

The diagnostics file is local and is not uploaded.

## Security and licensing

- No direct USB/HID output, GameSense events, firmware changes, browser access, device pairing changes or network cloud access.
- The SteelSeries GG bridge sends loopback-only HTTPS GET requests to GG's **undocumented internal status API**; it may break after updates.
- The optional DualSense HID path reads only input reports if explicitly enabled.
- An official SteelSeries product photo is present only on the development machine, excluded from Git. Public users must provide a permitted image.
- This project is an independent personal prototype and is **not affiliated with SteelSeries or Microsoft**.

See `THIRD_PARTY_NOTICES.md` for external references.

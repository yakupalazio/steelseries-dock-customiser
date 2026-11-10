# Steelseries Dock Customiser v0.9.2 — Live SteelSeries GG Batteries

A Windows .NET 10 **preview-only** customizer for the 128×64 monochrome OLED design.

## Verified on this Windows PC

The application's **read-only, local GG Engine provider** now dynamically discovers SteelSeries GG's Engine port, reads the active loadout and checks connected devices. Battery readings are obtained by HTTP GET from the `batteryLevels` field in GG's own device status response.

On the test PC, GG reported:
- **Aerox 3 Wireless Gen 2** — **96%**, charging, via the current GG Engine loadout (USB PID `1038:1892`).
- **Arctis Nova Pro Wireless** — headset **100%**, spare battery **87%** and charging.
- **Laptop** — **100%**, plugged in, through Windows PowerStatus.
- **DualSense** — disconnected, no reading.
- **Galaxy S24+** — no supported live Phone Link source established yet; displays `--%`, not a stale Bluetooth number.

**These numbers are test observations, not programmed default percentages.** They may change at the next poll.

## How the GG connection works

1. `GET https://127.0.0.1:6327/subApps` to discover the encrypted Engine port.
2. `GET https://127.0.0.1:6327/v1/loadouts/active` to discover the current loadout ID.
3. `GET https://127.0.0.1:<engine_port>/devices` to verify the two supported devices are connected.
4. `GET https://127.0.0.1:<engine_port>/v2/device/config/loadout?device=<name>&loadoutId=<id>` to inspect `loadoutConfiguration.statuses.batteryLevels`.

Only two known GG device identifiers are allowed: `aerox_3_wireless_gen2` and `arctis_nova_pro_wireless_tx`. Local TLS certificates are trusted **only** by the dedicated loopback HTTP client. Redirects and arbitrary network hosts are refused. This is an **undocumented GG internal API**, subject to change with future GG releases.

No device or firmware USB/HID command is sent. No external network request is made by the GG provider.

## Features retained

- Choose 3 of 5 available devices, including per-device source labels in **Cihaz pilleri**.
- Auto-refresh once every 45 seconds, plus manual refresh.
- Native monochrome 128×64 OLED canvas and photographic Dock Preview.
- Custom pixels, per-widget positioning, undo/redo and save/load.
- **İkon düzenleyici** with 1-bit sprites including improved DualSense.
- Experimental **read-only** DualSense Bluetooth HID option is off by default.
- Only the laptop and verified GG status values are displayed; old Windows Bluetooth cache is marked *not live*.

## Phone Link limitations

Microsoft Phone Link displays the phone's battery, but no supported third-party battery-read endpoint was verified on this PC. Local pairing metadata does **not** expose a current battery value. We intentionally do not parse private messages, scrape phone notifications, or use an unverified database to obtain one.

The next step for the Galaxy S24+ is a small **opt-in Android companion** reporting the phone battery through a mutually authenticated local-only channel (no cloud service), or a documented Microsoft API if one becomes available. It would require explicit installation/permission on the phone. Until then the percentage is unknown rather than fabricated.

## Run

Open `ONIZLEMEYI_AC.cmd`, or run:

```powershell
dotnet build -c Release
dotnet run -c Release
```

Read current GG and Windows battery data without launching the interface:

```powershell
.\bin\Release\net10.0-windows\SteelseriesDockCustomiser.exe --battery-scan
```

Preview a real reading at exactly 128×64:

```powershell
.\bin\Release\net10.0-windows\SteelseriesDockCustomiser.exe --battery-preview .\battery-preview.png
```

Run tests:

```powershell
.\bin\Release\net10.0-windows\SteelseriesDockCustomiser.exe --self-test
```

Tests include the exact GG JSON status shapes, unknown-battery rejection, loopback URL validation, LCD frame output, and photo preview regression tests.

## Limitations and privacy

The app never pushes the design to the physical SteelSeries base station. Its Custom/Default switch affects only a photograph. It does not inspect or modify SteelSeries device settings, messages, firmware, or GameSense configuration.

The official SteelSeries product photo used for private development is **not included in the Git repository** and may not be redistributed without the appropriate permission. Earlier app versions have been retained separately for safe rollback.

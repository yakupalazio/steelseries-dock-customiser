# Steelseries Dock Customiser v0.9.5 — Preserved Factory Battery Bar

Independent Windows/.NET 10 128×64 monochrome OLED pixel editor with a photographic dock preview, customizable 1-bit icons, and real device-battery sources.

## v0.9.5 hardware test — preserve top 12 rows

The user confirmed with photos of the Nova Pro Wireless base station and SteelSeries Now Playing app that the **original headset, spare battery and connection indicators occupy approximately the first 12 rows**. Reserving 16 rows wasted usable space.

The **editor now reserves Y=0..11 (12 rows)**. Only **Y=12..63 (52 rows)** are editable and sent to the actual physical OLED. The upper factory bar is *not copied from our simulated source frame*.

The open-source [ggoled](https://github.com/JerwuQu/ggoled) command pads HID report height to a multiple of 8 pixels. One 128×52 patch starting at y=12 could therefore pad outside the physical 64-row screen. To avoid this, the client uses two overlapping patches:

| Patch | OLED location | Image size | Physical rows written |
|---|---|---|---|
| A | x=0, y=12 | 128×48 | 12..59 |
| B | x=0, y=56 | 128×8 | 56..63 |

The overlap on rows 56..59 is intentional. Neither patch contains or transmits a pixel in rows 0..11; both have heights divisible by 8, with no padding beyond row 63. **The application never passes `-C` (clear screen)**. After ten seconds, it issues `return` to the original SteelSeries OLED interface.

The two-patch write and return were each checked locally: all three ggoled commands exited **0**. Unit tests verify the exact pixel coverage, reserved rows, and output dimensions. **Physical visual preservation still requires the owner to look at the device and confirm it.**

## Using the optional physical test

Open Dock Preview and select Custom. Click **Gerçek dock'a gönder (10 sn)** and approve the warning. The custom OLED image is displayed for 10 seconds and then returned to the SteelSeries UI. **Dock ekranını geri yükle** can end a test earlier.

The third-party **ggoled v0.3.0** executable must have been downloaded separately from its official release. The app verifies its pinned SHA-256 before sending a test. No device settings, brightness, firmware or pairing changes occur. This version is a limited *one-shot* test, not an always-running OLED app.

The dock photograph is a simulation and cannot verify the physical display. Unmodified factory indicators may still be replaced by other apps or vendor firmware events while this third-party interface is in use.

## Other functions

- Native 1-bit OLED renderer, 5×7 pixel text, icon drawing/persistence, custom widgets, undo/redo, save/load, PNG export and perspective photo preview.
- Read-only SteelSeries GG battery data for Aerox 3 Gen 2 and Nova Pro Wireless (including spare battery).
- Windows laptop battery.
- Phone Link battery through accessibility UI only when the Phone Link window is open or minimized. Closed-window background battery access not yet established.
- Optional experimental input-only DualSense HID battery reader, disabled by default.
- Three user-selectable battery widgets.

## Run

Open `ONIZLEMEYI_AC.cmd`, or:

```powershell
dotnet build -c Release
dotnet run -c Release
.\bin\Release\net10.0-windows\SteelseriesDockCustomiser.exe --self-test
```

The earlier v0.9.4 (16-row test) and v0.9.3 sources remain available. The v0.9.5 local development build is **not** a GitHub release yet.

## Privacy and licensing

The hardware interface is intentionally limited to ggoled `img` and `return` commands. GG status uses loopback-only read-only HTTP. Phone Link reads accessible battery status only; it does not read messages, contacts, photos or notifications. The official SteelSeries product photo and any third-party executable are **not redistributed** in the Git project. This tool is not affiliated with SteelSeries or Microsoft.

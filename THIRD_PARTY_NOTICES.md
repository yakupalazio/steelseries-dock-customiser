# Third-party notices

- **HidSharp 2.6.4**, <https://github.com/IntergatedCircuits/HidSharp>: Apache License 2.0 NuGet dependency used only for the optional, user-confirmed experimental high-frame-rate OLED feature-report transport. License and copyright notice are provided in `THIRD_PARTY_LICENSES/HidSharp-LICENSE.txt` and `THIRD_PARTY_LICENSES/HidSharp-NOTICE.txt`. No firmware flashing or brightness changes are implemented.
- **ggoled v0.3.0**, <https://github.com/JerwuQu/ggoled>: external, independently downloaded command-line utility for the experimental OLED physical display test. It is **not bundled**, copied into Git, or statically linked. A known SHA-256 protects against accidental substitution of the tested release binary.
- **SteelSeries GameSense SDK**, <https://github.com/SteelSeries/gamesense-sdk>: research reference for the documented GameSense API, not used by the physical dock test.
- **Microsoft Win32 / Phone Link / CrossDevice**: Windows APIs and system metadata used for phone and laptop battery status, with the documented limitations of unofficial internal integration.
- The SteelSeries product photograph used only on the development machine is excluded from public distribution.

This application is not endorsed by these companies. Always check licenses before distributing third-party files.

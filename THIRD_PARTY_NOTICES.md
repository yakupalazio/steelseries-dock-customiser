# Technical references and third-party notices

This v0.9 renderer, sprite shapes, icon editor, and Windows battery discovery are independently implemented. No third-party software library was copied into this project.

- [Microsoft System.Devices.BatteryLife](https://learn.microsoft.com/en-us/windows/win32/properties/props-system-devices-batterylife) and Windows PowerStatus
- [Microsoft Windows device and Bluetooth documentation](https://learn.microsoft.com/en-us/windows/uwp/devices-sensors/get-battery-info)
- [SteelSeries GameSense SDK](https://github.com/SteelSeries/gamesense-sdk) for future outbound events; not called in v0.9.
- [BatteryTray](https://github.com/qu1ncky/BatteryTray), [BT Device Battery Info](https://github.com/borferkic/BT-Device-Battery-Info) and Windows PnP battery property documentation as research references.
- [ggoled](https://github.com/JerwuQu/ggoled) and [OmniLED](https://github.com/llMBQll/OmniLED) for OLED layout research; their code and font assets are not included.

The photograph of the Arctis Nova Pro Wireless base station used in private tests was downloaded from SteelSeries' official product pages. It is **not included in this repository** and must not be redistributed without appropriate permission.

Optional DualSense experimental input-report reader is read-only. No device commands, no firmware changes and no SteelSeries GG data scraping occur in this version.

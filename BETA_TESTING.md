# SteelSeries Dock Customiser — Beta Test Guide / Beta Test Rehberi

**Version:** 1.0.0-beta.1 (Windows x64) · **Publisher:** Yakup Can Palaz · **Independent, unofficial software.**

## Kurulum / Install

1. Download the official `SteelseriesDockCustomiser-v1.0.0-beta.1-win-x64.zip` from the GitHub prerelease.
2. Extract the ZIP to a normal writable folder and run `SteelseriesDockCustomiser.exe`. This is a self-contained Windows x64 app; no .NET installation is expected.
3. Connect your SteelSeries Arctis Nova Pro Wireless dock. Install SteelSeries GG normally if required by your setup.
4. Use **Device setup** to inspect connected USB/Bluetooth devices and to add only verified live battery sources. A recognized brand does not mean its battery protocol is supported.
5. Existing local profiles are kept. Windows startup is off by default. Phone Link launching is opt-in.

This is an unsigned beta binary. Verify its SHA-256 against the GitHub release before opening. No third-party SDK, HID driver installer or firmware flasher is bundled.

## Geri bildirimler nereye gidiyor? / Where does feedback go?

Reports are **GitHub Issues** at:
https://github.com/yakupalazio/steelseries-dock-customiser/issues

- **Settings → Bug report / Hata bildir**: choose category, enter a clear title and reproduction steps, optionally select a PNG/JPEG screenshot.
- Click **Open GitHub report form**. Dock Customiser saves a local, unsent draft and opens the corresponding public GitHub Issue form with summary, category, app version and details prefilled.
- If a screenshot was selected, click **Copy clean image**, then paste it into the GitHub Issue's **Attach screenshot** field. The app removes original filename and EXIF and exports a clean PNG. You may also choose an image/log file directly in GitHub's upload field.
- **Review everything carefully**, log into a GitHub account, and click **Submit new issue**. **Only this final GitHub action publishes the report and its files.** Merely clicking the button in Dock Customiser does not transmit anything.
- Extremely long reports: press **Copy full description** and paste the complete text on GitHub before submitting.
- GitHub Issues and attached screenshots are **public**. Don't include personal information, chat screenshots, passwords, tokens, computer paths, device serial numbers or Bluetooth MAC addresses. If you're unsure, redact first.
- GitHub will notify the developer for new issues. You can follow your own Issue for fixes and follow-up questions.

For device compatibility, choose **Battery / Device**. Include the mouse/keyboard/headset model *if you know it*, Bluetooth vs 2.4GHz USB connection, what the official manufacturer app reports, what Dock Customiser reports, and reproduction steps. The in-app **Connected devices → Copy report** provides public VID/PID but review device names before sharing.

Direct links:
- Bug report: https://github.com/yakupalazio/steelseries-dock-customiser/issues/new?template=beta_bug.yml
- Device report: https://github.com/yakupalazio/steelseries-dock-customiser/issues/new?template=beta_device.yml
- All reports: https://github.com/yakupalazio/steelseries-dock-customiser/issues

## What to test

- Startup, reconnect, Windows tray behavior, media/game/idle transitions and low OLED CPU usage
- Profile creation, overwrite, preview and custom icon persistence
- Spotify/Windows synced lyrics, offset and 1–4 line transition styles
- Nova Pro headset and spare battery detection (when awake)
- ATK CompX dongles, WLMOUSE, Bluetooth LE and classic Bluetooth battery compatibility
- Battery readings vs the manufacturer's application, including charging state
- Turkish and English UI, DPI/scale and onboarding target positions
- Any crashes, missing controls or corrupted pixels

## Important beta limitations

- A device in the 364-model wireless recognition catalog is **not automatically** a verified battery-reading device. Unsupported vendors display `--%` rather than a guessed percentage.
- Phone Link and official manufacturer SDKs are optional and are not launched or installed without permission.
- The physical dock's native top header depends on firmware, so its exact image can differ from the editor's simulator.
- This beta build is not code-signed or officially supported by SteelSeries.
- The private prototype's user-provided dock photograph is intentionally **not redistributed** in the public ZIP. A built-in vector fallback is used where necessary.
- No silent telemetry or automatic bug upload; all report content requires your explicit GitHub submission.

## License

Free to use but **closed source / proprietary freeware**. See `LICENSE`, `TERMS.md`, and `THIRD_PARTY_NOTICES.md`. SteelSeries and other mentioned brands own their respective trademarks.

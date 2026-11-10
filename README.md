# Steelseries Dock Customiser v0.9.9 — Game Mini Player

A Windows .NET 10 pixel editor and experimental physical OLED display companion for the SteelSeries Arctis Nova Pro Wireless dock, preserving all v0.9.8 modes and batteries.

## New: Optional Mini Player in the Game screen

When you are gaming and listening to music at the same time, the **Game** screen remains active (priority **Game > Media > Idle**). You can now add a compact **Mini Player (Oyun)** widget to the **Oyun açıkken** layout:

1. Go to **Tasarım & Piksel Editörü → Oyun açıkken**.
2. Choose **Mini Player (Oyun)** from the **Widget** selector and press **Ekle**.
3. Adjust **X**, **Y**, **Boyut**, **Kalınlık (1–3 px)** and **Mini Player genişliği (35–128 px)**.
4. Click **Kaydet** to save all three mode layouts. The mini player remains optional and can be removed with **Sil**.

**What the hardware displays:**
- If a Windows media session is actively playing: a compact Spotify (or generic media) source icon and **the real song title**, scrolling smoothly by **3 OLED pixels per second** when longer than the selected width.
- If no media is playing, or playback is paused/stopped: **a fully black reserved rectangle**. No sample title, default text, timestamp or idle graphics is drawn there.
- The entire mini player, including custom icons, is confined to the selected bounding rectangle. It never writes over the game title outside that rectangle. If placed on top of another widget, the player intentionally covers that overlap, keeping its own region legible.
- Mini Player is **only available in Game mode**, never Media or Idle. It isn't included in the default Game layout. All existing game information and other optional widgets are unchanged.

It uses **the existing once-per-second Windows Global System Media Transport Controls polling**, without additional background services or extra Spotify API tokens. The live OLED retains skip-if-unchanged updates and your 10-pixel factory-battery-header setting.

### Design persistence and rollback

v0.9.9 stores design changes in:
`%LOCALAPPDATA%\SteelseriesDockCustomiser\studio-v099.json`

If this file isn't present yet, v0.9.9 **loads the older studio-v098.json design as a migration source**. Saving creates the new file and leaves v0.9.8's saved design untouched. The Mini Player's width, position, text scale and thickness persist in the new JSON.

## Existing features maintained

- Independent **Media / Game / Idle** OLED layouts.
- Automatic Game > Music > Clock priority. Active games are detected via SteelSeries GG's installed-game catalog and running Windows processes; they remain active across Alt+Tab.
- Media screen source icon/title/artist/progress, optional small clock and independently editable date/time.
- Battery priority menu including mouse, phone, laptop, DualSense, Nova Pro headset and dock spare battery.
- Phone Link status via its accessibility battery Group; optional minimized Phone Link launch instead of Android debugging.
- Three dynamic battery slots, per-pixel editing, saved layouts, sprite/icon editor, photographic dock preview.
- Protection toggle: ON preserves original rows 0–9; OFF intentionally allows full 128×64 custom display, with confirmation before physical writes.

## Running and verification

In the project's folder, open `ONIZLEMEYI_AC.cmd`, or:

```powershell
dotnet build -c Release
.\bin\Release\net10.0-windows10.0.19041.0\SteelseriesDockCustomiser.exe --self-test
```

To generate **sample images** without writing to the physical dock:

```powershell
.\bin\Release\net10.0-windows10.0.19041.0\SteelseriesDockCustomiser.exe --mini-player-preview-file .\mini-playing.png
```

This produces `mini-playing.png` and `mini-playing-paused.png` at 128×64.

**Physical dock output remains off by default.** Dock Preview's existing manually approved live mode is still required for an actual OLED transfer. The user-provided official dock photograph and separately installed ggoled executable remain private and excluded from Git.

Not affiliated with SteelSeries, Microsoft or Spotify.

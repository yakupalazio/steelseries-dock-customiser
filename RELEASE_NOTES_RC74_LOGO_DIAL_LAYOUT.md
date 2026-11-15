# RC7.4 — Game Logo, Battery Toolbar and Dock Dial Retention

**Private Windows x64 test build · 1.0.0-beta.2-rc7.4.** The public GitHub v1.0.0-beta.1 release and earlier RC7.3 ZIP remain unchanged.

## Critical fixes

### 1. Lies of P / custom PNG and game EXE icon show a solid white square

- **Root cause**: the previous 32×32 converter determined "transparent background" by counting transparent pixels AFTER fitting a non-square logo into a square. The artificial transparent margins were mistaken for transparency of the original image, causing every original opaque background pixel to become a white OLED pixel.
- **Fix**: classify the **original bitmap** before letterboxing using its corner colors/alpha, isolate foreground strokes, crop actual content, fit while maintaining aspect ratio and downsample with area coverage to preserve thin letter strokes. The same `ConvertImage` is called for user-imported PNG/JPG/ICO/BMP and registered game EXE icons.
- Keeps original geometry of already-designed 32×32 small icons rather than arbitrarily recentering them.
- Defensively ignores previously saved 32×32 all-white block overrides (and falls back to the EXE icon); future imports reject an almost-full solid block. **Existing corrupted logo overrides contain only the old 1-bit output, not the original photograph, so the user must re-import their intended logo image once after upgrading.**
- Two user-supplied **Lies of P** samples (black on light and white on dark) were brought into a private QA directory, converted to scaled 1-bit test input and rendered. Both now visibly produce the actual logo shape, not an all-white square. Fixture artwork is **not bundled or committed**.
- Synthetic black/white/transparent 520×384 shapes, actual Windows EXE icon extraction where possible, exact 128×64 OLED buffer parity and foreground-density regressions added. Source art is not transmitted.

### 2. Volume and ChatMix become zero after 10 minutes

- **Root cause**: `OledRenderer.DrawDialWidget` discarded a real `DialSnapshot` when the last physical wheel event was more than 10 minutes old, by setting `fresh=false`. Nova Pro Wireless emits dial input events only when a user turns the wheel; inactivity is not a new reading of zero.
- **Fix**: render **last actually decoded values** indefinitely until another genuine 0x25 volume or 0x45 ChatMix event updates them. A true 0% remains a valid 0%; a device with no actual reading remains unknown (`--%`), never fabricated zero. Two-second native wheel attenuation overlay stays transient rather than stuck.
- After two minutes without movement the editor's explanatory status reads **“Son bilinen tekerlek değeri (yeni hareket yok)”** / “Last known dial setting (no recent wheel movement)” rather than calling an aged value live.
- Automated 40-minute idle vs recent snapshot parity checks for Bar, Wheel and MiniWheel, Volume and ChatMix, real zero vs unknown, and status freshness passed. No firmware or audio settings are changed.

### 3. Battery & Priority Settings toolbar

- The **Son okuma / Last read** status label moved to its own row **beneath the four action buttons**, with a non-overlapping 20px-high label and status truncation when narrow.
- The OLED priority/alert controls below were moved down without enlarging the Settings window; body remains scrollable and Save footer fixed.
- Removed an old duplicate “DÜŞÜK PİL UYARILARI” heading that overlapped the source-information label after shifting the layout.
- The misleading generic **Üretici pili / Vendor battery** fallback became **Üretici kaynağı (HID/SDK)** / **Vendor HID / SDK**, describing a source of telemetry, not an extra physical battery.
- Real offscreen settings screenshot reviewed and a dedicated geometry regression confirms no overlap with any button.

## Test status

- `dotnet build -c Release`: 0 errors / 0 warnings
- Full existing test suite plus new logo/dial/layout regressions: PASS.
- User's two resized binary image fixtures and derived 32×32 OLED previews used **only in a private untracked QA folder**. They were not included in source commits, ZIP or external services.
- Private self-contained single-file Windows x64 EXE tested with `--self-test` and `--perf-smoke` after publish; settings window and rendered preview checked offscreen.
- Hardware acceptance still required for a real Nova Pro dial left untouched longer than ten minutes (tests simulate 40 minutes), and to confirm EXE icon extraction for specific protected games.

## Safety / packaging

- No physical firmware writes, wheel commands, mouse/keyboard input injection, profile reset, local lyrics cache deletion or changes to the public GitHub beta release.
- Original real dock photo is included in the **private** tester package. Its public redistribution rights are not assumed.
- This is a **private RC7.4 QA build**, not a stable/public 1.0 release. Keep previous ZIP and user-specific profiles untouched.

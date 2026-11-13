# Dock Customiser — RC4.1 Windows Startup & Priority

Private developer preview, based on RC4 commit `227fa81`. No public GitHub release and no automatic change to this computer's startup configuration.

## New Windows & performance Settings page
- **Launch at Windows sign-in** is **OFF** by default, reads the actual *current-user* Windows Run entry and only writes it when the person manually checks the option. Uses `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run`, never `HKLM`, the Task Scheduler or an elevated service. The enabled command is a safely quoted path to the **published** `SteelseriesDockCustomiser.exe` with a harmless `--windows-startup` flag. The development .NET DLL cannot accidentally register `dotnet.exe` as an autostart program.
- Unchecking removes the specific `SteelseriesDockCustomiser` Run entry. No unprompted installation or startup registration happens on the initial launch, when reviewing Settings, during screenshot capture, or during automated tests.
- Because this is a portable app, the Windows Run entry points at the current EXE. If the EXE is moved or deleted, turn the setting off and on again in the new location.

## Optional process priority
- **High process priority** is separately **OFF** by default; normal process priority is used unless the user requests otherwise. It uses `ProcessPriorityClass.High`, **never Realtime**.
- First tries to adjust this **application's own process** without admin privileges. Windows commonly permits that. A saved high-priority preference will be tried without any UAC prompt at subsequent launches.
- If Windows denies direct change, an explanation and explicit confirmation are required **before** showing any UAC request. The elevated helper is the same signed/packaged executable in a one-shot mode that changes only the exact matching original EXE process (PID, creation time, session, executable path) then exits; the main app, its OLED/HID/media features, and any future Workshop are never run as an elevated app.
- No always-admin manifest is installed. Declining UAC leaves the preference unchanged. Disabling high priority restores Normal.

## Validation
- Project builds with 0 errors and 0 warnings.
- All prior RC4 self-tests plus the new pure Windows integration tests pass: default-off flags, proper quoting, invalid command rejection, Normal vs High (not Realtime), and invalid privileged-helper arguments.
- Turkish/English Settings screenshots verified. No test modifies Windows startup entries, actually requests UAC, changes the test process's priority, touches hardware or uploads information.
- `RC4.1` remains a private preview. Real user acceptance of opt-in startup and UAC on the installed EXE is still necessary before public 1.0.

## Existing plan unaffected
RC4 splash, seven-step tutorial, Media/Game/Idle OLED effects, profile overwrite and optional Bug Report remain. Workshop stays **plan 2**; only its future-safe offline profile data contract exists. No community server is deployed and bug reports remain local drafts until configured securely.

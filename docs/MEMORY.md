# Project Memory

## Goal

Build a minimal Windows executable for the Ulanzi Studio D100H that loads JSON mappings and remaps the 7 physical buttons plus the dial. The app should be usable on work PCs where the official Ulanzi software cannot be installed.

## Current Decisions

- Language/runtime: C# with .NET 8.
- UI: WinForms.
- Distribution: self-contained single-file publish for `win-x64` by default.
- Admin privileges: not required.
- Startup: current-user registry key at `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Input source: low-level keyboard hook.
- Output: Win32 `SendInput`.
- Direct HID: Win32 SetupAPI/HID P/Invoke in `UlanziAdapter.Windows.Hid`.
- Config: versioned JSON.
- User config location: `%AppData%\UlanziAdapter\d100h.json`.
- Build bootstrap: `build.ps1` downloads a local .NET SDK into `.tools\dotnet` when needed.
- NuGet source: repository `NuGet.config` points to `https://api.nuget.org/v3/index.json`.

## Assumed D100H Source Mapping

Community-reported mapping used by `config/d100h.sample.json`:

```text
Dial clockwise       -> VolumeUp
Dial counter-clockwise -> VolumeDown
Dial press           -> VolumeMute
Top left             -> MediaPreviousTrack
Top middle           -> MediaPlayPause
Top right            -> MediaNextTrack
Side left top        -> Ctrl+V
Side left bottom     -> Ctrl+C
Side right top       -> Ctrl+Y
Side right bottom    -> Ctrl+Z
```

## Implemented Features

- Runtime remapping from JSON.
- Layer support.
- Dial press can toggle or hold a second layer.
- Keyboard shortcut output.
- Text output.
- Mouse wheel output for vertical and horizontal scroll.
- UI tab for editing bindings.
- Shortcut capture in the UI.
- Standard action presets for mouse wheel, navigation, editing, zoom, media, and layer actions.
- HID device enumeration from the UI.
- Experimental raw HID feature/output report application from JSON.
- Set Buttons now attempts direct HID mapping through `hid.mappingTemplates` whenever a shortcut or preset is saved.
- Windows startup registration.
- Self-contained build script.

## Open Risks

- The D100H has not been physically tested in this environment on Windows.
- Input suppression is gesture-based, not device-based.
- Common source gestures such as `Ctrl+C` can conflict with a normal keyboard while the app is active.
- Direct HID profile application requires the actual Ulanzi vendor report bytes or mapping templates. These are not known yet.
- If the D100H emits HID consumer reports that are not translated into virtual keys on some Windows machines, Raw Input diagnostics will still be needed for runtime observation.

## Recommended Next Steps

1. Test the app on Windows with the physical D100H.
2. Press every D100H control and verify the runtime log.
3. Confirm whether the sample sources match Windows behavior.
4. Add automated tests for config validation and `BindingEngine`.
5. Add Raw Input diagnostics for VID/PID discovery.
6. Capture USB/HID traffic from Ulanzi Studio while assigning a non-volume function to the dial.
7. Convert captured feature/output report payloads into `hid.reports`.
8. Add GitHub Actions for build verification and release artifacts.

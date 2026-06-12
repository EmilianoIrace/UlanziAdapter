# UlanziAdapter

Minimal Windows remapper for the **Ulanzi Studio D100H** controller.

UlanziAdapter lets you remap the physical buttons and dial of the D100H from a plain JSON file. It is designed for locked-down work PCs where installing the official Ulanzi software, drivers, or background services is not possible.

> This project is unofficial and is not affiliated with, endorsed by, or supported by Ulanzi.

## What It Does

- Loads button mappings from JSON.
- Remaps the D100H's 7 physical buttons.
- Remaps the dial clockwise/counter-clockwise actions.
- Supports a second layer triggered by pressing the dial.
- Sends keyboard shortcuts or text using the Windows `SendInput` API.
- Sends vertical and horizontal mouse wheel actions for scroll-style dial mappings.
- Provides a small **Set Buttons** UI for editing mappings without hand-editing JSON.
- Includes an experimental direct HID profile path for applying raw HID reports to the device.
- Includes a new experimental KMDF HID filter driver path for suppressing or rewriting raw HID read reports before Windows handles them.
- Can start automatically with Windows through the current user's startup registry key.
- Builds into a self-contained Windows executable.

## Project Status

This is an early driverless implementation.

The app does **not** install a kernel driver, does **not** flash the device firmware, and does **not** write profiles into the D100H. Instead, it listens for the standard keyboard/media events Windows receives from the controller and replaces them with the configured shortcuts.

That makes the app easy to run on restricted machines, but it also has technical limits. The repository now contains two lower-level paths:

- Direct HID profile reports, if the vendor report bytes are known.
- A KMDF HID filter driver that can suppress or rewrite raw HID read reports before Windows handles them.

For the D100H dial volume problem, the filter driver path is the correct architecture. See [Driver Filter Mode](#driver-filter-mode), [Direct HID Profile](#direct-hid-profile), and [Known Limitations](#known-limitations).

## Quick Start

1. Download or clone this repository on Windows.
2. Open PowerShell in the project directory.
3. Build the executable:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

4. Run the generated executable:

```powershell
.\artifacts\publish\win-x64\UlanziAdapter.App.exe
```

On first launch, the app copies the sample config to:

```text
%AppData%\UlanziAdapter\d100h.json
```

Edit that JSON file, click **Reload**, and test the device.

## Build From Source

The recommended build command is:

```powershell
.\build.ps1
```

If your PowerShell execution policy blocks local scripts:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The build script:

- checks for the .NET SDK;
- downloads a local .NET 8 SDK into `.tools\dotnet` if needed;
- uses the repository `NuGet.config`, which points to `https://api.nuget.org/v3/index.json`;
- restores packages;
- builds the solution;
- publishes a self-contained Windows executable.

Default output:

```text
artifacts\publish\win-x64\UlanziAdapter.App.exe
```

Advanced publish options:

```powershell
.\build\publish-win-x64.ps1 -Runtime win-x64
.\build\publish-win-x64.ps1 -Runtime win-arm64
.\build\publish-win-x64.ps1 -Configuration Debug
.\build\publish-win-x64.ps1 -FrameworkDependent
.\build\publish-win-x64.ps1 -NoDotNetBootstrap
```

## Usage

Start `UlanziAdapter.App.exe`.

The UI is intentionally small:

- **Config JSON**: selected mapping file.
- **Browse**: select another JSON file.
- **Reload**: reload mappings without restarting the app.
- **Start / Stop**: enable or disable remapping.
- **Start with Windows**: register the app in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- **Start minimized**: useful when the app launches at login.
- **List HID**: enumerate HID devices and log VID/PID, usage, report sizes, and paths.
- **Apply HID Profile**: send configured raw HID reports from the JSON to the selected device.
- **Apply Driver Rules**: send raw report suppress/rewrite rules to the installed filter driver.
- **Runtime tab**: shows active layer and runtime log.
- **Set Buttons tab**: edit mappings from the UI.

Closing the window hides the app to the tray. Use the tray menu to reopen or exit.

### Set Buttons UI

The **Set Buttons** tab lists every binding from the selected JSON file.

To record a keyboard shortcut:

1. Select a row.
2. Click **Capture Shortcut**.
3. Press the desired key or key combination.
4. The app saves the shortcut to JSON, tries to apply the matching HID report template, and reloads the mapping.

To use a standard action:

1. Select a row.
2. Pick a preset.
3. Click **Apply Preset**.

Available preset groups include:

- mouse scroll up/down/left/right;
- navigation arrows and page movement;
- editing shortcuts such as copy, paste, undo, redo, save;
- zoom in/out;
- media controls;
- layer toggle and momentary layer actions.

Direct HID behavior from this UI requires `hid.mappingTemplates`. If no matching template exists for the selected layer/control, the app saves the JSON runtime mapping but logs that direct HID mapping was not applied.

## Configuration

The default sample config is available at:

```text
config\d100h.sample.json
```

A binding maps a `source` input from the D100H to an action:

```json
{
  "source": "MediaPlayPause",
  "send": "Space",
  "description": "Top middle button"
}
```

Supported action types:

```json
{ "send": "Ctrl+Shift+Z" }
{ "send": "Ctrl+C;Ctrl+V" }
{ "text": "Hello world" }
{ "mouse": { "wheel": "down", "clicks": 1 } }
```

Layers are supported. The sample config uses the dial press to toggle a second layer:

```json
{
  "source": "VolumeMute",
  "layer": {
    "mode": "toggle",
    "target": "knobPressed",
    "fallback": "default"
  }
}
```

Layer modes:

- `switch`: move to another layer.
- `toggle`: toggle between target and fallback.
- `momentary`: use target while the source key is held.

## Direct HID Profile

Runtime remapping cannot always stop Windows from handling the original HID consumer-control event. For example, the D100H dial can emit `VolumeUp` and `VolumeDown`; on some Windows systems those events may still change system volume even when the app maps the action to scroll.

The source-level fix is to configure the D100H itself so it emits different HID usages. UlanziAdapter now supports an experimental raw HID profile section:

```json
{
  "hid": {
    "applyOnStart": false,
    "selector": {
      "devicePath": null,
      "vendorId": null,
      "productId": null,
      "usagePage": null,
      "usage": null,
      "productContains": "Ulanzi"
    },
    "reports": [
      {
        "enabled": true,
        "type": "feature",
        "bytes": "00 00 00 00",
        "delayAfterMs": 50,
        "description": "Example placeholder report"
      }
    ],
    "mappingTemplates": [
      {
        "enabled": false,
        "layer": "default",
        "control": "dialClockwise",
        "type": "feature",
        "bytes": "00 00 {keyboardModifier} {keyboardKey}",
        "delayAfterMs": 50,
        "description": "Placeholder template; replace with real D100H vendor report format"
      }
    ]
  }
}
```

Report types:

- `feature`: sent with `HidD_SetFeature`.
- `output`: sent with `WriteFile` to the HID device.

Important: the `bytes` value must include the report ID as the first byte. The sample does not include real D100H profile reports because the vendor protocol has not been documented in this repository yet.

`mappingTemplates` are used by the **Set Buttons** UI. A template matches a layer/control and turns the selected UI action into report bytes. Supported placeholders:

| Placeholder | Meaning |
| --- | --- |
| `{keyboardModifier}` | USB HID keyboard modifier byte for `Ctrl`, `Shift`, `Alt`, `Win`. |
| `{keyboardKey}` | USB HID keyboard usage for the main key. |
| `{consumerUsageLo}` | Low byte of a consumer-control usage such as media or volume. |
| `{consumerUsageHi}` | High byte of a consumer-control usage. |
| `{mouseWheelVertical}` | `01` for up, `FF` for down, else `00`. |
| `{mouseWheelHorizontal}` | `01` for right, `FF` for left, else `00`. |
| `{zero}` | `00`. |

Use **List HID** in the app to find the D100H VID/PID/path. To discover the actual report bytes, capture USB/HID traffic from Ulanzi Studio while changing a profile, then copy the relevant report payloads into `hid.reports`.

## Driver Filter Mode

If Windows still changes volume when the D100H dial turns, the app is seeing the event too late. The fix is a HID filter driver that intercepts read reports before Windows routes them to the consumer-control volume handler.

The repository includes an experimental KMDF filter driver scaffold:

```text
drivers\UlanziAdapter.Filter
```

The driver applies byte-level rules:

```json
{
  "driverFilter": {
    "applyOnStart": false,
    "clearExistingRules": true,
    "rules": [
      {
        "enabled": true,
        "name": "Suppress D100H dial clockwise volume report",
        "match": "01 E9 00",
        "suppress": true,
        "replacement": null
      }
    ]
  }
}
```

`match` and `replacement` are raw HID report bytes. The values above are placeholders, not confirmed D100H bytes. Use USBPcap/Wireshark or driver tracing to capture the actual reports emitted by your D100H.

Driver build requirements:

- Windows 10 or later.
- Visual Studio with C++ workload.
- Windows Driver Kit.
- Administrator rights.
- Test signing or a properly signed driver package.

Build the driver from a Windows Developer PowerShell:

```powershell
.\tools\build-driver.ps1
```

Before installing, edit `drivers\UlanziAdapter.Filter\UlanziAdapter.Filter.inf` and replace the placeholder hardware ID with the actual D100H HID interface hardware ID from Device Manager.

Install:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\install-driver.ps1
```

Once installed, launch the app and click **Apply Driver Rules**. The app sends the configured rules to `\\.\UlanziAdapterFilter`.

## Key Names

Common output examples:

```text
Ctrl+Z
Ctrl+Shift+Z
Alt+Tab
Space
Delete
Left
Right
Ctrl+Left
Ctrl+Right
NumpadAdd
NumpadSubtract
```

Known D100H source inputs used by the sample config:

```text
VolumeUp
VolumeDown
VolumeMute
MediaPreviousTrack
MediaPlayPause
MediaNextTrack
Ctrl+V
Ctrl+C
Ctrl+Y
Ctrl+Z
```

## Assumed D100H Mapping

The initial sample is based on community-reported D100H input mappings:

| D100H control | Source input |
| --- | --- |
| Dial clockwise | `VolumeUp` |
| Dial counter-clockwise | `VolumeDown` |
| Dial press | `VolumeMute` |
| Top left | `MediaPreviousTrack` |
| Top middle | `MediaPlayPause` |
| Top right | `MediaNextTrack` |
| Side left top | `Ctrl+V` |
| Side left bottom | `Ctrl+C` |
| Side right top | `Ctrl+Y` |
| Side right bottom | `Ctrl+Z` |

If your unit emits different inputs on Windows, use the app log to inspect what is being matched, then update the `source` fields in your JSON.

## Known Limitations

UlanziAdapter currently uses a low-level keyboard hook. Windows does not expose the physical device handle through that hook, so suppression is based on the input gesture, not on the exact USB device.

Practical consequence: media keys and volume events are usually safe to remap, but source inputs such as `Ctrl+C` may also match the same shortcut from a normal keyboard while the app is running.

If `VolumeUp`, `VolumeDown`, and `VolumeMute` still affect system volume, runtime remapping is not enough on that machine. The correct fix is to configure the D100H at the HID level so it stops emitting volume consumer-control events. UlanziAdapter now includes an experimental direct HID profile path for applying raw HID `feature` or `output` reports, but the exact vendor report bytes must be known or captured from Ulanzi Studio.

If vendor configuration reports are unavailable, use Driver Filter Mode. That does not reprogram the D100H; it suppresses or rewrites the raw report before Windows handles it.

Mitigations:

- keep `suppressOriginalInput` enabled only when needed;
- avoid mapping common keyboard shortcuts as sources if they conflict with normal typing;
- set `suppressOriginalInput` to `false` for diagnostic testing;
- add a future Raw Input or HID provider for per-device handling.

This app also does not replace the official Ulanzi profile editor. It remaps runtime input events; it does not persist mappings into the hardware.

## Configuration Reference

Top-level JSON structure:

```json
{
  "version": 1,
  "device": {
    "displayName": "Ulanzi Studio D100H",
    "vendorId": null,
    "productId": null
  },
  "behavior": {
    "suppressOriginalInput": true,
    "exactModifierMatch": true,
    "debounceMs": 10,
    "defaultLayer": "default"
  },
  "startup": {
    "enabled": false,
    "startMinimized": true
  },
  "hid": {
    "applyOnStart": false,
    "selector": {
      "productContains": "Ulanzi"
    },
    "reports": []
  },
  "bindings": {
    "default": {}
  }
}
```

Behavior options:

| Field | Description |
| --- | --- |
| `suppressOriginalInput` | Blocks the original matched input when possible. |
| `exactModifierMatch` | Requires exact modifier state for sources such as `Ctrl+C`. |
| `debounceMs` | Ignores repeated events inside the debounce window. |
| `defaultLayer` | Layer used when the app starts. |

Mouse wheel options:

| Field | Description |
| --- | --- |
| `mouse.wheel` | `up`, `down`, `left`, or `right`. |
| `mouse.clicks` | Positive number of wheel clicks sent per trigger. |

## Architecture

The codebase is split into three projects:

```text
src/UlanziAdapter.Core
src/UlanziAdapter.Windows
src/UlanziAdapter.App
```

- `Core`: JSON model, validation, normalized input events, binding engine, layers.
- `Windows`: Win32 keyboard hook, virtual-key translation, `SendInput`, startup registration.
- `App`: WinForms UI, tray icon, config loading, runtime wiring.

More detail: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)

## Roadmap

- Guided capture mode for detecting the next physical button press.
- Raw Input diagnostics for device VID/PID discovery.
- Optional HID input provider for cleaner per-device matching.
- Import/export profiles from the UI.
- Signed releases through GitHub Actions.
- Automated tests for config parsing and binding behavior.

## Privacy and Security

UlanziAdapter does not send telemetry and does not require an account.

The app listens to keyboard events locally in order to remap configured inputs. Do not run remapping tools you do not trust, and review the configuration before enabling startup launch.

## Contributing

Contributions are welcome, especially around:

- confirming D100H mappings on different Windows versions;
- improving per-device input detection;
- adding tests for the binding engine;
- improving the UI without making the app heavier.

Before changing behavior, please read:

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- [docs/MEMORY.md](docs/MEMORY.md)

## License

No license has been added yet. Until a license is published, all rights are reserved by the repository owner.

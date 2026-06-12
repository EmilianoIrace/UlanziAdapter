# Architecture

## Context

The Ulanzi Studio D100H can be handled in three broad ways:

1. User-mode remapper: listen to the standard input events Windows already receives and send replacement actions.
2. Vendor HID provider: communicate directly with the device using Ulanzi's proprietary protocol.
3. Kernel/filter driver: block or rewrite input per physical device.

The first implementation uses the user-mode remapper approach. It is the most practical option for locked-down work machines because it does not require admin privileges, driver installation, or firmware writes.

## Trade-offs

The low-level keyboard hook used by the app does not expose the physical device handle that produced an input. Suppression is therefore gesture-based, not device-based.

This is usually acceptable for D100H media and volume inputs, but common shortcuts such as `Ctrl+C` can also match a normal keyboard. A future Raw Input or HID provider can improve per-device precision without changing the JSON model.

## Project Layout

```text
src/UlanziAdapter.Core
  Configuration/    JSON model, loader, and validation
  Input/            Normalized input events, gestures, source abstraction
  Mapping/          BindingEngine, layers, debounce
  Actions/          Binding execution result

src/UlanziAdapter.Windows
  Input/            Low-level keyboard hook and virtual-key names
  Output/           SendInput keyboard, text, and mouse wheel output
  Startup/          HKCU Run startup registration
  Storage/          User settings in AppData
  Native/           Win32 P/Invoke definitions

src/UlanziAdapter.App
  Minimal WinForms UI, tray icon, config loading, binding editor

config/
  d100h.sample.json

docs/
  ARCHITECTURE.md
  MEMORY.md
```

## Runtime Flow

```text
KeyboardHookInputSource
  -> InputEvent
  -> BindingEngine.Handle(...)
  -> BindingExecution
  -> SendInputKeyboardOutput
```

`BindingEngine` is intentionally independent from Windows. A future HID input provider should be able to implement `IInputSource` and reuse the same config, validation, layer, and action logic.

## Actions

Bindings can perform one or more of these action families:

- `send`: keyboard shortcut sequence, for example `Ctrl+Shift+Z`.
- `text`: Unicode text output.
- `mouse`: mouse wheel output, for example vertical or horizontal scrolling.
- `layer`: switch, toggle, or momentary layer changes.

When the source input includes modifiers, such as `Ctrl+C`, the app releases source modifiers before sending the configured action. This avoids contaminating the replacement output with a physical modifier that is still down.

## JSON Layers

`bindings` is organized by layer:

```json
{
  "bindings": {
    "default": {
      "dialClockwise": {
        "source": "VolumeUp",
        "mouse": {
          "wheel": "up",
          "clicks": 1
        }
      }
    },
    "knobPressed": {}
  }
}
```

Layer actions:

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

Supported layer modes:

- `switch`
- `toggle`
- `momentary`

## Future HID Provider

The intended extension point is `IInputSource`.

A HID provider should:

1. enumerate HID devices;
2. filter by VID/PID or product string;
3. parse D100H reports into `InputEvent`;
4. avoid duplicate handling when Windows also emits keyboard/media events;
5. keep the existing JSON format unchanged.

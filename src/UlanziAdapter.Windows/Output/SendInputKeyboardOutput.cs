using System.Runtime.InteropServices;
using UlanziAdapter.Core.Input;
using UlanziAdapter.Windows.Input;
using UlanziAdapter.Windows.Native;

namespace UlanziAdapter.Windows.Output;

public sealed class SendInputKeyboardOutput
{
    public void ReleaseModifiers(ModifierState modifiers)
    {
        var inputs = new List<NativeMethods.INPUT>(4);
        AddModifierRelease(inputs, modifiers.Win, "Win");
        AddModifierRelease(inputs, modifiers.Alt, "Alt");
        AddModifierRelease(inputs, modifiers.Shift, "Shift");
        AddModifierRelease(inputs, modifiers.Ctrl, "Ctrl");

        if (inputs.Count > 0)
        {
            Send(inputs.ToArray());
        }
    }

    public void SendChordSequence(string sequence)
    {
        foreach (var chord in sequence.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            SendChord(chord);
        }
    }

    public void SendText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var character in text)
        {
            var inputs = new[]
            {
                CreateUnicodeInput(character, keyUp: false),
                CreateUnicodeInput(character, keyUp: true)
            };

            Send(inputs);
        }
    }

    public void SendMouseWheel(string direction, int clicks)
    {
        if (clicks <= 0)
        {
            return;
        }

        var normalizedDirection = direction.Trim().ToLowerInvariant();
        var isHorizontal = normalizedDirection is "left" or "right";
        var delta = normalizedDirection switch
        {
            "up" => NativeMethods.WHEEL_DELTA,
            "down" => -NativeMethods.WHEEL_DELTA,
            "right" => NativeMethods.WHEEL_DELTA,
            "left" => -NativeMethods.WHEEL_DELTA,
            _ => throw new InvalidOperationException($"Unknown mouse wheel direction '{direction}'.")
        };

        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = unchecked((uint)(delta * clicks)),
                    dwFlags = isHorizontal ? NativeMethods.MOUSEEVENTF_HWHEEL : NativeMethods.MOUSEEVENTF_WHEEL,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };

        Send(new[] { input });
    }

    private static void AddModifierRelease(List<NativeMethods.INPUT> inputs, bool isPressed, string modifierName)
    {
        if (!isPressed)
        {
            return;
        }

        if (VirtualKeyNames.TryGetVirtualKey(modifierName, out var virtualKey))
        {
            inputs.Add(CreateVirtualKeyInput(virtualKey, keyUp: true));
        }
    }

    private static void SendChord(string chord)
    {
        var modifiers = new List<ushort>(4);
        ushort? key = null;

        foreach (var rawPart in chord.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var part = KeyName.Normalize(rawPart);
            if (!VirtualKeyNames.TryGetVirtualKey(part, out var virtualKey))
            {
                throw new InvalidOperationException($"Unknown key '{rawPart}' in chord '{chord}'.");
            }

            if (KeyName.IsModifier(part))
            {
                modifiers.Add(virtualKey);
            }
            else if (key is null)
            {
                key = virtualKey;
            }
            else
            {
                throw new InvalidOperationException($"Chord '{chord}' contains more than one non-modifier key.");
            }
        }

        if (key is null)
        {
            throw new InvalidOperationException($"Chord '{chord}' does not contain a key.");
        }

        var inputs = new List<NativeMethods.INPUT>(modifiers.Count * 2 + 2);
        inputs.AddRange(modifiers.Select(modifier => CreateVirtualKeyInput(modifier, keyUp: false)));
        inputs.Add(CreateVirtualKeyInput(key.Value, keyUp: false));
        inputs.Add(CreateVirtualKeyInput(key.Value, keyUp: true));

        for (var i = modifiers.Count - 1; i >= 0; i--)
        {
            inputs.Add(CreateVirtualKeyInput(modifiers[i], keyUp: true));
        }

        Send(inputs.ToArray());
    }

    private static NativeMethods.INPUT CreateVirtualKeyInput(ushort virtualKey, bool keyUp)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };
    }

    private static NativeMethods.INPUT CreateUnicodeInput(char character, bool keyUp)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = (ushort)character,
                    dwFlags = NativeMethods.KEYEVENTF_UNICODE | (keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0),
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };
    }

    private static void Send(NativeMethods.INPUT[] inputs)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException($"SendInput sent {sent} of {inputs.Length} input events.");
        }
    }
}

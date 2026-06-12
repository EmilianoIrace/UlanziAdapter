using UlanziAdapter.Core.Input;

namespace UlanziAdapter.Core.Configuration;

public static class HidMappingReportBuilder
{
    public static HidMappingBuildResult Build(
        HidConfig hid,
        string layerName,
        string controlName,
        BindingConfig binding)
    {
        var template = hid.MappingTemplates.FirstOrDefault(template =>
            template.Enabled &&
            Matches(template.Layer, layerName) &&
            Matches(template.Control, controlName));

        if (template is null)
        {
            return HidMappingBuildResult.Fail(
                $"No HID mapping template configured for {layerName}.{controlName}. Add hid.mappingTemplates with layer/control and bytes placeholders.");
        }

        if (string.IsNullOrWhiteSpace(template.Bytes))
        {
            return HidMappingBuildResult.Fail($"HID mapping template for {layerName}.{controlName} has empty bytes.");
        }

        try
        {
            var context = HidActionContext.FromBinding(binding);
            var renderedBytes = RenderBytes(template.Bytes, context);
            _ = HexByteParser.Parse(renderedBytes);

            return HidMappingBuildResult.Ok(new HidReportConfig
            {
                Enabled = true,
                Type = template.Type,
                Bytes = renderedBytes,
                DelayAfterMs = template.DelayAfterMs,
                Description = template.Description ?? $"UI mapping for {layerName}.{controlName}"
            });
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            return HidMappingBuildResult.Fail($"Cannot build HID report for {layerName}.{controlName}: {ex.Message}");
        }
    }

    private static bool Matches(string pattern, string value)
    {
        return string.Equals(pattern, "*", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(pattern, value, StringComparison.OrdinalIgnoreCase);
    }

    private static string RenderBytes(string template, HidActionContext context)
    {
        return template
            .Replace("{keyboardModifier}", ToHex(context.KeyboardModifier), StringComparison.OrdinalIgnoreCase)
            .Replace("{keyboardKey}", ToHex(context.KeyboardKey), StringComparison.OrdinalIgnoreCase)
            .Replace("{consumerUsageLo}", ToHex((byte)(context.ConsumerUsage & 0xFF)), StringComparison.OrdinalIgnoreCase)
            .Replace("{consumerUsageHi}", ToHex((byte)((context.ConsumerUsage >> 8) & 0xFF)), StringComparison.OrdinalIgnoreCase)
            .Replace("{mouseWheelVertical}", ToHex(context.MouseWheelVertical), StringComparison.OrdinalIgnoreCase)
            .Replace("{mouseWheelHorizontal}", ToHex(context.MouseWheelHorizontal), StringComparison.OrdinalIgnoreCase)
            .Replace("{zero}", "00", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToHex(byte value) => value.ToString("X2");
}

public sealed record HidMappingBuildResult(bool Success, HidReportConfig? Report, string Message)
{
    public static HidMappingBuildResult Ok(HidReportConfig report) => new(true, report, "HID report built.");

    public static HidMappingBuildResult Fail(string message) => new(false, null, message);
}

internal sealed class HidActionContext
{
    public byte KeyboardModifier { get; private init; }

    public byte KeyboardKey { get; private init; }

    public ushort ConsumerUsage { get; private init; }

    public byte MouseWheelVertical { get; private init; }

    public byte MouseWheelHorizontal { get; private init; }

    public static HidActionContext FromBinding(BindingConfig binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.Send))
        {
            return FromSend(binding.Send);
        }

        if (binding.Mouse is not null)
        {
            return FromMouse(binding.Mouse);
        }

        throw new InvalidOperationException("Only keyboard shortcuts and mouse wheel actions can be converted to generic HID placeholders.");
    }

    private static HidActionContext FromSend(string send)
    {
        if (send.Contains(';', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("HID template conversion supports a single shortcut chord, not multi-step sequences.");
        }

        var gesture = SourceGesture.Parse(send);
        var consumerUsage = TryMapConsumerUsage(gesture.Key);
        if (consumerUsage is not null)
        {
            return new HidActionContext { ConsumerUsage = consumerUsage.Value };
        }

        return new HidActionContext
        {
            KeyboardModifier = BuildModifierByte(gesture.Modifiers),
            KeyboardKey = MapKeyboardUsage(gesture.Key)
        };
    }

    private static HidActionContext FromMouse(MouseActionConfig mouse)
    {
        return mouse.Wheel.Trim().ToLowerInvariant() switch
        {
            "up" => new HidActionContext { MouseWheelVertical = 0x01 },
            "down" => new HidActionContext { MouseWheelVertical = 0xFF },
            "right" => new HidActionContext { MouseWheelHorizontal = 0x01 },
            "left" => new HidActionContext { MouseWheelHorizontal = 0xFF },
            _ => throw new InvalidOperationException($"Unsupported mouse wheel direction '{mouse.Wheel}'.")
        };
    }

    private static byte BuildModifierByte(ModifierState modifiers)
    {
        byte value = 0;
        if (modifiers.Ctrl)
        {
            value |= 0x01;
        }

        if (modifiers.Shift)
        {
            value |= 0x02;
        }

        if (modifiers.Alt)
        {
            value |= 0x04;
        }

        if (modifiers.Win)
        {
            value |= 0x08;
        }

        return value;
    }

    private static ushort? TryMapConsumerUsage(string key)
    {
        return KeyName.Normalize(key) switch
        {
            "MediaPlayPause" => 0x00CD,
            "MediaNextTrack" => 0x00B5,
            "MediaPreviousTrack" => 0x00B6,
            "VolumeMute" => 0x00E2,
            "VolumeUp" => 0x00E9,
            "VolumeDown" => 0x00EA,
            _ => null
        };
    }

    private static byte MapKeyboardUsage(string key)
    {
        var normalized = KeyName.Normalize(key);
        if (normalized.Length == 1)
        {
            var character = normalized[0];
            if (character is >= 'A' and <= 'Z')
            {
                return (byte)(0x04 + character - 'A');
            }

            if (character is >= '1' and <= '9')
            {
                return (byte)(0x1E + character - '1');
            }

            if (character == '0')
            {
                return 0x27;
            }
        }

        if (normalized.StartsWith('F') && int.TryParse(normalized[1..], out var functionKey) && functionKey is >= 1 and <= 12)
        {
            return (byte)(0x3A + functionKey - 1);
        }

        return normalized switch
        {
            "Enter" => 0x28,
            "Escape" => 0x29,
            "Backspace" => 0x2A,
            "Tab" => 0x2B,
            "Space" => 0x2C,
            "Insert" => 0x49,
            "Home" => 0x4A,
            "PageUp" => 0x4B,
            "Delete" => 0x4C,
            "End" => 0x4D,
            "PageDown" => 0x4E,
            "Right" => 0x4F,
            "Left" => 0x50,
            "Down" => 0x51,
            "Up" => 0x52,
            "NumpadDivide" => 0x54,
            "NumpadMultiply" => 0x55,
            "NumpadSubtract" => 0x56,
            "NumpadAdd" => 0x57,
            "NumpadEnter" => 0x58,
            "Numpad1" => 0x59,
            "Numpad2" => 0x5A,
            "Numpad3" => 0x5B,
            "Numpad4" => 0x5C,
            "Numpad5" => 0x5D,
            "Numpad6" => 0x5E,
            "Numpad7" => 0x5F,
            "Numpad8" => 0x60,
            "Numpad9" => 0x61,
            "Numpad0" => 0x62,
            "NumpadDecimal" => 0x63,
            _ => throw new InvalidOperationException($"No generic HID keyboard usage mapping for '{key}'.")
        };
    }
}

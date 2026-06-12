namespace UlanziAdapter.Core.Input;

public static class KeyName
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CONTROL"] = "Ctrl",
        ["CTRL"] = "Ctrl",
        ["LCTRL"] = "Ctrl",
        ["RCTRL"] = "Ctrl",
        ["LEFTCTRL"] = "Ctrl",
        ["RIGHTCTRL"] = "Ctrl",
        ["SHIFT"] = "Shift",
        ["LSHIFT"] = "Shift",
        ["RSHIFT"] = "Shift",
        ["LEFTSHIFT"] = "Shift",
        ["RIGHTSHIFT"] = "Shift",
        ["ALT"] = "Alt",
        ["MENU"] = "Alt",
        ["LALT"] = "Alt",
        ["RALT"] = "Alt",
        ["LEFTALT"] = "Alt",
        ["RIGHTALT"] = "Alt",
        ["WIN"] = "Win",
        ["WINDOWS"] = "Win",
        ["LWIN"] = "Win",
        ["RWIN"] = "Win",
        ["CMD"] = "Win",
        ["COMMAND"] = "Win",
        ["ESC"] = "Escape",
        ["DEL"] = "Delete",
        ["INS"] = "Insert",
        ["PGUP"] = "PageUp",
        ["PGDN"] = "PageDown",
        ["RETURN"] = "Enter",
        ["KPPLUS"] = "NumpadAdd",
        ["NUMPADPLUS"] = "NumpadAdd",
        ["KPMINUS"] = "NumpadSubtract",
        ["NUMPADMINUS"] = "NumpadSubtract",
        ["MEDIAPREV"] = "MediaPreviousTrack",
        ["MEDIAPREVIOUS"] = "MediaPreviousTrack",
        ["MEDIAPREVIOUSTRACK"] = "MediaPreviousTrack",
        ["XF86AUDIOPREV"] = "MediaPreviousTrack",
        ["MEDIANEXT"] = "MediaNextTrack",
        ["MEDIANEXTTRACK"] = "MediaNextTrack",
        ["XF86AUDIONEXT"] = "MediaNextTrack",
        ["MEDIAPLAY"] = "MediaPlayPause",
        ["MEDIAPLAYPAUSE"] = "MediaPlayPause",
        ["XF86AUDIOPLAY"] = "MediaPlayPause",
        ["VOLUMEUP"] = "VolumeUp",
        ["XF86AUDIORAISEVOLUME"] = "VolumeUp",
        ["VOLUMEDOWN"] = "VolumeDown",
        ["XF86AUDIOLOWERVOLUME"] = "VolumeDown",
        ["VOLUMEMUTE"] = "VolumeMute",
        ["MUTE"] = "VolumeMute",
        ["XF86AUDIOMUTE"] = "VolumeMute",
        ["XF86AUDIOMUT"] = "VolumeMute"
    };

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var compact = value.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        if (compact.Length == 0)
        {
            return string.Empty;
        }

        if (compact.Length == 1)
        {
            return compact.ToUpperInvariant();
        }

        var upper = compact.ToUpperInvariant();
        if (Aliases.TryGetValue(upper, out var alias))
        {
            return alias;
        }

        if (upper.StartsWith('F') && int.TryParse(upper[1..], out var functionKey) && functionKey is >= 1 and <= 24)
        {
            return $"F{functionKey}";
        }

        if (upper.StartsWith("NUMPAD", StringComparison.Ordinal) &&
            int.TryParse(upper["NUMPAD".Length..], out var numpadKey) &&
            numpadKey is >= 0 and <= 9)
        {
            return $"Numpad{numpadKey}";
        }

        return compact[..1].ToUpperInvariant() + compact[1..];
    }

    public static bool IsModifier(string value)
    {
        var normalized = Normalize(value);
        return normalized is "Ctrl" or "Shift" or "Alt" or "Win";
    }
}

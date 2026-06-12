using UlanziAdapter.Core.Input;

namespace UlanziAdapter.Windows.Input;

public static class VirtualKeyNames
{
    private static readonly Dictionary<string, ushort> NameToVk = BuildNameToVk();
    private static readonly Dictionary<ushort, string> VkToName = NameToVk
        .GroupBy(item => item.Value)
        .ToDictionary(group => group.Key, group => group.First().Key);

    public static string FromVirtualKey(int virtualKey)
    {
        return VkToName.TryGetValue((ushort)virtualKey, out var name)
            ? name
            : $"VK_{virtualKey:X2}";
    }

    public static bool TryGetVirtualKey(string keyName, out ushort virtualKey)
    {
        return NameToVk.TryGetValue(KeyName.Normalize(keyName), out virtualKey);
    }

    private static Dictionary<string, ushort> BuildNameToVk()
    {
        var keys = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            ["Backspace"] = 0x08,
            ["Tab"] = 0x09,
            ["Enter"] = 0x0D,
            ["Shift"] = 0x10,
            ["Ctrl"] = 0x11,
            ["Alt"] = 0x12,
            ["Pause"] = 0x13,
            ["CapsLock"] = 0x14,
            ["Escape"] = 0x1B,
            ["Space"] = 0x20,
            ["PageUp"] = 0x21,
            ["PageDown"] = 0x22,
            ["End"] = 0x23,
            ["Home"] = 0x24,
            ["Left"] = 0x25,
            ["Up"] = 0x26,
            ["Right"] = 0x27,
            ["Down"] = 0x28,
            ["PrintScreen"] = 0x2C,
            ["Insert"] = 0x2D,
            ["Delete"] = 0x2E,
            ["Win"] = 0x5B,
            ["Numpad0"] = 0x60,
            ["Numpad1"] = 0x61,
            ["Numpad2"] = 0x62,
            ["Numpad3"] = 0x63,
            ["Numpad4"] = 0x64,
            ["Numpad5"] = 0x65,
            ["Numpad6"] = 0x66,
            ["Numpad7"] = 0x67,
            ["Numpad8"] = 0x68,
            ["Numpad9"] = 0x69,
            ["NumpadMultiply"] = 0x6A,
            ["NumpadAdd"] = 0x6B,
            ["NumpadSubtract"] = 0x6D,
            ["NumpadDecimal"] = 0x6E,
            ["NumpadDivide"] = 0x6F,
            ["NumLock"] = 0x90,
            ["ScrollLock"] = 0x91,
            ["VolumeMute"] = 0xAD,
            ["VolumeDown"] = 0xAE,
            ["VolumeUp"] = 0xAF,
            ["MediaNextTrack"] = 0xB0,
            ["MediaPreviousTrack"] = 0xB1,
            ["MediaStop"] = 0xB2,
            ["MediaPlayPause"] = 0xB3,
            ["BrowserBack"] = 0xA6,
            ["BrowserForward"] = 0xA7
        };

        for (var key = 'A'; key <= 'Z'; key++)
        {
            keys[key.ToString()] = (ushort)key;
        }

        for (var key = '0'; key <= '9'; key++)
        {
            keys[key.ToString()] = (ushort)key;
        }

        for (var i = 1; i <= 24; i++)
        {
            keys[$"F{i}"] = (ushort)(0x70 + i - 1);
        }

        return keys;
    }
}

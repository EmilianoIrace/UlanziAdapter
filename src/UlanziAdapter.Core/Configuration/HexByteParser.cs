using System.Globalization;
using System.Text;

namespace UlanziAdapter.Core.Configuration;

public static class HexByteParser
{
    public static byte[] Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<byte>();
        }

        var normalized = value
            .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(",", " ", StringComparison.Ordinal)
            .Replace(";", " ", StringComparison.Ordinal)
            .Replace("-", " ", StringComparison.Ordinal)
            .Replace(":", " ", StringComparison.Ordinal);

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 1)
        {
            return parts.Select(ParseByte).ToArray();
        }

        var compact = new StringBuilder();
        foreach (var character in normalized.Where(Uri.IsHexDigit))
        {
            compact.Append(character);
        }

        if (compact.Length == 0)
        {
            return Array.Empty<byte>();
        }

        if (compact.Length % 2 != 0)
        {
            throw new FormatException("Hex string must contain an even number of digits.");
        }

        var bytes = new byte[compact.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = ParseByte(compact.ToString(i * 2, 2));
        }

        return bytes;
    }

    private static byte ParseByte(string value)
    {
        if (value.Length is < 1 or > 2)
        {
            throw new FormatException($"Invalid hex byte '{value}'.");
        }

        return byte.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}

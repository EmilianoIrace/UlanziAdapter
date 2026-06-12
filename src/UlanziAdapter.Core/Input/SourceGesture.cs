namespace UlanziAdapter.Core.Input;

public sealed record SourceGesture(string Key, ModifierState Modifiers)
{
    public static SourceGesture Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Source gesture cannot be empty.", nameof(text));
        }

        var ctrl = false;
        var shift = false;
        var alt = false;
        var win = false;
        string? key = null;

        foreach (var rawPart in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var part = KeyName.Normalize(rawPart);
            switch (part)
            {
                case "Ctrl":
                    ctrl = true;
                    break;
                case "Shift":
                    shift = true;
                    break;
                case "Alt":
                    alt = true;
                    break;
                case "Win":
                    win = true;
                    break;
                default:
                    if (key is not null)
                    {
                        throw new ArgumentException($"Source gesture '{text}' contains more than one non-modifier key.");
                    }

                    key = part;
                    break;
            }
        }

        if (key is null)
        {
            throw new ArgumentException($"Source gesture '{text}' does not contain a key.");
        }

        return new SourceGesture(key, new ModifierState(ctrl, shift, alt, win));
    }

    public bool Matches(InputEvent input, bool exactModifierMatch)
    {
        if (!string.Equals(Key, input.NormalizedKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (exactModifierMatch)
        {
            return Modifiers == input.Modifiers;
        }

        return input.Modifiers.Contains(Modifiers);
    }

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.Ctrl)
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.Shift)
        {
            parts.Add("Shift");
        }

        if (Modifiers.Alt)
        {
            parts.Add("Alt");
        }

        if (Modifiers.Win)
        {
            parts.Add("Win");
        }

        parts.Add(Key);
        return string.Join("+", parts);
    }
}

namespace UlanziAdapter.Core.Input;

public sealed record ModifierState(bool Ctrl, bool Shift, bool Alt, bool Win)
{
    public static ModifierState Empty { get; } = new(false, false, false, false);

    public bool Contains(ModifierState required)
    {
        return (!required.Ctrl || Ctrl) &&
               (!required.Shift || Shift) &&
               (!required.Alt || Alt) &&
               (!required.Win || Win);
    }

    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Ctrl)
        {
            parts.Add("Ctrl");
        }

        if (Shift)
        {
            parts.Add("Shift");
        }

        if (Alt)
        {
            parts.Add("Alt");
        }

        if (Win)
        {
            parts.Add("Win");
        }

        return parts.Count == 0 ? "None" : string.Join("+", parts);
    }
}

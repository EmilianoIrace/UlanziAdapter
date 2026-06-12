namespace UlanziAdapter.Core.Input;

public sealed record InputEvent(
    string Key,
    bool IsDown,
    bool IsUp,
    ModifierState Modifiers,
    DateTimeOffset Timestamp,
    bool IsInjected)
{
    public string NormalizedKey { get; } = KeyName.Normalize(Key);

    public bool IsModifier => KeyName.IsModifier(NormalizedKey);

    public string ToGestureString()
    {
        if (Modifiers == ModifierState.Empty || Modifiers.ToString() == "None")
        {
            return NormalizedKey;
        }

        return $"{Modifiers}+{NormalizedKey}";
    }
}

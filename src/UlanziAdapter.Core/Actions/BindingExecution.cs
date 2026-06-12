namespace UlanziAdapter.Core.Actions;

public sealed record BindingExecution(
    bool Handled,
    string? ControlName = null,
    string? Description = null,
    string? Send = null,
    string? Text = null,
    string? MouseWheel = null,
    int MouseWheelClicks = 0,
    string? ActiveLayer = null)
{
    public static BindingExecution NotHandled { get; } = new(false);
}

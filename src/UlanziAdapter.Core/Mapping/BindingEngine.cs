using UlanziAdapter.Core.Actions;
using UlanziAdapter.Core.Configuration;
using UlanziAdapter.Core.Input;

namespace UlanziAdapter.Core.Mapping;

public sealed class BindingEngine
{
    private readonly AdapterConfig _config;
    private readonly Dictionary<string, Dictionary<string, RuntimeBinding>> _bindings;
    private readonly Dictionary<string, DateTimeOffset> _lastExecutionByControl = new(StringComparer.OrdinalIgnoreCase);
    private MomentaryLayerState? _momentaryLayer;

    public BindingEngine(AdapterConfig config)
    {
        _config = config;
        _bindings = BuildRuntimeBindings(config);
        ActiveLayer = config.Behavior.DefaultLayer;
    }

    public string ActiveLayer { get; private set; }

    public event Action<string>? ActiveLayerChanged;

    public BindingExecution Handle(InputEvent input)
    {
        if (input.IsInjected || input.IsModifier)
        {
            return BindingExecution.NotHandled;
        }

        if (input.IsUp && TryReleaseMomentaryLayer(input))
        {
            return new BindingExecution(true, ActiveLayer: ActiveLayer);
        }

        if (!input.IsDown)
        {
            return BindingExecution.NotHandled;
        }

        if (!_bindings.TryGetValue(ActiveLayer, out var layerBindings))
        {
            return BindingExecution.NotHandled;
        }

        var match = layerBindings.FirstOrDefault(item => item.Value.Source.Matches(input, _config.Behavior.ExactModifierMatch));
        if (match.Value is null)
        {
            return BindingExecution.NotHandled;
        }

        var controlName = match.Key;
        var binding = match.Value;
        if (IsDebounced(controlName, input.Timestamp))
        {
            return new BindingExecution(true, controlName, binding.Description, ActiveLayer: ActiveLayer);
        }

        ApplyLayerAction(binding, input);

        return new BindingExecution(
            true,
            controlName,
            binding.Description,
            binding.Send,
            binding.Text,
            binding.Mouse?.Wheel,
            binding.Mouse?.Clicks ?? 0,
            ActiveLayer);
    }

    private static Dictionary<string, Dictionary<string, RuntimeBinding>> BuildRuntimeBindings(AdapterConfig config)
    {
        var layers = new Dictionary<string, Dictionary<string, RuntimeBinding>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (layerName, configuredBindings) in config.Bindings)
        {
            var layer = new Dictionary<string, RuntimeBinding>(StringComparer.OrdinalIgnoreCase);
            foreach (var (controlName, binding) in configuredBindings)
            {
                if (!binding.Enabled || string.IsNullOrWhiteSpace(binding.Source))
                {
                    continue;
                }

                layer[controlName] = new RuntimeBinding(
                    SourceGesture.Parse(binding.Source),
                    binding.Send,
                    binding.Text,
                    binding.Mouse,
                    binding.Layer,
                    binding.Description);
            }

            layers[layerName] = layer;
        }

        return layers;
    }

    private bool IsDebounced(string controlName, DateTimeOffset now)
    {
        if (_config.Behavior.DebounceMs <= 0)
        {
            return false;
        }

        if (!_lastExecutionByControl.TryGetValue(controlName, out var lastExecution))
        {
            _lastExecutionByControl[controlName] = now;
            return false;
        }

        var elapsedMs = (now - lastExecution).TotalMilliseconds;
        _lastExecutionByControl[controlName] = now;
        return elapsedMs < _config.Behavior.DebounceMs;
    }

    private void ApplyLayerAction(RuntimeBinding binding, InputEvent input)
    {
        if (binding.Layer is null)
        {
            return;
        }

        var mode = binding.Layer.Mode.Trim().ToLowerInvariant();
        switch (mode)
        {
            case "switch":
                SetLayer(binding.Layer.Target);
                break;
            case "toggle":
                SetLayer(string.Equals(ActiveLayer, binding.Layer.Target, StringComparison.OrdinalIgnoreCase)
                    ? binding.Layer.Fallback
                    : binding.Layer.Target);
                break;
            case "momentary":
                _momentaryLayer = new MomentaryLayerState(binding.Source, ActiveLayer, binding.Layer.Fallback);
                SetLayer(binding.Layer.Target);
                break;
        }
    }

    private bool TryReleaseMomentaryLayer(InputEvent input)
    {
        if (_momentaryLayer is null)
        {
            return false;
        }

        if (!_momentaryLayer.Source.Matches(input, _config.Behavior.ExactModifierMatch))
        {
            return false;
        }

        SetLayer(string.IsNullOrWhiteSpace(_momentaryLayer.FallbackLayer)
            ? _momentaryLayer.PreviousLayer
            : _momentaryLayer.FallbackLayer);

        _momentaryLayer = null;
        return true;
    }

    private void SetLayer(string layerName)
    {
        if (!_bindings.ContainsKey(layerName))
        {
            return;
        }

        if (string.Equals(ActiveLayer, layerName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ActiveLayer = layerName;
        ActiveLayerChanged?.Invoke(ActiveLayer);
    }

    private sealed record RuntimeBinding(
        SourceGesture Source,
        string? Send,
        string? Text,
        MouseActionConfig? Mouse,
        LayerActionConfig? Layer,
        string? Description);

    private sealed record MomentaryLayerState(SourceGesture Source, string PreviousLayer, string FallbackLayer);
}

using UlanziAdapter.Core.Input;

namespace UlanziAdapter.Core.Configuration;

public static class ConfigValidator
{
    public static IEnumerable<string> Validate(AdapterConfig config)
    {
        if (config.Version != 1)
        {
            yield return "Only config version 1 is supported.";
        }

        if (string.IsNullOrWhiteSpace(config.Behavior.DefaultLayer))
        {
            yield return "behavior.defaultLayer cannot be empty.";
        }

        if (config.Behavior.DebounceMs < 0)
        {
            yield return "behavior.debounceMs must be zero or greater.";
        }

        if (config.Bindings.Count == 0)
        {
            yield return "bindings must contain at least one layer.";
            yield break;
        }

        if (!config.Bindings.ContainsKey(config.Behavior.DefaultLayer))
        {
            yield return $"bindings must contain the default layer '{config.Behavior.DefaultLayer}'.";
        }

        foreach (var (layerName, bindings) in config.Bindings)
        {
            if (string.IsNullOrWhiteSpace(layerName))
            {
                yield return "Layer names cannot be empty.";
            }

            foreach (var (controlName, binding) in bindings)
            {
                if (string.IsNullOrWhiteSpace(controlName))
                {
                    yield return $"Layer '{layerName}' contains an empty control name.";
                }

                if (!binding.Enabled)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(binding.Source))
                {
                    yield return $"Binding '{layerName}.{controlName}' has no source.";
                    continue;
                }

                string? sourceError = null;
                try
                {
                    _ = SourceGesture.Parse(binding.Source);
                }
                catch (ArgumentException ex)
                {
                    sourceError = ex.Message;
                }

                if (sourceError is not null)
                {
                    yield return $"Binding '{layerName}.{controlName}' source is invalid: {sourceError}";
                }

                if (string.IsNullOrWhiteSpace(binding.Send) &&
                    string.IsNullOrWhiteSpace(binding.Text) &&
                    binding.Layer is null)
                {
                    yield return $"Binding '{layerName}.{controlName}' has no action.";
                }

                if (binding.Layer is not null)
                {
                    var mode = binding.Layer.Mode.Trim().ToLowerInvariant();
                    if (mode is not ("switch" or "toggle" or "momentary"))
                    {
                        yield return $"Binding '{layerName}.{controlName}' layer.mode must be switch, toggle, or momentary.";
                    }

                    if (string.IsNullOrWhiteSpace(binding.Layer.Target))
                    {
                        yield return $"Binding '{layerName}.{controlName}' layer.target cannot be empty.";
                    }
                    else if (!config.Bindings.ContainsKey(binding.Layer.Target))
                    {
                        yield return $"Binding '{layerName}.{controlName}' references missing layer '{binding.Layer.Target}'.";
                    }

                    if (!string.IsNullOrWhiteSpace(binding.Layer.Fallback) &&
                        !config.Bindings.ContainsKey(binding.Layer.Fallback))
                    {
                        yield return $"Binding '{layerName}.{controlName}' references missing fallback layer '{binding.Layer.Fallback}'.";
                    }
                }
            }
        }
    }
}

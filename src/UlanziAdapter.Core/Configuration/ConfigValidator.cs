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

        foreach (var error in ValidateHid(config.Hid))
        {
            yield return error;
        }

        foreach (var error in ValidateDriverFilter(config.DriverFilter))
        {
            yield return error;
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
                    binding.Mouse is null &&
                    binding.Layer is null)
                {
                    yield return $"Binding '{layerName}.{controlName}' has no action.";
                }

                if (binding.Mouse is not null)
                {
                    var wheel = binding.Mouse.Wheel.Trim().ToLowerInvariant();
                    if (wheel is not ("up" or "down" or "left" or "right"))
                    {
                        yield return $"Binding '{layerName}.{controlName}' mouse.wheel must be up, down, left, or right.";
                    }

                    if (binding.Mouse.Clicks <= 0)
                    {
                        yield return $"Binding '{layerName}.{controlName}' mouse.clicks must be greater than zero.";
                    }
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

    private static IEnumerable<string> ValidateHid(HidConfig hid)
    {
        foreach (var report in hid.Reports)
        {
            if (!report.Enabled)
            {
                continue;
            }

            var type = report.Type.Trim().ToLowerInvariant();
            if (type is not ("feature" or "output"))
            {
                yield return "hid.reports[].type must be feature or output.";
            }

            if (string.IsNullOrWhiteSpace(report.Bytes))
            {
                yield return "hid.reports[].bytes cannot be empty when the report is enabled.";
                continue;
            }

            string? bytesError = null;
            try
            {
                _ = HexByteParser.Parse(report.Bytes);
            }
            catch (FormatException ex)
            {
                bytesError = ex.Message;
            }

            if (bytesError is not null)
            {
                yield return $"hid.reports[].bytes is invalid: {bytesError}";
            }

            if (report.DelayAfterMs < 0)
            {
                yield return "hid.reports[].delayAfterMs must be zero or greater.";
            }
        }
    }

    private static IEnumerable<string> ValidateDriverFilter(DriverFilterConfig driverFilter)
    {
        foreach (var rule in driverFilter.Rules.Where(rule => rule.Enabled))
        {
            if (string.IsNullOrWhiteSpace(rule.Match))
            {
                yield return "driverFilter.rules[].match cannot be empty when the rule is enabled.";
                continue;
            }

            string? matchError = null;
            try
            {
                _ = HexByteParser.Parse(rule.Match);
            }
            catch (FormatException ex)
            {
                matchError = ex.Message;
            }

            if (matchError is not null)
            {
                yield return $"driverFilter.rules[].match is invalid: {matchError}";
            }

            if (!string.IsNullOrWhiteSpace(rule.Replacement))
            {
                string? replacementError = null;
                try
                {
                    _ = HexByteParser.Parse(rule.Replacement);
                }
                catch (FormatException ex)
                {
                    replacementError = ex.Message;
                }

                if (replacementError is not null)
                {
                    yield return $"driverFilter.rules[].replacement is invalid: {replacementError}";
                }
            }
        }
    }
}

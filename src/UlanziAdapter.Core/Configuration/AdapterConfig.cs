namespace UlanziAdapter.Core.Configuration;

public sealed class AdapterConfig
{
    public int Version { get; set; } = 1;

    public DeviceConfig Device { get; set; } = new();

    public BehaviorConfig Behavior { get; set; } = new();

    public StartupConfig Startup { get; set; } = new();

    public Dictionary<string, Dictionary<string, BindingConfig>> Bindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DeviceConfig
{
    public string DisplayName { get; set; } = "Ulanzi Studio D100H";

    public int? VendorId { get; set; }

    public int? ProductId { get; set; }

    public string? Notes { get; set; }
}

public sealed class BehaviorConfig
{
    public bool SuppressOriginalInput { get; set; } = true;

    public bool ExactModifierMatch { get; set; } = true;

    public int DebounceMs { get; set; } = 10;

    public string DefaultLayer { get; set; } = "default";
}

public sealed class StartupConfig
{
    public bool Enabled { get; set; }

    public bool StartMinimized { get; set; } = true;
}

public sealed class BindingConfig
{
    public bool Enabled { get; set; } = true;

    public string? Source { get; set; }

    public string? Send { get; set; }

    public string? Text { get; set; }

    public LayerActionConfig? Layer { get; set; }

    public string? Description { get; set; }
}

public sealed class LayerActionConfig
{
    public string Mode { get; set; } = "switch";

    public string Target { get; set; } = "default";

    public string Fallback { get; set; } = "default";
}

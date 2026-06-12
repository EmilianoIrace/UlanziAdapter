using System.Text.Json;
using System.Text.Json.Serialization;

namespace UlanziAdapter.Core.Configuration;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    public static AdapterConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Configuration file not found.", path);
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<AdapterConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException("Configuration file is empty or invalid.");

        var errors = ConfigValidator.Validate(config).ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException("Invalid configuration:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        return config;
    }

    public static void Save(string path, AdapterConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));
    }
}

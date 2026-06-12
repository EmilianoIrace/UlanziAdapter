namespace UlanziAdapter.App;

internal sealed class CommandLineOptions
{
    public string? ConfigPath { get; private init; }

    public bool StartMinimized { get; private init; }

    public static CommandLineOptions Parse(string[] args)
    {
        string? configPath = null;
        var minimized = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                configPath = args[++i];
                continue;
            }

            if (string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase))
            {
                minimized = true;
            }
        }

        return new CommandLineOptions
        {
            ConfigPath = configPath,
            StartMinimized = minimized
        };
    }
}

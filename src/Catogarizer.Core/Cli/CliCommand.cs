namespace Catogarizer.Core.Cli;

/// <summary>
/// Parses argv for the app's two CLI entry points: "start" (wired into the Windows autostart
/// Run key, fires enabled Startup triggers) and "run &lt;name&gt;" (fires one named trigger on
/// demand, from a script or hotkey tool). This app has no console attached to report parse
/// errors to, so anything unrecognized falls back to None rather than failing loudly.
/// </summary>
public abstract record CliCommand
{
    private CliCommand() { }

    public sealed record None : CliCommand;
    public sealed record Start : CliCommand;
    public sealed record Run(string TriggerName) : CliCommand;

    public static CliCommand Parse(string[] args)
    {
        if (args.Length == 0) return new None();

        if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            return new Start();

        if (string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase) && args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            return new Run(args[1]);

        return new None();
    }
}

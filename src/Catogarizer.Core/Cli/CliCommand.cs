namespace Catogarizer.Core.Cli;

/// <summary>
/// Parses argv for the app's CLI entry points: "start" (wired into the Windows autostart Run
/// key, fires enabled Startup triggers), "run &lt;trigger&gt;", "switch &lt;category&gt;" and "back"
/// (for scripts, Stream Deck, AutoHotkey). This app has no console attached to report parse
/// errors to, so anything unrecognized falls back to None rather than failing loudly.
/// </summary>
public abstract record CliCommand
{
    private CliCommand() { }

    public sealed record None : CliCommand;
    public sealed record Start : CliCommand;
    public sealed record Run(string TriggerName) : CliCommand;
    public sealed record Switch(string CategoryName) : CliCommand;
    public sealed record Back : CliCommand;

    /// <summary>True for commands a second process relays to the running instance instead of just showing it.</summary>
    public bool IsRelayed => this is Run or Switch or Back;

    public static CliCommand Parse(string[] args)
    {
        if (args.Length == 0) return new None();

        var verb = args[0];
        var hasName = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]);

        if (Is(verb, "start")) return new Start();
        if (Is(verb, "back")) return new Back();
        if (Is(verb, "run") && hasName) return new Run(args[1]);
        if (Is(verb, "switch") && hasName) return new Switch(args[1]);
        return new None();
    }

    /// <summary>One line for the named pipe to the running instance; <see cref="FromPipeMessage"/> reverses it.</summary>
    public string ToPipeMessage() => this switch
    {
        Run r => $"run\t{r.TriggerName}",
        Switch s => $"switch\t{s.CategoryName}",
        Back => "back",
        _ => "",
    };

    public static CliCommand FromPipeMessage(string? message) =>
        string.IsNullOrWhiteSpace(message) ? new None() : Parse(message.Split('\t'));

    private static bool Is(string arg, string verb) => string.Equals(arg, verb, StringComparison.OrdinalIgnoreCase);
}

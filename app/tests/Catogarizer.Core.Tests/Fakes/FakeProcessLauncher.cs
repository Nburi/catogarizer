using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeProcessLauncher : IProcessLauncher
{
    public List<(string ExecutablePath, string? Arguments)> LaunchCalls { get; } = new();
    public int NextPid { get; set; } = 1000;

    public int Launch(string executablePath, string? arguments)
    {
        LaunchCalls.Add((executablePath, arguments));
        return NextPid++;
    }
}

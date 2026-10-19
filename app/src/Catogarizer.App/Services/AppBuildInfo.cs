using System.Reflection;
using Catogarizer.Core.Services;

namespace Catogarizer.App.Services;

/// <summary>The running build, read once from the exe itself. Debug builds (dotnet build / run) count as development builds.</summary>
public static class AppBuildInfo
{
    public static BuildInfo Current { get; } = BuildInfo.From(
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
#if DEBUG
        isDevelopment: true);
#else
        isDevelopment: false);
#endif

    /// <summary>Where the running exe lives - the one fact that settles "is this the installed app?".</summary>
    public static string ExePath { get; } = Environment.ProcessPath ?? "";
}

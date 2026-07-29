namespace Catogarizer.Core.Services;

public interface IProcessLauncher
{
    /// <summary>Starts the process and returns its process id.</summary>
    int Launch(string executablePath, string? arguments);
}

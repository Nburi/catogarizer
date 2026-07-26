namespace Catogarizer.Core.Services;

public interface IAutostartManager
{
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}

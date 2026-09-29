namespace Catogarizer.Core.Services;

public interface IAutostartService
{
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}

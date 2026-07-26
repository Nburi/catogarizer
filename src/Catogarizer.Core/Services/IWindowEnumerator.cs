namespace Catogarizer.Core.Services;

public interface IWindowEnumerator
{
    IReadOnlyList<OpenWindowInfo> GetOpenWindows();
}

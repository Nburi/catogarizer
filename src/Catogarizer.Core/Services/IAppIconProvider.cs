namespace Catogarizer.Core.Services;

/// <summary>Extracts an app's own icon from its executable, for display in place of the initial-letter fallback.</summary>
public interface IAppIconProvider
{
    /// <summary>Returns a native icon handle (HICON) for the executable, or 0 if it has none or doesn't exist.
    /// The caller owns the handle and must pass it to <see cref="ReleaseIconHandle"/> once done with it.</summary>
    nint GetIconHandle(string executablePath);

    void ReleaseIconHandle(nint handle);
}

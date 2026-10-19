namespace Catogarizer.Core.Resources;

public interface IAudioActivity
{
    /// <summary>
    /// Ids of the processes that currently have an active audio stream (music, a call, a video).
    /// A paused or idle player is not in the set. Empty if audio cannot be read - the caller then
    /// errs on the side of caution for freezing.
    /// </summary>
    IReadOnlySet<int> PlayingProcessIds();

    /// <summary>False when the audio session list could not be read, so "nothing is playing" cannot be trusted.</summary>
    bool IsReliable { get; }
}

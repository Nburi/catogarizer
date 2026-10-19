using Catogarizer.Core.Resources;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeAudioActivity : IAudioActivity
{
    public HashSet<int> Playing { get; } = [];
    public bool IsReliable { get; set; } = true;

    public IReadOnlySet<int> PlayingProcessIds() => Playing.ToHashSet();
}

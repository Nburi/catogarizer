using Catogarizer.Core.Persistence;

namespace Catogarizer.Core.Tests;

public sealed class JsonHiddenWindowStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "catogarizer-tests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "hidden.json");

    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        Assert.Empty(new JsonHiddenWindowStore(FilePath).Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsRecords()
    {
        var store = new JsonHiddenWindowStore(FilePath);
        store.Save([new HiddenWindowRecord(0x1A2B3C, 4711, "WINWORD", "Thesis.docx")]);

        var record = Assert.Single(new JsonHiddenWindowStore(FilePath).Load());

        Assert.Equal(new HiddenWindowRecord(0x1A2B3C, 4711, "WINWORD", "Thesis.docx"), record);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsEmptyInsteadOfThrowing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");

        Assert.Empty(new JsonHiddenWindowStore(FilePath).Load());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}

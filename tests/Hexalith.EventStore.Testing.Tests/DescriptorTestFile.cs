namespace Hexalith.EventStore.Testing.Tests;

internal sealed class DescriptorTestFile : IDisposable
{
    internal DescriptorTestFile()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "descriptor-" + Guid.NewGuid().ToString("N") + ".json");
        File.Copy(System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "ready-owner.json"), Path);
    }

    internal string Path { get; }

    public void Dispose() => File.Delete(Path);
}

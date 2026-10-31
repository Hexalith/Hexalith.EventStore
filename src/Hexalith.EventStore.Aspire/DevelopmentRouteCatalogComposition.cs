namespace Hexalith.EventStore.Aspire;

/// <summary>Owns only the private, per-run catalog coordination and rendered Dapr inputs, disposed after the application.</summary>
public sealed class DevelopmentRouteCatalogComposition : IAsyncDisposable
{
    internal DevelopmentRouteCatalogComposition(string directory, string rootDigest)
    {
        Directory = directory;
        RootDigest = rootDigest;
    }

    /// <summary>The newly created private directory; it never belongs to a checkout or another run.</summary>
    public string Directory { get; }

    /// <summary>The canonical complete owner catalog digest selected before any child starts.</summary>
    public string RootDigest { get; }

    /// <summary>Removes this run's private inputs after its application and sidecars have been disposed.</summary>
    public ValueTask DisposeAsync()
    {
        if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
        return ValueTask.CompletedTask;
    }
}

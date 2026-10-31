namespace Hexalith.EventStore.Aspire;

/// <summary>Owns only the private, per-run catalog coordination and rendered Dapr inputs, disposed after the application.</summary>
public sealed class DevelopmentRouteCatalogComposition : IAsyncDisposable
{
    private readonly DevelopmentRouteCatalogCustody _custody;
    private bool _disposed;

    internal DevelopmentRouteCatalogComposition(string directory, string rootDigest, DevelopmentRouteCatalogCustody custody)
    {
        Directory = directory;
        RootDigest = rootDigest;
        _custody = custody;
    }

    /// <summary>The newly created private directory; it never belongs to a checkout or another run.</summary>
    public string Directory { get; }

    /// <summary>The canonical complete owner catalog digest selected before any child starts.</summary>
    public string RootDigest { get; }

    /// <summary>Retains a complete committed generation, then removes private run inputs after the application and sidecars are disposed. Incomplete custody preserves the run for recovery.</summary>
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        try
        {
            _custody.Retain(Directory);
            if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
            return ValueTask.CompletedTask;
        }
        finally { _custody.Dispose(); }
    }
}

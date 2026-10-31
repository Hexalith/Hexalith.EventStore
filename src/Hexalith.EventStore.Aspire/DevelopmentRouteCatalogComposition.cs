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

    /// <summary>Removes this run's private inputs after its application and sidecars have been disposed.</summary>
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        try
        {
            if (System.IO.Directory.Exists(Directory))
            {
                _custody.Retain(Directory);
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
        finally { _custody.Dispose(); }
        return ValueTask.CompletedTask;
    }
}

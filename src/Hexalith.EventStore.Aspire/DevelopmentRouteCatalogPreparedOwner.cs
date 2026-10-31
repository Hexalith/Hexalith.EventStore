using System.Runtime.Loader;

namespace Hexalith.EventStore.Aspire;

/// <summary>Owns metadata loaded from the actual selected child output and its private preparation receipt.</summary>
public sealed class DevelopmentRouteCatalogPreparedOwner : IAsyncDisposable
{
    private readonly AssemblyLoadContext _context;

    internal DevelopmentRouteCatalogPreparedOwner(DevelopmentRouteCatalogOwner owner, string directory, AssemblyLoadContext context)
    {
        Owner = owner;
        Directory = directory;
        _context = context;
    }

    /// <summary>The selected resource and its compiled executable owner inventory.</summary>
    public DevelopmentRouteCatalogOwner Owner { get; }

    /// <summary>The private build diagnostics and content identity receipt, retained until disposal.</summary>
    public string Directory { get; }

    /// <summary>Releases preparation ownership after the application has stopped.</summary>
    public ValueTask DisposeAsync()
    {
        _context.Unload();
        if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
        return ValueTask.CompletedTask;
    }
}

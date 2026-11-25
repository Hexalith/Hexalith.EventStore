namespace Hexalith.EventStore.AppHost;

/// <summary>Owns the temporary Dapr component paths for one AppHost instance.</summary>
internal sealed class DaprResourceDirectory : IDisposable
{
    private readonly DirectoryInfo _root;
    private readonly bool _deleteOnDispose;

    private DaprResourceDirectory(DirectoryInfo root, string stateStorePath, string emptyResourcesPath, bool deleteOnDispose)
    {
        _root = root;
        _deleteOnDispose = deleteOnDispose;
        StateStorePath = stateStorePath;
        EmptyResourcesPath = emptyResourcesPath;
    }

    /// <summary>Gets the state-store YAML in a directory that contains no other components.</summary>
    public string StateStorePath { get; }

    /// <summary>Gets the empty directory for sidecars that receive no components.</summary>
    public string EmptyResourcesPath { get; }

    /// <summary>Creates the private Dapr resource paths for an AppHost instance.</summary>
    /// <param name="stateStoreSource">The path of the checked-in state-store component.</param>
    /// <param name="manifestPath">The generated manifest path, if exporting references for later use.</param>
    /// <returns>The directory owner for this AppHost instance.</returns>
    public static DaprResourceDirectory Create(string stateStoreSource, string? manifestPath = null)
    {
        const string prefix = "hexalith-eventstore-dapr-";
        DirectoryInfo root = manifestPath is null
            ? Directory.CreateTempSubdirectory(prefix)
            : Directory.CreateDirectory(Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(manifestPath))!,
                prefix + Guid.NewGuid().ToString("N")));

        try
        {
            string stateStoreDirectory = root.CreateSubdirectory("statestore").FullName;
            string emptyResourcesPath = root.CreateSubdirectory("empty").FullName;
            string stateStorePath = Path.Combine(stateStoreDirectory, Path.GetFileName(stateStoreSource));
            // Copy the bytes without carrying over read-only checkout attributes.
            File.WriteAllBytes(stateStorePath, File.ReadAllBytes(stateStoreSource));
            return new DaprResourceDirectory(root, stateStorePath, emptyResourcesPath, manifestPath is null);
        }
        catch
        {
            root.Delete(recursive: true);
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_deleteOnDispose)
        {
            return;
        }

        try
        {
            _root.Delete(recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // The directory has already been removed.
        }
    }
}

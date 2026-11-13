using System.Reflection;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Pairs a controlled current image path with the original loaded module identity for fresh PE observations.</summary>
internal sealed class RouteCatalogDiskImageAssembly(Assembly actual, string path) : Assembly
{
    /// <inheritdoc/>
    public override string Location => path;
    /// <inheritdoc/>
    public override string? FullName => actual.FullName;
    /// <inheritdoc/>
    public override Module ManifestModule => actual.ManifestModule;
}

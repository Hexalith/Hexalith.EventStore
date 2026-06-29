using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Cross-repo project metadata for the Hexalith.EventStore command-gateway service, resolved from the
/// consuming repository's <c>Hexalith.EventStore</c> checkout via
/// <see cref="RepositoryProjectPaths.GetReferencedModuleProjectPath"/>, which tolerates every layout (the
/// dependency under this repo's <c>references/</c>, a sibling under a parent's <c>references/</c>, or this repo
/// nested inside the EventStore repo). <see cref="SuppressBuild"/> stays <see langword="true"/> so Aspire
/// launches the server fast with <c>--no-build</c>; the consuming AppHost forces a fresh Debug compile via a
/// build-only <c>&lt;ProjectReference&gt;</c> (so <c>aspire run</c> never serves a stale binary), while Release
/// builds keep the per-repo package graphs isolated.
/// </summary>
internal sealed class EventStoreProjectMetadata : IProjectMetadata {
    /// <inheritdoc/>
    public string ProjectPath => RepositoryProjectPaths.GetReferencedModuleProjectPath(
        "Hexalith.EventStore",
        "src",
        "Hexalith.EventStore",
        "Hexalith.EventStore.csproj");

    /// <inheritdoc/>
    public bool SuppressBuild => true;
}

/// <summary>
/// Cross-repo project metadata for the Hexalith.EventStore Admin Server host, resolved from the consuming
/// repository's <c>references/Hexalith.EventStore</c> submodule. See <see cref="EventStoreProjectMetadata"/> for the
/// <see cref="SuppressBuild"/> rationale.
/// </summary>
internal sealed class EventStoreAdminServerHostProjectMetadata : IProjectMetadata {
    /// <inheritdoc/>
    public string ProjectPath => RepositoryProjectPaths.GetReferencedModuleProjectPath(
        "Hexalith.EventStore",
        "src",
        "Hexalith.EventStore.Admin.Server.Host",
        "Hexalith.EventStore.Admin.Server.Host.csproj");

    /// <inheritdoc/>
    public bool SuppressBuild => true;
}

/// <summary>
/// Cross-repo project metadata for the Hexalith.EventStore Admin UI (Blazor) host, resolved from the consuming
/// repository's <c>references/Hexalith.EventStore</c> submodule. See <see cref="EventStoreProjectMetadata"/> for the
/// <see cref="SuppressBuild"/> rationale.
/// </summary>
internal sealed class EventStoreAdminUIProjectMetadata : IProjectMetadata {
    /// <inheritdoc/>
    public string ProjectPath => RepositoryProjectPaths.GetReferencedModuleProjectPath(
        "Hexalith.EventStore",
        "src",
        "Hexalith.EventStore.Admin.UI",
        "Hexalith.EventStore.Admin.UI.csproj");

    /// <inheritdoc/>
    public bool SuppressBuild => true;
}

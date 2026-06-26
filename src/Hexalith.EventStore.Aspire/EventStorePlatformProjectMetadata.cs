using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Cross-repo project metadata for the Hexalith.EventStore command-gateway service, resolved from the
/// consuming repository's <c>references/Hexalith.EventStore</c> submodule. <see cref="SuppressBuild"/> is
/// <see langword="true"/>: the EventStore platform is built independently of the domain-module AppHost
/// (Aspire runs children with <c>--no-build</c>), so the AppHost build never compiles it and the two repos'
/// package graphs stay isolated.
/// </summary>
internal sealed class EventStoreProjectMetadata : IProjectMetadata {
    /// <inheritdoc/>
    public string ProjectPath => RepositoryProjectPaths.GetProjectPath(
        "references",
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
    public string ProjectPath => RepositoryProjectPaths.GetProjectPath(
        "references",
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
    public string ProjectPath => RepositoryProjectPaths.GetProjectPath(
        "references",
        "Hexalith.EventStore",
        "src",
        "Hexalith.EventStore.Admin.UI",
        "Hexalith.EventStore.Admin.UI.csproj");

    /// <inheritdoc/>
    public bool SuppressBuild => true;
}

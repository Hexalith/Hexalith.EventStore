namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Resolves on-disk paths to project files within a Hexalith mono-repo working tree, where the platform
/// modules (<c>Hexalith.EventStore</c>, <c>Hexalith.Memories</c>, …) are checked out as Git submodules at the
/// consuming repository root.
/// </summary>
/// <remarks>
/// <para>
/// This is the shared replacement for the per-domain-module <c>ProjectMetadataPaths</c> helper that every
/// domain AppHost previously hand-rolled. A domain module hosts its services on the Hexalith.EventStore
/// platform by referencing cross-repo project files (added with <see cref="Aspire.Hosting.ApplicationModel.IProjectMetadata.SuppressBuild"/>
/// set to <see langword="true"/>) instead of re-building them, so it needs to resolve those paths relative to
/// the AppHost's output directory.
/// </para>
/// <para>
/// The repository root is computed from <see cref="AppContext.BaseDirectory"/> assuming the standard
/// <c>&lt;repo-root&gt;/src/&lt;Module&gt;.AppHost/bin/&lt;config&gt;/&lt;tfm&gt;/</c> layout (five levels up).
/// All Hexalith domain-module AppHosts follow this layout, which makes the resolution identical across modules.
/// </para>
/// </remarks>
public static class RepositoryProjectPaths {
    /// <summary>
    /// Builds an absolute path to a file located under the consuming repository root.
    /// </summary>
    /// <param name="path">Path segments, relative to the repository root, ending in the target file.</param>
    /// <returns>The combined path rooted at the repository root.</returns>
    public static string GetProjectPath(params string[] path)
        => Path.Combine(GetRepositoryRoot(), Path.Combine(path));

    /// <summary>
    /// Resolves the consuming repository root from the AppHost output directory, assuming the standard
    /// <c>&lt;repo-root&gt;/src/&lt;Module&gt;.AppHost/bin/&lt;config&gt;/&lt;tfm&gt;/</c> layout.
    /// </summary>
    /// <returns>The absolute path to the repository root.</returns>
    public static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}

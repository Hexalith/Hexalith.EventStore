namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Resolves on-disk paths to project files within a Hexalith mono-repo working tree, where the platform
/// modules (<c>Hexalith.EventStore</c>, <c>Hexalith.Memories</c>, …) are either the current repository or checked
/// out as direct Git submodules under the consuming repository's <c>references</c> folder.
/// </summary>
/// <remarks>
/// <para>
/// This is the shared replacement for the per-domain-module <c>ProjectMetadataPaths</c> helper that every
/// domain AppHost previously hand-rolled. A domain module hosts its services on the Hexalith.EventStore
/// platform by referencing cross-repo project files (added with <c>IProjectMetadata.SuppressBuild</c>
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
    {
        ValidateRelativePathSegments(path);

        string repositoryRoot = GetRepositoryRoot();
        string resolvedPath = Path.GetFullPath(Path.Combine(repositoryRoot, Path.Combine(path)));
        string rootedPrefix = Path.EndsInDirectorySeparator(repositoryRoot)
            ? repositoryRoot
            : repositoryRoot + Path.DirectorySeparatorChar;

        return resolvedPath.StartsWith(rootedPrefix, StringComparison.Ordinal)
            ? resolvedPath
            : throw new ArgumentException("Project path segments must resolve under the repository root.", nameof(path));
    }

    /// <summary>
    /// Resolves the on-disk path to a project file inside a Hexalith platform module the consuming repository
    /// depends on (e.g. <c>Hexalith.EventStore</c>, <c>Hexalith.Memories</c>), with the same rule as the workspace
    /// source mapping. When <c>&lt;root&gt;/&lt;module&gt;.slnx</c> exists, the dependency is the consuming repository
    /// itself and the project resolves under <c>&lt;root&gt;</c>. Otherwise it resolves under the direct submodule
    /// <c>&lt;root&gt;/references/&lt;module&gt;</c>. No sibling, ancestor or root-level checkout is ever used.
    /// This keeps the launched project path (resolved here at runtime) identical to the build-time
    /// <c>&lt;ProjectReference&gt;</c> the AppHost uses to force a Debug build of the same project — so the AppHost
    /// never compiles one csproj and launches another.
    /// </summary>
    /// <param name="moduleDirectory">The dependency module's directory name (e.g. <c>Hexalith.EventStore</c>).</param>
    /// <param name="moduleRelativePath">Path segments inside the module, ending in the target project file.</param>
    /// <returns>The absolute path to the resolved project file.</returns>
    /// <exception cref="InvalidOperationException">
    /// The project file does not exist at the mapped location. The message names the dependency and the expected path.
    /// </exception>
    public static string GetReferencedModuleProjectPath(string moduleDirectory, params string[] moduleRelativePath)
        => ResolveReferencedModuleProjectPath(GetRepositoryRoot(), moduleDirectory, moduleRelativePath);

    /// <summary>
    /// Applies the <see cref="GetReferencedModuleProjectPath"/> rule to an explicit consuming repository root.
    /// </summary>
    /// <param name="root">The consuming repository root.</param>
    /// <param name="moduleDirectory">The dependency module's directory name (e.g. <c>Hexalith.EventStore</c>).</param>
    /// <param name="moduleRelativePath">Path segments inside the module, ending in the target project file.</param>
    /// <returns>The absolute path to the resolved project file.</returns>
    /// <exception cref="InvalidOperationException">
    /// The project file does not exist at the mapped location. The message names the dependency and the expected path.
    /// </exception>
    internal static string ResolveReferencedModuleProjectPath(string root, string moduleDirectory, params string[] moduleRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleDirectory);
        if (moduleRelativePath is null || moduleRelativePath.Length == 0)
        {
            throw new ArgumentException("At least one module-relative path segment is required.", nameof(moduleRelativePath));
        }

        string fullRoot = Path.GetFullPath(root);
        string relative = Path.Combine(moduleRelativePath);
        bool isCurrentRepository = File.Exists(Path.Combine(fullRoot, moduleDirectory + ".slnx"));
        string expected = isCurrentRepository
            ? Path.GetFullPath(Path.Combine(fullRoot, relative))
            : Path.GetFullPath(Path.Combine(fullRoot, "references", moduleDirectory, relative));

        return File.Exists(expected)
            ? expected
            : throw new InvalidOperationException(isCurrentRepository
                ? $"Dependency '{moduleDirectory}' is the repository at '{fullRoot}', but its project '{expected}' does not exist."
                : $"Dependency '{moduleDirectory}' must be checked out as the direct submodule 'references/{moduleDirectory}' of '{fullRoot}', but its project '{expected}' does not exist. Sibling, ancestor and root-level checkouts are not used. Initialize the submodule with 'git submodule update --init -- references/{moduleDirectory}'.");
    }

    /// <summary>
    /// Resolves the consuming repository root from the AppHost output directory, assuming the standard
    /// <c>&lt;repo-root&gt;/src/&lt;Module&gt;.AppHost/bin/&lt;config&gt;/&lt;tfm&gt;/</c> layout.
    /// </summary>
    /// <returns>The absolute path to the repository root.</returns>
    public static string GetRepositoryRoot()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Directory.Exists(Path.Combine(repositoryRoot, "src"))
            ? repositoryRoot
            : throw new InvalidOperationException(
                $"Cannot resolve a Hexalith repository root from '{AppContext.BaseDirectory}'. Expected a 'src' directory at '{repositoryRoot}'.");
    }

    private static void ValidateRelativePathSegments(string[] path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0)
        {
            throw new ArgumentException("At least one project path segment is required.", nameof(path));
        }

        foreach (string segment in path)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                throw new ArgumentException("Project path segments cannot be null, empty, or whitespace.", nameof(path));
            }

            if (Path.IsPathRooted(segment))
            {
                throw new ArgumentException("Project path segments must be relative.", nameof(path));
            }

            string normalizedSegment = segment.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (normalizedSegment
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                .Any(static part => part == ".."))
            {
                throw new ArgumentException("Project path segments cannot contain parent-directory traversal.", nameof(path));
            }
        }
    }
}

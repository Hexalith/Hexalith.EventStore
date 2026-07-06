namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Resolves on-disk paths to project files within a Hexalith mono-repo working tree, where the platform
/// modules (<c>Hexalith.EventStore</c>, <c>Hexalith.Memories</c>, …) may be the current repository or checked
/// out as Git submodules under the consuming repository's <c>references</c> folder.
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
    /// Resolves the on-disk path to a project file inside a sibling Hexalith platform module
    /// (e.g. <c>Hexalith.EventStore</c>, <c>Hexalith.Memories</c>), probing every checkout layout in the same
    /// order as the <c>$(Hexalith*Root)</c> auto-detection in <c>Directory.Build.props</c>. This keeps the
    /// launched project path (resolved here at runtime) identical to the build-time <c>&lt;ProjectReference&gt;</c>
    /// the AppHost uses to force a Debug build of the same project — so the AppHost never compiles one csproj and
    /// launches another. Returns the first candidate that exists; if none do, returns the standalone
    /// <c>&lt;root&gt;/references/&lt;module&gt;/…</c> path so the error names a diagnosable location.
    /// </summary>
    /// <param name="moduleDirectory">The dependency module's directory name (e.g. <c>Hexalith.EventStore</c>).</param>
    /// <param name="moduleRelativePath">Path segments inside the module, ending in the target project file.</param>
    /// <returns>The absolute path to the resolved project file.</returns>
    public static string GetReferencedModuleProjectPath(string moduleDirectory, params string[] moduleRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleDirectory);
        if (moduleRelativePath is null || moduleRelativePath.Length == 0)
        {
            throw new ArgumentException("At least one module-relative path segment is required.", nameof(moduleRelativePath));
        }

        string root = GetRepositoryRoot();
        string relative = Path.Combine(moduleRelativePath);

        // Candidate module locations, mirroring Directory.Build.props $(Hexalith*Root) precedence:
        //   1. dependency is the current repository                   (module src under this root)
        //   2. this repo nested directly inside the dependency repo   (module src one level up)
        //   3. this repo nested two levels inside the dependency repo
        //   4. dependency under this repo root                         (root-level sibling module checkout)
        //   5. dependency under this repo's references/                (standalone dev)
        //   6. dependency is a sibling of this repo                    (e.g. both under a parent's references/)
        //   7. dependency under the parent's references/
        string standalone = Path.GetFullPath(Path.Combine(root, "references", moduleDirectory, relative));
        string[] candidates =
        [
            Path.GetFullPath(Path.Combine(root, relative)),
            Path.GetFullPath(Path.Combine(root, "..", relative)),
            Path.GetFullPath(Path.Combine(root, "..", "..", relative)),
            Path.GetFullPath(Path.Combine(root, moduleDirectory, relative)),
            standalone,
            Path.GetFullPath(Path.Combine(root, "..", moduleDirectory, relative)),
            Path.GetFullPath(Path.Combine(root, "..", "references", moduleDirectory, relative)),
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return standalone;
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

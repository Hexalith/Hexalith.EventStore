using System.Diagnostics;
using System.Text;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Resolves governance sources only from declared repositories or explicit source overrides.
/// </summary>
internal static class PackagingRepositoryPaths
{
    /// <summary>
    /// Finds a dependency in a standalone checkout or its declaring umbrella workspace.
    /// </summary>
    /// <param name="repositoryRoot">The owning EventStore repository.</param>
    /// <param name="repositoryName">The expected Hexalith repository name.</param>
    /// <param name="sourceOverride">An explicit source checkout, when supplied.</param>
    /// <returns>The verified dependency worktree root.</returns>
    internal static string ResolveDependency(string repositoryRoot, string repositoryName, string? sourceOverride = null)
    {
        VerifyRepositoryRoot(repositoryRoot, "Hexalith.EventStore");
        if (sourceOverride is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceOverride);
            return VerifyRepositoryRoot(sourceOverride, repositoryName);
        }

        string? declared = ResolveDeclaredDependency(repositoryRoot, repositoryName);
        if (declared is not null)
        {
            return declared;
        }

        // An ancestor is a workspace only when its root declaration names this exact
        // EventStore checkout. An unrelated sibling clone is never an implicit input.
        for (DirectoryInfo? parent = Directory.GetParent(repositoryRoot); parent is not null; parent = parent.Parent)
        {
            string? eventStore = DeclaredPath(parent.FullName, "Hexalith.EventStore");
            if (eventStore is null || !PathsEqual(eventStore, repositoryRoot))
            {
                continue;
            }

            VerifyWorktreeRoot(parent.FullName);
            if (HasRepositoryIdentity(parent.FullName, repositoryName))
            {
                return VerifyRepositoryRoot(parent.FullName, repositoryName);
            }

            declared = ResolveDeclaredDependency(parent.FullName, repositoryName);
            if (declared is not null)
            {
                return declared;
            }

            break;
        }

        throw new DirectoryNotFoundException(
            $"No initialized declared {repositoryName} source exists for {repositoryRoot}. Supply the required workspace dependency or an explicit source override.");
    }

    /// <summary>
    /// Verifies Git owns exactly the supplied path and its origin identifies the expected repository.
    /// </summary>
    /// <param name="path">The source worktree root.</param>
    /// <param name="repositoryName">The expected Hexalith repository name.</param>
    /// <returns>The normalized worktree root.</returns>
    internal static string VerifyRepositoryRoot(string path, string repositoryName)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        VerifyWorktreeRoot(root);
        if (!HasRepositoryIdentity(root, repositoryName))
        {
            throw new InvalidDataException($"Source repository {root} does not identify Hexalith/{repositoryName}.");
        }

        return root;
    }

    /// <summary>
    /// Verifies a selected dependency belongs to the owner or its declaring workspace.
    /// </summary>
    /// <param name="repositoryRoot">The owning EventStore repository.</param>
    /// <param name="repositoryName">The expected dependency identity.</param>
    /// <param name="selectedRoot">The dependency selected by the effective import chain.</param>
    /// <returns>The verified dependency worktree root.</returns>
    internal static string VerifyDeclaredDependency(string repositoryRoot, string repositoryName, string selectedRoot)
    {
        string owner = VerifyRepositoryRoot(repositoryRoot, "Hexalith.EventStore");
        string selected = VerifyRepositoryRoot(selectedRoot, repositoryName);
        string? declared = DeclaredPath(owner, repositoryName);
        if (declared is not null && PathsEqual(declared, selected))
        {
            return selected;
        }

        for (DirectoryInfo? parent = Directory.GetParent(owner); parent is not null; parent = parent.Parent)
        {
            string? eventStore = DeclaredPath(parent.FullName, "Hexalith.EventStore");
            if (eventStore is null || !PathsEqual(eventStore, owner))
            {
                continue;
            }

            VerifyWorktreeRoot(parent.FullName);
            declared = DeclaredPath(parent.FullName, repositoryName);
            if (declared is not null && PathsEqual(declared, selected))
            {
                return selected;
            }

            break;
        }

        throw new InvalidDataException($"Source repository {selected} is not a declared {repositoryName} dependency of {owner} or its declaring workspace.");
    }

    /// <summary>
    /// Prevents inherited repository selectors from redirecting a verified Git operation.
    /// </summary>
    /// <param name="start">The child process environment to sanitize.</param>
    internal static void RemoveRepositorySelectors(ProcessStartInfo start)
    {
        _ = start.Environment.Remove("GIT_DIR");
        _ = start.Environment.Remove("GIT_WORK_TREE");
        _ = start.Environment.Remove("GIT_COMMON_DIR");
    }

    /// <summary>
    /// Reads an exact historical blob after verifying the source repository owns its worktree.
    /// </summary>
    /// <param name="repositoryRoot">The source repository root.</param>
    /// <param name="repositoryName">The expected repository identity.</param>
    /// <param name="revision">The exact pinned revision.</param>
    /// <param name="relativePath">The repository-relative blob path.</param>
    /// <returns>The unmodified Git blob bytes.</returns>
    internal static byte[] ReadPinnedFile(string repositoryRoot, string repositoryName, string revision, string relativePath)
    {
        string root = VerifyRepositoryRoot(repositoryRoot, repositoryName);
        if (revision.Length != 40 || revision.Any(character => !char.IsAsciiHexDigitLower(character)))
        {
            throw new InvalidDataException("Historical source reads require an exact lowercase 40-character commit SHA.");
        }

        ValidateRelativePath(relativePath);
        if (Encoding.UTF8.GetString(RunGit(root, "cat-file", "-t", revision)).Trim() != "commit")
        {
            throw new InvalidDataException("Historical source revision must be a commit object.");
        }

        if (Encoding.UTF8.GetString(RunGit(root, "cat-file", "-t", revision + ":" + relativePath)).Trim() != "blob")
        {
            throw new InvalidDataException("Historical source path must select a blob object.");
        }

        return RunGit(root, "show", revision + ":" + relativePath);
    }

    private static string? ResolveDeclaredDependency(string host, string repositoryName)
    {
        string? path = DeclaredPath(host, repositoryName);
        if (path is null || !File.Exists(Path.Combine(path, ".git")) && !Directory.Exists(Path.Combine(path, ".git")))
        {
            // Uninitialized nested submodules are deliberately left untouched.
            return null;
        }

        return VerifyRepositoryRoot(path, repositoryName);
    }

    private static string? DeclaredPath(string host, string repositoryName)
    {
        string modules = Path.Combine(host, ".gitmodules");
        if (!File.Exists(modules))
        {
            return null;
        }

        string[] declarations = Encoding.UTF8.GetString(RunGit(
            host, ["config", "--file", modules, "--get-regexp", @"^submodule\..*\.url$"], allowMissing: true))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string[] matches = declarations.Where(line =>
        {
            int separator = line.IndexOf(' ');
            return separator >= 0 && MatchesRepository(line[(separator + 1)..].Trim(), repositoryName);
        }).ToArray();
        if (matches.Length == 0)
        {
            return null;
        }

        if (matches.Length != 1)
        {
            throw new InvalidDataException($"Workspace {host} declares {repositoryName} more than once.");
        }

        string key = matches[0][..matches[0].IndexOf(' ')];
        string relative = Encoding.UTF8.GetString(RunGit(host, "config", "--file", modules, "--get", key[..^3] + "path")).Trim();
        ValidateRelativePath(relative);
        return Path.GetFullPath(Path.Combine(host, relative));
    }

    private static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)
            || path.Replace('\\', '/').Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException($"Source path must be repository-relative without traversal: {path}");
        }
    }

    private static void VerifyWorktreeRoot(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Source repository does not exist: {path}");
        }

        string actual = Encoding.UTF8.GetString(RunGit(path, "rev-parse", "--show-toplevel")).Trim();
        if (!PathsEqual(actual, path))
        {
            throw new InvalidDataException($"Source path {path} does not own its Git worktree; Git discovered {actual}.");
        }
    }

    private static bool HasRepositoryIdentity(string root, string repositoryName) =>
        MatchesRepository(Encoding.UTF8.GetString(RunGit(root, ["config", "--get", "remote.origin.url"], allowMissing: true)).Trim(), repositoryName);

    private static bool MatchesRepository(string url, string repositoryName)
    {
        string normalized = url.Replace("git@github.com:", "https://github.com/", StringComparison.Ordinal)
            .Replace("ssh://git@github.com/", "https://github.com/", StringComparison.Ordinal).TrimEnd('/');
        if (normalized.EndsWith(".git", StringComparison.Ordinal))
        {
            normalized = normalized[..^4];
        }

        return string.Equals(normalized, "https://github.com/Hexalith/" + repositoryName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string first, string second) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static byte[] RunGit(string root, params string[] arguments) => RunGit(root, arguments, false);

    private static byte[] RunGit(string root, string[] arguments, bool allowMissing)
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        RemoveRepositorySelectors(start);
        start.ArgumentList.Add("--no-replace-objects");
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidDataException("Could not start source repository verification.");
        using MemoryStream output = new();
        Task copy = process.StandardOutput.BaseStream.CopyToAsync(output);
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("Source repository verification timed out after 30 seconds.");
        }

        copy.GetAwaiter().GetResult();
        string diagnostic = error.GetAwaiter().GetResult();
        if (process.ExitCode != 0 && !(allowMissing && process.ExitCode == 1))
        {
            throw new InvalidDataException("Source repository verification failed: " + diagnostic.Trim());
        }

        return output.ToArray();
    }
}

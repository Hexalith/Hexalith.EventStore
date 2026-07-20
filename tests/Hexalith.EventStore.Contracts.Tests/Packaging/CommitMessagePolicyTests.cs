using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies repository guidance and local tooling enforce one commit-message policy.
/// </summary>
public sealed class CommitMessagePolicyTests
{
    private const string CommitHeaderFormat = "<type>[optional scope][!]: <description>";
    private const string SharedGitInstructionsRelativePath = "hexalith-git-instructions.md";

    private static readonly Regex MarkdownDestinationPattern = new(
        @"\](?:\(\s*(?:<(?<destination>[^>]+)>|(?<destination>[^)\s]+))|:\s*(?:<(?<destination>[^>]+)>|(?<destination>[^\s]+)))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

    /// <summary>
    /// Verifies Copilot delegates the commitlint contract to resolvable shared instructions.
    /// </summary>
    [Fact]
    public void CopilotInstructionsDelegateCommitlintContractToSharedInstructions()
    {
        string copilotInstructionsPath = RepositoryPath(".github", "copilot-instructions.md");
        string copilotInstructions = File.ReadAllText(copilotInstructionsPath);

        copilotInstructions.Contains(
            "`hexalith-llm-instructions.md`",
            StringComparison.Ordinal).ShouldBeTrue(
                "The Copilot entry point must name the shared hexalith-llm-instructions.md baseline.");
        copilotInstructions.Contains(
            "references/Hexalith.AI.Tools/hexalith-llm-instructions.md",
            StringComparison.Ordinal).ShouldBeTrue(
                "The Copilot entry point must document the workspace-relative fallback path to the shared instructions.");

        // The three entry points are identical normalized text, so any relative markdown
        // link resolves differently per entry point (.github/ vs repository root) and is
        // wrong from at least one of them; the baseline documents locations as prose.
        ContainsAnchoredReferencesLink(copilotInstructions).ShouldBeFalse(
            "A Markdown link anchored under references/ resolves from a different base in the Copilot entry point and silently loses the shared instructions.");

        string sharedLlmInstructionsPath = RepositoryPath("references", "Hexalith.AI.Tools", "hexalith-llm-instructions.md");
        File.Exists(sharedLlmInstructionsPath).ShouldBeTrue(
            "The Copilot entry point must delegate to an initialized shared LLM instruction file.");

        string sharedLlmInstructions = File.ReadAllText(sharedLlmInstructionsPath);
        sharedLlmInstructions.ShouldContain(
            $"[hexalith-git-instructions.md]({SharedGitInstructionsRelativePath})");
        sharedLlmInstructions.ShouldContain("Conventional Commits are mandatory");
        sharedLlmInstructions.ShouldContain("<type>[scope][!]: <description>");

        string sharedGitInstructionsPath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(sharedLlmInstructionsPath).ShouldNotBeNull(),
            SharedGitInstructionsRelativePath));
        File.Exists(sharedGitInstructionsPath).ShouldBeTrue(
            "The shared LLM instructions must delegate Git policy to a resolvable colocated file.");

        string sharedGitInstructions = File.ReadAllText(sharedGitInstructionsPath);
        sharedGitInstructions.ShouldContain("## Message Rules");
        sharedGitInstructions.ShouldContain(CommitHeaderFormat);
        sharedGitInstructions.ShouldContain("The description starts lowercase");
        sharedGitInstructions.ShouldContain("Never use the `chore` type");
        sharedGitInstructions.ShouldContain("--no-verify");

        string[] duplicatedPolicyMarkers =
        [
            "@commitlint/config-conventional",
            CommitHeaderFormat,
            "Start the description with a lowercase letter",
            "200 characters or fewer",
            "near 50 characters",
            "Choose the type by release impact",
        ];
        foreach (string marker in duplicatedPolicyMarkers)
        {
            copilotInstructions.Contains(marker, StringComparison.Ordinal).ShouldBeFalse(
                $"The Copilot entry point must delegate commit policy instead of duplicating '{marker}'.");
        }

        string submodules = ReadRepositoryFile(".gitmodules");
        submodules.ShouldContain("path = references/Hexalith.AI.Tools");
    }

    /// <summary>
    /// Verifies normalized inline and reference-style Markdown destinations cannot bypass the
    /// shared-entry-point link guard through whitespace, angle brackets, or parent traversal.
    /// </summary>
    /// <param name="markdown">A Markdown link form that resolves below a references directory.</param>
    [Theory]
    [InlineData("[baseline](references/Hexalith.AI.Tools/file.md)")]
    [InlineData("[baseline]( <references/Hexalith.AI.Tools/file.md> )")]
    [InlineData("[baseline](../../references/Hexalith.AI.Tools/file.md)")]
    [InlineData("[baseline]:references/Hexalith.AI.Tools/file.md")]
    [InlineData("[baseline]:\t<../references/Hexalith.AI.Tools/file.md>")]
    [InlineData("[baseline](/references/Hexalith.AI.Tools/file.md)")]
    [InlineData("[baseline](././references/Hexalith.AI.Tools/file.md)")]
    [InlineData("[baseline](docs/../references/Hexalith.AI.Tools/file.md)")]
    [InlineData("[baseline](references%2FHexalith.AI.Tools%2Ffile.md)")]
    public void AnchoredReferencesLinksAreRecognizedAcrossCommonMarkForms(string markdown)
        => ContainsAnchoredReferencesLink(markdown).ShouldBeTrue();

    /// <summary>
    /// Verifies VS Code commit generation loads the repository-wide Copilot policy.
    /// </summary>
    [Fact]
    public void VsCodeCommitGenerationUsesTheRepositoryCopilotInstructions()
    {
        using JsonDocument settings = ParseRepositoryJson(".vscode", "settings.json");

        JsonElement instructions = settings.RootElement.GetProperty(
            "github.copilot.chat.commitMessageGeneration.instructions");

        instructions
            .EnumerateArray()
            .Any(instruction => instruction.GetProperty("file").GetString() == ".github/copilot-instructions.md")
            .ShouldBeTrue(
                "VS Code Source Control commit generation must consume the repository-wide Copilot policy.");
    }

    private static bool ContainsAnchoredReferencesLink(string markdown)
        => MarkdownDestinationPattern
            .Matches(markdown)
            .Any(match => IsAnchoredReferencesDestination(match.Groups["destination"].Value));

    private static bool IsAnchoredReferencesDestination(string destination)
    {
        destination = Uri.UnescapeDataString(destination).Replace('\\', '/');
        int suffixStart = destination.IndexOfAny(['?', '#']);
        if (suffixStart >= 0)
        {
            destination = destination[..suffixStart];
        }

        if (Regex.IsMatch(destination, @"^[A-Za-z][A-Za-z0-9+.-]*:", RegexOptions.CultureInvariant))
        {
            return false;
        }

        List<string> normalizedSegments = [];
        foreach (string segment in destination.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (normalizedSegments.Count > 0)
                {
                    normalizedSegments.RemoveAt(normalizedSegments.Count - 1);
                }

                continue;
            }

            normalizedSegments.Add(segment);
        }

        return normalizedSegments.Count > 0
            && string.Equals(normalizedSegments[0], "references", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies npm setup installs the repository-pinned Husky integration.
    /// </summary>
    [Fact]
    public void NodeSetupInstallsRepositoryPinnedHuskyHooks()
    {
        using JsonDocument package = ParseRepositoryJson("package.json");
        using JsonDocument packageLock = ParseRepositoryJson("package-lock.json");

        package.RootElement
            .GetProperty("engines")
            .GetProperty("node")
            .GetString()
            .ShouldBe("^22.14.0 || >=24.10.0");
        package.RootElement.GetProperty("scripts").GetProperty("prepare").GetString().ShouldBe("husky");
        package.RootElement.GetProperty("devDependencies").GetProperty("husky").GetString().ShouldBe("^9.1.7");
        package.RootElement.GetProperty("devDependencies").GetProperty("@commitlint/cli").GetString().ShouldBe("21.1.0");
        package.RootElement
            .GetProperty("devDependencies")
            .GetProperty("@commitlint/config-conventional")
            .GetString()
            .ShouldBe("21.1.0");

        JsonElement lockPackages = packageLock.RootElement.GetProperty("packages");
        lockPackages
            .GetProperty("")
            .GetProperty("engines")
            .GetProperty("node")
            .GetString()
            .ShouldBe("^22.14.0 || >=24.10.0");
        lockPackages.GetProperty("").GetProperty("devDependencies").GetProperty("husky").GetString().ShouldBe("^9.1.7");
        lockPackages.GetProperty("node_modules/husky").GetProperty("version").GetString().ShouldBe("9.1.7");
        lockPackages.GetProperty("node_modules/@commitlint/cli").GetProperty("version").GetString().ShouldBe("21.1.0");
        lockPackages
            .GetProperty("node_modules/@commitlint/config-conventional")
            .GetProperty("version")
            .GetString()
            .ShouldBe("21.1.0");
        lockPackages.GetProperty("node_modules/cosmiconfig").GetProperty("version").GetString().ShouldBe("9.0.2");
    }

    /// <summary>
    /// Verifies the commit hook invokes commitlint and remains executable by POSIX shells.
    /// </summary>
    [Fact]
    public void CommitMessageHookExecutesCommitlintAndRemainsUnixCompatible()
    {
        string hookPath = RepositoryPath(".husky", "commit-msg");
        byte[] hookBytes = File.ReadAllBytes(hookPath);
        string hook = File.ReadAllText(hookPath);
        string commitlintConfig = ReadRepositoryFile("commitlint.config.mjs");

        hook.ShouldBe("#!/bin/sh\nnpx --no -- commitlint --edit \"$1\"\n");
        hookBytes.ShouldNotContain((byte)'\r', "Husky hooks must remain LF-only so /bin/sh can execute the shebang.");
        commitlintConfig.ShouldContain("extends: ['@commitlint/config-conventional']");
        commitlintConfig.ShouldContain("'type-enum'");
        commitlintConfig.ShouldNotContain("'chore'");

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(hookPath).HasFlag(UnixFileMode.UserExecute).ShouldBeTrue(
                "The tracked commit-msg hook must be executable on Unix systems.");
        }

        string attributes = ReadRepositoryFile(".gitattributes");
        attributes.ShouldContain(".husky/* text eol=lf");
        attributes.ShouldContain("commitlint.config.mjs text eol=lf");
    }

    /// <summary>
    /// Verifies no direct or meta-configuration source can shadow the canonical commitlint configuration.
    /// </summary>
    [Fact]
    public void CanonicalCommitlintConfigurationHasNoCompetingRootDiscoverySources()
    {
        const string canonicalConfig = "commitlint.config.mjs";
        string repositoryRoot = FindRepositoryRoot();

        File.Exists(Path.Combine(repositoryRoot, canonicalConfig)).ShouldBeTrue(
            $"The canonical {canonicalConfig} must exist at the repository root.");

        List<string> competingSources = Directory
            .EnumerateFiles(repositoryRoot, ".commitlintrc*", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(repositoryRoot, "commitlint.config.*", SearchOption.TopDirectoryOnly))
            .Select(filePath => new FileInfo(filePath).Name)
            .Where(fileName => !string.Equals(fileName, canonicalConfig, StringComparison.Ordinal))
            .ToList();

        if (File.Exists(Path.Combine(repositoryRoot, "package.yaml")))
        {
            competingSources.Add("package.yaml");
        }

        string[] cosmiconfigMetaFileNames =
        [
            "config.json",
            "config.yaml",
            "config.yml",
            "config.js",
            "config.ts",
            "config.cjs",
            "config.mjs",
        ];
        foreach (string fileName in cosmiconfigMetaFileNames)
        {
            if (File.Exists(Path.Combine(repositoryRoot, ".config", fileName)))
            {
                competingSources.Add($".config/{fileName}");
            }
        }

        using JsonDocument package = ParseRepositoryJson("package.json");
        string[] packageConfigProperties = ["commitlint", "cosmiconfig"];
        foreach (string propertyName in packageConfigProperties)
        {
            if (package.RootElement.TryGetProperty(propertyName, out _))
            {
                competingSources.Add($"package.json#{propertyName}");
            }
        }

        competingSources.Sort(StringComparer.Ordinal);
        competingSources.ShouldBeEmpty(
            $"Remove competing root commitlint source(s) so {canonicalConfig} remains authoritative: "
            + string.Join(", ", competingSources));
    }

    /// <summary>
    /// Verifies contributors receive complete hook setup and validation guidance.
    /// </summary>
    [Fact]
    public void ContributorGuideDocumentsSetupPolicyAndLocalValidation()
    {
        string contributing = ReadRepositoryFile("CONTRIBUTING.md");

        contributing.ShouldContain("npm ci");
        contributing.ShouldContain("Husky `prepare` script");
        contributing.ShouldContain(CommitHeaderFormat);
        contributing.ShouldContain("lowercase letter");
        contributing.ShouldContain("200 characters or fewer");
        contributing.ShouldContain("near 50 characters");
        contributing.ShouldContain("`revert`");
        contributing.ShouldContain("Do not use `chore`");
        contributing.ShouldContain("^22.14.0");
        contributing.ShouldContain(">=24.10.0");
        contributing.ShouldContain("npx commitlint --edit <message-file> --verbose");
        contributing.ShouldContain("npx commitlint --last --verbose");
        contributing.ShouldContain("Do not bypass the hook with `--no-verify`");
    }

    private static string FindRepositoryRoot()
    {
        string[] startPaths = [Directory.GetCurrentDirectory(), AppContext.BaseDirectory];
        foreach (string startPath in startPaths.Distinct(StringComparer.Ordinal))
        {
            DirectoryInfo? directory = new(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "package.json"))
                    && Directory.Exists(Path.Combine(directory.FullName, "src", "Hexalith.EventStore.Contracts")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test working directory.");
    }

    private static JsonDocument ParseRepositoryJson(params string[] pathParts)
        => JsonDocument.Parse(ReadRepositoryFile(pathParts));

    private static string ReadRepositoryFile(params string[] pathParts)
        => File.ReadAllText(RepositoryPath(pathParts));

    private static string RepositoryPath(params string[] pathParts)
        => Path.Combine([FindRepositoryRoot(), .. pathParts]);
}

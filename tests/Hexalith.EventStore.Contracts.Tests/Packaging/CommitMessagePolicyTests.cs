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
            "100 characters or fewer",
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
        => Regex.IsMatch(
            markdown,
            @"\](?:\(\s*<?|:\s*<?)(?:/|\./|(?:\.\./)+)?references/",
            RegexOptions.CultureInvariant | RegexOptions.Multiline);

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

        JsonElement lockPackages = packageLock.RootElement.GetProperty("packages");
        lockPackages
            .GetProperty("")
            .GetProperty("engines")
            .GetProperty("node")
            .GetString()
            .ShouldBe("^22.14.0 || >=24.10.0");
        lockPackages.GetProperty("").GetProperty("devDependencies").GetProperty("husky").GetString().ShouldBe("^9.1.7");
        lockPackages.GetProperty("node_modules/husky").GetProperty("version").GetString().ShouldBe("9.1.7");
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
        commitlintConfig.ShouldBe("export default {\n  extends: ['@commitlint/config-conventional'],\n};\n");

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(hookPath).HasFlag(UnixFileMode.UserExecute).ShouldBeTrue(
                "The tracked commit-msg hook must be executable on Unix systems.");
        }

        string attributes = ReadRepositoryFile(".gitattributes");
        attributes.ShouldContain(".husky/* text eol=lf");
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
        contributing.ShouldContain("100 characters or fewer");
        contributing.ShouldContain("near 50 characters");
        contributing.ShouldContain("`revert`");
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

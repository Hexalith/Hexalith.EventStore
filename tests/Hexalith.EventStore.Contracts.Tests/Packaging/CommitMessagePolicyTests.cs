using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies repository guidance and local tooling enforce one commit-message policy.
/// </summary>
public sealed class CommitMessagePolicyTests
{
    private const string CommitHeaderFormat = "<type>[optional scope][!]: <description>";

    /// <summary>
    /// Verifies Copilot receives the commitlint contract without relying on a broken link.
    /// </summary>
    [Fact]
    public void CopilotInstructionsExposeTheCommitlintContractDirectly()
    {
        string instructions = ReadRepositoryFile(".github", "copilot-instructions.md");

        instructions.Contains(
            "[`hexalith-llm-instructions.md`](../references/Hexalith.AI.Tools/hexalith-llm-instructions.md)",
            StringComparison.Ordinal).ShouldBeTrue(
                "The Copilot entry point must resolve the shared instructions relative to .github.");
        instructions.Contains(
            "[`hexalith-llm-instructions.md`](./references/Hexalith.AI.Tools/hexalith-llm-instructions.md)",
            StringComparison.Ordinal).ShouldBeFalse(
                "The former link resolves under .github/references and silently loses the shared instructions.");
        instructions.ShouldContain("@commitlint/config-conventional");
        instructions.ShouldContain(CommitHeaderFormat);
        instructions.ShouldContain("Start the description with a lowercase letter");
        instructions.ShouldContain("100 characters or fewer");
        instructions.ShouldContain("near 50 characters");
        instructions.ShouldContain("Choose the type by release impact");
        instructions.ShouldContain("`revert`");

        string submodules = ReadRepositoryFile(".gitmodules");
        submodules.ShouldContain("path = references/Hexalith.AI.Tools");
    }

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

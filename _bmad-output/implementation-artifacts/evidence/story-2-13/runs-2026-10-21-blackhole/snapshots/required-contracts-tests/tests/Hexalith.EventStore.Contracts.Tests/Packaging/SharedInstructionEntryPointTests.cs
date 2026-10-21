namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies the shared AI-assistant entry points remain synchronized normalized text.
/// </summary>
public sealed class SharedInstructionEntryPointTests
{
    private static readonly string[] EntryPointRelativePaths =
    [
        "AGENTS.md",
        "CLAUDE.md",
        Path.Combine(".github", "copilot-instructions.md"),
    ];

    /// <summary>
    /// Verifies AGENTS.md, CLAUDE.md, and .github/copilot-instructions.md carry identical normalized text.
    /// </summary>
    [Fact]
    public void SharedEntryPointsCarryIdenticalNormalizedText()
    {
        string agents = ReadNormalized("AGENTS.md");

        ReadNormalized("CLAUDE.md").ShouldBe(
            agents,
            "CLAUDE.md must stay synchronized with AGENTS.md; the shared baseline declares the entry points identical normalized text.");
        ReadNormalized(Path.Combine(".github", "copilot-instructions.md")).ShouldBe(
            agents,
            ".github/copilot-instructions.md must stay synchronized with AGENTS.md; the shared baseline declares the entry points identical normalized text.");
    }

    /// <summary>
    /// Verifies every entry point still delegates to the required Hexalith LLM baseline.
    /// </summary>
    [Fact]
    public void SharedEntryPointsDelegateToTheRequiredBaseline()
    {
        foreach (string relativePath in EntryPointRelativePaths)
        {
            string text = ReadNormalized(relativePath);

            text.Contains("## Required Hexalith LLM Baseline", StringComparison.Ordinal).ShouldBeTrue(
                $"{relativePath} must keep the baseline-delegation section.");
            text.Contains("hexalith-llm-instructions.md", StringComparison.Ordinal).ShouldBeTrue(
                $"{relativePath} must name the shared hexalith-llm-instructions.md baseline.");
        }
    }

    private static string ReadNormalized(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRepositoryRoot()
    {
        string[] startPaths = [Directory.GetCurrentDirectory(), AppContext.BaseDirectory];
        foreach (string startPath in startPaths.Distinct(StringComparer.Ordinal))
        {
            DirectoryInfo? directory = new(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                    && Directory.Exists(Path.Combine(directory.FullName, "src", "Hexalith.EventStore.Contracts")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test working directory.");
    }
}

using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Checks that deferred-work evidence can be read from any checkout.
/// </summary>
public sealed class DeferredWorkPathTests
{
    /// <summary>
    /// Rejects machine-local home paths in both the active ledger and the archive.
    /// </summary>
    /// <param name="fileName">The deferred-work ledger filename.</param>
    [Theory]
    [InlineData("deferred-work.md")]
    [InlineData("deferred-work-archive.md")]
    public void LedgerUsesPortablePaths(string fileName)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx")))
        {
            root = root.Parent;
        }

        _ = root.ShouldNotBeNull();
        string path = Path.Combine(root.FullName, "_bmad-output", "implementation-artifacts", fileName);
        int[] offendingLines = File.ReadLines(path)
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(item => Regex.IsMatch(item.Line, @"(?<![\w./~-])/(?:home|Users)/[\w.-]+(?=/|[\s`'""<>),;]|$)", RegexOptions.CultureInvariant))
            .Select(item => item.Number)
            .ToArray();
        offendingLines.ShouldBeEmpty($"{fileName} contains machine-local home paths on lines {string.Join(", ", offendingLines)}.");
    }
}

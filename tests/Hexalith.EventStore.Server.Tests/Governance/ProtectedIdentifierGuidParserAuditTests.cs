using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Governance;

public partial class ProtectedIdentifierGuidParserAuditTests {
    private static readonly string[] ProtectedFields = [
        "messageId",
        "correlationId",
        "aggregateId",
        "causationId",
    ];

    private static readonly string[] AllowedMiddlewarePaths = [
        "src/Hexalith.EventStore/Middleware/CorrelationIdMiddleware.cs",
        "src/Hexalith.EventStore.Admin.Server.Host/Middleware/CorrelationIdMiddleware.cs",
    ];

    [Fact]
    public void ProtectedIdentifierValidators_DoNotUseGuidParseOrTryParse() {
        string? root = TryFindRepositoryRoot();
        if (root is null) {
            Assert.Skip(
                "Repository root unavailable; protected identifier audit requires source tree access (set HEXALITH_REPOROOT or run from the repo workspace).");
            return;
        }

        string[] rootsToScan = [
            Path.Combine(root, "src"),
            Path.Combine(root, "samples"),
            Path.Combine(root, "tests"),
        ];

        var findings = new List<string>();
        foreach (string file in rootsToScan
            .Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            .Where(static file => !IsGeneratedOrBuildOutput(file))) {
            string[] lines = File.ReadAllLines(file);
            for (int index = 0; index < lines.Length; index++) {
                string line = lines[index];
                if (!GuidParserCall().IsMatch(line)) {
                    continue;
                }

                string context = GetContext(lines, index);
                string? protectedField = ProtectedFields.FirstOrDefault(
                    field => context.Contains(field, StringComparison.OrdinalIgnoreCase));
                if (protectedField is null || IsAllowedNonValidatorHit(file, line)) {
                    continue;
                }

                findings.Add(
                    $"{Path.GetRelativePath(root, file)}:{index + 1}: Guid.Parse/TryParse appears to validate protected field '{protectedField}'. Use Ulid.TryParse, UniqueIdHelper, or documented non-whitespace AggregateIdentity semantics. Allowed exclusions: HTTP middleware correlation headers, GUID generators, UniqueIdHelper.ToGuid conversion, and assertion-only test lines.");
            }
        }

        findings.ShouldBeEmpty(
            "Protected EventStore identifiers (messageId, correlationId, aggregateId, causationId) must not be validated with Guid.Parse/TryParse.");
    }

    private static string? TryFindRepositoryRoot() {
        string? envRoot = Environment.GetEnvironmentVariable("HEXALITH_REPOROOT");
        if (!string.IsNullOrWhiteSpace(envRoot) && IsRepositoryRoot(envRoot)) {
            return envRoot;
        }

        // Compile-time source path of THIS test file: ...\tests\Hexalith.EventStore.Server.Tests\Governance\<this>.cs
        string sourcePath = GetThisSourceFilePath();
        DirectoryInfo? current = new FileInfo(sourcePath).Directory;
        while (current is not null) {
            if (IsRepositoryRoot(current.FullName)) {
                return current.FullName;
            }

            current = current.Parent;
        }

        // Fallback: walk up from the test runtime base directory (covers `dotnet test` with sources still on disk).
        current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null) {
            if (IsRepositoryRoot(current.FullName)) {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool IsRepositoryRoot(string path) =>
        File.Exists(Path.Combine(path, "Directory.Packages.props"))
            && Directory.Exists(Path.Combine(path, "src"))
            && Directory.Exists(Path.Combine(path, "tests"));

    private static string GetThisSourceFilePath([CallerFilePath] string path = "") => path;

    private static string GetContext(string[] lines, int index) {
        int start = Math.Max(0, index - 3);
        int end = Math.Min(lines.Length - 1, index + 3);
        return string.Join(Environment.NewLine, lines[start..(end + 1)]);
    }

    private static bool IsGeneratedOrBuildOutput(string file) {
        string normalized = file.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedNonValidatorHit(string file, string line) {
        string normalized = file.Replace('\\', '/');
        if (AllowedMiddlewarePaths.Any(allowed =>
            normalized.EndsWith("/" + allowed, StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(allowed, StringComparison.OrdinalIgnoreCase))) {
            return true;
        }

        if (line.Contains("Guid.NewGuid", StringComparison.Ordinal)) {
            return true;
        }

        if (line.Contains("UniqueIdHelper.ToGuid", StringComparison.Ordinal)) {
            return true;
        }

        // Allow assertion-only test patterns ONLY when the assertion is on the same line as the Guid parser call.
        // A 7-line context window was previously used and is too permissive: a real validator in a test file
        // accompanied by a separate Should... assertion would be silently allowed.
        return normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            && SameLineAssertion().IsMatch(line);
    }

    [GeneratedRegex(@"Guid\.(TryParse|Parse)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex GuidParserCall();

    // Allow same-line patterns that prove a value is/isn't GUID-shaped without using Guid.Parse as a validator:
    //   - `Guid.TryParse(...).ShouldBeTrue()` / `.ShouldBeFalse()` (Shouldly fluent assertion)
    //   - `Assert.True(Guid.TryParse(...))` (xUnit assertion)
    //   - `bool isGuid = Guid.TryParse(...)` (assertion-evidence pattern; companion `Assert.False(isGuid)` follows)
    [GeneratedRegex(@"(\bShould(Be|NotBe|BeTrue|BeFalse|Throw|NotThrow)\b|\bAssert\.[A-Z]|\bbool\s+isGuid\s*=)", RegexOptions.CultureInvariant)]
    private static partial Regex SameLineAssertion();
}

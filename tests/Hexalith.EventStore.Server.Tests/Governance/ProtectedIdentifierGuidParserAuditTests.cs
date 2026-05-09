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

    [Fact]
    public void ProtectedIdentifierValidators_DoNotUseGuidParseOrTryParse() {
        string root = FindRepositoryRoot();
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
                if (protectedField is null || IsAllowedNonValidatorHit(file, line, context)) {
                    continue;
                }

                findings.Add(
                    $"{Path.GetRelativePath(root, file)}:{index + 1}: Guid.Parse/TryParse appears to validate protected field '{protectedField}'. Use Ulid.TryParse, UniqueIdHelper, or documented non-whitespace AggregateIdentity semantics. Allowed exclusions: HTTP middleware correlation headers, GUID generators, UniqueIdHelper.ToGuid conversion, and assertion-only tests.");
            }
        }

        findings.ShouldBeEmpty(
            "Protected EventStore identifiers (messageId, correlationId, aggregateId, causationId) must not be validated with Guid.Parse/TryParse.");
    }

    private static string FindRepositoryRoot() {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null) {
            if (File.Exists(Path.Combine(current.FullName, "Directory.Packages.props"))
                && Directory.Exists(Path.Combine(current.FullName, "src"))
                && Directory.Exists(Path.Combine(current.FullName, "tests"))) {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root for protected identifier audit.");
    }

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

    private static bool IsAllowedNonValidatorHit(string file, string line, string context) {
        string normalized = file.Replace('\\', '/');
        if (normalized.EndsWith("/Middleware/CorrelationIdMiddleware.cs", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (line.Contains("Guid.NewGuid", StringComparison.Ordinal)) {
            return true;
        }

        if (context.Contains("UniqueIdHelper.ToGuid", StringComparison.Ordinal)) {
            return true;
        }

        return normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            && AssertionContext().IsMatch(context);
    }

    [GeneratedRegex(@"Guid\.(TryParse|Parse)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex GuidParserCall();

    [GeneratedRegex(@"(\bShould(Be|NotBe|BeTrue|BeFalse)?\b|\bAssert\.|bool\s+isGuid\s*=)", RegexOptions.CultureInvariant)]
    private static partial Regex AssertionContext();
}

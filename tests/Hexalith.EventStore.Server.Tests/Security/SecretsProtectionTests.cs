using System.Diagnostics;
using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>
/// Guards all tracked, reusable text content against committed authentication material.
/// </summary>
public sealed partial class SecretsProtectionTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    /// <summary>
    /// Verifies Git-tracked text, including root/build configuration and workflows. Generated output,
    /// VCS data, and submodule contents are excluded by Git's tracked-file model rather than by path.
    /// </summary>
    [Fact]
    public void TrackedReusableContent_DoesNotContainUsableSecrets()
    {
        string[] trackedFiles = GetTrackedFiles();
        trackedFiles.ShouldNotBeEmpty("Git should return tracked repository content.");

        string[] violations = trackedFiles
            .Where(ShouldScan)
            .Where(static path => !path.EndsWith(
                "tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs",
                StringComparison.Ordinal))
            .SelectMany(FindViolations)
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.ShouldBeEmpty(
            "Tracked reusable content contains secret material. Only path and line are reported: "
            + string.Join(", ", violations));
    }

    [Theory]
    [InlineData("SigningKey=${JWT_SIGNING_KEY}")]
    [InlineData("password={env:POSTGRES_PASSWORD}")]
    [InlineData("\"value\": \"__HEXALITH_ADMIN_PASSWORD__\"")]
    [InlineData("ClientSecret=<client-secret>")]
    [InlineData("Password=[redacted]")]
    public void ExplicitInertPlaceholders_AreAllowed(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        FindViolations("samples/placeholder.json", line).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("SigningKey=reusable-signing-material-that-can-mint-tokens")]
    [InlineData("Password=reusable-database-password")]
    [InlineData("ClientSecret: reusable-client-secret")]
    [InlineData("Password=x")]
    [InlineData("SigningKey=TestReusableSigningMaterialThatCanMintTokens")]
    [InlineData("AccessToken=[reusable-token]")]
    public void UsableLiteralAssignments_AreRejected(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        FindViolations("samples/fixture.json", line).ShouldBe(["samples/fixture.json:1"]);
    }

    [Fact]
    public void AssignmentSeparatedFromValueByNewline_IsRejected()
        => FindViolations(".github/workflows/example.yml", "password:\n  reusable-value")
            .ShouldBe([".github/workflows/example.yml:2"]);

    [Fact]
    public void DecodedPrivilegedJwtPayload_IsRejected()
        => FindViolations(
            "docs/token.json",
            "{ \"sub\": \"administrator\", \"iss\": \"issuer\", \"aud\": \"api\", \"exp\": 9999999999, \"global_admin\": true }")
            .ShouldBe(["docs/token.json:1"]);

    [Fact]
    public void CredentialAssignmentEmbeddedInCSharpString_IsRejected()
        => FindViolations("tests/fixture.cs", "string fixture = \"Password=short\";")
            .ShouldBe(["tests/fixture.cs:1"]);

    private static IEnumerable<string> FindViolations(string relativePath)
    {
        string content = File.ReadAllText(Path.Combine(RepoRoot, relativePath));
        return FindViolations(relativePath, content);
    }

    private static IEnumerable<string> FindViolations(string relativePath, string content)
    {
        var violationLines = new SortedSet<int>();

        foreach (Match match in CompactJwtPattern().Matches(content))
        {
            _ = violationLines.Add(LineNumber(content, match.Index));
        }

        foreach (Match match in PrivateKeyPattern().Matches(content))
        {
            _ = violationLines.Add(LineNumber(content, match.Index));
        }

        foreach (Match match in DecodedPrivilegedJwtPattern().Matches(content))
        {
            _ = violationLines.Add(LineNumber(content, match.Index));
        }

        foreach (Match match in SecretAssignmentPattern().Matches(content))
        {
            string value = GetAssignmentValue(match);
            if (IsUsableLiteral(
                relativePath,
                match.Groups["name"].Value,
                value,
                match.Groups["bare"].Success,
                IsInsideCSharpString(relativePath, content, match.Groups["value"].Index)))
            {
                _ = violationLines.Add(LineNumber(content, match.Groups["value"].Index));
            }
        }

        if (relativePath.Contains("KeycloakRealms/", StringComparison.Ordinal)
            && relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            foreach (Match match in RealmPasswordPattern().Matches(content))
            {
                if (IsUsableLiteral(
                    relativePath,
                    "password",
                    match.Groups["value"].Value,
                    isBare: false,
                    isInsideCSharpString: false))
                {
                    _ = violationLines.Add(LineNumber(content, match.Groups["value"].Index));
                }
            }
        }

        return violationLines.Select(line => $"{relativePath}:{line}");
    }

    private static string[] GetTrackedFiles()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add("ls-files");
        process.StartInfo.ArgumentList.Add("-z");

        _ = process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, $"git ls-files failed: {error}");

        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool ShouldScan(string path)
    {
        if (IsExplicitlyGeneratedOrToolingContent(path))
        {
            return false;
        }

        string fullPath = Path.Combine(RepoRoot, path);
        if (!File.Exists(fullPath))
        {
            return false;
        }

        using FileStream stream = File.OpenRead(fullPath);
        Span<byte> buffer = stackalloc byte[4096];
        int count;
        while ((count = stream.Read(buffer)) > 0)
        {
            if (buffer[..count].Contains((byte)0))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsExplicitlyGeneratedOrToolingContent(string path)
    {
        string[] prefixes =
        [
            ".agent/skills/",
            ".agents/skills/",
            ".claude/skills/",
            ".github/skills/",
            ".opencode/skill/",
            ".playwright-cli/traces/",
            "_bmad-output/",
        ];

        return prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal))
            || path.Split('/').Any(static segment => segment is "bin" or "obj" or ".artifacts");
    }

    private static string GetAssignmentValue(Match match)
    {
        foreach (string groupName in new[] { "double", "single", "bare" })
        {
            Group group = match.Groups[groupName];
            if (group.Success)
            {
                return group.Value.Trim();
            }
        }

        return string.Empty;
    }

    private static bool IsUsableLiteral(
        string relativePath,
        string name,
        string value,
        bool isBare,
        bool isInsideCSharpString)
    {
        string candidate = value.Trim().Trim('"', '\'').TrimEnd(',', ';').Trim();
        if (IsInertPlaceholder(candidate) || IsIdentifierLabel(name, candidate))
        {
            return false;
        }

        if (name.EndsWith("ConnectionString", StringComparison.OrdinalIgnoreCase)
            && !candidate.Contains("password", StringComparison.OrdinalIgnoreCase)
            && !candidate.Contains("pwd=", StringComparison.OrdinalIgnoreCase)
            && !candidate.Contains('@'))
        {
            return false;
        }

        if (isInsideCSharpString)
        {
            return true;
        }

        if (isBare
            && string.Equals(Path.GetExtension(relativePath), ".cs", StringComparison.OrdinalIgnoreCase))
        {
            // C# literals are captured by the quoted groups. An unquoted value is an identifier,
            // invocation, interpolation marker, enum value, or other source expression.
            return false;
        }

        bool shellExpression = string.Equals(Path.GetExtension(relativePath), ".sh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(relativePath), ".bash", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(relativePath), ".ps1", StringComparison.OrdinalIgnoreCase);
        return !((isBare || shellExpression) && IsRecognizedSourceExpression(relativePath, candidate));
    }

    private static bool IsInsideCSharpString(string relativePath, string content, int index)
    {
        if (!string.Equals(Path.GetExtension(relativePath), ".cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        bool inside = false;
        for (int position = 0; position < index; position++)
        {
            if (content[position] != '"')
            {
                continue;
            }

            int backslashes = 0;
            for (int previous = position - 1; previous >= 0 && content[previous] == '\\'; previous--)
            {
                backslashes++;
            }

            if ((backslashes & 1) == 0)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool IsIdentifierLabel(string name, string value)
    {
        string normalizedName = NormalizeIdentifier(name);
        string normalizedValue = NormalizeIdentifier(value);
        return normalizedName.Length > 0
            && string.Equals(normalizedName, normalizedValue, StringComparison.OrdinalIgnoreCase);

        static string NormalizeIdentifier(string input)
            => new(input.Where(char.IsLetterOrDigit).ToArray());
    }

    private static bool IsInertPlaceholder(string value)
        => string.IsNullOrWhiteSpace(value)
            || EnvironmentPlaceholderPattern().IsMatch(value)
            || DaprEnvironmentPlaceholderPattern().IsMatch(value)
            || PowerShellEnvironmentPlaceholderPattern().IsMatch(value)
            || AnglePlaceholderPattern().IsMatch(value)
            || RealmPlaceholderPattern().IsMatch(value)
            || RedactedPlaceholderPattern().IsMatch(value)
            || SourcePlaceholderPattern().IsMatch(value)
            || ConfigurationKeyPattern().IsMatch(value)
            || TemplateValuePattern().IsMatch(value)
            || ProtectedMarkerPattern().IsMatch(value)
            || value is "..." or "null" or "not-a-jwt" or "invalid-token" or "malformed" or "too-short"
                or "wrong-key" or "missing" or "invalid" or "true" or "false"
                or "secret" or "admin-pass" or "pass" or "must-not-be-sent"
                or "hexalith-container-smoke-only-key-not-a-secret"
                or "Oq8EvidenceOnlySigningKey-AtLeast32Characters";

    private static bool IsRecognizedSourceExpression(string relativePath, string value)
    {
        string extension = Path.GetExtension(relativePath);
        if (value is "string.Empty" or "Array.Empty<string>()" or "default"
            || EnvironmentVariableReferencePattern().IsMatch(value))
        {
            return true;
        }

        if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
        {
            return CSharpSourceExpressionPattern().IsMatch(value);
        }

        if (string.Equals(extension, ".py", StringComparison.OrdinalIgnoreCase))
        {
            return PythonSourceExpressionPattern().IsMatch(value);
        }

        if (string.Equals(extension, ".sh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".bash", StringComparison.OrdinalIgnoreCase))
        {
            return ShellSourceExpressionPattern().IsMatch(value);
        }

        return false;
    }

    private static int LineNumber(string content, int index)
        => 1 + content.AsSpan(0, index).Count('\n');

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{16,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CompactJwtPattern();

    [GeneratedRegex(@"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----\s+[A-Za-z0-9+/=\r\n]{32,}\s+-----END (?:RSA |EC |OPENSSH )?PRIVATE KEY-----", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateKeyPattern();

    [GeneratedRegex(
        @"\{(?=[^{}]{0,1200}\""sub\""\s*:)(?=[^{}]{0,1200}\""iss\""\s*:)(?=[^{}]{0,1200}\""aud\""\s*:)(?=[^{}]{0,1200}\""exp\""\s*:)(?=[^{}]{0,1200}(?:\""global_admin\""\s*:\s*true|admin:(?:read|write)))[^{}]{1,1200}\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DecodedPrivilegedJwtPattern();

    [GeneratedRegex(
        @"(?:\[\s*)?[\""']?(?<name>(?:[A-Za-z0-9_]*(?:SigningKey|Password|Secret|ClientSecret|ApiKey|BearerToken|AccessToken|RefreshToken|IdToken|JwtToken|PrivateKey|Credential|ConnectionString)|POSTGRES_PASSWORD|REDIS_PASSWORD))[\""']?(?:\s*\])?\s*(?::(?![-+?])|=(?![=>]))\s*(?<value>(?:\""(?<double>[^\""\r\n]*)\""|'(?<single>[^'\r\n]*)'|(?<bare>[^\s,;#`\""']+)))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentPattern();

    [GeneratedRegex(@"\""type\""\s*:\s*\""password\""[\s\S]{0,160}?\""value\""\s*:\s*\""(?<value>[^\""\r\n]*)\""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RealmPasswordPattern();

    [GeneratedRegex(@"^\$\{[A-Z][A-Z0-9_]*\}$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentPlaceholderPattern();

    [GeneratedRegex(@"^\{env:[A-Z][A-Z0-9_]*\}$", RegexOptions.CultureInvariant)]
    private static partial Regex DaprEnvironmentPlaceholderPattern();

    [GeneratedRegex(@"^\$env:[A-Z][A-Z0-9_]*\}?$", RegexOptions.CultureInvariant)]
    private static partial Regex PowerShellEnvironmentPlaceholderPattern();

    [GeneratedRegex(@"^<[A-Za-z][A-Za-z0-9_.-]*>$", RegexOptions.CultureInvariant)]
    private static partial Regex AnglePlaceholderPattern();

    [GeneratedRegex(@"^__HEXALITH_[A-Z0-9_]+__$", RegexOptions.CultureInvariant)]
    private static partial Regex RealmPlaceholderPattern();

    [GeneratedRegex(@"^\[?redacted(?:-[a-z]+)?\]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RedactedPlaceholderPattern();

    [GeneratedRegex(@"^(?:\{\{[^{}\r\n]+\}\}|@Microsoft\.KeyVault\(SecretUri=[^\r\n]+\)|(?:secret|keyvault)ref:[A-Za-z0-9_.:/<>-]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourcePlaceholderPattern();

    [GeneratedRegex(@"^(?:[A-Za-z][A-Za-z0-9]*(?::[A-Za-z][A-Za-z0-9]*)+|[A-Za-z][A-Za-z0-9]*(?:__[A-Za-z0-9]+)+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ConfigurationKeyPattern();

    [GeneratedRegex(@"^\{[A-Za-z_][A-Za-z0-9_]*\}$", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateValuePattern();

    [GeneratedRegex(@"^PROTECTED_[A-Z0-9_]+_MARKER(?:_[A-Z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ProtectedMarkerPattern();

    [GeneratedRegex(@"^\$(?:\{)?[A-Za-z_][A-Za-z0-9_]*(?:\})?$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentVariableReferencePattern();

    [GeneratedRegex(
        @"^(?:\$|\(\)\s*=>\s*[^\r\n]+|[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*(?:\[[^\]\r\n]+\])?|(?:new\s+)?[A-Za-z_][A-Za-z0-9_.<>]*\([^\r\n;]*\)?|[A-Za-z_][A-Za-z0-9_]*\?*\.Value)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CSharpSourceExpressionPattern();

    [GeneratedRegex(@"^(?:os\.environ(?:\.get)?\([^\r\n]*\)|[A-Za-z_][A-Za-z0-9_.]*\([^\r\n]*\))$", RegexOptions.CultureInvariant)]
    private static partial Regex PythonSourceExpressionPattern();

    [GeneratedRegex(@"^(?:\$\{[A-Za-z_][A-Za-z0-9_]*(?::[-+?][^}\r\n]*)?\}|\$\((?:openssl\s+rand|head\s+-c|dd\s+if=|[A-Za-z_][A-Za-z0-9_.-]*)[^\r\n]*\))$", RegexOptions.CultureInvariant)]
    private static partial Regex ShellSourceExpressionPattern();
}

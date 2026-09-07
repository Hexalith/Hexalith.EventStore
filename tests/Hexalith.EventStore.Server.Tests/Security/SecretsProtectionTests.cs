using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            .SelectMany(FindViolations)
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.ShouldBeEmpty(
            "Tracked reusable content contains secret material. Only path and line are reported: "
            + string.Join(", ", violations));
    }

    public static TheoryData<string> InertPlaceholderAssignments => new()
    {
        Assignment("Signing" + "Key", "${JWT_SIGNING_KEY}"),
        Assignment("pass" + "word", "{env:POSTGRES_PASSWORD}"),
        Assignment("Client" + "Secret", "<client-secret>"),
        Assignment("Pass" + "word", "[redacted]"),
    };

    [Theory]
    [MemberData(nameof(InertPlaceholderAssignments))]
    public void ExplicitInertPlaceholders_AreAllowed(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        FindViolations("samples/placeholder.json", line).ShouldBeEmpty();
    }

    public static TheoryData<string> UsableLiteralAssignments => new()
    {
        Assignment("Signing" + "Key", Guid.NewGuid().ToString("N")),
        Assignment("Pass" + "word", Guid.NewGuid().ToString("N")),
        Assignment("Client" + "Secret", Guid.NewGuid().ToString("N"), separator: ':'),
        Assignment("Pass" + "word", "x"),
        Assignment("JWT_SIGNING_" + "KEY", Guid.NewGuid().ToString("N")),
        Assignment("client-" + "secret", Guid.NewGuid().ToString("N")),
        Assignment("SECRET_" + "ACCESS_KEY", Guid.NewGuid().ToString("N")),
        Assignment("TO" + "KEN", Guid.NewGuid().ToString("N")),
        Assignment("EVENTSTORE_ADMIN_" + "TOKEN", Guid.NewGuid().ToString("N")),
        Assignment("Account" + "Key", Guid.NewGuid().ToString("N")),
        Assignment("Storage" + "Key", Guid.NewGuid().ToString("N")),
        Assignment("service" + "Credentials", Guid.NewGuid().ToString("N")),
        Assignment("backup" + "Password", Guid.NewGuid().ToString("N")),
        Assignment("Access" + "Token", "[" + Guid.NewGuid().ToString("N") + "]"),
        Assignment("Pass" + "word", "correct:" + "horse:battery:staple"),
    };

    [Theory]
    [MemberData(nameof(UsableLiteralAssignments))]
    public void UsableLiteralAssignments_AreRejected(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        FindViolations("samples/fixture.json", line).ShouldBe(["samples/fixture.json:1"]);
    }

    [Fact]
    public void AssignmentSeparatedFromValueByNewline_IsRejected()
        => FindViolations(
            ".github/workflows/example.yml",
            string.Concat("pass", "word:\n  ", Guid.NewGuid().ToString("N")))
            .ShouldBe([".github/workflows/example.yml:2"]);

    [Fact]
    public void GenericDecodedJwtPayload_IsRejected()
        => FindViolations("docs/token.json", JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString("N"),
            ["iss"] = "https://issuer.example.test",
            ["aud"] = "api",
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
        }))
            .ShouldBe(["docs/token.json:1"]);

    [Fact]
    public void CredentialAssignmentEmbeddedInCSharpString_IsRejected()
        => FindViolations(
            "tests/fixture.cs",
            string.Concat("string fixture = \"", "Pass", "word=", Guid.NewGuid().ToString("N"), "\";"))
            .ShouldBe(["tests/fixture.cs:1"]);

    [Fact]
    public void CSharpRawMultilineCredentialAssignment_IsRejected()
        => FindViolations(
            "tests/fixture.cs",
            string.Concat("string fixture = \"\"\"\n", "client-", "secret", ":\n", Guid.NewGuid().ToString("N"), "\n\"\"\";"))
            .ShouldBe(["tests/fixture.cs:3"]);

    [Fact]
    public void ShellDefaultContainingASecret_IsRejected()
        => FindViolations(
            "scripts/example.sh",
            string.Concat("JWT_SIGNING_", "KEY=${KEY:", "-", Guid.NewGuid().ToString("N"), "}"))
            .ShouldBe(["scripts/example.sh:1"]);

    [Fact]
    public void OpaqueBearerToken_IsRejected()
        => FindViolations(
            "docs/request.http",
            string.Concat("Authorization: Bear", "er x"))
            .ShouldBe(["docs/request.http:1"]);

    [Theory]
    [InlineData("x")]
    [InlineData("!x")]
    [InlineData("_x")]
    public void StandaloneBearerSchemeValue_IsRejected(string token)
        => FindViolations(
            "docs/token.txt",
            string.Concat("Bear", "er ", token))
            .ShouldBe(["docs/token.txt:1"]);

    [Fact]
    public void AuthenticationHeaderValueLiteral_IsRejected()
        => FindViolations(
            "tests/client.cs",
            string.Concat(
                "new AuthenticationHeader",
                "Value(\"Bear",
                "er\", \"",
                Guid.NewGuid().ToString("N"),
                "\")"))
            .ShouldBe(["tests/client.cs:1"]);

    [Fact]
    public void CredentialBearingUriUserInfo_IsRejected()
        => FindViolations(
            "docs/database.txt",
            string.Concat("postgresql://app:", Guid.NewGuid().ToString("N"), "@database.example.test/store"))
            .ShouldBe(["docs/database.txt:1"]);

    [Fact]
    public void NestedLargeDecodedJwtPayload_IsRejected()
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString("N"),
            ["iss"] = "https://issuer.example.test",
            ["aud"] = "api",
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            ["padding"] = new string('x', 1_500),
        };
        string content = JsonSerializer.Serialize(new { wrapper = new { token = claims } });

        FindViolations("docs/token.json", content).ShouldHaveSingleItem();
    }

    [Fact]
    public void ArbitraryCSharpRawStringDelimiter_IsRejected()
    {
        string delimiter = new('"', 4);
        string content = string.Concat(
            "string fixture = ",
            delimiter,
            "\n",
            "Pass",
            "word=",
            Guid.NewGuid().ToString("N"),
            "\n",
            delimiter,
            ";");

        FindViolations("tests/fixture.cs", content).ShouldBe(["tests/fixture.cs:2"]);
    }

    [Fact]
    public void LiteralOnlyCredentialCall_IsRejected()
        => FindViolations(
            "tests/fixture.cs",
            string.Concat(
                "Signing",
                "Key=Convert.FromBase64String(\"",
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
                "\")"))
            .ShouldBe(["tests/fixture.cs:1"]);

    [Fact]
    public void AnyPrivateKeyPemLabel_IsRejected()
        => FindViolations(
            "deploy/private-key.pem",
            string.Concat(
                "-----BEGIN ENCRYPTED PRIVATE ",
                "KEY-----\n",
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
                "\n-----END ENCRYPTED PRIVATE ",
                "KEY-----"))
            .ShouldBe(["deploy/private-key.pem:1"]);

    [Fact]
    public void Utf16Text_IsDecodedBeforeBinaryClassification()
    {
        string assignment = Assignment("Pass" + "word", Guid.NewGuid().ToString("N"));
        byte[] encoded = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(assignment)).ToArray();

        DecodeSupportedText(encoded).ShouldBe(assignment);
        FindViolations("config/example.txt", DecodeSupportedText(encoded)!).ShouldBe(["config/example.txt:1"]);
    }

    [Fact]
    public void MalformedTextRetainsRecoverableCredentialSpans()
    {
        byte[] assignment = Encoding.UTF8.GetBytes(Assignment("Pass" + "word", Guid.NewGuid().ToString("N")));
        byte[] malformed = [0xff, (byte)'\n', .. assignment];
        string decoded = DecodeSupportedText(malformed)!;

        decoded.ShouldNotBeNull();
        FindViolations("config/example.txt", decoded).ShouldBe(["config/example.txt:2"]);
    }

    private static string Assignment(string name, string value, char separator = '=')
        => string.Concat(name, separator, value);

    private static IEnumerable<string> FindViolations(string relativePath)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(RepoRoot, relativePath));
        string? content = DecodeSupportedText(bytes);
        if (content is null)
        {
            return [];
        }

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

        foreach (Match match in BearerTokenPattern().Matches(content))
        {
            if (!IsInertPlaceholder(match.Groups["token"].Value))
            {
                _ = violationLines.Add(LineNumber(content, match.Groups["token"].Index));
            }
        }

        foreach (Match match in StandaloneBearerTokenPattern().Matches(content))
        {
            if (!IsInertPlaceholder(match.Groups["token"].Value))
            {
                _ = violationLines.Add(LineNumber(content, match.Groups["token"].Index));
            }
        }

        foreach (Match match in AuthenticationHeaderValuePattern().Matches(content))
        {
            if (!IsInertPlaceholder(match.Groups["token"].Value))
            {
                _ = violationLines.Add(LineNumber(content, match.Groups["token"].Index));
            }
        }

        foreach (Match match in CredentialUriPattern().Matches(content))
        {
            if (!IsInertPlaceholder(match.Groups["password"].Value))
            {
                _ = violationLines.Add(LineNumber(content, match.Groups["password"].Index));
            }
        }

        AddDecodedJwtViolations(content, violationLines);

        foreach (Match match in SecretAssignmentPattern().Matches(content))
        {
            string value = GetAssignmentValue(match);
            if (IsUsableLiteral(
                relativePath,
                match.Groups["name"].Value,
                value,
                match.Groups["bare"].Success
                    || match.Groups["expression"].Success
                    || match.Groups["call"].Success,
                IsInsideCSharpString(relativePath, content, match.Groups["value"].Index),
                GetEmbeddedSourceLanguage(relativePath, content, match.Groups["value"].Index)))
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
                    isInsideCSharpString: false,
                    embeddedSourceLanguage: null))
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

        return true;
    }

    private static bool IsExplicitlyGeneratedOrToolingContent(string path)
    {
        return path.EndsWith(".lscache", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(".playwright-cli/traces/", StringComparison.Ordinal)
            || path.Split('/').Any(static segment => segment is "bin" or "obj" or ".artifacts");
    }

    private static string GetAssignmentValue(Match match)
    {
        foreach (string groupName in new[] { "raw", "call", "double", "single", "expression", "bare" })
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
        bool isInsideCSharpString,
        string? embeddedSourceLanguage)
    {
        string candidate = value.Trim().Trim('"', '\'').TrimEnd(',', ';').Trim();
        if (IsInertPlaceholder(candidate))
        {
            return false;
        }

        string sourceCandidate = candidate.TrimEnd(')', '}', ']', ':').Trim();
        bool sourceSyntax = embeddedSourceLanguage is not null || IsSourceFile(relativePath);
        if (LiteralOnlyFunctionCallPattern().IsMatch(candidate))
        {
            return true;
        }

        if (IsInertPlaceholder(sourceCandidate)
            || (isBare && sourceSyntax && TypeAnnotationPattern().IsMatch(sourceCandidate))
            || IsWorkflowPermission(relativePath, name, sourceCandidate))
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
        if ((isBare || shellExpression)
            && (IsRecognizedSourceExpression(relativePath, candidate, embeddedSourceLanguage)
                || IsRecognizedSourceExpression(relativePath, sourceCandidate, embeddedSourceLanguage)))
        {
            return false;
        }

        return true;
    }

    private static bool IsSourceFile(string relativePath)
        => Path.GetExtension(relativePath).ToLowerInvariant()
            is ".cs" or ".fs" or ".vb" or ".ts" or ".tsx" or ".js" or ".jsx" or ".py" or ".java";

    private static bool IsInsideCSharpString(string relativePath, string content, int index)
    {
        if (!string.Equals(Path.GetExtension(relativePath), ".cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        bool inside = false;
        int rawDelimiterLength = 0;
        for (int position = 0; position < index; position++)
        {
            if (content[position] != '"')
            {
                continue;
            }

            int quoteCount = 1;
            while (position + quoteCount < index && content[position + quoteCount] == '"')
            {
                quoteCount++;
            }

            if (rawDelimiterLength > 0)
            {
                if (quoteCount >= rawDelimiterLength)
                {
                    rawDelimiterLength = 0;
                }

                position += quoteCount - 1;
                continue;
            }

            if (!inside && quoteCount >= 3)
            {
                rawDelimiterLength = quoteCount;
                position += quoteCount - 1;
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

            position += quoteCount - 1;
        }

        return inside || rawDelimiterLength > 0;
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
            || TemplateValuePattern().IsMatch(value)
            || ProtectedMarkerPattern().IsMatch(value)
            || value is "..." or "null" or "not-a-jwt" or "invalid-token" or "malformed" or "too-short"
                or "invalid-key" or "wrong-key" or "missing" or "invalid" or "true" or "false";

    private static bool IsRecognizedSourceExpression(
        string relativePath,
        string value,
        string? embeddedSourceLanguage)
    {
        string extension = embeddedSourceLanguage ?? Path.GetExtension(relativePath);
        if (value is "string.Empty" or "Array.Empty<string>()" or "default"
            || EnvironmentVariableReferencePattern().IsMatch(value)
            || GitHubExpressionPattern().IsMatch(value))
        {
            return true;
        }

        if (extension is ".cs" or "cs" or "csharp")
        {
            return CSharpSourceExpressionPattern().IsMatch(value);
        }

        if (extension is ".py" or "py" or "python")
        {
            return PythonSourceExpressionPattern().IsMatch(value);
        }

        if (extension is ".sh" or ".bash" or "sh" or "bash" or "shell")
        {
            return ShellSourceExpressionPattern().IsMatch(value);
        }

        if (extension is ".ts" or ".js" or "ts" or "js" or "typescript" or "javascript")
        {
            return JavaScriptSourceExpressionPattern().IsMatch(value);
        }

        return false;
    }

    private static bool IsWorkflowPermission(string relativePath, string name, string value)
        => string.Equals(name.Trim('"', '\''), "id-token", StringComparison.OrdinalIgnoreCase)
            && value is "read" or "write" or "none";

    private static string? GetEmbeddedSourceLanguage(string relativePath, string content, int index)
    {
        if (!string.Equals(Path.GetExtension(relativePath), ".md", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string prefix = content[..index];
        int fence = prefix.LastIndexOf("```", StringComparison.Ordinal);
        if (fence < 0)
        {
            return null;
        }

        int languageStart = fence + 3;
        int languageEnd = prefix.IndexOfAny(['\r', '\n'], languageStart);
        if (languageEnd < 0)
        {
            languageEnd = prefix.Length;
        }

        string language = prefix[languageStart..languageEnd].Trim().ToLowerInvariant();
        return language.Length > 0 ? language : null;
    }

    private static int LineNumber(string content, int index)
        => 1 + content.AsSpan(0, index).Count('\n');

    private static void AddDecodedJwtViolations(string content, ISet<int> violationLines)
    {
        if (!content.Contains("\"sub\"", StringComparison.OrdinalIgnoreCase)
            || !content.Contains("\"iss\"", StringComparison.OrdinalIgnoreCase)
            || !content.Contains("\"aud\"", StringComparison.OrdinalIgnoreCase)
            || !content.Contains("\"exp\"", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var objectStarts = new Stack<int>();
        bool insideString = false;
        bool escaped = false;
        for (int index = 0; index < content.Length; index++)
        {
            char character = content[index];
            if (insideString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    insideString = false;
                }

                continue;
            }

            if (character == '"')
            {
                insideString = true;
            }
            else if (character == '{')
            {
                objectStarts.Push(index);
            }
            else if (character == '}' && objectStarts.TryPop(out int start))
            {
                ReadOnlySpan<char> objectContent = content.AsSpan(start, index - start + 1);
                if (objectContent.IndexOf("\"sub\"", StringComparison.OrdinalIgnoreCase) < 0
                    || objectContent.IndexOf("\"iss\"", StringComparison.OrdinalIgnoreCase) < 0
                    || objectContent.IndexOf("\"aud\"", StringComparison.OrdinalIgnoreCase) < 0
                    || objectContent.IndexOf("\"exp\"", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                try
                {
                    using JsonDocument document = JsonDocument.Parse(content.AsMemory(start, index - start + 1));
                    if (IsDecodedJwtObject(document.RootElement))
                    {
                        _ = violationLines.Add(LineNumber(content, start));
                    }
                }
                catch (JsonException)
                {
                    // The balanced source span is not a standalone JSON object.
                }
            }
        }
    }

    private static bool IsDecodedJwtObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        bool subject = false;
        bool issuer = false;
        bool audience = false;
        bool expiration = false;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            subject |= property.NameEquals("sub");
            issuer |= property.NameEquals("iss");
            audience |= property.NameEquals("aud");
            expiration |= property.NameEquals("exp");
        }

        return subject && issuer && audience && expiration;
    }

    private static string? DecodeSupportedText(ReadOnlySpan<byte> bytes)
    {
        string decoded;
        if (bytes.StartsWith(Encoding.UTF8.Preamble))
        {
            decoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: false)
                .GetString(bytes[Encoding.UTF8.Preamble.Length..]);
        }
        else if (bytes.StartsWith(Encoding.Unicode.Preamble))
        {
            decoded = new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: false)
                .GetString(bytes[Encoding.Unicode.Preamble.Length..]);
        }
        else if (bytes.StartsWith(Encoding.BigEndianUnicode.Preamble))
        {
            decoded = new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: false)
                .GetString(bytes[Encoding.BigEndianUnicode.Preamble.Length..]);
        }
        else if (LooksLikeUtf16(bytes, nullByteParity: 1))
        {
            decoded = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: false)
                .GetString(bytes);
        }
        else if (LooksLikeUtf16(bytes, nullByteParity: 0))
        {
            decoded = new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: false)
                .GetString(bytes);
        }
        else
        {
            decoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
                .GetString(bytes);
        }

        int malformed = decoded.Count(static character => character == '\uFFFD'
            || character == '\0'
            || (char.IsControl(character) && character is not '\r' and not '\n' and not '\t' and not '\f'));
        if (malformed > Math.Max(32, decoded.Length / 5))
        {
            return null;
        }

        return string.Concat(decoded.Select(static character => character == '\0'
            || (char.IsControl(character) && character is not '\r' and not '\n' and not '\t' and not '\f')
                ? '\n'
                : character));
    }

    private static bool LooksLikeUtf16(ReadOnlySpan<byte> bytes, int nullByteParity)
    {
        if (bytes.Length < 4 || (bytes.Length & 1) != 0)
        {
            return false;
        }

        int pairs = Math.Min(bytes.Length / 2, 512);
        int nullBytes = 0;
        for (int pair = 0; pair < pairs; pair++)
        {
            if (bytes[(pair * 2) + nullByteParity] == 0)
            {
                nullBytes++;
            }
        }

        return nullBytes >= Math.Max(2, pairs / 2);
    }

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{16,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CompactJwtPattern();

    [GeneratedRegex(@"-----BEGIN (?<label>[A-Z0-9][A-Z0-9 ]*PRIVATE KEY)-----\s+[A-Za-z0-9+/=\r\n]{16,}\s+-----END \k<label>-----", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateKeyPattern();

    [GeneratedRegex(
        @"\bAuthorization\b[\""']?\s*\]?\s*(?::|=)\s*(?:[A-Za-z_][A-Za-z0-9_.]*\()?\s*[\""'`]?\s*Bearer\s+(?<token>(?:[!#$%&'*+.^_`|~A-Za-z0-9-][!#$%&'*+.^_`|~A-Za-z0-9=-]*|<[A-Za-z][A-Za-z0-9_.-]*>))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(
        @"^[\t ]*[\""'`]?(?:Bearer)[\t ]+(?<token>(?:[!#$%&'*+.^_`|~A-Za-z0-9-][!#$%&'*+.^_`|~A-Za-z0-9=-]*|<[A-Za-z][A-Za-z0-9_.-]*>))[\""'`]?[\t ]*[,;)]?[\t ]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex StandaloneBearerTokenPattern();

    [GeneratedRegex(
        @"AuthenticationHeaderValue\s*\(\s*[\""']Bearer[\""']\s*,\s*[\""'](?<token>[!#$%&'*+.^_`|~A-Za-z0-9-][!#$%&'*+.^_`|~A-Za-z0-9=-]*)[\""']\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthenticationHeaderValuePattern();

    [GeneratedRegex(
        @"\b[A-Za-z][A-Za-z0-9+.-]*://(?<username>[^\s/@:]+):(?<password>[^\s/@]+)@[^\s/]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex CredentialUriPattern();

    [GeneratedRegex(
        @"(?:\[\s*)?[\""']?(?<name>(?:(?:[A-Za-z0-9_.-]+):)*[A-Za-z0-9_.-]*(?:signing[-_]?key|passwords?|passphrase|client[-_]?secret|auth[-_]?secret|api[-_]?key|secret[-_]?access[-_]?key|bearer[-_]?token|access[-_]?token|refresh[-_]?token|id[-_]?token|jwt[-_]?token|private[-_]?key|credentials?|connection[-_]?string|secret|token|[A-Za-z0-9_.-]+(?:key|token|password|credentials?)))[\""']?(?:\s*\])?\s*(?::(?![-+?])|=(?![=>]))\s*(?<value>(?:(?<rawquotes>\""{3,})(?<raw>[\s\S]*?)\k<rawquotes>|(?<call>[A-Za-z_$][A-Za-z0-9_$.]*\([^\r\n)]*\)!?)|@?\""(?<double>(?:\\.|[^\""\r\n])*)\""|'(?<single>[^'\r\n]*)'|(?<expression>\$\{\{[^\r\n}]+\}\})|(?<bare>[^\s,;#`\""']+)))",
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

    [GeneratedRegex(@"^(?:\$\{\{\s*(?:secrets|vars)\.[A-Za-z_][A-Za-z0-9_]*\s*\}\}|\{\{[^{}\r\n]+\}\}|@Microsoft\.KeyVault\(SecretUri=[^\r\n]+\)|(?:secret|keyvault)ref:[A-Za-z0-9_.:/<>-]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourcePlaceholderPattern();

    [GeneratedRegex(@"^\{[A-Za-z_][A-Za-z0-9_.-]*\}$", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateValuePattern();

    [GeneratedRegex(@"^PROTECTED_[A-Z0-9_]+_MARKER(?:_[A-Z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ProtectedMarkerPattern();

    [GeneratedRegex(@"^\$(?:\{)?[A-Za-z_][A-Za-z0-9_]*(?:\})?$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentVariableReferencePattern();

    [GeneratedRegex(@"^\$\{\{\s*(?:secrets|vars)\.[A-Za-z_][A-Za-z0-9_]*\s*\}\}$", RegexOptions.CultureInvariant)]
    private static partial Regex GitHubExpressionPattern();

    [GeneratedRegex(
        @"^(?:\$|\(\)\s*=>\s*[^\r\n]+|[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*(?:\[[^\]\r\n]+\])?|(?:new\s+)?[A-Za-z_][A-Za-z0-9_.<>]*\([^\r\n;]*\)?|[A-Za-z_][A-Za-z0-9_]*\?*\.Value)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CSharpSourceExpressionPattern();

    [GeneratedRegex(@"^(?:os\.environ(?:\.get)?\([^\r\n]*\)|[A-Za-z_][A-Za-z0-9_.]*\([^\r\n]*\))$", RegexOptions.CultureInvariant)]
    private static partial Regex PythonSourceExpressionPattern();

    [GeneratedRegex(@"^(?:string|number|boolean|any|unknown|process\.env\.[A-Za-z_][A-Za-z0-9_]*!?|[A-Za-z_$][A-Za-z0-9_$.]*(?:\([^\r\n]*\))?)$", RegexOptions.CultureInvariant)]
    private static partial Regex JavaScriptSourceExpressionPattern();

    [GeneratedRegex(@"^(?:string|number|boolean|any|unknown)$", RegexOptions.CultureInvariant)]
    private static partial Regex TypeAnnotationPattern();

    [GeneratedRegex(
        @"^[A-Za-z_$][A-Za-z0-9_$.]*\(\s*(?:(?:\""(?:\\.|[^\""\r\n])*\""|'[^'\r\n]*'|[-+]?\d+(?:\.\d+)?|true|false|null)\s*(?:,\s*(?:\""(?:\\.|[^\""\r\n])*\""|'[^'\r\n]*'|[-+]?\d+(?:\.\d+)?|true|false|null)\s*)*)?\)!?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LiteralOnlyFunctionCallPattern();

    [GeneratedRegex(@"^(?:\$[A-Za-z_][A-Za-z0-9_]*|\$\{[A-Za-z_][A-Za-z0-9_]*(?::-)?\}|\$\((?:openssl\s+rand|head\s+-c|dd\s+if=)[^\r\n]*\))$", RegexOptions.CultureInvariant)]
    private static partial Regex ShellSourceExpressionPattern();
}

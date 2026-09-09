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
    private const string RetiredEvidenceManifest =
        "_bmad-output/implementation-artifacts/evidence/story-5-3-retired-captures.json";

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
            .SelectMany(path => ReadTrackedText(path) is { } content
                ? FindViolations(path, content)
                : [])
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.Length.ShouldBe(
            0,
            "Tracked reusable content contains secret material. Only path and line are reported: "
            + string.Join(", ", violations.Take(200)));
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

    [Fact]
    public void UsableLiteralAssignments_AreRejected()
    {
        string[] lines =
        [
            Assignment("Signing" + "Key", RandomSecret()),
            Assignment("Pass" + "word", RandomSecret()),
            ("Client" + "Secret") + ": " + RandomSecret(),
            Assignment("Pass" + "word", "x"),
            Assignment("Pass" + "word", "invalid-key"),
            Assignment("Access" + "Token", "fake-token"),
            Assignment("Access" + "Token", "cached"),
            Assignment("Access" + "Token", "tok123"),
            Assignment("Access" + "Token", "[" + RandomSecret() + "]"),
        ];

        foreach (string line in lines)
        {
            FindViolations("samples/fixture.json", line).ShouldBe(["samples/fixture.json:1"]);
        }
    }

    [Fact]
    public void AssignmentSeparatedFromValueByNewline_IsRejected()
        => FindViolations(
            ".github/workflows/example.yml",
            ("pass" + "word") + ":\n  " + RandomSecret())
            .ShouldBe([".github/workflows/example.yml:2"]);

    [Fact]
    public void DecodedPrivilegedJwtPayload_IsRejected()
        => FindViolations("docs/token.json", CreateDecodedJwtPayload(nested: false))
            .ShouldBe(["docs/token.json:1"]);

    [Fact]
    public void CredentialAssignmentEmbeddedInCSharpString_IsRejected()
        => FindViolations(
            "tests/fixture.cs",
            "string fixture = \"" + Assignment("Pass" + "word", "short") + "\";")
            .ShouldBe(["tests/fixture.cs:1"]);

    [Fact]
    public void StandaloneAndAuthenticationHeaderBearerCredentials_AreRejected()
    {
        string[] candidateValues =
        [
            "~" + RandomSecret(),
            new string(['a']),
            new string(['a', 'b', 'c', '1', '2', '3']),
            new string(['t', 'o', 'k', 'e', 'n']),
            new string(['m', 'o', 'c', 'k', '-', 't', 'o', 'k', 'e', 'n']),
            new string(['!']),
            new string(['+', '-', '.', '_', '~']),
            "/" + RandomSecret().TrimEnd('=') + "==",
            new string(['n', 'u', 'l', 'l']),
        ];

        for (int candidateIndex = 0; candidateIndex < candidateValues.Length; candidateIndex++)
        {
            string candidateValue = candidateValues[candidateIndex];
            string[] fixtures =
            [
                "\"" + "Bearer " + candidateValue + "\"",
                "new AuthenticationHeaderValue(\"" + "Bearer\", \"" + candidateValue + "\")",
            ];

            foreach (string fixture in fixtures)
            {
                FindViolations("tests/bearer.cs", fixture).ShouldBe(
                    ["tests/bearer.cs:1"],
                    $"Candidate category {candidateIndex} must not bypass bearer detection.");
            }
        }
    }

    [Fact]
    public void GenericAndSuffixedCredentialNames_AreRejected()
    {
        string[] names =
        [
            "TO" + "KEN",
            "GH_" + "TOKEN",
            "EVENTSTORE_ADMIN_" + "TOKEN",
            "Account" + "Key",
            "Storage" + "Key",
            "database-credentials",
            "backup-password",
        ];

        foreach (string name in names)
        {
            FindViolations(".github/workflows/example.yml", Assignment(name, RandomSecret()))
                .ShouldBe([".github/workflows/example.yml:1"]);
        }
    }

    [Fact]
    public void LiteralOnlyCallAndCredentialUri_AreRejected()
    {
        string literalCall = Assignment(
            "Signing" + "Key",
            "Convert.FromBase64String(\"" + RandomSecret() + "\")");
        string helperLiteralCall = Assignment(
            "Client" + "Secret",
            "_decoder.Decode(\"" + RandomSecret() + "\")");
        string pythonLiteralCall = Assignment(
            "api_" + "token",
            "decode(\"" + RandomSecret() + "\")");
        string credentialUri = "postgresql://runtime-user:" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24))
            + "@database.example.test/store";

        FindViolations("tests/fixture.cs", literalCall).ShouldBe(["tests/fixture.cs:1"]);
        FindViolations("tests/fixture.cs", helperLiteralCall).ShouldBe(["tests/fixture.cs:1"]);
        FindViolations("tools/fixture.py", pythonLiteralCall).ShouldBe(["tools/fixture.py:1"]);
        FindViolations("docs/database.md", credentialUri).ShouldBe(["docs/database.md:1"]);
    }

    [Fact]
    public void NestedLargeDecodedJwtPayload_IsRejectedWithoutSizeLimit()
    {
        string payload = CreateDecodedJwtPayload(nested: true, padding: new string('x', 4096));

        FindViolations("docs/token.json", payload).ShouldBe(["docs/token.json:1"]);
    }

    [Fact]
    public void ArbitraryCSharpRawStringDelimiter_DoesNotHideCredentialAssignment()
    {
        string delimiter = new('"', 4);
        string content = delimiter + "\n" + Assignment("Pass" + "word", RandomSecret()) + "\n" + delimiter;

        FindViolations("tests/fixture.cs", content).ShouldBe(["tests/fixture.cs:2"]);
    }

    [Fact]
    public void RecoverableTextAfterMalformedByte_IsStillScanned()
    {
        byte[] prefix = Encoding.UTF8.GetBytes("plain text\n");
        byte[] suffix = Encoding.UTF8.GetBytes("\n" + Assignment("TO" + "KEN", RandomSecret()));
        byte[] malformed = [.. prefix, 0xff, .. suffix];

        string? recovered = DecodeTrackedText("docs/malformed.txt", malformed);

        recovered.ShouldNotBeNull();
        FindViolations("docs/malformed.txt", recovered).ShouldBe(["docs/malformed.txt:3"]);
    }

    [Fact]
    public void RuntimeSourcesWithLiteralFallbacksOrOperands_AreRejected()
    {
        string literal = RandomSecret();
        ContainsUnsafeRuntimeLiteralOperand(
            "tools/fixture.ts",
            "process.env.API_TOKEN || \"" + literal + "\"").ShouldBeTrue();
        (string Path, string Content)[] fixtures =
        [
            ("tests/fixture.cs", Assignment("Signing" + "Key", "configuration[\"JWT_SIGNING_KEY\"] ?? \"" + literal + "\"")),
            ("tests/fixture.cs", Assignment("Signing" + "Key", "Resolve(configuration[\"JWT_SIGNING_KEY\"], \"" + literal + "\")")),
            ("tests/fixture.cs", Assignment("Signing" + "Key", "Resolve()")),
            ("tools/fixture.ts", Assignment("api_" + "token", "process.env.API_TOKEN || \"" + literal + "\"")),
            ("tools/fixture.ts", Assignment("api_" + "token", "process.env.API_TOKEN.replaceAll(/^[\\\"']|[\\\"']$/g, '') || \"" + literal + "\"")),
            ("tools/fixture-template.ts", Assignment("api_" + "token", "`${process.env.API_TOKEN}-" + literal + "`")),
            ("tools/fixture.py", Assignment("api_" + "token", "os.environ.get(\"API_TOKEN\", \"" + literal + "\")")),
            ("scripts/fixture.sh", Assignment("TO" + "KEN", "${RUNTIME_TOKEN:-" + literal + "}")),
            ("scripts/fixture.sh", Assignment("TO" + "KEN", "$(curl -u runtime-user:" + literal + " https://identity.example.test/token)")),
        ];

        Match javaScriptFallback = SecretAssignmentPattern().Match(fixtures[3].Content);
        javaScriptFallback.Success.ShouldBeTrue();
        string javaScriptExpression = ExtractSourceExpression(
            fixtures[3].Content,
            javaScriptFallback.Groups["value"].Index,
            stopAtNewline: false);
        ContainsUnsafeRuntimeLiteralOperand(fixtures[3].Path, javaScriptExpression).ShouldBeTrue();
        IsUsableLiteral(fixtures[3].Path, "api_token", javaScriptExpression, isBare: true).ShouldBeTrue();

        foreach ((string path, string content) in fixtures)
        {
            FindViolations(path, content).ShouldBe([$"{path}:1"]);
        }
    }

    [Fact]
    public void PaddedBearerSharedAccessKeyAndCredentialSemver_AreRejected()
    {
        string paddedBearer = "\"" + "Bearer " + RandomSecret().TrimEnd('=') + "==\"";
        FindViolations("docs/token.md", paddedBearer).ShouldBe(["docs/token.md:1"]);
        FindViolations(
            "deploy/configuration.yaml",
            Assignment("Shared" + "Access" + "Key", RandomSecret()))
            .ShouldBe(["deploy/configuration.yaml:1"]);
        FindViolations("package.json", Assignment("TO" + "KEN", "1.2.3"))
            .ShouldBe(["package.json:1"]);
    }

    [Fact]
    public void ArbitraryBracketedValues_AreNotPlaceholders()
    {
        string value = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        string[] candidates = ["<" + value + ">", "{" + value + "}", "{{" + value + "}}"];

        foreach (string candidate in candidates)
        {
            FindViolations("samples/fixture.json", Assignment("Client" + "Secret", candidate))
                .ShouldBe(["samples/fixture.json:1"]);
        }
    }

    [Fact]
    public void KnownTextPath_WithControlHeavyMalformedBytes_IsRecoveredAndScanned()
    {
        byte[] prefix = [0x01, 0x02, 0x03, 0xff, (byte)'\n'];
        byte[] suffix = Encoding.UTF8.GetBytes(Assignment("TO" + "KEN", RandomSecret()));

        string? recovered = DecodeTrackedText("docs/control-heavy.txt", [.. prefix, .. suffix]);

        recovered.ShouldNotBeNull();
        FindViolations("docs/control-heavy.txt", recovered).ShouldBe(["docs/control-heavy.txt:2"]);
    }

    [Theory]
    [InlineData("config/settings.toml")]
    [InlineData("views/Index.razor")]
    [InlineData("tools/config.cjs")]
    [InlineData("tools/config.mjs")]
    [InlineData("ui/component.tsx")]
    [InlineData("config/runtime")]
    [InlineData("fixtures/textual.dat")]
    public void TrackedTextExtensionsAndExtensionlessConfig_AreRecoveredAndScanned(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string content = Assignment("Client" + "Secret", "\"" + RandomSecret() + "\"");

        string? recovered = DecodeTrackedText(path, Encoding.UTF8.GetBytes(content));

        recovered.ShouldNotBeNull();
        FindViolations(path, recovered).ShouldBe([$"{path}:1"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BomlessUtf16TrackedText_IsRecoveredAndScanned(bool bigEndian)
    {
        string content = Assignment("Signing" + "Key", RandomSecret());
        byte[] bytes = new UnicodeEncoding(bigEndian, byteOrderMark: false).GetBytes(content);

        string? recovered = DecodeTrackedText("config/runtime.dat", bytes);

        recovered.ShouldNotBeNull();
        FindViolations("config/runtime.dat", recovered).ShouldBe(["config/runtime.dat:1"]);
    }

    [Fact]
    public void BinaryExclusion_RequiresAKnownContentSignature()
    {
        string content = Assignment("Pass" + "word", RandomSecret());

        DecodeTrackedText("image.png", Encoding.UTF8.GetBytes(content)).ShouldBe(content);
        DecodeTrackedText("image.png", [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])
            .ShouldBeNull();
    }

    [Theory]
    [InlineData(".playwright-cli/traces/session.json")]
    [InlineData(".playwright-cli/traces/page.html")]
    [InlineData(".playwright-cli/traces/style.css")]
    [InlineData(".playwright-cli/traces/app.js")]
    [InlineData(".playwright-cli/traces/output.txt")]
    [InlineData(".playwright-cli/traces/resource.dat")]
    public void DecodableTrackedTraceResources_AreRecoveredAndScanned(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string content = "\"" + "Bearer /" + RandomSecret().TrimEnd('=') + "==\"";

        IsExplicitGeneratedPath(path).ShouldBeFalse();
        string? recovered = DecodeTrackedText(path, Encoding.UTF8.GetBytes(content));

        recovered.ShouldBe(content);
        FindViolations(path, recovered).ShouldBe([$"{path}:1"]);
    }

    [Fact]
    public void TrackedTraceBinaryExclusion_RequiresAKnownContentSignature()
        => DecodeTrackedText(
            ".playwright-cli/traces/resource.dat",
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])
            .ShouldBeNull();

    [Fact]
    public void RuntimeDefaultsAndLiteralOperands_AreRejectedRegardlessOfLength()
    {
        string[] values =
        [
            "configuration[\"JWT_SIGNING_KEY\"] + \"x\"",
            "configuration.GetValue<string>(\"JWT_SIGNING_KEY\", \"x\")",
            "runtimeValue ?? \"x\"",
        ];

        foreach (string value in values)
        {
            FindViolations("tests/fixture.cs", Assignment("Signing" + "Key", value))
                .ShouldBe(["tests/fixture.cs:1"]);
        }
    }

    [Fact]
    public void AppHostTokenCommand_IsRecognizedAsARuntimeCredentialSource()
        => FindViolations(
            "scripts/fixture.sh",
            Assignment("TO" + "KEN", "$(aspire resource sample-api issue-smoke-token --apphost \"${APPHOST}\")"))
            .ShouldBeEmpty();

    [Fact]
    public void ConfigurationKeysAndBracketPlaceholders_RequireStructuralContextAndVocabulary()
    {
        string configurationPath = "Authentication__JwtBearer__SigningKey";
        FindViolations(
            "tests/fixture.cs",
            Assignment("Signing" + "KeyConfigurationKey", configurationPath)).ShouldBeEmpty();
        FindViolations(
            "tests/fixture.cs",
            Assignment("Signing" + "Key", configurationPath)).ShouldBe(["tests/fixture.cs:1"]);
        FindViolations(
            "config/fixture.json",
            Assignment("Pass" + "word", "<YOUR_PASSWORD>")).ShouldBeEmpty();
        FindViolations(
            "config/fixture.json",
            Assignment("Pass" + "word", "<arbitrarycredentialmaterial>"))
            .ShouldBe(["config/fixture.json:1"]);
    }

    [Fact]
    public void PackageVersionExemption_IsLimitedToDependencyObjects()
    {
        string name = "registry-auth-" + "token";
        string dependency = "{\"dependencies\":{\"" + name + "\":\"1.2.3\"}}";
        string topLevel = "{\"" + name + "\":\"1.2.3\"}";

        FindViolations("package.json", dependency).ShouldBeEmpty();
        FindViolations("package.json", topLevel).ShouldBe(["package.json:1"]);
    }

    [Fact]
    public void UriUserInfoColonKeysAndSuffixedNames_AreRejected()
    {
        string credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        string[] uriValues =
        [
            "postgresql://" + credential + "@database.example.test/store",
            "postgresql://:" + credential + "@database.example.test/store",
        ];
        foreach (string value in uriValues)
        {
            FindViolations("docs/database.md", value).ShouldBe(["docs/database.md:1"]);
        }

        string[] names = ["client" + "SecretValue", "pass" + "wordBytes", "api" + "TokenText"];
        foreach (string name in names)
        {
            FindViolations("config/runtime.json", Assignment(name, credential))
                .ShouldBe(["config/runtime.json:1"]);
        }

        string colonKey = "{\"auth:" + "password\":\"" + credential + "\"}";
        FindViolations("config/runtime.json", colonKey).ShouldBe(["config/runtime.json:1"]);
    }

    [Fact]
    public void XmlCredentialShapesAndJavaScriptDestructuringDefaults_AreRejected()
    {
        string credential = RandomSecret();
        string element = "<" + "Password>" + credential + "</" + "Password>";
        string keyName = "Signing" + "Key";
        string attribute = "<add key=\"" + keyName + "\" value=\"" + credential + "\" />";
        string destructuring = "const { " + "pass" + "word = \"x\" } = runtimeConfig;";

        FindViolations("config/runtime.xml", element).ShouldBe(["config/runtime.xml:1"]);
        FindViolations("config/runtime.xml", attribute).ShouldBe(["config/runtime.xml:1"]);
        FindViolations("tools/runtime.mjs", destructuring).ShouldBe(["tools/runtime.mjs:1"]);
    }

    [Fact]
    public void MinimalJwtAndInlineCredentialConstructors_AreRejected()
    {
        string payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["iss"] = "issuer",
            ["aud"] = "audience",
            ["exp"] = 9999999999,
        });
        FindViolations("docs/token.json", payload).ShouldBe(["docs/token.json:1"]);

        string compact = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\"}"))
            + "." + Base64Url(Encoding.UTF8.GetBytes(payload))
            + "." + Base64Url(RandomNumberGenerator.GetBytes(16));
        FindViolations("docs/token.txt", compact).ShouldBe(["docs/token.txt:1"]);

        string networkFixture = "new Network" + "Credential(\"runtime-user\", \"x\")";
        string symmetricFixture = "new Symmetric" + "SecurityKey(Encoding.UTF8.GetBytes(\"x\"))";
        FindViolations("tests/fixture.cs", networkFixture).ShouldBe(["tests/fixture.cs:1"]);
        FindViolations("tests/fixture.cs", symmetricFixture).ShouldBe(["tests/fixture.cs:1"]);
    }

    [Fact]
    public void RetirementAndGeneratedExemptions_AreConstrainedToGovernedPaths()
    {
        IsGovernedRetirementPath(
            "_bmad-output/implementation-artifacts/evidence/story-3-14/"
            + new string('a', 40)
            + "/successful/builds/"
            + new string('b', 40)
            + "/Github/capture.py").ShouldBeTrue();
        IsGovernedRetirementPath("tools/credential-capture.py").ShouldBeFalse();
        IsGovernedRetirementPath("_bmad-output/implementation-artifacts/evidence/arbitrary.txt").ShouldBeFalse();

        IsExplicitGeneratedPath(
            "src/Hexalith.EventStore.Admin.UI/.artifacts/ui-test-obj/project.assets.json")
            .ShouldBeTrue();
        IsExplicitGeneratedPath("tools/bin/credential-generator.cs").ShouldBeFalse();
        IsExplicitGeneratedPath("tools/obj/credential-generator.cs").ShouldBeFalse();
        IsExplicitGeneratedPath("tools/.artifacts/credential-generator.cs").ShouldBeFalse();
    }

    private static IEnumerable<string> FindViolations(string relativePath)
    {
        string content = File.ReadAllText(Path.Combine(RepoRoot, relativePath));
        return FindViolations(relativePath, content);
    }

    private static IEnumerable<string> FindViolations(string relativePath, string content)
    {
        var violationLines = new SortedSet<int>();
        void Record(int index, string category)
        {
            _ = violationLines.Add(LineNumber(content, index));
            if (Environment.GetEnvironmentVariable("HEXALITH_SCANNER_CATEGORY_DIAGNOSTIC") == "1"
                && relativePath.StartsWith(".playwright-cli/traces/", StringComparison.Ordinal))
            {
                string categoryName = category.Split(':')[0];
                string safeDetail = string.Empty;
                if (category.StartsWith("assignment:", StringComparison.Ordinal))
                {
                    int separator = category.IndexOf('=', StringComparison.Ordinal);
                    string assignmentName = separator > 11 ? category[11..separator] : "unknown";
                    string assignmentValue = separator >= 0 ? category[(separator + 1)..] : string.Empty;
                    safeDetail = $":name={assignmentName}:value-length={assignmentValue.Length}:bare-identifier={SourceIdentifierPattern().IsMatch(assignmentValue)}";
                }

                Console.Error.WriteLine($"{relativePath}:{LineNumber(content, index)}:{categoryName}{safeDetail}");
            }
        }

        foreach (Match match in CompactJwtPattern().Matches(content))
        {
            if (IsCompactJwt(match.Value))
            {
                Record(match.Index, "compact-jwt");
            }
        }

        foreach (Match match in PrivateKeyPattern().Matches(content))
        {
            Record(match.Index, "private-key");
        }

        foreach (int index in FindDecodedJwtPayloadIndexes(content))
        {
            Record(index, "decoded-jwt");
        }

        foreach (Match match in BearerCredentialPattern().Matches(content))
        {
            if (IsStructuredBearerValue(content, match)
                && !IsBearerChallenge(content, match)
                && !IsBearerSourceExpression(content, match)
                && !IsInertPlaceholder(match.Groups["token"].Value))
            {
                Record(match.Groups["token"].Index, $"bearer:{match.Groups["token"].Value}");
            }
        }

        foreach (Match match in AuthenticationHeaderValuePattern().Matches(content))
        {
            string value = GetMatchedValue(match);
            if (match.Groups["bare"].Success && SourceIdentifierPattern().IsMatch(value))
            {
                continue;
            }

            if (IsUsableLiteral(relativePath, "token", value, match.Groups["bare"].Success))
            {
                Record(match.Groups["value"].Index, "auth-header");
            }
        }

        foreach (Match match in CredentialConstructorPattern().Matches(content))
        {
            string invocation = ExtractBalancedInvocation(content, match.Index, match.Length);
            foreach ((int literalIndex, _, string literal) in EnumerateQuotedLiterals(
                invocation,
                includeBackticks: false))
            {
                if (!IsInertPlaceholder(literal)
                    && !IsRuntimeSourceLookupLiteral(invocation, literalIndex))
                {
                    Record(match.Index + literalIndex, "credential-constructor");
                    break;
                }
            }
        }

        foreach (Match match in CredentialUriPattern().Matches(content))
        {
            string userInfo = match.Groups["userinfo"].Value;
            string effectivePath = GetEffectiveSourcePath(relativePath, content, match.Groups["userinfo"].Index);
            if (!IsInertUriUserInfo(userInfo)
                && !(effectivePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    && ContainsRuntimeCSharpInterpolation(userInfo)))
            {
                Record(match.Groups["userinfo"].Index, "credential-uri");
            }
        }

        foreach (Match match in XmlSecretElementPattern().Matches(content))
        {
            string name = match.Groups["name"].Value;
            string value = match.Groups["value"].Value.Trim();
            if (IsCredentialName(name) && IsUsableLiteral(relativePath, name, value, isBare: false))
            {
                Record(match.Groups["value"].Index, "xml-secret-element");
            }
        }

        foreach (Match match in XmlKeyValueSecretPattern().Matches(content))
        {
            string name = match.Groups["name"].Value;
            string value = match.Groups["value"].Value.Trim();
            if (IsCredentialName(name) && IsUsableLiteral(relativePath, name, value, isBare: false))
            {
                Record(match.Groups["value"].Index, "xml-secret-attribute");
            }
        }

        foreach (Match match in CurlUserCredentialPattern().Matches(content))
        {
            string userInfo = match.Groups["userinfo"].Value;
            int separator = userInfo.IndexOf(':');
            if (separator >= 0)
            {
                string password = userInfo[(separator + 1)..];
                if (!IsInertPlaceholder(password)
                    && !EnvironmentVariableReferencePattern().IsMatch(password))
                {
                    Record(match.Groups["userinfo"].Index + separator + 1, "curl-user-credential");
                }
            }
        }

        foreach (Match match in SecretAssignmentPattern().Matches(content))
        {
            if (!IsCredentialName(match.Groups["name"].Value))
            {
                continue;
            }

            ReadOnlySpan<char> assignmentSeparator = content.AsSpan(
                match.Groups["name"].Index + match.Groups["name"].Length,
                match.Groups["value"].Index - match.Groups["name"].Index - match.Groups["name"].Length);
            bool usesEquals = assignmentSeparator.Contains('=');
            if (!usesEquals && !HasStructuralColonPrefix(content, match.Groups["name"].Index))
            {
                continue;
            }

            string effectivePath = GetEffectiveSourcePath(relativePath, content, match.Groups["name"].Index);
            string value = GetAssignmentValue(match);
            if (IsArgparseMetavar(content, match.Groups["name"].Index)
                || IsJavaScriptDestructuringAlias(effectivePath, content, match.Groups["name"].Index)
                || IsPackageDependencyMetadata(
                    effectivePath,
                    content,
                    match.Groups["name"].Index,
                    match.Groups["name"].Value,
                    value))
            {
                continue;
            }

            if (!usesEquals
                && value == "-"
                && IsYamlSequenceMarker(content, match.Groups["value"].Index))
            {
                continue;
            }

            if (content.AsSpan(match.Groups["value"].Index).StartsWith("${{"))
            {
                int expressionEnd = content.IndexOf("}}", match.Groups["value"].Index, StringComparison.Ordinal);
                if (expressionEnd >= 0)
                {
                    value = content[match.Groups["value"].Index..(expressionEnd + 2)];
                }
            }

            bool isBare = match.Groups["bare"].Success;
            bool isCSharp = effectivePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || effectivePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);
            bool isPython = effectivePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase);
            string effectiveExtension = Path.GetExtension(effectivePath);
            bool isJavaScript = effectiveExtension.Equals(".js", StringComparison.OrdinalIgnoreCase)
                || effectiveExtension.Equals(".cjs", StringComparison.OrdinalIgnoreCase)
                || effectiveExtension.Equals(".mjs", StringComparison.OrdinalIgnoreCase)
                || effectiveExtension.Equals(".ts", StringComparison.OrdinalIgnoreCase)
                || effectiveExtension.Equals(".tsx", StringComparison.OrdinalIgnoreCase);
            bool isShell = effectiveExtension.Equals(".sh", StringComparison.OrdinalIgnoreCase)
                || effectiveExtension.Equals(".bash", StringComparison.OrdinalIgnoreCase)
                || effectiveExtension.Equals(".ps1", StringComparison.OrdinalIgnoreCase);
            bool isCSharpConfigurationIndexer = isCSharp
                && IsCSharpConfigurationIndexerAssignment(content, match.Groups["name"]);
            bool isInsideCSharpString = isCSharp
                && !isCSharpConfigurationIndexer
                && IsInsideCSharpString(content, match.Groups["name"].Index);
            bool isInsideJavaScriptOrPythonString = (isJavaScript || isPython)
                && IsInsideCSharpString(content, match.Groups["name"].Index);
            if ((isCSharp || isPython)
                && !isInsideCSharpString
                && !usesEquals)
            {
                continue;
            }

            if (isInsideCSharpString)
            {
                value = ExtractEmbeddedAssignmentValue(
                    content,
                    match.Groups["name"].Index + match.Groups["name"].Length,
                    match.Groups["value"].Index);
                isBare = false;
            }
            else if (!isBare
                && !match.Groups["template"].Success
                && (isCSharp || isJavaScript || isPython)
                && HasExpressionContinuation(content, match.Groups["value"]))
            {
                value = ExtractSourceExpression(
                    content,
                    Math.Max(0, match.Groups["value"].Index - 1),
                    isPython);
                isBare = true;
            }
            else if ((isBare || match.Groups["template"].Success)
                && !isInsideJavaScriptOrPythonString
                && (isCSharp || isPython || isJavaScript)
                || (isShell && isBare
                    && (value.StartsWith("$(", StringComparison.Ordinal) || value.StartsWith('('))))
            {
                value = ExtractSourceExpression(content, match.Groups["value"].Index, isPython);
                isBare = true;
            }

            if (isShell
                && usesEquals
                && match.Groups["value"].Index > 0
                && match.Groups["name"].Index > 0
                && content[match.Groups["value"].Index] is '"' or '\''
                && content[match.Groups["value"].Index] == content[match.Groups["name"].Index - 1])
            {
                // A quoted shell argument such as "SETTING=" has no value. The quote after
                // the equals sign closes the argument; it does not begin a multiline literal.
                value = string.Empty;
                isBare = false;
            }

            if (IsUsableLiteral(
                effectivePath,
                match.Groups["name"].Value,
                value,
                isBare))
            {
                Record(match.Groups["value"].Index, $"assignment:{match.Groups["name"].Value}={value}");
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
                    isBare: false))
                {
                    Record(match.Groups["value"].Index, "realm-password");
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

    private static string? ReadTrackedText(string path)
    {
        if (IsRetiredEvidenceCapture(path) || IsExplicitGeneratedPath(path))
        {
            return null;
        }

        string fullPath = Path.Combine(RepoRoot, path);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return DecodeTrackedText(path, File.ReadAllBytes(fullPath));
    }

    private static string? DecodeTrackedText(string path, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        if (HasKnownBinarySignature(bytes))
        {
            return null;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe)
        {
            return new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: false)
                .GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff)
        {
            return new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: false)
                .GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
        {
            return ReplaceControlCharacters(
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: false)
                    .GetString(bytes, 3, bytes.Length - 3));
        }

        if (TryDecodeBomlessUtf16(bytes, out string? utf16Text))
        {
            return ReplaceControlCharacters(utf16Text);
        }

        try
        {
            return ReplaceControlCharacters(
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                    .GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            string recovered = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
                .GetString(bytes);
            return ReplaceControlCharacters(recovered);
        }
    }

    private static bool IsRetiredEvidenceCapture(string path)
    {
        string manifestPath = Path.Combine(RepoRoot, RetiredEvidenceManifest);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        foreach (JsonElement retired in document.RootElement.GetProperty("retired").EnumerateArray())
        {
            if (!string.Equals(retired.GetProperty("path").GetString(), path, StringComparison.Ordinal))
            {
                continue;
            }

            IsGovernedRetirementPath(path).ShouldBeTrue(
                "Only governed historical evidence or explicitly owner-approved proof packets may be retired from scanning.");
            retired.GetProperty("status").GetString().ShouldBe("retired");
            retired.GetProperty("authoritative").GetBoolean().ShouldBeFalse();
            retired.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
            string expectedHash = retired.GetProperty("sha256").GetString()!;
            expectedHash.ShouldMatch("^[0-9a-f]{64}$");
            string actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(RepoRoot, path))))
                .ToLowerInvariant();
            actualHash.ShouldBe(expectedHash, "Retired evidence must remain byte-preserved.");
            return true;
        }

        return false;
    }

    private static bool IsGovernedRetirementPath(string path)
        => GovernedEvidenceCapturePathPattern().IsMatch(path)
            || OwnerApprovedProofPacketPathPattern().IsMatch(path);

    private static bool HasKnownBinarySignature(ReadOnlySpan<byte> bytes)
    {
        return bytes.StartsWith("MZ"u8)
            || bytes.StartsWith("BSJB"u8)
            || bytes.StartsWith("Microsoft C/C++ MSF"u8)
            || bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a })
            || bytes.StartsWith(new byte[] { 0xff, 0xd8, 0xff })
            || bytes.StartsWith("GIF87a"u8)
            || bytes.StartsWith("GIF89a"u8)
            || bytes.StartsWith(new byte[] { 0x00, 0x00, 0x01, 0x00 })
            || bytes.StartsWith("%PDF-"u8)
            || bytes.StartsWith(new byte[] { 0x50, 0x4b, 0x03, 0x04 })
            || bytes.StartsWith(new byte[] { 0x50, 0x4b, 0x05, 0x06 })
            || bytes.StartsWith(new byte[] { 0x50, 0x4b, 0x07, 0x08 })
            || bytes.StartsWith(new byte[] { 0x1f, 0x8b })
            || bytes.Length >= 262 && bytes[257..].StartsWith("ustar"u8);
    }

    private static bool IsExplicitGeneratedPath(string path)
        => ExplicitUiTestArtifactPathPattern().IsMatch(path);

    private static bool TryDecodeBomlessUtf16(byte[] bytes, out string text)
    {
        text = string.Empty;
        if (bytes.Length < 8 || bytes.Length % 2 != 0)
        {
            return false;
        }

        int evenNulls = 0;
        int oddNulls = 0;
        for (int index = 0; index < bytes.Length; index += 2)
        {
            evenNulls += bytes[index] == 0 ? 1 : 0;
            oddNulls += bytes[index + 1] == 0 ? 1 : 0;
        }

        int pairs = bytes.Length / 2;
        bool littleEndian = oddNulls >= Math.Max(3, pairs / 3) && evenNulls <= pairs / 10;
        bool bigEndian = evenNulls >= Math.Max(3, pairs / 3) && oddNulls <= pairs / 10;
        if (!littleEndian && !bigEndian)
        {
            return false;
        }

        text = new UnicodeEncoding(bigEndian, byteOrderMark: false, throwOnInvalidBytes: false)
            .GetString(bytes);
        return true;
    }

    private static string ReplaceControlCharacters(string content)
        => new(content.Select(static character => character < 0x20 && character is not '\r' and not '\n' and not '\t'
            ? '\ufffd'
            : character).ToArray());

    private static string GetAssignmentValue(Match match)
        => GetMatchedValue(match).Trim();

    private static string GetMatchedValue(Match match)
    {
        foreach (string groupName in new[] { "double", "single", "template", "bare" })
        {
            Group group = match.Groups[groupName];
            if (group.Success)
            {
                return group.Value;
            }
        }

        return string.Empty;
    }

    private static bool IsUsableLiteral(
        string relativePath,
        string name,
        string value,
        bool isBare)
    {
        string candidate = value.Trim().TrimEnd(',', ';').Trim();
        if (candidate.Length >= 2
            && candidate[0] is '"' or '\''
            && candidate[^1] == candidate[0])
        {
            candidate = candidate[1..^1];
        }
        if (IsInertPlaceholder(candidate))
        {
            return false;
        }

        if (IsConfigurationKeyReference(name, candidate))
        {
            return false;
        }

        if (ConfigurationKeyPattern().IsMatch(candidate))
        {
            return true;
        }

        if (isBare && IsCredentialFactoryExpression(relativePath, candidate))
        {
            return ContainsDirectLiteralReturn(candidate);
        }

        if (isBare && IsStructuralNonValueLiteral(relativePath, candidate))
        {
            return false;
        }

        if (string.Equals(
                Regex.Replace(name, "[^A-Za-z0-9]", string.Empty, RegexOptions.CultureInvariant),
                "idtoken",
                StringComparison.OrdinalIgnoreCase)
            && candidate is "read" or "write" or "none")
        {
            return false;
        }

        if (isBare
            && (relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || relativePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)))
        {
            return ContainsUnsafeRuntimeLiteralOperand(relativePath, candidate)
                || !IsRecognizedSourceExpression(relativePath, name, candidate);
        }

        if ((relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || relativePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            && ContainsRuntimeCSharpInterpolation(candidate))
        {
            return false;
        }

        if (IsConnectionStringName(name))
        {
            Match uri = CredentialUriPattern().Match(candidate);
            if (uri.Success)
            {
                return !IsInertPlaceholder(uri.Groups["password"].Value);
            }

            Match password = ConnectionStringPasswordPattern().Match(candidate);
            return password.Success && !IsInertPlaceholder(password.Groups["password"].Value);
        }

        bool shellExpression = string.Equals(Path.GetExtension(relativePath), ".sh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(relativePath), ".bash", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(relativePath), ".ps1", StringComparison.OrdinalIgnoreCase);
        return !((isBare || shellExpression)
            && !ContainsUnsafeRuntimeLiteralOperand(relativePath, candidate)
            && IsRecognizedSourceExpression(relativePath, name, candidate));
    }

    private static bool IsStructuralNonValueLiteral(string relativePath, string value)
    {
        string extension = Path.GetExtension(relativePath);
        bool supportsNonValueLiteral = extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".razor", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cjs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mjs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ts", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase);
        return supportsNonValueLiteral && value is "null" or "true" or "false";
    }

    private static bool IsArgparseMetavar(string content, int nameIndex)
    {
        int prefixStart = Math.Max(0, nameIndex - 16);
        return content.AsSpan(prefixStart, nameIndex - prefixStart)
            .EndsWith("metavar=\"", StringComparison.Ordinal);
    }

    private static bool HasExpressionContinuation(string content, Group valueGroup)
    {
        int cursor = valueGroup.Index + valueGroup.Length + 1;
        while (cursor < content.Length && content[cursor] is ' ' or '\t')
        {
            cursor++;
        }

        return cursor < content.Length && content[cursor] is '+' or '?' or '|';
    }

    private static bool IsCSharpConfigurationIndexerAssignment(string content, Group nameGroup)
    {
        int cursor = nameGroup.Index + nameGroup.Length;
        if (cursor >= content.Length || content[cursor] is not ('"' or '\''))
        {
            return false;
        }

        cursor++;
        while (cursor < content.Length && content[cursor] is ' ' or '\t')
        {
            cursor++;
        }

        if (cursor >= content.Length || content[cursor] != ']')
        {
            return false;
        }

        cursor++;
        while (cursor < content.Length && content[cursor] is ' ' or '\t')
        {
            cursor++;
        }

        return cursor < content.Length && content[cursor] == '=';
    }

    private static bool IsPackageDependencyMetadata(
        string path,
        string content,
        int nameIndex,
        string name,
        string value)
    {
        if (Path.GetFileName(path) is not ("package.json" or "package-lock.json")
            || !PackageVersionRangePattern().IsMatch(value))
        {
            return false;
        }

        try
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(content);
            long targetByteIndex = Encoding.UTF8.GetByteCount(content.AsSpan(0, nameIndex));
            var reader = new Utf8JsonReader(
                utf8,
                new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var contexts = new Stack<string?>();
            string? pendingProperty = null;
            while (reader.Read())
            {
                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    contexts.Push(pendingProperty);
                    pendingProperty = null;
                }
                else if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
                {
                    _ = contexts.Pop();
                    pendingProperty = null;
                }
                else if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString() ?? string.Empty;
                    if (targetByteIndex >= reader.TokenStartIndex
                        && targetByteIndex <= reader.BytesConsumed
                        && string.Equals(propertyName, name, StringComparison.Ordinal))
                    {
                        return contexts.TryPeek(out string? context)
                            && context is "dependencies" or "devDependencies" or "peerDependencies" or "optionalDependencies";
                    }

                    pendingProperty = propertyName;
                }
                else
                {
                    pendingProperty = null;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool IsJavaScriptDestructuringAlias(string path, string content, int nameIndex)
    {
        string extension = Path.GetExtension(path);
        if (!extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".cjs", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".mjs", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".ts", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int lineStart = content.LastIndexOf('\n', Math.Max(0, nameIndex - 1)) + 1;
        int lineEnd = content.IndexOf('\n', nameIndex);
        lineEnd = lineEnd < 0 ? content.Length : lineEnd;
        int open = content.LastIndexOf('{', nameIndex, nameIndex - lineStart + 1);
        int close = content.IndexOf('}', nameIndex, lineEnd - nameIndex);
        if (open < lineStart || close < 0)
        {
            return false;
        }

        int segmentEnd = content.IndexOf(',', nameIndex, close - nameIndex);
        segmentEnd = segmentEnd < 0 ? close : segmentEnd;
        if (content.AsSpan(nameIndex, segmentEnd - nameIndex).Contains('='))
        {
            return false;
        }

        int cursor = close + 1;
        while (cursor < lineEnd && char.IsWhiteSpace(content[cursor]))
        {
            cursor++;
        }

        return cursor < lineEnd && content[cursor] == '=';
    }

    private static bool IsCredentialName(string name)
    {
        string[] words = Regex.Split(
                Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2", RegexOptions.CultureInvariant),
                "[^A-Za-z0-9]+",
                RegexOptions.CultureInvariant)
            .Where(static word => word.Length > 0)
            .Select(static word => word.ToUpperInvariant())
            .ToArray();
        if (words.Length == 0)
        {
            return false;
        }

        if (string.Equals(name, "ThemeStorageKey", StringComparison.Ordinal))
        {
            return false;
        }

        string normalizedName = string.Concat(name.Where(char.IsLetterOrDigit));
        if (normalizedName.EndsWith("AllowInsecureSymmetricKey", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string last = words[^1];
        if (last is "VALUE" or "TEXT" or "BYTES" or "CHARS" or "DATA" or "MATERIAL"
            && words.Length > 1)
        {
            words = words[..^1];
            last = words[^1];
        }
        bool uppercaseEnvironmentStyle = name.Any(char.IsLetter)
            && string.Equals(name, name.ToUpperInvariant(), StringComparison.Ordinal);
        return last is "PASSWORD" or "PASSWORDS" or "PASSWD" or "PWD"
            or "SECRET" or "CREDENTIAL" or "CONNECTIONSTRING"
            or "SIGNINGKEY" or "PRIVATEKEY" or "APIKEY" or "ACCOUNTKEY" or "STORAGEKEY"
            || last is "SECRETS" or "CREDENTIALS"
                && (words.Length > 1 || uppercaseEnvironmentStyle)
            || last is "TOKEN" or "TOKENS"
                && (last == "TOKEN" && words.Length == 1
                    || uppercaseEnvironmentStyle
                    || words.Any(static word => word is "ACCESS" or "API" or "AUTH" or "AUTHENTICATION" or "BEARER"
                        or "CLIENT" or "ID" or "IDENTITY" or "JWT" or "OIDC" or "REFRESH" or "ADMIN"))
            || last == "STRING" && words.Length > 1 && words[^2] == "CONNECTION"
            || last == "KEY" && ((words.Length == 1 && uppercaseEnvironmentStyle) || words.Any(static word => word is
                "AUTH" or "AUTHENTICATION" or "JWT" or "SIGNING" or "PRIVATE" or "API" or "ACCESS" or "ACCOUNT" or "STORAGE"));
    }

    private static bool HasStructuralColonPrefix(string content, int nameIndex)
    {
        int cursor = nameIndex - 1;
        while (cursor >= 0 && (char.IsWhiteSpace(content[cursor]) || content[cursor] is '"' or '\'' or '['))
        {
            if (content[cursor] == '\n')
            {
                return true;
            }

            cursor--;
        }

        return cursor < 0 || content[cursor] is '{' or ',' || content[cursor] == '-'
            && content.AsSpan(0, cursor).LastIndexOf('\n') == cursor - 1;
    }

    private static bool IsYamlSequenceMarker(string content, int valueIndex)
    {
        int lineStart = content.LastIndexOf('\n', Math.Max(0, valueIndex - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        return content[lineStart..valueIndex].All(char.IsWhiteSpace)
            && valueIndex + 1 < content.Length
            && char.IsWhiteSpace(content[valueIndex + 1]);
    }

    private static bool IsInertPlaceholder(string value)
        => string.IsNullOrWhiteSpace(value)
            || EnvironmentPlaceholderPattern().IsMatch(value)
            || DaprEnvironmentPlaceholderPattern().IsMatch(value)
            || PowerShellEnvironmentPlaceholderPattern().IsMatch(value)
            || IsSemanticBracketPlaceholder(value)
            || RealmPlaceholderPattern().IsMatch(value)
            || RedactedPlaceholderPattern().IsMatch(value)
            || SourcePlaceholderPattern().IsMatch(value)
            || ProtectedMarkerPattern().IsMatch(value)
            || MarkdownFencePattern().IsMatch(value);

    private static bool IsSemanticBracketPlaceholder(string value)
    {
        string? name = value switch
        {
            _ when value.Length > 2 && value[0] == '<' && value[^1] == '>' => value[1..^1],
            _ when value.Length > 4 && value.StartsWith("{{", StringComparison.Ordinal)
                && value.EndsWith("}}", StringComparison.Ordinal) => value[2..^2],
            _ when value.Length > 2 && value[0] == '{' && value[^1] == '}' => value[1..^1],
            _ => null,
        };
        if (name is null || !SemanticPlaceholderNamePattern().IsMatch(name))
        {
            return false;
        }

        string[] words = Regex.Split(
                Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2", RegexOptions.CultureInvariant),
                "[^A-Za-z0-9]+",
                RegexOptions.CultureInvariant)
            .Where(static word => word.Length > 0)
            .Select(static word => word.ToUpperInvariant())
            .ToArray();
        return words.Length > 0 && words.All(static word => word is
            "PASSWORD" or "PASS" or "SECRET" or "TOKEN" or "KEY" or "CREDENTIAL"
            or "CONNECTION" or "STRING" or "REDACTED"
            or "USERNAME" or "USER" or "CLIENT" or "AUTH" or "AUTHORIZATION"
            or "VALUE" or "NAME" or "ID" or "AUTHORITY" or "ISSUER" or "AUDIENCE"
            or "HOST" or "PORT" or "URL" or "ENDPOINT" or "TENANT" or "ENV"
            or "INPUT" or "REPLACE" or "EXAMPLE" or "GENERATED" or "RUNTIME"
            or "YOUR" or "ENTER" or "INSERT" or "PLACEHOLDER" or "HERE"
            or "REQUIRED" or "OPTIONAL" or "CURRENT" or "NEW" or "OLD"
            or "TEST" or "SAMPLE" or "DEV" or "LOCAL" or "SERVICE" or "DATABASE"
            or "INVALID" or "API" or "AT" or "CACHED" or "SESSION" or "RABBITMQ");
    }

    private static bool IsConfigurationKeyReference(string name, string value)
    {
        string normalizedName = string.Concat(name.Where(char.IsLetterOrDigit));
        return (normalizedName.EndsWith("ConfigurationKey", StringComparison.OrdinalIgnoreCase)
                || normalizedName.EndsWith("EnvironmentKey", StringComparison.OrdinalIgnoreCase)
                || normalizedName.EndsWith("SettingKey", StringComparison.OrdinalIgnoreCase)
                || normalizedName.EndsWith("KeyName", StringComparison.OrdinalIgnoreCase))
            && ConfigurationKeyPattern().IsMatch(value);
    }

    private static bool IsInertUriUserInfo(string userInfo)
    {
        int separator = userInfo.IndexOf(':');
        string credential = separator >= 0 ? userInfo[(separator + 1)..] : userInfo;
        return IsInertPlaceholder(credential);
    }

    private static bool IsCredentialFactoryExpression(string relativePath, string value)
    {
        string extension = Path.GetExtension(relativePath);
        bool isJavaScript = extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cjs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mjs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ts", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase);
        return isJavaScript
            && (value.StartsWith("async (", StringComparison.Ordinal)
                || value.StartsWith("async(", StringComparison.Ordinal)
                || value.StartsWith("async function", StringComparison.Ordinal)
                || value.StartsWith("(", StringComparison.Ordinal))
            && value.Contains("=>", StringComparison.Ordinal);
    }

    private static bool ContainsDirectLiteralReturn(string value)
    {
        foreach (Match match in DirectLiteralReturnPattern().Matches(value))
        {
            string literal = match.Groups["literal"].Value;
            if (!IsInertPlaceholder(literal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsUnsafeRuntimeLiteralOperand(string relativePath, string value)
    {
        string extension = Path.GetExtension(relativePath);
        if (extension.Equals(".sh", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bash", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            foreach (Match fallback in ShellDefaultValuePattern().Matches(value))
            {
                string candidate = fallback.Groups["fallback"].Value.Trim().Trim('"', '\'');
                if (!IsInertPlaceholder(candidate)
                    && !EnvironmentVariableReferencePattern().IsMatch(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        bool isCSharp = extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".razor", StringComparison.OrdinalIgnoreCase);
        bool isJavaScript = extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cjs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mjs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ts", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase);
        bool isPython = extension.Equals(".py", StringComparison.OrdinalIgnoreCase);
        if (!isCSharp && !isJavaScript && !isPython)
        {
            return false;
        }

        if (isJavaScript && IsJavaScriptRuntimeTransform(value))
        {
            int statementTerminator = value.IndexOf(';');
            string transformStatement = statementTerminator >= 0 ? value[..statementTerminator] : value;
            foreach (Match fallback in RuntimeLiteralFallbackPattern().Matches(transformStatement))
            {
                string literal = fallback.Groups["literal"].Value;
                if (!IsInertPlaceholder(literal))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (Match fallback in RuntimeLiteralFallbackPattern().Matches(value))
        {
            string literal = fallback.Groups["literal"].Value;
            if (!IsInertPlaceholder(literal))
            {
                return true;
            }
        }

        foreach (Match operand in RuntimeLiteralOperandPattern().Matches(value))
        {
            string literal = operand.Groups["literal"].Value;
            if (!IsInertPlaceholder(literal))
            {
                return true;
            }
        }

        if (isJavaScript)
        {
            foreach ((_, char delimiter, string literal) in EnumerateQuotedLiterals(value, includeBackticks: true))
            {
                if (delimiter == '`')
                {
                    string staticText = JavaScriptInterpolationPattern().Replace(literal, string.Empty);
                    if (!string.IsNullOrWhiteSpace(staticText))
                    {
                        return true;
                    }
                }
            }
        }

        foreach ((_, _, string literal) in EnumerateQuotedLiterals(value, isJavaScript))
        {
            if (!IsInertPlaceholder(literal) && RuntimeCredentialMaterialPattern().IsMatch(literal))
            {
                return true;
            }
        }

        return false;
    }

    private static string ExtractBalancedInvocation(string content, int matchIndex, int matchLength)
    {
        int open = content.IndexOf('(', matchIndex, matchLength);
        if (open < 0)
        {
            return content[matchIndex..Math.Min(content.Length, matchIndex + matchLength)];
        }

        bool insideString = false;
        bool insideCharacter = false;
        bool escaped = false;
        int depth = 0;
        for (int index = open; index < content.Length; index++)
        {
            char character = content[index];
            if (insideString || insideCharacter)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if ((insideString && character == '"') || (insideCharacter && character == '\''))
                {
                    insideString = false;
                    insideCharacter = false;
                }

                continue;
            }

            if (character == '"')
            {
                insideString = true;
            }
            else if (character == '\'')
            {
                insideCharacter = true;
            }
            else if (character == '(')
            {
                depth++;
            }
            else if (character == ')' && --depth == 0)
            {
                return content[matchIndex..(index + 1)];
            }
        }

        return content[matchIndex..];
    }

    private static bool IsRuntimeSourceLookupLiteral(string invocation, int literalIndex)
    {
        string prefix = invocation[..literalIndex];
        return RuntimeSourceLookupPrefixPattern().IsMatch(prefix);
    }

    private static IEnumerable<(int Index, char Delimiter, string Literal)> EnumerateQuotedLiterals(
        string value,
        bool includeBackticks)
    {
        for (int index = 0; index < value.Length; index++)
        {
            char delimiter = value[index];
            if (delimiter is not ('"' or '\'') && (!includeBackticks || delimiter != '`'))
            {
                continue;
            }

            var literal = new StringBuilder();
            int start = index;
            bool escaped = false;
            for (index++; index < value.Length; index++)
            {
                char character = value[index];
                if (escaped)
                {
                    literal.Append(character);
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == delimiter)
                {
                    yield return (start, delimiter, literal.ToString());
                    break;
                }
                else
                {
                    literal.Append(character);
                }
            }
        }
    }

    private static bool IsRecognizedSourceExpression(string relativePath, string name, string value)
    {
        string extension = Path.GetExtension(relativePath);
        if (value is "string.Empty" or "Array.Empty<string>()" or "default"
            || EnvironmentVariableReferencePattern().IsMatch(value)
            || JavaScriptEnvironmentReferencePattern().IsMatch(value)
            || GitHubExpressionPattern().IsMatch(value)
            || CredentialRuntimeCallPattern().IsMatch(value))
        {
            return true;
        }

        if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".razor", StringComparison.OrdinalIgnoreCase))
        {
            return IsCSharpRuntimeExpression(name, value);
        }

        if (string.Equals(extension, ".py", StringComparison.OrdinalIgnoreCase))
        {
            return IsPythonRuntimeExpression(name, value);
        }

        if (extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cjs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mjs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ts", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase))
        {
            return IsJavaScriptRuntimeExpression(name, value);
        }

        if (string.Equals(extension, ".sh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".bash", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".ps1", StringComparison.OrdinalIgnoreCase))
        {
            return IsShellRuntimeExpression(value);
        }

        return false;
    }

    private static bool IsJavaScriptRuntimeExpression(string name, string value)
    {
        if (JavaScriptEnvironmentReferencePattern().IsMatch(value)
            || JavaScriptConfigurationAccessPattern().IsMatch(value)
            || IsJavaScriptRuntimeTransform(value)
            || value.Contains("${", StringComparison.Ordinal))
        {
            return true;
        }

        if (JavaScriptReferenceExpressionPattern().IsMatch(value))
        {
            return !IdentifiersEqual(name, value);
        }

        int open = value.IndexOf('(');
        int close = value.LastIndexOf(')');
        if (open < 0 || close <= open)
        {
            return Regex.IsMatch(
                JavaScriptLiteralPattern().Replace(value, string.Empty),
                @"\b[A-Za-z_$][A-Za-z0-9_$]*\b",
                RegexOptions.CultureInvariant);
        }

        string arguments = value[(open + 1)..close];
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return false;
        }

        return Regex.IsMatch(
            JavaScriptLiteralPattern().Replace(arguments, string.Empty),
            @"\b(?!true\b|false\b|null\b|undefined\b)[A-Za-z_$][A-Za-z0-9_$]*\b",
            RegexOptions.CultureInvariant);
    }

    private static bool IsJavaScriptRuntimeTransform(string value)
    {
        int transform = value.IndexOf(".replaceAll(", StringComparison.Ordinal);
        return transform > 0
            && JavaScriptReferenceExpressionPattern().IsMatch(value[..transform]);
    }

    private static string GetEffectiveSourcePath(string relativePath, string content, int index)
    {
        if (!relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return relativePath;
        }

        string? language = null;
        int position = 0;
        while (position < index)
        {
            int lineEnd = content.IndexOf('\n', position);
            if (lineEnd < 0 || lineEnd > index)
            {
                lineEnd = index;
            }

            string line = content[position..lineEnd].Trim();
            while (line.StartsWith('>'))
            {
                line = line[1..].TrimStart();
            }
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                language = language is null ? line[3..].Trim() : null;
            }
            else if (line.StartsWith("~~~", StringComparison.Ordinal))
            {
                language = language is null ? line[3..].Trim() : null;
            }

            position = lineEnd + 1;
        }

        return language?.ToLowerInvariant() switch
        {
            "c#" or "csharp" or "cs" => relativePath + ".cs",
            "python" or "py" => relativePath + ".py",
            "javascript" or "js" or "typescript" or "ts" or "tsx" or "jsx" => relativePath + ".js",
            "bash" or "shell" or "sh" => relativePath + ".sh",
            "powershell" or "ps1" => relativePath + ".ps1",
            _ => relativePath,
        };
    }

    private static bool IsPythonRuntimeExpression(string name, string value)
    {
        if (value.StartsWith("os.environ", StringComparison.Ordinal)
            || value.StartsWith("secrets.", StringComparison.Ordinal)
            || value.StartsWith("uuid.uuid4", StringComparison.Ordinal))
        {
            return true;
        }

        int open = value.IndexOf('(');
        int close = value.LastIndexOf(')');
        if (open < 0 || close <= open)
        {
            return PythonReferenceExpressionPattern().IsMatch(value);
        }

        string arguments = value[(open + 1)..close];
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return false;
        }

        string withoutLiterals = PythonLiteralPattern().Replace(arguments, string.Empty);
        return Regex.IsMatch(
            withoutLiterals,
            @"\b(?!True\b|False\b|None\b)[A-Za-z_][A-Za-z0-9_]*\b",
            RegexOptions.CultureInvariant);
    }

    private static bool IsCSharpRuntimeExpression(string name, string value)
    {
        if (value is "string.Empty" or "default")
        {
            return true;
        }

        if (CSharpRuntimeTransformPattern().IsMatch(value))
        {
            return true;
        }


        if (value.StartsWith("$\"", StringComparison.Ordinal)
            || value.StartsWith("$@\"", StringComparison.Ordinal)
            || value.StartsWith("@$\"", StringComparison.Ordinal))
        {
            return ContainsRuntimeCSharpInterpolation(value);
        }

        if (CSharpReferenceExpressionPattern().IsMatch(value))
        {
            return true;
        }

        if (!value.Contains('(')
            && CSharpRuntimeReferencePattern().IsMatch(CSharpLiteralPattern().Replace(value, string.Empty)))
        {
            return true;
        }

        if (!value.Contains('('))
        {
            return false;
        }

        if (CSharpCryptographicGenerationPattern().IsMatch(value)
            || CSharpConfigurationAccessPattern().IsMatch(value))
        {
            return true;
        }

        int open = value.IndexOf('(');
        int close = value.LastIndexOf(')');
        if (open < 0 || close <= open)
        {
            return false;
        }

        string arguments = value[(open + 1)..close];
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return false;
        }

        string withoutLiterals = CSharpLiteralPattern().Replace(arguments, string.Empty);
        return Regex.IsMatch(
            withoutLiterals,
            @"\b(?!true\b|false\b|null\b|default\b|nameof\b)[A-Za-z_][A-Za-z0-9_]*\b",
            RegexOptions.CultureInvariant);
    }

    private static bool ContainsRuntimeCSharpInterpolation(string value)
    {
        foreach (Match interpolation in CSharpInterpolationPattern().Matches(value))
        {
            string expression = interpolation.Groups["expression"].Value.Trim();
            int formatSeparator = expression.LastIndexOf(':');
            if (formatSeparator > 0)
            {
                expression = expression[..formatSeparator].TrimEnd();
            }

            if (IsCSharpRuntimeExpression("interpolation", expression))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IdentifiersEqual(string left, string right)
        => string.Equals(
            Regex.Replace(left, "[^A-Za-z0-9]", string.Empty, RegexOptions.CultureInvariant),
            Regex.Replace(right, "[^A-Za-z0-9]", string.Empty, RegexOptions.CultureInvariant),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsShellRuntimeExpression(string value)
    {
        if (ShellSourceExpressionPattern().IsMatch(value))
        {
            return true;
        }

        string candidate = value.Trim();
        if (candidate.StartsWith("$(", StringComparison.Ordinal) && candidate.EndsWith(')'))
        {
            string command = candidate[2..^1].TrimStart();
            if (command.StartsWith("curl ", StringComparison.Ordinal)
                || command.StartsWith("az account get-access-token ", StringComparison.Ordinal)
                || command.StartsWith("aspire resource sample-api issue-smoke-token ", StringComparison.Ordinal)
                || command.StartsWith("openssl rand ", StringComparison.Ordinal)
                || command.StartsWith("head -c ", StringComparison.Ordinal)
                || command.StartsWith("dd if=/dev/urandom", StringComparison.Ordinal)
                || command.StartsWith("uuidgen", StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (candidate.StartsWith("(Invoke-RestMethod ", StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith("(az account get-access-token ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsConnectionStringName(string name)
        => Regex.IsMatch(
            name,
            @"(?:ConnectionString|CONNECTION_STRING)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool IsStructuredBearerValue(string content, Match match)
    {
        int lineStart = content.LastIndexOf('\n', Math.Max(0, match.Index - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        string prefix = content[lineStart..match.Index].TrimStart();
        return prefix.Length == 0
            || prefix.EndsWith('"')
            || prefix.EndsWith('\'')
            || prefix.EndsWith('`')
            || prefix.EndsWith('=')
            || prefix.EndsWith(':')
            || prefix.EndsWith('(')
            || AuthorizationPrefixPattern().IsMatch(prefix);
    }

    private static bool IsBearerChallenge(string content, Match match)
    {
        Group token = match.Groups["token"];
        int afterToken = token.Index + token.Length;
        return token.Value.Equals("realm=", StringComparison.OrdinalIgnoreCase)
            && afterToken < content.Length
            && (content[afterToken] is '\'' or '"'
                || (content[afterToken] == '\\'
                    && afterToken + 1 < content.Length
                    && content[afterToken + 1] is '\'' or '"'));
    }

    private static bool IsBearerSourceExpression(string content, Match match)
    {
        Group token = match.Groups["token"];
        int afterToken = token.Index + token.Length;
        if (token.Value == "$" && afterToken < content.Length && content[afterToken] == '{')
        {
            int close = content.IndexOf('}', afterToken + 1);
            return close > afterToken + 1;
        }

        if (EnvironmentVariableReferencePattern().IsMatch(token.Value))
        {
            return true;
        }

        if (token.Value == "`" && match.Index > 0 && content[match.Index - 1] == '`')
        {
            return true;
        }

        if (token.Value.EndsWith('`') && IsInertPlaceholder(token.Value.TrimEnd('`')))
        {
            return true;
        }

        return token.Value.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase)
            && token.Value.TrimEnd('`').Equals("Bearer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInsideCSharpString(string content, int targetIndex)
    {
        bool insideBlockComment = false;
        bool insideLineComment = false;
        bool insideString = false;
        bool insideVerbatimString = false;
        bool insideCharacter = false;
        int rawDelimiterLength = 0;

        for (int index = 0; index < targetIndex; index++)
        {
            char character = content[index];
            char next = index + 1 < targetIndex ? content[index + 1] : '\0';

            if (insideLineComment)
            {
                insideLineComment = character != '\n';
                continue;
            }

            if (insideBlockComment)
            {
                if (character == '*' && next == '/')
                {
                    insideBlockComment = false;
                    index++;
                }

                continue;
            }

            if (rawDelimiterLength > 0)
            {
                if (character == '"')
                {
                    int quoteCount = CountRun(content, index, '"');
                    if (quoteCount >= rawDelimiterLength)
                    {
                        rawDelimiterLength = 0;
                        index += quoteCount - 1;
                    }
                }

                continue;
            }

            if (insideVerbatimString)
            {
                if (character == '"')
                {
                    if (next == '"')
                    {
                        index++;
                    }
                    else
                    {
                        insideVerbatimString = false;
                    }
                }

                continue;
            }

            if (insideString || insideCharacter)
            {
                if (character == '\\')
                {
                    index++;
                }
                else if ((insideString && character == '"') || (insideCharacter && character == '\''))
                {
                    insideString = false;
                    insideCharacter = false;
                }

                continue;
            }

            if (character == '/' && next == '/')
            {
                insideLineComment = true;
                index++;
            }
            else if (character == '/' && next == '*')
            {
                insideBlockComment = true;
                index++;
            }
            else if (character == '"')
            {
                int quoteCount = CountRun(content, index, '"');
                if (quoteCount >= 3)
                {
                    rawDelimiterLength = quoteCount;
                    index += quoteCount - 1;
                }
                else
                {
                    insideString = true;
                }
            }
            else if (character == '@' && next == '"')
            {
                insideVerbatimString = true;
                index++;
            }
            else if (character == '\'')
            {
                insideCharacter = true;
            }
        }

        return insideString || insideVerbatimString || rawDelimiterLength > 0;
    }

    private static string ExtractSourceExpression(string content, int start, bool stopAtNewline)
    {
        bool insideDouble = false;
        bool insideSingle = false;
        bool escaped = false;
        int parenthesisDepth = 0;
        int bracketDepth = 0;
        int braceDepth = 0;

        for (int index = start; index < content.Length; index++)
        {
            char character = content[index];
            if (insideDouble || insideSingle)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if ((insideDouble && character == '"') || (insideSingle && character == '\''))
                {
                    insideDouble = false;
                    insideSingle = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    insideDouble = true;
                    break;
                case '\'':
                    insideSingle = true;
                    break;
                case '(':
                    parenthesisDepth++;
                    break;
                case ')':
                    if (parenthesisDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                    {
                        return content[start..index].Trim();
                    }

                    parenthesisDepth = Math.Max(0, parenthesisDepth - 1);
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    braceDepth = Math.Max(0, braceDepth - 1);
                    break;
                case ';' when parenthesisDepth == 0 && bracketDepth == 0 && braceDepth == 0:
                case ',' when parenthesisDepth == 0 && bracketDepth == 0 && braceDepth == 0:
                    return content[start..index].Trim();
                case '\r':
                case '\n' when stopAtNewline || parenthesisDepth == 0 && bracketDepth == 0 && braceDepth == 0:
                    return content[start..index].Trim();
            }
        }

        return content[start..].Trim();
    }

    private static string ExtractEmbeddedAssignmentValue(string content, int nameEnd, int detectedValueStart)
    {
        int operatorIndex = -1;
        for (int index = nameEnd; index < detectedValueStart; index++)
        {
            if (content[index] is '=' or ':')
            {
                operatorIndex = index;
            }
        }

        int start = operatorIndex >= 0 ? operatorIndex + 1 : detectedValueStart;
        while (start < content.Length && content[start] is ' ' or '\t')
        {
            start++;
        }

        if (start >= content.Length || content[start] is '"' or '\'')
        {
            return string.Empty;
        }

        int end = start;
        while (end < content.Length && content[end] is not '"' and not '\'' and not '\r' and not '\n')
        {
            end++;
        }

        return content[start..end].Trim();
    }

    private static int CountRun(string content, int start, char character)
    {
        int count = 0;
        while (start + count < content.Length && content[start + count] == character)
        {
            count++;
        }

        return count;
    }

    private static IEnumerable<int> FindDecodedJwtPayloadIndexes(string content)
    {
        if (!content.Contains("\"iss\"", StringComparison.OrdinalIgnoreCase)
            || !content.Contains("\"aud\"", StringComparison.OrdinalIgnoreCase)
            || !content.Contains("\"exp\"", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var indexes = new SortedSet<int>();
        for (int start = 0; start < content.Length; start++)
        {
            if (content[start] != '{')
            {
                continue;
            }

            bool insideString = false;
            bool escaped = false;
            int depth = 0;
            for (int position = start; position < content.Length; position++)
            {
                char character = content[position];
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
                    depth++;
                }
                else if (character == '}' && --depth == 0)
                {
                    string candidate = content[start..(position + 1)];
                    if (candidate.Contains("\"iss\"", StringComparison.OrdinalIgnoreCase)
                        && candidate.Contains("\"aud\"", StringComparison.OrdinalIgnoreCase)
                        && candidate.Contains("\"exp\"", StringComparison.OrdinalIgnoreCase)
                        && IsDecodedJwtJson(candidate))
                    {
                        _ = indexes.Add(start);
                    }

                    break;
                }
            }
        }

        foreach (int index in indexes)
        {
            yield return index;
        }
    }

    private static bool IsDecodedJwtJson(string candidate)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(candidate);
            return ContainsJwtClaims(document.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ContainsJwtClaims(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(
                element.EnumerateObject().Select(static property => property.Name),
                StringComparer.OrdinalIgnoreCase);
            if (names.Contains("iss")
                && names.Contains("aud")
                && names.Contains("exp"))
            {
                return true;
            }

            return element.EnumerateObject().Any(static property => ContainsJwtClaims(property.Value));
        }

        return element.ValueKind == JsonValueKind.Array
            && element.EnumerateArray().Any(ContainsJwtClaims);
    }

    private static bool IsCompactJwt(string value)
    {
        string[] segments = value.Split('.');
        if (segments.Length != 3)
        {
            return false;
        }

        try
        {
            using JsonDocument header = JsonDocument.Parse(DecodeBase64Url(segments[0]));
            using JsonDocument payload = JsonDocument.Parse(DecodeBase64Url(segments[1]));
            return header.RootElement.ValueKind == JsonValueKind.Object
                && header.RootElement.TryGetProperty("alg", out _)
                && ContainsJwtClaims(payload.RootElement);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static byte[] DecodeBase64Url(string segment)
    {
        string padded = segment.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - (padded.Length % 4)) % 4);
        return Convert.FromBase64String(padded);
    }

    private static string Assignment(string name, string value)
        => name + "=" + value;

    private static string RandomSecret()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string CreateDecodedJwtPayload(bool nested, string? padding = null)
    {
        object payload = new Dictionary<string, object>
        {
            ["sub"] = "runtime-subject",
            ["iss"] = "runtime-issuer",
            ["aud"] = "runtime-audience",
            ["exp"] = 9999999999,
            ["nonce"] = padding ?? string.Empty,
        };
        object root = nested
            ? new Dictionary<string, object> { ["wrapper"] = payload }
            : payload;
        return JsonSerializer.Serialize(root);
    }

    private static int LineNumber(string content, int index)
        => 1 + content.AsSpan(0, index).Count('\n');

    [GeneratedRegex(@"(?<![A-Za-z0-9_-])[A-Za-z0-9_-]{4,}\.[A-Za-z0-9_-]{4,}\.[A-Za-z0-9_-]{8,}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant)]
    private static partial Regex CompactJwtPattern();

    [GeneratedRegex(@"-----BEGIN (?<label>[A-Z0-9 ]*PRIVATE KEY)-----[\s\S]+?-----END \k<label>-----", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateKeyPattern();

    [GeneratedRegex(@"\bBearer[ \t]+(?<token>[!#$%&'*+\-./^_`|~0-9A-Za-z]+=*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerCredentialPattern();

    [GeneratedRegex(
        @"AuthenticationHeaderValue\s*\(\s*[\""']Bearer[\""']\s*,\s*(?<value>(?:\""(?<double>(?:\\.|[^\""\\])*)\""|'(?<single>(?:\\.|[^'\\])*)'|(?<bare>[A-Za-z_][A-Za-z0-9_.\[\]-]*)))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthenticationHeaderValuePattern();

    [GeneratedRegex(
        @"\b[A-Za-z][A-Za-z0-9+.-]*://(?<userinfo>[^\s/@]*)@[^\s/]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex CredentialUriPattern();

    [GeneratedRegex(
        @"<(?<name>[A-Za-z_][A-Za-z0-9_.:-]*)\b[^>]*>(?<value>[^<\r\n]*)</\k<name>\s*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex XmlSecretElementPattern();

    [GeneratedRegex(
        @"\b(?:key|name)\s*=\s*[\""'](?<name>[A-Za-z_][A-Za-z0-9_.:-]*)[\""'][^>\r\n]*?\bvalue\s*=\s*[\""'](?<value>[^\""'\r\n]*)[\""']",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex XmlKeyValueSecretPattern();

    [GeneratedRegex(
        @"\bnew\s+(?:NetworkCredential|SymmetricSecurityKey)\s*\(",
        RegexOptions.CultureInvariant)]
    private static partial Regex CredentialConstructorPattern();

    [GeneratedRegex(@"(?:Password|Pwd|SharedAccessKey)\s*=\s*(?<password>[^;\""']+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringPasswordPattern();

    [GeneratedRegex(@"\bcurl\b[^\r\n]*?(?:-u|--user(?:=|\s+))\s*[\""']?(?<userinfo>[^\s\""']+)", RegexOptions.CultureInvariant)]
    private static partial Regex CurlUserCredentialPattern();

    [GeneratedRegex(
        @"(?<![A-Za-z0-9_.:-])(?=(?:\[[\t ]*)?[\""']?(?<name>[A-Za-z_][A-Za-z0-9_.:-]*)[\""']?(?:[\t ]*\])?[\t ]*(?::(?![-+?])|=(?![=>]))\s*(?<value>(?:\""(?<double>(?:\\.|[^\""\\])*)\""|'(?<single>(?:\\.|[^'\\])*)'|`(?<template>(?:\\.|[^`\\])*)`|(?<bare>[^\s,;#`)\""']+))))",
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

    [GeneratedRegex(@"^__HEXALITH_[A-Z0-9_]+__$", RegexOptions.CultureInvariant)]
    private static partial Regex RealmPlaceholderPattern();

    [GeneratedRegex(@"^\[?redacted(?:-[a-z]+)?\]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RedactedPlaceholderPattern();

    [GeneratedRegex(@"^(?:FC_CONTRACT_TOKEN|\$\{input:[A-Za-z_][A-Za-z0-9_]*\}|@Microsoft\.KeyVault\(SecretUri=[^\r\n]+\)|(?:secret|keyvault)ref:[A-Za-z0-9_.:/<>-]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourcePlaceholderPattern();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*(?:__[A-Za-z0-9]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex ConfigurationKeyPattern();

    [GeneratedRegex(@"^[~^]?[0-9]+(?:\.[0-9]+){1,3}(?:-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageVersionRangePattern();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.:-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticPlaceholderNamePattern();

    [GeneratedRegex(@"^PROTECTED_[A-Z0-9_]+_MARKER(?:_[A-Z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ProtectedMarkerPattern();

    [GeneratedRegex(@"^(?:`{3,}|~{3,})$", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownFencePattern();

    [GeneratedRegex(@"Authorization[\""']?\s*[:=]\s*[\""']?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationPrefixPattern();

    [GeneratedRegex(@"^\$(?:\{)?[A-Za-z_][A-Za-z0-9_]*(?:\})?$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentVariableReferencePattern();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.\[\]-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceIdentifierPattern();

    [GeneratedRegex(@"^(?:process\.env\.[A-Z_][A-Z0-9_]*|Deno\.env\.get\([\""'][A-Z_][A-Z0-9_]*[\""']\))!?$", RegexOptions.CultureInvariant)]
    private static partial Regex JavaScriptEnvironmentReferencePattern();

    [GeneratedRegex(@"^(?:Cypress\.env|Deno\.env\.get|Bun\.env)\s*\([\""'][A-Za-z_][A-Za-z0-9_]*[\""']\)!?$", RegexOptions.CultureInvariant)]
    private static partial Regex JavaScriptConfigurationAccessPattern();

    [GeneratedRegex(@"^[A-Za-z_$][A-Za-z0-9_$]*(?:(?:\??\.[A-Za-z_$][A-Za-z0-9_$]*)|(?:\[[^\]\r\n]+\]))*$", RegexOptions.CultureInvariant)]
    private static partial Regex JavaScriptReferenceExpressionPattern();

    [GeneratedRegex(@"(?:`(?:\\.|[^`\\])*`|\""(?:\\.|[^\""\\])*\""|'(?:\\.|[^'\\])*'|\b\d+(?:\.\d+)?\b)", RegexOptions.CultureInvariant)]
    private static partial Regex JavaScriptLiteralPattern();

    [GeneratedRegex(@"\$\{[^{}]+\}", RegexOptions.CultureInvariant)]
    private static partial Regex JavaScriptInterpolationPattern();

    [GeneratedRegex(@"(?:\?\?|\|\||os\.environ\.get\([^,\r\n]+,|(?:Resolve|Decode)\s*\([^,\r\n]+,|GetValue(?:<[^>]+>)?\s*\([^,\r\n]+,)\s*[\""'](?<literal>[^\""'\r\n]+)[\""']", RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeLiteralFallbackPattern();

    [GeneratedRegex(@"(?:\+|\?\?|\|\|)\s*[\""'](?<literal>[^\""'\r\n]+)[\""']", RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeLiteralOperandPattern();

    [GeneratedRegex(@"(?:Environment\.GetEnvironmentVariable|\b[A-Za-z_][A-Za-z0-9_]*\s*\[|GetValue(?:<[^>]+>)?\s*\(|RequireOpaqueConfiguration\s*\()\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeSourceLookupPrefixPattern();

    [GeneratedRegex(@"^(?:[A-Za-z0-9+/]{20,}={0,2}|[0-9A-Fa-f]{24,})$", RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeCredentialMaterialPattern();

    [GeneratedRegex(@"(?:\breturn\s+|=>\s*)[\""'](?<literal>[^\""'\r\n]+)[\""']", RegexOptions.CultureInvariant)]
    private static partial Regex DirectLiteralReturnPattern();

    [GeneratedRegex(@"^(?:await\s+)?(?=[A-Za-z0-9_.]*(?:token|password|secret|credential|key))(?=[A-Za-z0-9_.]*(?:get|fetch|load|request|acquire|create|generate|random|resolve))[A-Za-z_][A-Za-z0-9_.]*\s*\(\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CredentialRuntimeCallPattern();

    [GeneratedRegex(@"^\$\{\{\s*(?:secrets|env|vars|github|runner|inputs|steps|needs)\.[A-Za-z_][A-Za-z0-9_.-]*\s*\}\}$", RegexOptions.CultureInvariant)]
    private static partial Regex GitHubExpressionPattern();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(?:\?*\.[A-Za-z_][A-Za-z0-9_]*)*(?:\[[^\]\r\n]+\])?$", RegexOptions.CultureInvariant)]
    private static partial Regex CSharpReferenceExpressionPattern();

    [GeneratedRegex(@"(?:RandomNumberGenerator\.GetBytes|Guid\.NewGuid|Convert\.ToBase64String\s*\(\s*RandomNumberGenerator\.GetBytes)", RegexOptions.CultureInvariant)]
    private static partial Regex CSharpCryptographicGenerationPattern();

    [GeneratedRegex(@"(?:Environment\.GetEnvironmentVariable|configuration\s*\[|_configuration\s*\[|GetValue(?:Async)?\s*\(|RequireOpaqueConfiguration\s*\()", RegexOptions.CultureInvariant)]
    private static partial Regex CSharpConfigurationAccessPattern();

    [GeneratedRegex(@"\b[_a-z][A-Za-z0-9_]*\??\.[A-Za-z_][A-Za-z0-9_]*", RegexOptions.CultureInvariant)]
    private static partial Regex CSharpRuntimeReferencePattern();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(?:\[[^\]\r\n]+\]|\??\.[A-Za-z_][A-Za-z0-9_]*)+\.(?:Trim|ToString)\s*\(\s*\)$", RegexOptions.CultureInvariant)]
    private static partial Regex CSharpRuntimeTransformPattern();

    [GeneratedRegex(@"(?:@?\""(?:\""\""|[^\""\r\n])*\""|'(?:\\.|[^'\\])*'|\b\d+(?:\.\d+)?\b)", RegexOptions.CultureInvariant)]
    private static partial Regex CSharpLiteralPattern();

    [GeneratedRegex(@"(?<!\{)\{(?<expression>[^{}]+)\}(?!\})", RegexOptions.CultureInvariant)]
    private static partial Regex CSharpInterpolationPattern();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(?:(?:\.[A-Za-z_][A-Za-z0-9_]*)|(?:\[[^\]\r\n]+\]))*$", RegexOptions.CultureInvariant)]
    private static partial Regex PythonReferenceExpressionPattern();

    [GeneratedRegex(@"(?:\""(?:\\.|[^\""\\])*\""|'(?:\\.|[^'\\])*'|\b\d+(?:\.\d+)?\b)", RegexOptions.CultureInvariant)]
    private static partial Regex PythonLiteralPattern();

    [GeneratedRegex(@"^(?:\$\{[A-Za-z_][A-Za-z0-9_]*(?::-)?\}|\$(?:env:)?[A-Za-z_][A-Za-z0-9_.]*|\$\((?:openssl\s+rand|head\s+-c\s+[^\r\n]*/dev/urandom|dd\s+if=/dev/urandom)[^\r\n]*\))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ShellSourceExpressionPattern();

    [GeneratedRegex(@"\$\{[A-Za-z_][A-Za-z0-9_]*:[-+?](?<fallback>[^}\r\n]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex ShellDefaultValuePattern();

    [GeneratedRegex(@"^_bmad-output/implementation-artifacts/evidence/story-\d+-\d+/[0-9a-f]{40}/successful/builds/[0-9a-f]{40}/.+$", RegexOptions.CultureInvariant)]
    private static partial Regex GovernedEvidenceCapturePathPattern();

    [GeneratedRegex(@"^_bmad-output/implementation-artifacts/\d+-\d+-owner-approved-[A-Za-z0-9-]*proof-packet\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnerApprovedProofPacketPathPattern();

    [GeneratedRegex(@"^(?:src|tests)/[^/]+/\.artifacts/ui-test-obj/(?:project\.assets\.json|[^/]+\.csproj\.nuget\.(?:dgspec\.json|g\.props|g\.targets))$", RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitUiTestArtifactPathPattern();
}

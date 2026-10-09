using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

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
    private const string SealedP1RRemediationSourceCapture =
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation/source-candidate.diff";
    private const string SealedP1RRemediationSourceCaptureSha256 =
        "220af5d8dbe27386311c7bc1cef1900a65d06db7c6eb9fcc27250913ae2056aa";
    private const string SealedCounterSerializationTestResults =
        "_bmad-output/implementation-artifacts/evidence/story-6-6/counter-v1-serialization-2026-10-07/sample-full.xml";
    private const string SealedCounterSerializationTestResultsSha256 =
        "cd94c23807386ef2a245b9f7e81e5243a36b00449de3d19579484989afa82622";

    private const string RetiredCallbackOtlpCapture = "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-callback-fences-2026-10-08/story66-callback-aspire-baseline.json";
    private const string RetiredCommandStateOtlpCapture = "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-command-state-2026-10-08/earlier-attempts/story66-command-state-aspire-baseline.json";

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

    /// <summary>
    /// Verifies GitHub's fixed SSH transport username is not treated as a credential.
    /// </summary>
    /// <param name="path">The reusable text path containing the public repository URL.</param>
    [Theory]
    [InlineData("docs/git.md")]
    [InlineData("tests/git.cs")]
    [InlineData(".gitmodules")]
    public void PublicGitHubSshUsername_IsAllowed(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string url = "ssh://git@github.com/Hexalith/Hexalith.EventStore.git";
        FindViolations(path, url).ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies the public Git SSH exception does not admit passwords or other URI credentials.
    /// </summary>
    /// <param name="scheme">The URI scheme.</param>
    /// <param name="host">The URI host.</param>
    /// <param name="includePassword">Whether a runtime password is added to the user information.</param>
    [Theory]
    [InlineData("ssh", "github.com", true)]
    [InlineData("https", "github.com", false)]
    [InlineData("ssh", "github.com.example.test", false)]
    public void GitSshException_StillRejectsUriCredentials(string scheme, string host, bool includePassword)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(host);
        string userInfo = includePassword ? "git:" + Uri.EscapeDataString(RandomSecret()) : "git";
        string url = scheme + "://" + userInfo + "@" + host + "/Hexalith/Hexalith.EventStore.git";
        FindViolations("docs/git.md", url).ShouldBe(["docs/git.md:1"]);
    }

    /// <summary>
    /// Verifies only the fixed Git transport username is exempt from URI credential detection.
    /// </summary>
    [Fact]
    public void UnrecognizedGitHubSshUsername_IsRejected()
    {
        string userInfo = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        string url = "ssh://" + userInfo + "@github.com/Hexalith/Hexalith.EventStore.git";
        FindViolations("docs/git.md", url).ShouldBe(["docs/git.md:1"]);
    }

    /// <summary>
    /// Verifies an allowed Git transport URL does not conceal another credential in the same document.
    /// </summary>
    /// <param name="sameLine">Whether the two URLs appear on the same line.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PublicGitHubSshUrl_DoesNotConcealOtherUriCredentials(bool sameLine)
    {
        string publicUrl = "ssh://git@github.com/Hexalith/Hexalith.EventStore.git";
        string credentialUrl = "ssh://git:" + Uri.EscapeDataString(RandomSecret()) + "@github.com/Hexalith/private.git";
        string content = publicUrl + (sameLine ? " " : "\n") + credentialUrl;
        FindViolations("docs/git.md", content).ShouldBe([sameLine ? "docs/git.md:1" : "docs/git.md:2"]);
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
        string content = "\"" + "Bearer " + "/" + RandomSecret().TrimEnd('=') + "==\"";

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
    public void TrackedTraceDatLiteralCredentialAssignment_IsRejected()
    {
        string path = ".playwright-cli/traces/resources/captured.dat";
        string content = Assignment("Pass" + "word", "\"" + RandomSecret() + "\"");

        FindViolations(path, content).ShouldBe([$"{path}:1"]);
    }

    [Fact]
    public void TrackedTraceDatJavaScriptRuntimeCredentialAssignments_AreAllowed()
    {
        const string Path = ".playwright-cli/traces/resources/captured.dat";
        string accessTokenName = "access" + "Token";
        string passwordName = "pass" + "word";

        FindViolations(Path, $"this._{accessTokenName} = await response.json()").ShouldBeEmpty();
        FindViolations(Path, $"{passwordName}={passwordName}").ShouldBeEmpty();
    }

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

    /// <summary>
    /// Verifies the sealed 6.1-P1R remediation exemption covers only its source capture, whose captured C# lines are
    /// classified as literals outside a <c>.cs</c> path. Every other tracked remediation or qualification packet file
    /// stays eligible for scanning, and near-miss paths are not exempt.
    /// </summary>
    [Fact]
    public void SealedP1RRemediationExemption_IsLimitedToTheSourceCapture()
    {
        string[] packetFiles = GetTrackedFiles()
            .Where(static path => path.StartsWith(
                    "_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation/",
                    StringComparison.Ordinal)
                || path.StartsWith(
                    "_bmad-output/implementation-artifacts/evidence/6-1-p1r-qualification/",
                    StringComparison.Ordinal))
            .ToArray();

        packetFiles.ShouldContain(SealedP1RRemediationSourceCapture);
        packetFiles.Where(IsExplicitGeneratedPath).ShouldBe([SealedP1RRemediationSourceCapture]);

        string[] nearMisses =
        [
            SealedP1RRemediationSourceCapture + ".orig",
            "_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation-2/source-candidate.diff",
            "tools/source-candidate.diff",
        ];
        foreach (string path in nearMisses)
        {
            IsExplicitGeneratedPath(path).ShouldBeFalse(path);
        }
    }

    /// <summary>
    /// Verifies the exemption is bound to the packet seal inside the scan path: the pinned hash is the packet's
    /// SHA256SUMS entry, the tracked capture matches it, a CRLF checkout keeps the same content, and altered content
    /// fails the scan instead of being skipped.
    /// </summary>
    [Fact]
    public void SealedP1RRemediationSourceCapture_ExemptionRequiresItsSealedContent()
    {
        string seal = File.ReadAllLines(Path.Combine(
                RepoRoot,
                "_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation/SHA256SUMS"))
            .Single(static line => line.EndsWith("  source-candidate.diff", StringComparison.Ordinal));
        seal.ShouldBe(SealedP1RRemediationSourceCaptureSha256 + "  source-candidate.diff");

        byte[] sealedBytes = File.ReadAllBytes(Path.Combine(RepoRoot, SealedP1RRemediationSourceCapture));
        ReadTrackedText(SealedP1RRemediationSourceCapture).ShouldBeNull();
        VerifySealedP1RRemediationSourceCapture(
            [.. sealedBytes.SelectMany(static value => value == (byte)'\n' ? new[] { (byte)'\r', value } : [value])]);

        Should.Throw<ShouldAssertException>(
            () => VerifySealedP1RRemediationSourceCapture([.. sealedBytes, (byte)'\n']));
    }

    /// <summary>
    /// Verifies the exemption hides only the known extension-classification false positive. A scanner change or a new
    /// real finding alters this result and requires revisiting the exemption.
    /// </summary>
    [Fact]
    public void SealedP1RRemediationSourceCapture_HidesOnlyTheKnownFalsePositive()
        => FindViolations(SealedP1RRemediationSourceCapture)
            .ShouldBe([SealedP1RRemediationSourceCapture + ":2590"]);

    [Fact]
    public void SealedCounterSerializationResults_ExemptionRequiresExactPathAndContent()
    {
        IsExplicitGeneratedPath(SealedCounterSerializationTestResults).ShouldBeTrue();
        IsExplicitGeneratedPath(SealedCounterSerializationTestResults + ".orig").ShouldBeFalse();
        IsExplicitGeneratedPath("tools/sample-full.xml").ShouldBeFalse();
        ReadTrackedText(SealedCounterSerializationTestResults).ShouldBeNull();
        byte[] bytes = File.ReadAllBytes(Path.Combine(RepoRoot, SealedCounterSerializationTestResults));
        Should.Throw<ShouldAssertException>(() => VerifySealedCounterSerializationTestResults([.. bytes, (byte)'\n']));
        FindViolations(SealedCounterSerializationTestResults).ShouldBe([SealedCounterSerializationTestResults + ":1"]);
    }

    /// <summary>Only empty framework cancellation sources in executable C# are noncredential expressions.</summary>
    [Theory]
    [InlineData(".cs", "CancellationTokenSource")]
    [InlineData(".razor", "CancellationTokenSource")]
    [InlineData(".cs", "System.Threading.CancellationTokenSource")]
    [InlineData(".razor", "System.Threading.CancellationTokenSource")]
    [InlineData(".cs", "global::System.Threading.CancellationTokenSource")]
    [InlineData(".razor", "global::System.Threading.CancellationTokenSource")]
    public void CancellationSourceRecognition_RequiresExactFrameworkExpression(string extension, string type)
    {
        string path = "tests/cancellation" + extension;
        string name = "to" + "ken";
        string expression = "new " + type + "( )";
        FindViolations(path, Assignment(name, expression)).ShouldBeEmpty();
        foreach (string rejected in new[] { "new Other.CancellationTokenSource()", "new OtherTokenSource()",
            "new " + type + "(\"" + RandomSecret() + "\")", expression + " + \"" + RandomSecret() + "\"" })
        {
            FindViolations(path, Assignment(name, rejected)).ShouldBe([path + ":1"]);
        }
        FindViolations("capture.log", Assignment(name, expression)).ShouldBe(["capture.log:1"]);
        FindViolations(path, "string fixture = \"" + Assignment(name, expression) + "\";").ShouldBe([path + ":1"]);
        FindViolations(path, Assignment(name, expression) + "\n" + Assignment("pass" + "word", "\"" + RandomSecret() + "\"")).ShouldBe([path + ":2"]);
    }

    /// <summary>Typed mock extraction admits only a literal-free cancellation argument.</summary>
    [Theory]
    [InlineData(".cs", "CancellationToken")]
    [InlineData(".razor", "CancellationToken")]
    [InlineData(".cs", "System.Threading.CancellationToken")]
    [InlineData(".razor", "System.Threading.CancellationToken")]
    [InlineData(".cs", "global::System.Threading.CancellationToken")]
    [InlineData(".razor", "global::System.Threading.CancellationToken")]
    public void TypedCancellationRecognition_RequiresExactArgumentExtraction(string extension, string type)
    {
        string path = "tests/cancellation" + extension;
        string name = "to" + "ken";
        string expression = "call.Arg<" + type + ">()";
        FindViolations(path, Assignment(name, expression)).ShouldBeEmpty();
        foreach (string rejected in new[] { "call.Arg<Other.CancellationToken>()", "call.Arg<string>()",
            "call.Arg<" + type + ">(\"" + RandomSecret() + "\")", expression + " + \"" + RandomSecret() + "\"" })
        {
            FindViolations(path, Assignment(name, rejected)).ShouldBe([path + ":1"]);
        }
        FindViolations("capture.log", Assignment(name, expression)).ShouldBe(["capture.log:1"]);
        FindViolations(path, "string fixture = \"" + Assignment(name, expression) + "\";").ShouldBe([path + ":1"]);
        FindViolations(path, Assignment(name, expression) + "\n" + Assignment("pass" + "word", "\"" + RandomSecret() + "\"")).ShouldBe([path + ":2"]);
    }

    /// <summary>State-address and protocol fixtures require their exact reviewed source context.</summary>
    [Theory]
    [InlineData("tests/Hexalith.EventStore.Server.Tests/Security/GovernanceGuardFixture.cs", 42)]
    public void FixtureAssignmentRecognition_RequiresExactContext(string path, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(path);
        string line = File.ReadLines(Path.Combine(RepoRoot, path)).ElementAt(lineNumber - 1).Trim();
        FindViolations(path, line).ShouldBeEmpty();
        FindViolations("tests/unrelated.cs", line).ShouldNotBeEmpty();
        string changed = line.Replace("=", "= \"" + RandomSecret() + "\" +", StringComparison.Ordinal);
        FindViolations(path, changed).ShouldNotBeEmpty();
        FindViolations(path, line + "\n" + Assignment("pass" + "word", "\"" + RandomSecret() + "\"")).ShouldBe([path + ":2"]);
    }

    /// <summary>Historical cancellation fixtures retain executable-source classification without seal changes.</summary>
    [Fact]
    public void HistoricalCancellationFixtures_RemainScannedAsSource()
    {
        const string path = "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-command-state-2026-10-08/owned-source-snapshot/tests/Hexalith.EventStore.Server.Tests/Events/DaprLogicalCommandStateTests.cs";
        FindViolations(path).ShouldBeEmpty();
        FindViolations(path, Assignment("pass" + "word", "\"" + RandomSecret() + "\"")).ShouldBe([path + ":1"]);
    }

    /// <summary>Only the exact known synthetic name in valid TRX test metadata is recognized.</summary>
    [Theory]
    [InlineData("result", false, "d0047b8113e592d3e3ade8137e8f45e7c0753f27d9171aca0b5d9073b922b71e")]
    [InlineData("definition", false, "d0047b8113e592d3e3ade8137e8f45e7c0753f27d9171aca0b5d9073b922b71e")]
    [InlineData("outside-path", true, "d0047b8113e592d3e3ade8137e8f45e7c0753f27d9171aca0b5d9073b922b71e")]
    [InlineData("other-attribute", true, "d0047b8113e592d3e3ade8137e8f45e7c0753f27d9171aca0b5d9073b922b71e")]
    [InlineData("other-element", true, "d0047b8113e592d3e3ade8137e8f45e7c0753f27d9171aca0b5d9073b922b71e")]
    [InlineData("mutated-name", true, "d0047b8113e592d3e3ade8137e8f45e7c0753f27d9171aca0b5d9073b922b71e")]
    [InlineData("extra-secret", true, "d0047b8113e592d3e3ade8137e8f45e7c0753f27d9171aca0b5d9073b922b71e")]
    [InlineData("malformed", true, "d0047b8113e592d3e3ade8137e8f45e7c0753f27d9171aca0b5d9073b922b71e")]
    [InlineData("wrong-namespace", true, "d0047b8113e592d3e3ade8137e8f45e7c0753f27d9171aca0b5d9073b922b71e")]
    [InlineData("result", false, "55e36b209d7e3df100a62f76fd8c707a7019005ad0f84ebc353517cdef9e174b")]
    [InlineData("definition", false, "55e36b209d7e3df100a62f76fd8c707a7019005ad0f84ebc353517cdef9e174b")]
    [InlineData("outside-path", true, "55e36b209d7e3df100a62f76fd8c707a7019005ad0f84ebc353517cdef9e174b")]
    [InlineData("other-attribute", true, "55e36b209d7e3df100a62f76fd8c707a7019005ad0f84ebc353517cdef9e174b")]
    [InlineData("other-element", true, "55e36b209d7e3df100a62f76fd8c707a7019005ad0f84ebc353517cdef9e174b")]
    [InlineData("mutated-name", true, "55e36b209d7e3df100a62f76fd8c707a7019005ad0f84ebc353517cdef9e174b")]
    [InlineData("extra-secret", true, "55e36b209d7e3df100a62f76fd8c707a7019005ad0f84ebc353517cdef9e174b")]
    [InlineData("malformed", true, "55e36b209d7e3df100a62f76fd8c707a7019005ad0f84ebc353517cdef9e174b")]
    [InlineData("wrong-namespace", true, "55e36b209d7e3df100a62f76fd8c707a7019005ad0f84ebc353517cdef9e174b")]
    [InlineData("result", false, "411f2812dd2931fcf20be8d4985bad37487c7cabb23be39fcec70337fc5917dd")]
    [InlineData("definition", false, "411f2812dd2931fcf20be8d4985bad37487c7cabb23be39fcec70337fc5917dd")]
    [InlineData("outside-path", true, "411f2812dd2931fcf20be8d4985bad37487c7cabb23be39fcec70337fc5917dd")]
    [InlineData("other-attribute", true, "411f2812dd2931fcf20be8d4985bad37487c7cabb23be39fcec70337fc5917dd")]
    [InlineData("other-element", true, "411f2812dd2931fcf20be8d4985bad37487c7cabb23be39fcec70337fc5917dd")]
    [InlineData("mutated-name", true, "411f2812dd2931fcf20be8d4985bad37487c7cabb23be39fcec70337fc5917dd")]
    [InlineData("extra-secret", true, "411f2812dd2931fcf20be8d4985bad37487c7cabb23be39fcec70337fc5917dd")]
    [InlineData("malformed", true, "411f2812dd2931fcf20be8d4985bad37487c7cabb23be39fcec70337fc5917dd")]
    [InlineData("wrong-namespace", true, "411f2812dd2931fcf20be8d4985bad37487c7cabb23be39fcec70337fc5917dd")]
    [InlineData("result", false, "fa0f36160bd613ec2296a72e0eeff07a7d3df029508e02b733f524e647365218")]
    [InlineData("definition", false, "fa0f36160bd613ec2296a72e0eeff07a7d3df029508e02b733f524e647365218")]
    [InlineData("outside-path", true, "fa0f36160bd613ec2296a72e0eeff07a7d3df029508e02b733f524e647365218")]
    [InlineData("other-attribute", true, "fa0f36160bd613ec2296a72e0eeff07a7d3df029508e02b733f524e647365218")]
    [InlineData("other-element", true, "fa0f36160bd613ec2296a72e0eeff07a7d3df029508e02b733f524e647365218")]
    [InlineData("mutated-name", true, "fa0f36160bd613ec2296a72e0eeff07a7d3df029508e02b733f524e647365218")]
    [InlineData("extra-secret", true, "fa0f36160bd613ec2296a72e0eeff07a7d3df029508e02b733f524e647365218")]
    [InlineData("malformed", true, "fa0f36160bd613ec2296a72e0eeff07a7d3df029508e02b733f524e647365218")]
    [InlineData("wrong-namespace", true, "fa0f36160bd613ec2296a72e0eeff07a7d3df029508e02b733f524e647365218")]
    [InlineData("result", false, "95cc55e5c362a0b76c4cda11a1d66b6799f459cf7bc46978e99b25add2844f1a")]
    [InlineData("definition", false, "95cc55e5c362a0b76c4cda11a1d66b6799f459cf7bc46978e99b25add2844f1a")]
    [InlineData("outside-path", true, "95cc55e5c362a0b76c4cda11a1d66b6799f459cf7bc46978e99b25add2844f1a")]
    [InlineData("other-attribute", true, "95cc55e5c362a0b76c4cda11a1d66b6799f459cf7bc46978e99b25add2844f1a")]
    [InlineData("other-element", true, "95cc55e5c362a0b76c4cda11a1d66b6799f459cf7bc46978e99b25add2844f1a")]
    [InlineData("mutated-name", true, "95cc55e5c362a0b76c4cda11a1d66b6799f459cf7bc46978e99b25add2844f1a")]
    [InlineData("extra-secret", true, "95cc55e5c362a0b76c4cda11a1d66b6799f459cf7bc46978e99b25add2844f1a")]
    [InlineData("malformed", true, "95cc55e5c362a0b76c4cda11a1d66b6799f459cf7bc46978e99b25add2844f1a")]
    [InlineData("wrong-namespace", true, "95cc55e5c362a0b76c4cda11a1d66b6799f459cf7bc46978e99b25add2844f1a")]
    [InlineData("result", false, "708f75f3c51d38e3e826a1a68580073fc969a6629cd0ae27c961a62c56065d47")]
    [InlineData("definition", false, "708f75f3c51d38e3e826a1a68580073fc969a6629cd0ae27c961a62c56065d47")]
    [InlineData("outside-path", true, "708f75f3c51d38e3e826a1a68580073fc969a6629cd0ae27c961a62c56065d47")]
    [InlineData("other-attribute", true, "708f75f3c51d38e3e826a1a68580073fc969a6629cd0ae27c961a62c56065d47")]
    [InlineData("other-element", true, "708f75f3c51d38e3e826a1a68580073fc969a6629cd0ae27c961a62c56065d47")]
    [InlineData("mutated-name", true, "708f75f3c51d38e3e826a1a68580073fc969a6629cd0ae27c961a62c56065d47")]
    [InlineData("extra-secret", true, "708f75f3c51d38e3e826a1a68580073fc969a6629cd0ae27c961a62c56065d47")]
    [InlineData("malformed", true, "708f75f3c51d38e3e826a1a68580073fc969a6629cd0ae27c961a62c56065d47")]
    [InlineData("wrong-namespace", true, "708f75f3c51d38e3e826a1a68580073fc969a6629cd0ae27c961a62c56065d47")]
    [InlineData("result", false, "8cdc24d9b328aace2cd1dc0f68033b32631060cd1a8a0a3db9feea804ffd7b7e")]
    [InlineData("definition", false, "8cdc24d9b328aace2cd1dc0f68033b32631060cd1a8a0a3db9feea804ffd7b7e")]
    [InlineData("outside-path", true, "8cdc24d9b328aace2cd1dc0f68033b32631060cd1a8a0a3db9feea804ffd7b7e")]
    [InlineData("other-attribute", true, "8cdc24d9b328aace2cd1dc0f68033b32631060cd1a8a0a3db9feea804ffd7b7e")]
    [InlineData("other-element", true, "8cdc24d9b328aace2cd1dc0f68033b32631060cd1a8a0a3db9feea804ffd7b7e")]
    [InlineData("mutated-name", true, "8cdc24d9b328aace2cd1dc0f68033b32631060cd1a8a0a3db9feea804ffd7b7e")]
    [InlineData("extra-secret", true, "8cdc24d9b328aace2cd1dc0f68033b32631060cd1a8a0a3db9feea804ffd7b7e")]
    [InlineData("malformed", true, "8cdc24d9b328aace2cd1dc0f68033b32631060cd1a8a0a3db9feea804ffd7b7e")]
    [InlineData("wrong-namespace", true, "8cdc24d9b328aace2cd1dc0f68033b32631060cd1a8a0a3db9feea804ffd7b7e")]
    [InlineData("result", false, "607fac6462e42415e0b3749b93cd1db04a5feaa6ab58c662fccb2934270db812")]
    [InlineData("definition", false, "607fac6462e42415e0b3749b93cd1db04a5feaa6ab58c662fccb2934270db812")]
    [InlineData("outside-path", true, "607fac6462e42415e0b3749b93cd1db04a5feaa6ab58c662fccb2934270db812")]
    [InlineData("other-attribute", true, "607fac6462e42415e0b3749b93cd1db04a5feaa6ab58c662fccb2934270db812")]
    [InlineData("other-element", true, "607fac6462e42415e0b3749b93cd1db04a5feaa6ab58c662fccb2934270db812")]
    [InlineData("mutated-name", true, "607fac6462e42415e0b3749b93cd1db04a5feaa6ab58c662fccb2934270db812")]
    [InlineData("extra-secret", true, "607fac6462e42415e0b3749b93cd1db04a5feaa6ab58c662fccb2934270db812")]
    [InlineData("malformed", true, "607fac6462e42415e0b3749b93cd1db04a5feaa6ab58c662fccb2934270db812")]
    [InlineData("wrong-namespace", true, "607fac6462e42415e0b3749b93cd1db04a5feaa6ab58c662fccb2934270db812")]
    [InlineData("result", false, "d85963155dbfecf30616f7010a79c975a5747b005ea709ef54baefde579794a4")]
    [InlineData("definition", false, "d85963155dbfecf30616f7010a79c975a5747b005ea709ef54baefde579794a4")]
    [InlineData("outside-path", true, "d85963155dbfecf30616f7010a79c975a5747b005ea709ef54baefde579794a4")]
    [InlineData("other-attribute", true, "d85963155dbfecf30616f7010a79c975a5747b005ea709ef54baefde579794a4")]
    [InlineData("other-element", true, "d85963155dbfecf30616f7010a79c975a5747b005ea709ef54baefde579794a4")]
    [InlineData("mutated-name", true, "d85963155dbfecf30616f7010a79c975a5747b005ea709ef54baefde579794a4")]
    [InlineData("extra-secret", true, "d85963155dbfecf30616f7010a79c975a5747b005ea709ef54baefde579794a4")]
    [InlineData("malformed", true, "d85963155dbfecf30616f7010a79c975a5747b005ea709ef54baefde579794a4")]
    [InlineData("wrong-namespace", true, "d85963155dbfecf30616f7010a79c975a5747b005ea709ef54baefde579794a4")]
    public void SyntheticTrxRecognition_RequiresExactTestMetadata(string mutation, bool rejected, string fixtureHash)
    {
        const string directory = "_bmad-output/implementation-artifacts/evidence/story-8-3/closure-2026-10-09/";
        const string trxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        string receipt = fixtureHash is "d0047b8113e592d3e3ade8137e8f45e7c0753f27d9171aca0b5d9073b922b71e" or "55e36b209d7e3df100a62f76fd8c707a7019005ad0f84ebc353517cdef9e174b" ? "contracts-results.trx.xml" : "server-results.trx.xml";
        XDocument original = XDocument.Load(Path.Combine(RepoRoot, directory + receipt));
        string name = original.Descendants(XName.Get("UnitTestResult", trxNamespace))
            .Select(element => element.Attribute("testName")!.Value)
            .Single(value => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant() == fixtureHash);
        if (mutation == "mutated-name")
        {
            name += RandomSecret();
        }

        string escaped = System.Security.SecurityElement.Escape(name)!;
        string body = mutation switch
        {
            "definition" => "<TestDefinitions><UnitTest name=\"" + escaped + "\" /></TestDefinitions>",
            "other-attribute" => "<Results><UnitTestResult outcome=\"" + escaped + "\" /></Results>",
            "other-element" => "<Results><Message testName=\"" + escaped + "\" /></Results>",
            _ => "<Results><UnitTestResult testName=\"" + escaped + "\" /></Results>",
        };
        if (mutation == "extra-secret")
        {
            body += "<Message>" + Assignment("pass" + "word", "\"" + RandomSecret() + "\"") + "</Message>";
        }

        string content = "<TestRun xmlns=\"" + (mutation == "wrong-namespace" ? "urn:other" : trxNamespace) + "\">" + body + "</TestRun>";
        if (mutation == "malformed")
        {
            content += "<";
        }

        string path = mutation == "outside-path" ? "tools/candidate.trx.xml" : directory + "candidate.trx.xml";
        FindViolations(path, content).Any().ShouldBe(rejected);
        IsExplicitGeneratedPath(path).ShouldBeFalse();
    }

    /// <summary>Frozen binding metadata requires exact reviewed bytes and path.</summary>
    [Theory]
    [InlineData("_bmad-output/implementation-artifacts/evidence/story-8-3/closure-2026-10-09/before-authorized-remediation-binding.json")]
    public void SyntheticBindingRecognition_RequiresExactSealedContent(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string content = File.ReadAllText(Path.Combine(RepoRoot, path));
        FindViolations(path, content).ShouldBeEmpty();
        FindViolations("tools/binding.json", content).ShouldNotBeEmpty();
        FindViolations(path, content + " ").ShouldNotBeEmpty();
        FindViolations(path, content + "\n" + Assignment("pass" + "word", "\"" + RandomSecret() + "\"")).ShouldNotBeEmpty();
    }

    /// <summary>Candidate receipts and sanitized replacements are scanned even before Git tracks them.</summary>
    [Fact]
    public void DatedCandidateReportsAndSanitizedCaptures_AreScannedDirectly()
    {
        string receipts = Path.Combine(RepoRoot, "_bmad-output/implementation-artifacts/evidence/story-8-3");
        string[] reports = Directory.GetFiles(receipts, "*.trx.xml", SearchOption.AllDirectories);
        reports.ShouldNotBeEmpty();
        string[] captures =
        [
            "_bmad-output/implementation-artifacts/evidence/story-6-6/otlp-capture-remediation-2026-10-09/story66-callback-aspire-baseline.json",
            "_bmad-output/implementation-artifacts/evidence/story-6-6/otlp-capture-remediation-2026-10-09/story66-command-state-aspire-baseline.json",
        ];
        string[] datedDirectories =
        [
            Path.Combine(receipts, "closure-2026-10-09"),
            Path.Combine(RepoRoot, "_bmad-output/implementation-artifacts/evidence/story-6-6/otlp-capture-remediation-2026-10-09"),
        ];
        IEnumerable<string> metadata = datedDirectories.SelectMany(directory =>
            Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)));
        foreach (string path in reports.Concat(metadata).Select(path => Path.GetRelativePath(RepoRoot, path).Replace('\\', '/')).Concat(captures).Distinct(StringComparer.Ordinal))
        {
            File.Exists(Path.Combine(RepoRoot, path)).ShouldBeTrue();
            FindViolations(path).ShouldBeEmpty("Candidate content is scanned directly: " + path);
        }
    }

    /// <summary>Only the two owner-authorized OTLP originals are eligible for governed retirement.</summary>
    [Fact]
    public void OtlpRetirementEligibility_IsLimitedToExactOriginalPaths()
    {
        foreach (string path in new[] { RetiredCallbackOtlpCapture, RetiredCommandStateOtlpCapture })
        {
            IsGovernedRetirementPath(path).ShouldBeTrue();
            IsExplicitGeneratedPath(path).ShouldBeFalse();
            IsGovernedRetirementPath(path + ".orig").ShouldBeFalse();
            IsGovernedRetirementPath(path.Replace("2026-10-08", "2026-10-09", StringComparison.Ordinal)).ShouldBeFalse();
            // Path eligibility alone never removes credential detection from the raw capture.
            FindViolations(path, Assignment("pass" + "word", "\"" + RandomSecret() + "\"")).ShouldBe([path + ":1"]);
        }
        IsGovernedRetirementPath("_bmad-output/implementation-artifacts/evidence/story-6-6/arbitrary.json").ShouldBeFalse();
        IsGovernedRetirementPath("_bmad-output/implementation-artifacts/evidence/story-6-6/otlp-capture-remediation-2026-10-09/story66-callback-aspire-baseline.json").ShouldBeFalse();
    }

    [Theory]
    [InlineData("null", true, false)]
    [InlineData("\"null\"", true, true)]
    [InlineData("null", false, true)]
    public void JsonNull_IsAllowedOnlyAsAParsedBareValue(string value, bool validJson, bool violation)
    {
        string content = "{\"pass" + "word\":" + value + "}" + (validJson ? string.Empty : " trailing-text");
        FindViolations("capture.log", content).Any().ShouldBe(violation);
    }

    /// <summary>
    /// Verifies only the framework boolean cancellation constructor is recognized as noncredential source.
    /// </summary>
    /// <param name="extension">The C# source surface.</param>
    /// <param name="type">The supported framework type spelling.</param>
    [Theory]
    [InlineData(".cs", "CancellationToken")]
    [InlineData(".razor", "CancellationToken")]
    [InlineData(".cs", "System.Threading.CancellationToken")]
    [InlineData(".razor", "System.Threading.CancellationToken")]
    [InlineData(".cs", "global::System.Threading.CancellationToken")]
    [InlineData(".razor", "global::System.Threading.CancellationToken")]
    public void CancellationTokenBooleanConstructor_IsAllowedOnlyAsExactSource(string extension, string type)
    {
        string path = "tests/fixture" + extension;
        string name = "to" + "ken";
        foreach (string boolean in new[] { "true", "false" })
        {
            foreach (string argument in new[] { boolean, "canceled: " + boolean })
            {
                FindViolations(path, Assignment(name, "new " + type + "( " + argument + " )")).ShouldBeEmpty();
                FindViolations(path, Assignment(name, "new OtherToken(" + argument + ")")).ShouldBe([path + ":1"]);
                FindViolations(path, Assignment(name, "new Other.CancellationToken(" + argument + ")")).ShouldBe([path + ":1"]);
            }
        }

        foreach (string operand in new[] { "\"null\"", "\"" + RandomSecret() + "\"", "1", "true, false" })
        {
            foreach (string argument in new[] { operand, "canceled: " + operand })
            {
                FindViolations(path, Assignment(name, "new " + type + "(" + argument + ")")).ShouldBe([path + ":1"]);
            }
        }

        FindViolations(path, Assignment(name, "new " + type + "(true) + \"" + RandomSecret() + "\""))
            .ShouldBe([path + ":1"]);
        FindViolations(path, Assignment(name, "new " + type + "(true)") + "\n" + Assignment("pass" + "word", "\"null\""))
            .ShouldBe([path + ":2"]);
        FindViolations(path, "string fixture = \"" + Assignment(name, "new " + type + "(true)") + "\";")
            .ShouldBe([path + ":1"]);
        FindViolations("fixture.log", Assignment(name, "new " + type + "(true)")).ShouldBe(["fixture.log:1"]);
    }

    /// <summary>
    /// Verifies constructor labels are removed only at argument boundaries, preserving runtime ternary operands.
    /// </summary>
    /// <param name="extension">The C# source surface.</param>
    [Theory]
    [InlineData(".cs")]
    [InlineData(".razor")]
    public void ConstructorArgumentLabels_PreserveRuntimeTernaryOperands(string extension)
    {
        string path = "tests/fixture" + extension;
        string name = "access" + "Token";
        FindViolations(path, Assignment(name, "new AccessToken(value: true ? sourceToken : null)")).ShouldBeEmpty();
        FindViolations(path, Assignment(name, "new AccessToken(false, value: true ? sourceToken : null)")).ShouldBeEmpty();
        FindViolations(path, Assignment(name, "new AccessToken(value: true)")).ShouldBe([path + ":1"]);
        FindViolations(path, Assignment(name, "new AccessToken(false, value: true)")).ShouldBe([path + ":1"]);
    }

    [Theory]
    [InlineData("lock")]
    [InlineData("assets")]
    [InlineData("capture")]
    public void NuGetDependencyMetadata_ExemptsOnlySchemaBoundPackageNames(string shape)
    {
        string packageName = "Microsoft.IdentityModel." + "Tokens";
        string digest = Convert.ToBase64String(new byte[64]);
        string package = "{\"type\":\"Direct\",\"resolved\":\"8.14.0\",\"contentHash\":\"" + digest + "\","
            + "\"dependencies\":{\"" + packageName + "\":\"8.14.0\"}}";
        string target = "{\"net10.0\":{\"Sample.Package/1.0.0\":{\"dependencies\":{\""
            + packageName + "\":\"8.14.0\"}}}}";
        string libraries = "{\"Sample.Package/1.0.0\":{\"type\":\"package\"},\"" + packageName
            + "/8.14.0\":{\"type\":\"package\",\"sha512\":\"" + digest + "\"}}";
        string content = shape switch
        {
            "lock" => "{\"version\":1,\"dependencies\":{\"net10.0\":{\"" + packageName + "\":" + package + "}}}",
            "assets" => "{\"targets\":" + target + ",\"libraries\":" + libraries + "}",
            _ => "{\"localReleaseArtifacts\":[{\"depsDeclarations\":[{\"declaredTargets\":"
                + target + ",\"libraries\":" + libraries + ",\"runtimeTarget\":{}}]}]}",
        };
        FindViolations("capture.json", content).ShouldBeEmpty();
        FindViolations("capture.json", content + " trailing-text").ShouldNotBeEmpty();
        FindViolations("capture.json", "{\"" + packageName + "\":\"8.14.0\"}").ShouldNotBeEmpty();
        string adjacentField = content.Insert(1, "\"pass" + "word\":\"" + RandomSecret() + "\",");
        FindViolations("capture.json", adjacentField).ShouldBe(["capture.json:1"]);
        string unicodePrefix = content.Insert(1, "\"label\":\"é😀\",");
        FindViolations("capture.json", unicodePrefix).ShouldBeEmpty();
        string invalidVersions = content.Replace("8.14.0", RandomSecret(), StringComparison.Ordinal);
        FindViolations("capture.json", invalidVersions).ShouldNotBeEmpty();
        string lookalike = "{\"libraries\":{},\"targets\":{\"tenant\":{\"foo/bar\":{\"dependencies\":{\"database."
            + "password\":\"1.2.3\"}}}}}";
        FindViolations("capture.json", lookalike).ShouldBe(["capture.json:1"]);
        FindViolations("capture.json", lookalike.Replace("database.password", packageName, StringComparison.Ordinal))
            .ShouldBe(["capture.json:1"]);
        if (shape != "lock")
        {
            FindViolations("capture.json", content.Replace(
                    "\"Sample.Package/1.0.0\":{\"type\":\"package\"}",
                    "\"Sample.Package/1.0.0\":\"unexpected\"", StringComparison.Ordinal))
                .ShouldBe(["capture.json:1"]);
            FindViolations("capture.json", "{\"libraries\":{},\"targets\":{\"net10.0\":[{\"dependencies\":{\""
                + packageName + "\":\"8.14.0\"}}]}}")
                .ShouldBe(["capture.json:1"]);
        }
    }

    [Theory]
    [InlineData("8.14.0+build.1")]
    [InlineData("[8.14.0,9.0.0)")]
    [InlineData("[8.14.0]")]
    [InlineData("(,8.14.0]")]
    [InlineData("8.14.0.1")]
    [InlineData("[8.14.0,)")]
    [InlineData("[8.14.0,8.14.0]")]
    [InlineData("[8.14.0-alpha.2,8.14.0-alpha.10)")]
    [InlineData("[8.14.0-alpha,8.14.0)")]
    [InlineData("[8.14.0+build.1,8.14.0+build.2]")]
    public void NuGetDependencyMetadata_AllowsVersionMetadataAndRanges(string version)
    {
        string packageName = "Microsoft.IdentityModel." + "Tokens";
        string digest = Convert.ToBase64String(new byte[64]);
        string content = "{\"version\":1,\"dependencies\":{\"net10.0\":{\"Sample.Package\":{\"type\":\"Direct\","
            + "\"resolved\":\"1.0.0\",\"contentHash\":\"" + digest + "\",\"dependencies\":{\""
            + packageName + "\":\"" + version + "\"}}}}}";
        FindViolations("packages.lock.json", content).ShouldBeEmpty();
        FindViolations("packages.lock.json", content.Replace(version, RandomSecret(), StringComparison.Ordinal))
            .ShouldNotBeEmpty();
    }

    /// <summary>
    /// Verifies malformed or empty NuGet intervals cannot classify a credential property as package metadata.
    /// </summary>
    /// <param name="version">The invalid dependency range.</param>
    [Theory]
    [InlineData("")]
    [InlineData("^8.14.0")]
    [InlineData("~8.14.0")]
    [InlineData("[,]")]
    [InlineData("(,)")]
    [InlineData("[,8.14.0]")]
    [InlineData("[8.14.0,]")]
    [InlineData("(8.14.0)")]
    [InlineData("[8.14.0,9.0.0")]
    [InlineData("[8.14.0,9.0.0,10.0.0]")]
    [InlineData("[9.0.0,8.14.0]")]
    [InlineData("(8.14.0,8.14.0)")]
    [InlineData("[8.14.0,8.14.0)")]
    [InlineData("(8.14.0,8.14.0]")]
    [InlineData("[8.14.0,8.14.0-alpha]")]
    [InlineData("[8.14.0-alpha.10,8.14.0-alpha.2]")]
    [InlineData("[8.14.0.2,8.14.0.1]")]
    [InlineData("[8.14.0+build.1,8.14.0+build.2)")]
    public void NuGetDependencyMetadata_RejectsMalformedEmptyOrReversedIntervals(string version)
    {
        ArgumentNullException.ThrowIfNull(version);
        IsNuGetDependencyVersion(version).ShouldBeFalse();
        string packageName = "Microsoft.IdentityModel." + "Tokens";
        string digest = Convert.ToBase64String(new byte[64]);
        string content = "{\"version\":1,\"dependencies\":{\"net10.0\":{\"Sample.Package\":{\"type\":\"Direct\","
            + "\"resolved\":\"1.0.0\",\"contentHash\":\"" + digest + "\",\"dependencies\":{\""
            + packageName + "\":\"" + version + "\"}}}}}";
        // An empty assignment has no credential bytes for the scanner to report, but is never an exempt edge.
        if (version.Length > 0)
        {
            FindViolations("packages.lock.json", content).ShouldBe(["packages.lock.json:1"]);
        }
    }

    private static IEnumerable<string> FindViolations(string relativePath)
    {
        string content = File.ReadAllText(Path.Combine(RepoRoot, relativePath));
        return FindViolations(relativePath, content);
    }

    private static IEnumerable<string> FindViolations(string relativePath, string content)
    {
        content = MaskKnownSyntheticBindingTestNames(relativePath, content);
        content = MaskKnownSyntheticTrxTestNames(relativePath, content);
        var violationLines = new SortedSet<int>();
        (HashSet<int> nullValues, HashSet<int> dependencyNames) = GetJsonNonSecretMetadata(content);
        void Record(int index, string _) => violationLines.Add(LineNumber(content, index));

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
                && !IsPublicGitHubSshUsername(match.Value)
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
            if (dependencyNames.Contains(match.Groups["name"].Index)
                || match.Groups["bare"].Success && nullValues.Contains(match.Groups["value"].Index)
                || IsArgparseMetavar(content, match.Groups["name"].Index)
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

            if (!IsNonCredentialFixtureAssignment(effectivePath, content, match)
                && IsUsableLiteral(
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

    private static bool IsNonCredentialFixtureAssignment(string path, string content, Match assignment)
    {
        int start = content.LastIndexOf('\n', Math.Max(0, assignment.Index - 1)) + 1;
        int end = content.IndexOf('\n', assignment.Index);
        string line = content[start..(end < 0 ? content.Length : end)].Trim();
        // These are reviewed state-address/protocol fixtures, not a general StorageKey or token exemption.
        string lineHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(line))).ToLowerInvariant();
        return (path, assignment.Groups["name"].Value, lineHash) switch
        {
            ("tests/Hexalith.EventStore.Server.Tests/Security/GovernanceGuardFixture.cs", "token", "f26481a946a1ce2f8b4dc68ded2466a055e5abf0466bea4ead8f5bb5c0a3cb03") => true,
            _ => false,
        };
    }

    private static bool IsKnownSyntheticTestNameHash(string hash)
        => hash is "d0047b8113e592d3e3ade8137e8f45e7c0753f27d9171aca0b5d9073b922b71e"
            or "55e36b209d7e3df100a62f76fd8c707a7019005ad0f84ebc353517cdef9e174b"
            or "411f2812dd2931fcf20be8d4985bad37487c7cabb23be39fcec70337fc5917dd"
            or "fa0f36160bd613ec2296a72e0eeff07a7d3df029508e02b733f524e647365218"
            or "95cc55e5c362a0b76c4cda11a1d66b6799f459cf7bc46978e99b25add2844f1a"
            or "708f75f3c51d38e3e826a1a68580073fc969a6629cd0ae27c961a62c56065d47"
            or "8cdc24d9b328aace2cd1dc0f68033b32631060cd1a8a0a3db9feea804ffd7b7e"
            or "607fac6462e42415e0b3749b93cd1db04a5feaa6ab58c662fccb2934270db812"
            or "d85963155dbfecf30616f7010a79c975a5747b005ea709ef54baefde579794a4";

    private static string MaskKnownSyntheticBindingTestNames(string path, string content)
    {
        if (!string.Equals(path, "_bmad-output/implementation-artifacts/evidence/story-8-3/closure-2026-10-09/before-authorized-remediation-binding.json", StringComparison.Ordinal))
        {
            return content;
        }
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        bool reviewedBinding = (path, hash) switch
        {
            ("_bmad-output/implementation-artifacts/evidence/story-8-3/closure-2026-10-09/before-authorized-remediation-binding.json", "2d9f6bb7e4f2006249fe249e081902b7015d3d3802393b62796650aad9eb6bb6") => true,
            _ => false,
        };
        if (!reviewedBinding)
        {
            return content;
        }

        char[] masked = content.ToCharArray();
        foreach (Match match in SyntheticBindingTestNamePattern().Matches(content))
        {
            Group value = match.Groups["value"];
            string name = JsonSerializer.Deserialize<string>(value.Value)!;
            string nameHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name))).ToLowerInvariant();
            if (IsKnownSyntheticTestNameHash(nameHash))
            {
                Array.Fill(masked, ' ', value.Index + 1, value.Length - 2);
            }
        }
        return new string(masked);
    }

    [GeneratedRegex("\"testName\"\\s*:\\s*(?<value>\"(?:\\\\.|[^\"\\\\])*\")")]
    private static partial Regex SyntheticBindingTestNamePattern();

    private static string MaskKnownSyntheticTrxTestNames(string path, string content)
    {
        if (!SyntheticTrxReceiptPathPattern().IsMatch(path))
        {
            return content;
        }

        const string trxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var ranges = new List<(int Start, int Length)>();
        var lineStarts = new List<int> { 0 };
        for (int index = 0; index < content.Length; index++)
        {
            if (content[index] == '\n')
            {
                lineStarts.Add(index + 1);
            }
        }

        try
        {
            using var text = new StringReader(content);
            using XmlReader reader = XmlReader.Create(text, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            });
            bool isTrx = false;
            string? container = null;
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (reader.Depth == 0)
                {
                    isTrx = reader.LocalName == "TestRun" && reader.NamespaceURI == trxNamespace;
                }

                if (reader.Depth == 1)
                {
                    container = reader.NamespaceURI == trxNamespace ? reader.LocalName : null;
                }

                string? attribute = reader.LocalName switch
                {
                    "UnitTestResult" when reader.Depth == 2 && container == "Results" => "testName",
                    "UnitTest" when reader.Depth == 2 && container == "TestDefinitions" => "name",
                    _ => null,
                };
                if (!isTrx || reader.NamespaceURI != trxNamespace || attribute is null
                    || !reader.MoveToAttribute(attribute))
                {
                    continue;
                }

                // Exact reviewed negative-fixture display name only; never arbitrary theory arguments.
                string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reader.Value))).ToLowerInvariant();
                if (IsKnownSyntheticTestNameHash(hash))
                {
                    var info = (IXmlLineInfo)reader;
                    int start = lineStarts[info.LineNumber - 1] + info.LinePosition - 1;
                    int equals = content.IndexOf('=', start);
                    int quote = equals + 1;
                    while (quote < content.Length && char.IsWhiteSpace(content[quote]))
                    {
                        quote++;
                    }

                    if (quote >= content.Length || content[quote] is not ('\"' or '\''))
                    {
                        return content;
                    }

                    int end = content.IndexOf(content[quote], quote + 1);
                    if (end < 0)
                    {
                        return content;
                    }

                    ranges.Add((quote + 1, end - quote - 1));
                }

                reader.MoveToElement();
            }
        }
        catch (XmlException)
        {
            // Malformed reports receive the complete ordinary credential scan.
            return content;
        }

        char[] masked = content.ToCharArray();
        foreach ((int start, int length) in ranges)
        {
            for (int index = start; index < start + length; index++)
            {
                if (masked[index] is not ('\r' or '\n'))
                {
                    masked[index] = ' ';
                }
            }
        }

        return new string(masked);
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
            if (string.Equals(path, SealedP1RRemediationSourceCapture, StringComparison.Ordinal))
            {
                VerifySealedP1RRemediationSourceCapture(File.ReadAllBytes(Path.Combine(RepoRoot, path)));
            }
            else if (string.Equals(path, SealedCounterSerializationTestResults, StringComparison.Ordinal))
            {
                VerifySealedCounterSerializationTestResults(File.ReadAllBytes(Path.Combine(RepoRoot, path)));
            }

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

    private static void VerifySealedP1RRemediationSourceCapture(byte[] bytes)
    {
        // The sealed capture is LF-only and its packet has no eol rule, so only CRLF pairs are normalized.
        byte[] lineFeedOnly = bytes
            .Where((value, index) => value != (byte)'\r' || index + 1 >= bytes.Length || bytes[index + 1] != (byte)'\n')
            .ToArray();
        Convert.ToHexString(SHA256.HashData(lineFeedOnly)).ToLowerInvariant().ShouldBe(
            SealedP1RRemediationSourceCaptureSha256,
            "The sealed 6-1-p1r-remediation source capture changed, so its scan exemption no longer applies. Restore "
            + "the sealed content; never re-pin this hash without rescanning the changed capture.");
    }

    private static bool IsGovernedRetirementPath(string path)
        => GovernedEvidenceCapturePathPattern().IsMatch(path)
            || OwnerApprovedProofPacketPathPattern().IsMatch(path)
            || string.Equals(path, RetiredCallbackOtlpCapture, StringComparison.Ordinal)
            || string.Equals(path, RetiredCommandStateOtlpCapture, StringComparison.Ordinal);

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
        => ExplicitUiTestArtifactPathPattern().IsMatch(path)
            || ExplicitEvidenceArtifactPathPattern().IsMatch(path)
            || string.Equals(path, SealedCounterSerializationTestResults, StringComparison.Ordinal);

    private static void VerifySealedCounterSerializationTestResults(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant().ShouldBe(
            SealedCounterSerializationTestResultsSha256,
            "The sealed serialization test results changed; their scan exemption no longer applies.");

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
        if (candidate.EndsWith('\\'))
        {
            candidate = candidate.TrimEnd('\\').Trim();
        }

        if (candidate.Length >= 2
            && candidate[0] is '"' or '\''
            && candidate[^1] == candidate[0])
        {
            candidate = candidate[1..^1];
        }

        if (candidate.EndsWith('\\'))
        {
            candidate = candidate.TrimEnd('\\').Trim();
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

    private static (HashSet<int> NullValues, HashSet<int> DependencyNames) GetJsonNonSecretMetadata(string content)
    {
        HashSet<int> nullValues = [];
        HashSet<int> dependencyNames = [];
        try
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(content);
            using JsonDocument document = JsonDocument.Parse(utf8);
            var reader = new Utf8JsonReader(utf8);
            _ = reader.Read();
            (int ByteIndex, int CharIndex) position = (0, 0);
            CollectJsonNonSecretMetadata(
                ref reader, document.RootElement, null, [], utf8, ref position, nullValues, dependencyNames);
        }
        catch (JsonException)
        {
            // A malformed capture grants no structural exceptions, including any valid-looking prefix.
            nullValues.Clear();
            dependencyNames.Clear();
        }

        return (nullValues, dependencyNames);
    }

    private static void CollectJsonNonSecretMetadata(
        ref Utf8JsonReader reader,
        JsonElement element,
        string? name,
        List<(string? Name, JsonElement Element)> ancestors,
        byte[] utf8,
        ref (int ByteIndex, int CharIndex) position,
        HashSet<int> nullValues,
        HashSet<int> dependencyNames)
    {
        ancestors.Add((name, element));
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                _ = reader.Read();
                if (IsNuGetDependencyProperty(ancestors, property))
                {
                    dependencyNames.Add(GetJsonCharIndex(utf8, (int)reader.TokenStartIndex, ref position) + 1);
                }

                _ = reader.Read();
                CollectJsonNonSecretMetadata(
                    ref reader, property.Value, property.Name, ancestors, utf8, ref position, nullValues, dependencyNames);
            }

            _ = reader.Read();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                _ = reader.Read();
                CollectJsonNonSecretMetadata(ref reader, child, null, ancestors, utf8, ref position, nullValues, dependencyNames);
            }

            _ = reader.Read();
        }
        else if (element.ValueKind == JsonValueKind.Null)
        {
            nullValues.Add(GetJsonCharIndex(utf8, (int)reader.TokenStartIndex, ref position));
        }

        ancestors.RemoveAt(ancestors.Count - 1);
    }

    private static int GetJsonCharIndex(byte[] utf8, int byteIndex, ref (int ByteIndex, int CharIndex) position)
    {
        // Tokens arrive in source order, so each UTF-8 byte is counted at most once.
        position.CharIndex += Encoding.UTF8.GetCharCount(utf8.AsSpan(position.ByteIndex, byteIndex - position.ByteIndex));
        position.ByteIndex = byteIndex;
        return position.CharIndex;
    }

    private static bool IsNuGetDependencyProperty(
        List<(string? Name, JsonElement Element)> ancestors,
        JsonProperty property)
    {
        // Recognize NuGet package identities only at their schema positions, never arbitrary credential fields.
        if (property.Name is not ("Microsoft.IdentityModel.Tokens"
                or "Microsoft.IdentityModel.JsonWebTokens"
                or "System.IdentityModel.Tokens.Jwt")
            || ancestors.Count < 3)
        {
            return false;
        }

        if (ancestors[^2].Name == "dependencies"
            && IsLockDocument(ancestors[^3].Element)
            && IsLockPackage(property.Value))
        {
            return true;
        }

        if (ancestors[^1].Name != "dependencies"
            || property.Value.ValueKind != JsonValueKind.String
            || !IsNuGetDependencyVersion(property.Value.GetString()))
        {
            return false;
        }

        if (ancestors.Count >= 5
            && ancestors[^4].Name == "dependencies"
            && IsLockDocument(ancestors[^5].Element)
            && IsLockPackage(ancestors[^2].Element))
        {
            return true;
        }

        if (ancestors.Count < 5
            || ancestors[^4].Name is not ("targets" or "declaredTargets")
            || ancestors[^2].Name is not { } parentName
            || parentName.LastIndexOf('/') <= 0
            || !ancestors[^5].Element.TryGetProperty("libraries", out JsonElement libraries)
            || libraries.ValueKind != JsonValueKind.Object
            || !IsNuGetVersion(parentName[(parentName.LastIndexOf('/') + 1)..])
            || !libraries.TryGetProperty(parentName, out JsonElement parent)
            || parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty("type", out JsonElement parentType)
            || parentType.ValueKind != JsonValueKind.String
            || parentType.GetString() is not ("package" or "project"))
        {
            return false;
        }

        return libraries.EnumerateObject().Any(library =>
            library.Name.StartsWith(property.Name + "/", StringComparison.Ordinal)
            && IsNuGetVersion(library.Name[(property.Name.Length + 1)..])
            && library.Value.ValueKind == JsonValueKind.Object
            && library.Value.TryGetProperty("type", out JsonElement type)
            && type.ValueKind == JsonValueKind.String
            && type.GetString() == "package"
            && library.Value.TryGetProperty("sha512", out JsonElement hash)
            && IsNuGetContentHash(hash));
    }

    private static bool IsLockDocument(JsonElement element)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("version", out JsonElement version)
            && version.ValueKind == JsonValueKind.Number
            && version.TryGetInt32(out int value)
            && value is 1 or 2;

    private static bool IsLockPackage(JsonElement element)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("type", out JsonElement type)
            && type.ValueKind == JsonValueKind.String
            && type.GetString() is "Direct" or "Transitive"
            && element.TryGetProperty("resolved", out JsonElement resolved)
            && resolved.ValueKind == JsonValueKind.String
            && IsNuGetVersion(resolved.GetString())
            && element.TryGetProperty("contentHash", out JsonElement hash)
            && IsNuGetContentHash(hash);

    private static bool IsNuGetContentHash(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String || element.GetString() is not { } value)
        {
            return false;
        }

        ReadOnlySpan<char> encoded = value.StartsWith("sha512-", StringComparison.Ordinal) ? value.AsSpan(7) : value;
        Span<byte> hash = stackalloc byte[64];
        return Convert.TryFromBase64Chars(encoded, hash, out int written) && written == hash.Length;
    }

    private static bool IsNuGetVersion(string? value)
        => value is not null && NuGetVersionPattern().IsMatch(value);

    private static bool IsNuGetDependencyVersion(string? value)
    {
        if (IsNuGetVersion(value))
        {
            return true;
        }

        if (value is not { Length: >= 3 } || value[0] is not ('[' or '(') || value[^1] is not (']' or ')'))
        {
            return false;
        }

        string[] bounds = value[1..^1].Split(',', StringSplitOptions.TrimEntries);
        if (bounds.Length == 1)
        {
            return value[0] == '[' && value[^1] == ']' && IsNuGetVersion(bounds[0]);
        }

        if (bounds.Length != 2
            || bounds.All(static bound => bound.Length == 0)
            || !bounds.All(static bound => bound.Length == 0 || IsNuGetVersion(bound))
            || bounds[0].Length == 0 && value[0] != '('
            || bounds[1].Length == 0 && value[^1] != ')')
        {
            return false;
        }

        if (bounds[0].Length == 0 || bounds[1].Length == 0)
        {
            return true;
        }

        int comparison = CompareNuGetVersions(bounds[0], bounds[1]);
        return comparison < 0 || comparison == 0 && value[0] == '[' && value[^1] == ']';
    }

    private static int CompareNuGetVersions(string left, string right)
    {
        // Build metadata does not affect precedence; omitted numeric components are zero.
        string[] leftParts = left.Split('+', 2)[0].Split('-', 2);
        string[] rightParts = right.Split('+', 2)[0].Split('-', 2);
        string[] leftNumbers = leftParts[0].Split('.');
        string[] rightNumbers = rightParts[0].Split('.');
        for (int index = 0; index < 4; index++)
        {
            int comparison = CompareNumericVersionIdentifiers(
                index < leftNumbers.Length ? leftNumbers[index] : "0",
                index < rightNumbers.Length ? rightNumbers[index] : "0");
            if (comparison != 0)
            {
                return comparison;
            }
        }

        if (leftParts.Length == 1 || rightParts.Length == 1)
        {
            return leftParts.Length == rightParts.Length ? 0 : leftParts.Length == 1 ? 1 : -1;
        }

        string[] leftLabels = leftParts[1].Split('.');
        string[] rightLabels = rightParts[1].Split('.');
        for (int index = 0; index < Math.Min(leftLabels.Length, rightLabels.Length); index++)
        {
            bool leftNumeric = leftLabels[index].All(char.IsAsciiDigit);
            bool rightNumeric = rightLabels[index].All(char.IsAsciiDigit);
            int comparison = leftNumeric && rightNumeric
                ? CompareNumericVersionIdentifiers(leftLabels[index], rightLabels[index])
                : leftNumeric != rightNumeric ? leftNumeric ? -1 : 1
                : StringComparer.OrdinalIgnoreCase.Compare(leftLabels[index], rightLabels[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return leftLabels.Length.CompareTo(rightLabels.Length);
    }

    private static int CompareNumericVersionIdentifiers(string left, string right)
    {
        left = left.TrimStart('0');
        right = right.TrimStart('0');
        return left.Length != right.Length ? left.Length.CompareTo(right.Length) : StringComparer.Ordinal.Compare(left, right);
    }

    [GeneratedRegex(@"^[0-9]+(?:\.[0-9]+){0,3}(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex NuGetVersionPattern();

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

    /// <summary>
    /// Identifies GitHub's password-free SSH transport identity, whose credentials are external keys.
    /// </summary>
    private static bool IsPublicGitHubSshUsername(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && string.Equals(uri.Scheme, "ssh", StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.UserInfo, "git", StringComparison.Ordinal);

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
            return true;
        }

        if (JavaScriptAwaitedMemberCallPattern().IsMatch(value))
        {
            return true;
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
        if (relativePath.StartsWith(".playwright-cli/traces/", StringComparison.Ordinal)
            && relativePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
        {
            // Playwright stores fetched script resources under content-addressed .dat names.
            // Treat them as JavaScript source for expression analysis; this does not exclude any
            // decoded content and literal credentials remain violations.
            return relativePath + ".js";
        }

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
        if (value is "string.Empty" or "default"
            || CancellationTokenBooleanConstructorPattern().IsMatch(value)
            || CancellationTokenSourceEmptyConstructorPattern().IsMatch(value)
            || TypedCancellationArgumentPattern().IsMatch(value))
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
        if (value.Length > 3 && value.StartsWith("new", StringComparison.Ordinal) && char.IsWhiteSpace(value[3]))
        {
            // A constructor's named-argument labels alone do not supply a runtime value (for example canceled: true).
            withoutLiterals = Regex.Replace(withoutLiterals, @"(^|,)\s*[A-Za-z_][A-Za-z0-9_]*\s*:(?!:)", "$1", RegexOptions.CultureInvariant);
        }

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

    [GeneratedRegex(@"\A_bmad-output/implementation-artifacts/evidence/story-8-3/(?:closure|verification)-[0-9]{4}-[0-9]{2}-[0-9]{2}(?:-postreview)?/[^/]+\.trx\.xml\z", RegexOptions.CultureInvariant)]
    private static partial Regex SyntheticTrxReceiptPathPattern();

    [GeneratedRegex(@"\Anew\s+(?:CancellationTokenSource|(?:global::)?System\.Threading\.CancellationTokenSource)\s*\(\s*\)\z", RegexOptions.CultureInvariant)]
    private static partial Regex CancellationTokenSourceEmptyConstructorPattern();

    [GeneratedRegex(@"\Acall\.Arg\s*<\s*(?:CancellationToken|(?:global::)?System\.Threading\.CancellationToken)\s*>\s*\(\s*\)\z", RegexOptions.CultureInvariant)]
    private static partial Regex TypedCancellationArgumentPattern();

    [GeneratedRegex(@"\Anew\s+(?:CancellationToken|(?:global::)?System\.Threading\.CancellationToken)\s*\(\s*(?:canceled\s*:\s*)?(?:true|false)\s*\)\z", RegexOptions.CultureInvariant)]
    private static partial Regex CancellationTokenBooleanConstructorPattern();

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

    [GeneratedRegex(@"^(?:await\s+)?[A-Za-z_$][A-Za-z0-9_$]*(?:(?:\??\.[A-Za-z_$][A-Za-z0-9_$]*)|(?:\[[^\]\r\n]+\]))+\(\)$", RegexOptions.CultureInvariant)]
    private static partial Regex JavaScriptAwaitedMemberCallPattern();

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

    [GeneratedRegex(@"^_bmad-output/implementation-artifacts/(?:evidence/(?:.+\.ctrf\.json|6-1-p1r-3110/verification/.+|6-1-p1r-remediation/source-candidate\.diff)|6-5d-simplification/previous-candidate\.md)$", RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitEvidenceArtifactPathPattern();
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Proves source recognition cannot conceal substituted or adjacent credentials.</summary>
public sealed partial class SecretsProtectionTests
{
    /// <summary>Decoded JSON keys and content keep JSON credential semantics despite a claimed language.</summary>
    [Fact]
    public void SourceContexts_JsonCredentialMeaningSurvivesEscapedKeysAndClaimedSourceLanguage()
    {
        string credential = RandomSecret();
        string source = "{\n  \"pass\\u0077ord\": " + JsonSerializer.Serialize(credential) + "\n}";
        FindViolations("evidence/capture.json", source).ShouldContain("evidence/capture.json:2");
        foreach (string claimed in new[] { "Program.cs", "runtime.go", "capture.py" })
        {
            foreach (string json in new[] { source, source.Replace("pass\\u0077ord", "password", StringComparison.Ordinal) })
            {
                string envelope = JsonSerializer.Serialize(new
                {
                    path = claimed,
                    sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant(),
                    content = json,
                }, new JsonSerializerOptions { WriteIndented = true });
                int line = envelope[..envelope.IndexOf("\"content\"", StringComparison.Ordinal)].Count(character => character == '\n') + 1;
                FindViolations("evidence/capture.json", envelope).ShouldContain("evidence/capture.json:" + line);
            }
        }
        FindViolations("Program.cs", source).ShouldContain("Program.cs:2");
        FindViolations("evidence/capture.json", "{\"pass\\u0077ord\":\"\"}").ShouldBeEmpty();
        foreach (string scalar in new[] { JsonSerializer.Serialize(credential), "123456789", "true", "false", "null", "\"\"", "\"${RUNTIME_VALUE}\"" })
        {
            string plain = "{\n  \"password\": " + scalar + "\n}";
            string escaped = plain.Replace("password", "pass\\u0077ord", StringComparison.Ordinal);
            FindViolations("evidence/scalar.json", escaped).ToArray().ShouldBe(FindViolations("evidence/scalar.json", plain).ToArray());
        }
        string dependency = "{\"dependencies\":{\"registry-auth-to\\u006ben\":\"1.2.3\"}}";
        FindViolations("package.json", dependency).ShouldBeEmpty();
        FindViolations("package.json", "{\"registry-auth-to\\u006ben\":\"1.2.3\"}").ShouldNotBeEmpty();
    }

    /// <summary>Decoded names retain the assignment grammar; slash keys do not hide scalar or adjacent credentials.</summary>
    [Fact]
    public void SourceContexts_JsonSlashKeysRetainTheOriginalAssignmentNameGrammar()
    {
        foreach ((string key, string value) in new[]
        {
            ("pkg/runtime/processor/secret", "12345"),
            ("tests/integration/suite/daprd/secret", "98765"),
            ("dapr.io/api-token-secret", JsonSerializer.Serialize("public-reference-name")),
            ("dapr.io/app-token-secret", JsonSerializer.Serialize("another-reference-name")),
        })
        {
            string plain = "{\"" + key + "\":" + value + "}";
            string escaped = plain.Replace("/", "\\u002f", StringComparison.Ordinal);
            FindViolations("evidence/control.json", plain).ShouldBeEmpty();
            FindViolations("evidence/control.json", escaped).ShouldBeEmpty();
            string adjacent = ",\"pass" + "word\":" + JsonSerializer.Serialize(RandomSecret()) + "}";
            FindViolations("evidence/control.json", plain[..^1] + adjacent).ShouldNotBeEmpty();
            FindViolations("evidence/control.json", escaped[..^1] + adjacent).ShouldNotBeEmpty();
        }
        string scalar = Assignment("pass" + "word", RandomSecret());
        FindViolations("evidence/control.json", JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["dapr.io/api-token-secret"] = scalar,
        })).ShouldNotBeEmpty();
        foreach (string path in new[]
        {
            "_bmad-output/implementation-artifacts/evidence/story-2-13/review-2026-10-21-runtime-recovery/source-tree-comparison.json",
            "_bmad-output/implementation-artifacts/evidence/story-3-21/qualification-2026-10-20/cluster-before.json",
            "_bmad-output/implementation-artifacts/evidence/story-3-21/qualification-2026-10-20/cluster-after.json",
        })
        {
            string original = File.ReadAllText(Path.Combine(RepoRoot, path));
            FindViolations(path, original).ShouldBeEmpty();
            string adjacent = ",\"pass" + "word\":" + JsonSerializer.Serialize(RandomSecret()) + "}";
            int closing = original.LastIndexOf('}');
            FindViolations(path, original[..closing] + adjacent + original[(closing + 1)..]).ShouldNotBeEmpty();
        }
    }

    /// <summary>Go runtime fields must occur in code, outside raw strings, quoted strings and comments.</summary>
    [Fact]
    public void SourceContexts_GoRuntimeFieldsCannotHideRawStringCredentials()
    {
        string field = "    Pass" + "word: " + RandomSecret() + ",\n";
        string runtime = "    Pass" + "word: runtimeValue,\n";
        FindViolations("snapshot/runtime.go", "package sample\nvar value = Config{\n" + runtime + "}\n").ShouldBeEmpty();
        foreach (string fragment in new[] { field, runtime })
        {
            FindViolations("snapshot/runtime.go", "package sample\nvar rendered = `\n" + fragment + "`\n").ShouldNotBeEmpty();
            FindViolations("snapshot/runtime.go", "package sample\n/*\n" + fragment + "*/\n").ShouldNotBeEmpty();
        }
        FindViolations("snapshot/runtime.go", "package sample\nvar value = Config{\n" + runtime + "}\n"
            + Assignment("api" + "Key", JsonSerializer.Serialize(RandomSecret()))).ShouldNotBeEmpty();
    }

    /// <summary>Every decoded capture retains source scanning and its original container line.</summary>
    /// <param name="mutation">The credential or identity change applied to the capture.</param>
    [Theory]
    [InlineData("none")]
    [InlineData("literal")]
    [InlineData("adjacent")]
    [InlineData("seal")]
    [InlineData("sibling")]
    public void SourceContexts_BoundCaptureScansCompleteSourceAndIdentity(string mutation)
    {
        string source = "string " + Assignment("pass" + "word", " = source.Value") + ";\n";
        // Assignment supplies its own equals separator.
        source = source.Replace("= =", "=", StringComparison.Ordinal);
        if (mutation == "literal") source = "string " + Assignment("pass" + "word", JsonSerializer.Serialize(RandomSecret())) + ";\n";
        if (mutation == "adjacent") source += "string " + Assignment("api" + "Key", JsonSerializer.Serialize(RandomSecret())) + ";\n";
        var capture = new Dictionary<string, string>
        {
            ["path"] = "snapshot/Test.cs",
            ["sha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant(),
            ["content"] = source,
        };
        if (mutation == "seal") capture["sha256"] = new string('0', 64);
        string json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["capture"] = capture,
            ["sibling"] = mutation == "sibling" ? Assignment("pass" + "word", RandomSecret()) : "public metadata",
        }, new JsonSerializerOptions { WriteIndented = true });
        string[] result = FindViolations("evidence/capture.json", json).ToArray();
        if (mutation == "none") result.ShouldBeEmpty();
        else result.ShouldNotBeEmpty();
        if (mutation is "literal" or "adjacent" or "seal")
        {
            int line = json[..json.IndexOf("\"content\"", StringComparison.Ordinal)].Count(character => character == '\n') + 1;
            result.ShouldContain("evidence/capture.json:" + line);
        }
    }

    /// <summary>Nested tool JSON is decoded; scalar credentials retain their owning field meaning.</summary>
    [Fact]
    public void SourceContexts_NestedScalarsNeverLoseCredentialMeaning()
    {
        string nested = JsonSerializer.Serialize(new { unrelated = RandomSecret() });
        FindViolations("evidence/tool.json", JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["pass" + "word"] = nested,
        })).ShouldNotBeEmpty();
        string credentials = Assignment("pass" + "word", RandomSecret());
        for (int depth = 0; depth < 4; depth++)
        {
            credentials = JsonSerializer.Serialize(new { text = credentials });
            FindViolations("evidence/tool.json", credentials).ShouldNotBeEmpty();
        }
    }

    /// <summary>A decoded checkout boolean is a control; literal or adjacent credentials still fail.</summary>
    [Fact]
    public void SourceContexts_CheckoutBooleanRetainsCredentialRejection()
    {
        const string path = "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run/reconciliation/parent-call-history-57af736d095149c3a14de49bcd8f9187/call_0cc49eb27a1e46eab9601033ca5d4f00.output-6025.json";
        string original = File.ReadAllText(Path.Combine(RepoRoot, path));
        string control = "persist-" + "credentials: false";
        original.ShouldContain(control);
        FindViolations(path, original).ShouldBeEmpty();
        using JsonDocument envelope = JsonDocument.Parse(original);
        string text = envelope.RootElement.GetProperty("output").EnumerateArray()
            .Select(item => item.TryGetProperty("text", out JsonElement value) ? value.GetString() : null)
            .Single(value => value is not null && value.Contains(control, StringComparison.Ordinal))!;
        using JsonDocument command = JsonDocument.Parse(text);
        string workflow = command.RootElement.GetProperty("output").GetString()!;
        static string Capture(string output) => JsonSerializer.Serialize(new
        {
            text = JsonSerializer.Serialize(new { chunk_id = "control", exit_code = 0, output }),
        });
        foreach (string replacement in new[] { RandomSecret(), JsonSerializer.Serialize(RandomSecret()), "false" + RandomSecret() })
        {
            string changed = workflow.Replace(control, "persist-" + "credentials: " + replacement,
                StringComparison.Ordinal);
            FindViolations(path, Capture(changed)).ShouldNotBeEmpty();
        }
        FindViolations(path, Capture(workflow + "\n" + Assignment("pass" + "word", RandomSecret()))).ShouldNotBeEmpty();
        FindViolations("evidence/unrelated.txt", control).ShouldNotBeEmpty();
    }

    /// <summary>Hunk markers cannot grant Python interpretation to shell or unknown output.</summary>
    /// <param name="framing">The real or forged diff language framing.</param>
    [Theory]
    [InlineData("shell")]
    [InlineData("unknown")]
    [InlineData("forged")]
    [InlineData("mixed")]
    public void SourceContexts_DiffLanguageNeverConcealsLiteralCredentials(string framing)
    {
        string injectedAssignment = Assignment("pass" + "word", RandomSecret());
        string header = "diff --git a/control.sh b/control.sh\n--- a/control.sh\n+++ b/control.sh\n";
        string body = "@@ -1,2 +1,2 @@ class Executor:\n+    def first(self):\n+        " + injectedAssignment
            + "\n@@ -4,2 +4,2 @@ class Executor:\n+    def second(self):\n+        return None\n";
        if (framing == "unknown") header = header.Replace(".sh", ".unknown", StringComparison.Ordinal);
        if (framing == "forged") header = string.Empty;
        if (framing == "mixed") header = "diff --git a/control.py b/control.py\n--- a/control.py\n+++ b/control.py\n@@ -1 +1 @@\n+value = source\n" + header;
        string output = JsonSerializer.Serialize(new { chunk_id = "control", exit_code = 0, output = header + body });
        FindViolations("evidence/tool.json", JsonSerializer.Serialize(new { text = output })).ShouldNotBeEmpty();
    }

    /// <summary>Both unchanged retained diff scalars pass, but runtime-slot substitutions and adjacency fail.</summary>
    /// <param name="name">The existing retained tool-output file.</param>
    [Theory]
    [InlineData("call_147c0acc7e2a45a09a03b8eab5859772.output-6260.json")]
    [InlineData("call_8920f237a01b4c748a10c03b97b2846a.output-5805.json")]
    public void SourceContexts_RetainedDiffFragmentsRequireCompleteRuntimeCredentialSlots(string name)
    {
        string path = "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run/reconciliation/parent-call-history-57af736d095149c3a14de49bcd8f9187/" + name;
        string original = File.ReadAllText(Path.Combine(RepoRoot, path));
        FindViolations(path, original).ShouldBeEmpty();
        string runtime = "{self.postgres_password}";
        original.ShouldContain(runtime);
        foreach (string value in new[] { RandomSecret(), RandomSecret() + runtime, runtime + RandomSecret(),
            runtime + " " + Assignment("api" + "Key", RandomSecret()) })
        {
            FindViolations(path, original.Replace(runtime, value, StringComparison.Ordinal)).ShouldNotBeEmpty();
        }
    }

    /// <summary>Source snapshots keep language rules and reject literal substitutions.</summary>
    [Fact]
    public void SourceContexts_CSharpSnapshotsKeepTheirContentsVisible()
    {
        string source = "string " + Assignment("client" + "Secret", "configuration.Value") + ";\n";
        FindViolations("evidence/Program.cs.source", source).ShouldBeEmpty();
        FindViolations("evidence/Program.cs.source", source + "string "
            + Assignment("pass" + "word", JsonSerializer.Serialize(RandomSecret())) + ";\n").ShouldNotBeEmpty();
    }

    /// <summary>Only the complete endpoint grammar proves a runtime credential value.</summary>
    /// <param name="mutation">The alteration to the runtime expression.</param>
    [Theory]
    [InlineData("none")]
    [InlineData("literal")]
    [InlineData("prefix")]
    [InlineData("suffix")]
    [InlineData("adjacent")]
    [InlineData("dictionary")]
    public void SourceContexts_PythonBrokerValuesRejectLiteralOperands(string mutation)
    {
        string password = "{self.password}";
        if (mutation == "literal") password = RandomSecret();
        if (mutation == "prefix") password = string.Concat(RandomSecret(), password);
        if (mutation == "suffix") password += RandomSecret();
        string source = "self.broker_" + "secret = {\"connectionString\": f\"amqps://qualification:" + password
            + "@127.0.0.1:{self.broker_port}/%2F\"}\n";
        if (mutation == "dictionary") source = "self.broker_" + "secret = {\"password\": f\"{runtimeValue}" + RandomSecret() + "\"}\n";
        if (mutation == "adjacent") source += Assignment("pass" + "word", JsonSerializer.Serialize(RandomSecret())) + "\n";
        if (mutation == "none") FindViolations("tools/control.py", source).ShouldBeEmpty();
        else FindViolations("tools/control.py", source).ShouldNotBeEmpty();
        string keyword = "status, body = self.request(url, " + Assignment("token", "token or self.root_token") + ", method=method)\n";
        FindViolations("tools/control.py", keyword).ShouldBeEmpty();
        FindViolations("tools/control.py", keyword.Replace("token or self.root_token", JsonSerializer.Serialize(RandomSecret()), StringComparison.Ordinal))
            .ShouldNotBeEmpty();
    }

    /// <summary>Go typed runtime diagnostics cannot hide substituted or adjacent credential bytes.</summary>
    [Fact]
    public void SourceContexts_GoValuesRequireRuntimeSyntax()
    {
        string field = "package control\nfunc read() result { return result{\n" + "    secret: res,\n}}\n";
        FindViolations("source.go", field).ShouldBeEmpty();
        FindViolations("source.go", field.Replace("res,", JsonSerializer.Serialize(RandomSecret()) + ",", StringComparison.Ordinal)).ShouldNotBeEmpty();
        string diagnostic = "expected: 0xabc &grpc.ClientConn{Transport" + "Credentials:insecure.insecureTC{}, PerRPC" + "Credentials:[]credentials.PerRPCCredentials(nil)}\n";
        FindViolations("evidence/runtime.log", diagnostic).ShouldBeEmpty();
        FindViolations("evidence/runtime.log", diagnostic + Assignment("pass" + "word", RandomSecret())).ShouldNotBeEmpty();
        FindViolations("evidence/runtime.log", diagnostic.Replace("insecure.insecureTC{}", JsonSerializer.Serialize(RandomSecret()), StringComparison.Ordinal)).ShouldNotBeEmpty();
    }

    /// <summary>The unchanged frozen wrong-secret branch is accepted only with its real denial proof.</summary>
    /// <param name="mutation">The negative-test proof mutation.</param>
    [Theory]
    [InlineData("none")]
    [InlineData("literal")]
    [InlineData("removed")]
    [InlineData("changed")]
    [InlineData("unrelated")]
    [InlineData("comment")]
    [InlineData("string")]
    [InlineData("adjacent")]
    public void SourceContexts_FrozenWrongSecretRequiresExecutableDenial(string mutation)
    {
        const string path = "_bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-20-network/snapshots/supporting-credential-conformance/tests/Hexalith.EventStore.Server.Tests/Authentication/WorkloadBindingAuthorityTests.QueryAuthority.cs";
        string source = File.ReadAllText(Path.Combine(RepoRoot, path));
        if (mutation == "literal") source = source.Replace("new string('x', 64)", JsonSerializer.Serialize(RandomSecret()), StringComparison.Ordinal);
        if (mutation == "removed") source = source.Replace(".ShouldBeNull();", ";", StringComparison.Ordinal);
        if (mutation == "changed") source = source.Replace(".ShouldBeNull();", ".ShouldNotBeNull();", StringComparison.Ordinal);
        if (mutation == "unrelated") source = source.Replace("QueryDigestCapabilityCannotWidenAnExactGrant", "AcceptsCredentials", StringComparison.Ordinal);
        if (mutation == "comment") source = "/*\n" + source + "\n*/";
        if (mutation == "string") source = "var document = \"\"\"\n" + source + "\n\"\"\";";
        if (mutation == "adjacent") source += "\nstring " + Assignment("pass" + "word", JsonSerializer.Serialize(RandomSecret())) + ";\n";
        if (mutation == "none") FindViolations("frozen/Test.cs", source).ShouldBeEmpty();
        else FindViolations("frozen/Test.cs", source).ShouldNotBeEmpty();
    }
}

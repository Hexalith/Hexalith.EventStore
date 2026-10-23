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

    /// <summary>Nested JSON is decoded; scalar credentials retain their owning field meaning.</summary>
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

    /// <summary>Go runtime field values cannot hide substituted credential bytes.</summary>
    [Fact]
    public void SourceContexts_GoValuesRequireRuntimeSyntax()
    {
        string field = "package control\nfunc read() result { return result{\n" + "    secret: res,\n}}\n";
        FindViolations("source.go", field).ShouldBeEmpty();
        FindViolations("source.go", field.Replace("res,", JsonSerializer.Serialize(RandomSecret()) + ",", StringComparison.Ordinal)).ShouldNotBeEmpty();
    }
}

using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies both operative Story 1.20 approval-role allowlist validators remain bound to
/// their executable proof-packet commands and reject adversarial input. Approved membership
/// stays single-sourced in the allowlist and packet rather than being restated by this test.
/// </summary>
public sealed class ProofPacketValidatorIntegrityTests
{
    private const string PacketRelativePath =
        "_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md";

    private const string AllowlistRelativePath =
        "_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json";

    private static readonly Regex BashBlockPattern = new(
        @"^```bash\r?\n(?<body>.*?)^```\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Singleline);

    private static readonly Regex ValidatorPattern = new(
        @"^[ \t]*jq[ \t]+-e[ \t]+-s[ \t]+'(?<program>.*?)'[ \t\r\n]+""(?<input>\$(?:APPROVAL_ROLE_ALLOWLIST|A_APPROVAL_ROLE_ALLOWLIST))""[ \t]+>/dev/null[ \t]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Singleline);

    /// <summary>
    /// Verifies both allowlist validators are executable, bound to their expected inputs, accept
    /// the approved allowlist, and reject malformed or over-authorized variants.
    /// </summary>
    [Fact]
    public void PacketAllowlistValidatorsFailClosedForAdversarialInputs()
    {
        string root = FindRepositoryRoot();
        string packet = File.ReadAllText(Path.Combine(root, PacketRelativePath));
        string executableBash = string.Join(
            Environment.NewLine,
            BashBlockPattern.Matches(packet).Select(match => match.Groups["body"].Value));
        Match[] validators = ValidatorPattern.Matches(executableBash).ToArray();
        string commentedValidators = Regex.Replace(
            executableBash,
            @"^[ \t]*(?=jq[ \t]+-e[ \t]+-s)",
            "# ",
            RegexOptions.CultureInvariant | RegexOptions.Multiline);

        validators.Length.ShouldBe(
            2,
            "The executable packet must contain exactly the candidate and evidence-commit-A allowlist validators.");
        ValidatorPattern.Matches(commentedValidators).ShouldBeEmpty(
            "Commented jq text is not an executable proof-packet validator.");
        validators.Select(match => match.Groups["input"].Value).ShouldBe(
            ["$APPROVAL_ROLE_ALLOWLIST", "$A_APPROVAL_ROLE_ALLOWLIST"],
            ignoreOrder: true,
            customMessage: "Each operative validator must remain bound to its intended allowlist input.");

        string validAllowlist = File.ReadAllText(Path.Combine(root, AllowlistRelativePath));
        string invalidSchema = MutateAllowlist(validAllowlist, rootObject =>
            rootObject["schema"] = "invalid-schema");
        string invalidRepository = MutateAllowlist(validAllowlist, rootObject =>
            rootObject["repository"] = "invalid/repository");
        string extraRole = MutateAllowlist(validAllowlist, rootObject =>
        {
            JsonObject roles = rootObject["roles"].ShouldBeOfType<JsonObject>();
            roles["unexpected_role"] = new JsonArray("unexpected-reviewer");
        });
        string extraMember = MutateAllowlist(validAllowlist, rootObject =>
        {
            JsonObject roles = rootObject["roles"].ShouldBeOfType<JsonObject>();
            JsonArray firstRole = roles.First().Value.ShouldBeOfType<JsonArray>();
            firstRole.Add("unexpected-reviewer");
        });
        string missingRole = MutateAllowlist(validAllowlist, rootObject =>
        {
            JsonObject roles = rootObject["roles"].ShouldBeOfType<JsonObject>();
            roles.Remove(roles.First().Key).ShouldBeTrue();
        });
        string emptyRole = MutateAllowlist(validAllowlist, rootObject =>
        {
            JsonObject roles = rootObject["roles"].ShouldBeOfType<JsonObject>();
            roles[roles.First().Key] = new JsonArray();
        });
        string replacedMember = MutateAllowlist(validAllowlist, rootObject =>
        {
            JsonObject roles = rootObject["roles"].ShouldBeOfType<JsonObject>();
            JsonArray firstRole = roles.First().Value.ShouldBeOfType<JsonArray>();
            firstRole[0] = "unexpected-reviewer";
        });
        string duplicateMember = MutateAllowlist(validAllowlist, rootObject =>
        {
            JsonObject roles = rootObject["roles"].ShouldBeOfType<JsonObject>();
            JsonArray firstRole = roles.First().Value.ShouldBeOfType<JsonArray>();
            firstRole.Add(firstRole[0].ShouldNotBeNull().DeepClone());
        });
        string multipleDocuments = validAllowlist + Environment.NewLine + validAllowlist;

        foreach (Match validator in validators)
        {
            string program = validator.Groups["program"].Value;
            RunJq(program, validAllowlist).ShouldBe(
                0,
                "Each packet validator must accept the current owner-approved allowlist.");
            RunJq(program, "{}").ShouldNotBe(0, "An empty object must fail closed.");
            RunJq(program, invalidSchema).ShouldNotBe(0, "An invalid schema must fail closed.");
            RunJq(program, invalidRepository).ShouldNotBe(0, "An invalid repository must fail closed.");
            RunJq(program, extraRole).ShouldNotBe(0, "An extra role must fail closed.");
            RunJq(program, extraMember).ShouldNotBe(0, "An extra approved-role member must fail closed.");
            RunJq(program, missingRole).ShouldNotBe(0, "A missing approved role must fail closed.");
            RunJq(program, emptyRole).ShouldNotBe(0, "An approved role without a member must fail closed.");
            RunJq(program, replacedMember).ShouldNotBe(0, "A substituted approved-role member must fail closed.");
            RunJq(program, duplicateMember).ShouldNotBe(0, "A duplicated approved-role member must fail closed.");
            RunJq(program, multipleDocuments).ShouldNotBe(0, "Multiple JSON documents must fail closed.");
        }
    }

    /// <summary>
    /// Verifies the packet consumes the counter placement emitted by xUnit v3 and fails closed
    /// for the obsolete root-only shape or any incomplete or contradictory summary.
    /// </summary>
    [Fact]
    public void PacketXunitValidatorAcceptsRealV3SummaryAndRejectsInvalidSummaries()
    {
        string root = FindRepositoryRoot();
        string packet = File.ReadAllText(Path.Combine(root, PacketRelativePath));
        const string startMarker = "# xunit-result-contract-start";
        const string endMarker = "# xunit-result-contract-end";
        int start = packet.IndexOf(startMarker, StringComparison.Ordinal);
        int bodyStart = start < 0 ? -1 : packet.IndexOf('\n', start) + 1;
        int end = bodyStart <= 0 ? -1 : packet.IndexOf(endMarker, bodyStart, StringComparison.Ordinal);

        start.ShouldBeGreaterThanOrEqualTo(0, "The executable packet must retain the xUnit validator start marker.");
        bodyStart.ShouldBeGreaterThan(0, "The executable packet must place the validator after its start marker.");
        end.ShouldBeGreaterThan(bodyStart, "The executable packet must retain the xUnit validator end marker.");
        packet.LastIndexOf(startMarker, StringComparison.Ordinal).ShouldBe(start, "The xUnit validator marker must be unique.");
        packet.LastIndexOf(endMarker, StringComparison.Ordinal).ShouldBe(end, "The xUnit validator marker must be unique.");

        string validator = packet[bodyStart..end];
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"hexalith-xunit-validator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            string scriptPath = Path.Combine(temporaryDirectory, "validate.sh");
            File.WriteAllText(scriptPath, validator + Environment.NewLine + "validate_xunit_result \"$1\"" + Environment.NewLine);
            string valid =
                "<assemblies><assembly name=\"Fixture.Tests.dll\" total=\"1\" passed=\"1\" failed=\"0\" errors=\"0\" skipped=\"0\" not-run=\"0\">" +
                "<collection><test type=\"Fixture.Tests.Case\" method=\"Passes\" result=\"Pass\" /></collection></assembly></assemblies>";
            RunBashValidator(scriptPath, temporaryDirectory, "valid.xml", valid).ShouldBe(
                0,
                "The packet must accept the single-assembly summary emitted by xUnit v3.");

            Dictionary<string, string> invalidFixtures = new(StringComparer.Ordinal)
            {
                ["root-only.xml"] =
                    "<assemblies total=\"1\" passed=\"1\" failed=\"0\" errors=\"0\" skipped=\"0\" not-run=\"0\">" +
                    "<assembly name=\"Fixture.Tests.dll\"><collection><test type=\"Fixture.Tests.Case\" method=\"Passes\" result=\"Pass\" /></collection></assembly></assemblies>",
                ["multiple-assemblies.xml"] = valid.Replace("</assemblies>", "<assembly name=\"Other.Tests.dll\" total=\"0\" passed=\"0\" failed=\"0\" errors=\"0\" skipped=\"0\" not-run=\"0\" /></assemblies>", StringComparison.Ordinal),
                ["skipped.xml"] = valid.Replace("skipped=\"0\"", "skipped=\"1\"", StringComparison.Ordinal),
                ["not-run.xml"] = valid.Replace("not-run=\"0\"", "not-run=\"1\"", StringComparison.Ordinal),
                ["counter-mismatch.xml"] = valid.Replace("total=\"1\"", "total=\"2\"", StringComparison.Ordinal),
                ["failed-result.xml"] = valid.Replace("result=\"Pass\"", "result=\"Fail\"", StringComparison.Ordinal),
                ["aggregate-mismatch.xml"] = valid.Replace("<assemblies>", "<assemblies total=\"2\" passed=\"2\" failed=\"0\" errors=\"0\" skipped=\"0\" not-run=\"0\">", StringComparison.Ordinal),
            };

            foreach ((string fileName, string xml) in invalidFixtures)
            {
                RunBashValidator(scriptPath, temporaryDirectory, fileName, xml).ShouldNotBe(
                    0,
                    $"The packet must fail closed for {fileName}.");
            }
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static string MutateAllowlist(string json, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(json).ShouldBeOfType<JsonObject>();
        mutate(root);
        return root.ToJsonString();
    }

    private static int RunJq(string program, string input)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "jq",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add("-e");
        process.StartInfo.ArgumentList.Add("-s");
        process.StartInfo.ArgumentList.Add(program);

        process.Start().ShouldBeTrue("jq must be available to execute the proof-packet validators.");
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        process.WaitForExit(5000).ShouldBeTrue("jq validator execution must finish within five seconds.");
        _ = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        return process.ExitCode;
    }

    private static int RunBashValidator(string scriptPath, string directory, string fileName, string xml)
    {
        string fixturePath = Path.Combine(directory, fileName);
        File.WriteAllText(fixturePath, xml);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "bash",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add(scriptPath);
        process.StartInfo.ArgumentList.Add(fixturePath);

        process.Start().ShouldBeTrue("bash must be available to execute the proof-packet validator.");
        process.WaitForExit(5000).ShouldBeTrue("The xUnit validator must finish within five seconds.");
        _ = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        return process.ExitCode;
    }

    private static string FindRepositoryRoot()
    {
        string[] startPaths = [Directory.GetCurrentDirectory(), AppContext.BaseDirectory];
        foreach (string startPath in startPaths.Distinct(StringComparer.Ordinal))
        {
            DirectoryInfo? directory = new(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, PacketRelativePath.Replace('/', Path.DirectorySeparatorChar)))
                    && File.Exists(Path.Combine(directory.FullName, AllowlistRelativePath.Replace('/', Path.DirectorySeparatorChar)))
                    && Directory.Exists(Path.Combine(directory.FullName, "src", "Hexalith.EventStore.Contracts")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing the Story 1.20 proof packet.");
    }
}

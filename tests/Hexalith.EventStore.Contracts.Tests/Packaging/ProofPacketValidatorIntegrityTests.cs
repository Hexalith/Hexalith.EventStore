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
        @"jq\s+-e\s+-s\s+'(?<program>.*?)'\s+""(?<input>\$(?:APPROVAL_ROLE_ALLOWLIST|A_APPROVAL_ROLE_ALLOWLIST))""\s+>/dev/null",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

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

        validators.Length.ShouldBe(
            2,
            "The executable packet must contain exactly the candidate and evidence-commit-A allowlist validators.");
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
            RunJq(program, multipleDocuments).ShouldNotBe(0, "Multiple JSON documents must fail closed.");
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

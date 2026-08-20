using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies the authorized Story 1.21 repair remains bound to the frozen Story 1.20 evidence.
/// </summary>
public sealed class FrozenStory120EvidenceIntegrityRepairTests
{
    private const string AuthorizationReceiptChecksumPath =
        "_bmad-output/implementation-artifacts/evidence/story-1-21/evidence-owner-authorization-sha256.txt";
    private const string AuthorizationReceiptPath =
        "_bmad-output/implementation-artifacts/evidence/story-1-21/evidence-owner-authorization.json";
    private const string AuthorizationScope =
        "Restore exactly three frozen Story 1.20 environment captures from the sole parent blobs of the introducing commit; add supporting Story 1.21 guardrail and verification artifacts without changing Story 1.20's decision or Epic 1 status.";
    private const string BaselineCommit = "1e5abd261339c831347b4717f5d311a214f97059";
    private const string EvidenceRoot =
        "_bmad-output/implementation-artifacts/evidence/story-1-20";
    private const string FrozenSpecBlockSha256 =
        "26c7a378bffb3a90eee0fe037aeeeec2e16a290ed91fadd5b4a4db6219db7e92";
    private const string IntroducingCommit = "089369bb8fa34117c1d5f912f5cbe80ab07fa9a3";
    private const string IntroducingParent = "f670892f0826de2097e9f47175f5caf5c5ad346a";
    private const string ProtectedPath =
        $"{EvidenceRoot}/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/environment.txt";
    private const string ProtectedBlob = "32f5728dbbd38bfa0a9ee846a57f7e3aec85abc4";
    private const string ProtectedSha256 =
        "fc1c214a90dd2d892e3cc10e5b487ca0b53354b2ad2027f137e5f53d66899c4d";
    private const string RepairResultPath =
        "_bmad-output/implementation-artifacts/evidence/story-1-21/repair-result.json";
    private const string RepairSubjectChecksumPath =
        "_bmad-output/implementation-artifacts/evidence/story-1-21/repair-subject-sha256.txt";
    private const string RepairSubjectPath =
        "_bmad-output/implementation-artifacts/evidence/story-1-21/repair-subject.json";
    private const string SpecPath =
        "_bmad-output/implementation-artifacts/spec-1-21-frozen-story-1-20-evidence-integrity-repair.md";
    private const string VerificationChecksumPath =
        "_bmad-output/implementation-artifacts/evidence/story-1-21/test-architect-verification-sha256.txt";
    private const string VerificationConclusion =
        "The authorized repair is content-bound to the canonical subject and result; all three frozen critical manifests pass 33/33, proof packages remain independently unavailable at 0/14 per tree, no broader Story 1.20 drift exists, and the protected tree remains unchanged.";
    private const string VerificationPath =
        "_bmad-output/implementation-artifacts/evidence/story-1-21/test-architect-verification.json";

    private static readonly (
        string Tree,
        string Path,
        string PreimageBlob,
        string PreimageSha256,
        string TargetBlob,
        string TargetSha256)[] _expectedRepairs =
    [
        (
            "38f85086fc2513e06fe85482dfade96578d649e5",
            $"{EvidenceRoot}/38f85086fc2513e06fe85482dfade96578d649e5/environment.txt",
            "e4bfbbf98ea8d3faa91ac8b1bcd0a4be13fa2b77",
            "79a4c6f3eb05a602c86cbff9d99bcff16fc2792fc6562254f921fc7e60afa2cc",
            "a9fccff513e0c86813ed1edd129df5fc31355ef2",
            "ae3a92f39a9149f5cc61ea3d7bdd0ec2d2d40458c3ad92cbb7f03332da985675"),
        (
            "4983299103bfa5bbbd40e695767eb5ddbc1369d5",
            $"{EvidenceRoot}/4983299103bfa5bbbd40e695767eb5ddbc1369d5/environment.txt",
            "b1f2274b6f84ce754ac53b62b0a0a94a6ee4c408",
            "4c54b30bf272c5f17e4ef5ff62fb4af268e43490d127618c65c5f7e36f00f5c2",
            "53122faef296332df79199d2886e45215f84a720",
            "a32d875cec09b4e27dc9959b7bc2907ab55558ff58d669056eec014524300faa"),
        (
            "ec0d35a082bcc70b090afa1c1544306008d767da",
            $"{EvidenceRoot}/ec0d35a082bcc70b090afa1c1544306008d767da/environment.txt",
            "878ac4b6bb0fa70b5a748666d84070515eae585f",
            "b1a0203e354a21d8ca119e423829db0fa08fdbd0228db52d141db50663cbf6db",
            "c48af936a2893f0305e9330ec1197c161e841c84",
            "9fef028eba774c54e9c9bf072d80c81f803ad4eb8101569cd6861239eaef1422"),
    ];

    /// <summary>
    /// Verifies the approved subject binds the exact introducing commit, parent, and three-file repair.
    /// </summary>
    [Fact]
    public void RepairSubjectBindsApprovedCommitParentAndExactThreeFileScope()
    {
        string root = FindRepositoryRoot();
        byte[] subjectBytes = File.ReadAllBytes(Path.Combine(root, RepairSubjectPath));
        JsonObject subject = JsonNode.Parse(subjectBytes)!.AsObject();
        string checksum = File.ReadAllText(Path.Combine(root, RepairSubjectChecksumPath));

        ValidateSubject(subject, root).ShouldBeTrue();
        ValidateChecksumSidecar(checksum, subjectBytes, "repair-subject.json").ShouldBeTrue();
        HasCanonicalLfBytes(subjectBytes).ShouldBeTrue();
        HasCanonicalLfBytes(File.ReadAllBytes(Path.Combine(root, RepairSubjectChecksumPath))).ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a mutation to an authorized blob identity invalidates the repair subject.
    /// </summary>
    [Fact]
    public void RepairSubjectRejectsBlobIdentityMutation()
    {
        string root = FindRepositoryRoot();
        JsonObject subject = LoadObject(root, RepairSubjectPath);
        JsonObject mutated = subject.DeepClone().AsObject();
        mutated["repair_scope"]!.AsArray()[0]!.AsObject()["preimage_git_blob"] = new string('0', 40);

        ValidateSubject(mutated, root).ShouldBeFalse();
        ValidateSubject(new JsonObject { ["schema"] = null }, root).ShouldBeFalse();

        JsonObject receipt = LoadObject(root, AuthorizationReceiptPath);
        JsonObject mutatedReceipt = receipt.DeepClone().AsObject();
        mutatedReceipt["decision"] = "Approve";
        ValidateAuthorizationReceipt(
            mutatedReceipt,
            root,
            ComputeSha256(File.ReadAllBytes(Path.Combine(root, RepairSubjectPath))))
            .ShouldBeFalse();
        ValidateAuthorizationReceipt(new JsonObject { ["actor"] = null }, root, new string('0', 64))
            .ShouldBeFalse();
        ValidateResult(new JsonObject { ["completed_at"] = null }, root, new string('0', 64))
            .ShouldBeFalse();
    }

    /// <summary>
    /// Verifies a fourth predecessor path cannot be added to the authorized repair scope.
    /// </summary>
    [Fact]
    public void RepairSubjectRejectsBroaderScopeMutation()
    {
        string root = FindRepositoryRoot();
        JsonObject subject = LoadObject(root, RepairSubjectPath);
        JsonObject mutated = subject.DeepClone().AsObject();
        mutated["repair_scope"]!.AsArray().Add(mutated["already_restored_exclusions"]![0]!.DeepClone());

        ValidateSubject(mutated, root).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies repaired bytes match the parent blobs without any broader Story 1.20 drift.
    /// </summary>
    [Fact]
    public void RepairedEvidenceMatchesParentWithoutBroaderStory120Drift()
    {
        string root = FindRepositoryRoot();
        foreach (var expected in _expectedRepairs)
        {
            string fullPath = Path.Combine(root, expected.Path);
            ComputeSha256(File.ReadAllBytes(fullPath)).ShouldBe(expected.TargetSha256);
            RunGit(root, "hash-object", expected.Path).ShouldBe(expected.TargetBlob);
        }

        RunGit(root, "hash-object", ProtectedPath).ShouldBe(ProtectedBlob);
        ComputeSha256(File.ReadAllBytes(Path.Combine(root, ProtectedPath))).ShouldBe(ProtectedSha256);
        RunGit(root, "diff", "--name-only", IntroducingParent, "--", EvidenceRoot).ShouldBeEmpty();
        RunGit(root, "ls-files", "--others", "--exclude-standard", "--", EvidenceRoot).ShouldBeEmpty();
        RunGit(root, "ls-files", "--others", "--ignored", "--exclude-standard", "--", EvidenceRoot)
            .ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies every repaired tree's frozen critical manifest passes all thirty-three entries.
    /// </summary>
    [Fact]
    public void RepairedCriticalManifestsPassAllThirtyThreeEntries()
    {
        string root = FindRepositoryRoot();
        foreach (var expected in _expectedRepairs)
        {
            string treeRoot = Path.Combine(root, EvidenceRoot, expected.Tree);
            (int count, int passed, string[] failures) =
                CheckManifest(treeRoot, "critical-evidence-sha256.txt");

            count.ShouldBe(33);
            passed.ShouldBe(33);
            failures.ShouldBeEmpty();
        }
    }

    /// <summary>
    /// Verifies missing proof archives are reported independently as zero of fourteen available.
    /// </summary>
    [Fact]
    public void MissingProofPackagesReportZeroOfFourteenWithoutManifestMutation()
    {
        string root = FindRepositoryRoot();
        foreach (var expected in _expectedRepairs)
        {
            string manifestPath = $"{EvidenceRoot}/{expected.Tree}/nuget-sha256.txt";
            string treeRoot = Path.Combine(root, EvidenceRoot, expected.Tree);
            (string Hash, string RelativePath)[] entries = ParseManifest(
                File.ReadAllText(Path.Combine(root, manifestPath)));

            entries.Length.ShouldBe(14);
            entries.ShouldAllBe(entry => entry.RelativePath.EndsWith(".nupkg", StringComparison.Ordinal));
            entries.Count(entry => File.Exists(Path.Combine(treeRoot, entry.RelativePath))).ShouldBe(0);
            RunGit(root, "hash-object", manifestPath)
                .ShouldBe(RunGit(root, "rev-parse", $"{IntroducingParent}:{manifestPath}"));
            RunGit(root, "diff", "--name-only", IntroducingParent, "--", manifestPath).ShouldBeEmpty();
        }
    }

    /// <summary>
    /// Verifies the Test Architect record binds both canonical subject and observed repair result.
    /// </summary>
    [Fact]
    public void TestArchitectVerificationBindsSubjectAndResultAndRejectsMismatch()
    {
        string root = FindRepositoryRoot();
        JsonObject verification = LoadObject(root, VerificationPath);

        CanCloseStory(root, verification).ShouldBeTrue();

        JsonObject mismatched = verification.DeepClone().AsObject();
        mismatched["result_sha256"] = new string('0', 64);
        CanCloseStory(root, mismatched).ShouldBeFalse(
            "a verification mismatch must keep Story 1.21 from closing");

        JsonObject badChronology = verification.DeepClone().AsObject();
        badChronology["verified_at"] = "not-a-timestamp";
        CanCloseStory(root, badChronology).ShouldBeFalse();

        JsonObject badConclusion = verification.DeepClone().AsObject();
        badConclusion["conclusion"] = "pass";
        CanCloseStory(root, badConclusion).ShouldBeFalse();
        CanCloseStory(root, new JsonObject { ["verified_at"] = null }).ShouldBeFalse();

        byte[] verificationBytes = File.ReadAllBytes(Path.Combine(root, VerificationPath));
        string sidecar = File.ReadAllText(Path.Combine(root, VerificationChecksumPath));
        ValidateChecksumSidecar(sidecar, verificationBytes, "test-architect-verification.json")
            .ShouldBeTrue();
        ValidateChecksumSidecar(
            sidecar.Replace(ComputeSha256(verificationBytes), new string('0', 64), StringComparison.Ordinal),
            verificationBytes,
            "test-architect-verification.json")
            .ShouldBeFalse();
    }

    private static bool CanCloseStory(string root, JsonObject verification)
    {
        try
        {
            byte[] subjectBytes = File.ReadAllBytes(Path.Combine(root, RepairSubjectPath));
            byte[] resultBytes = File.ReadAllBytes(Path.Combine(root, RepairResultPath));
            byte[] verificationBytes = File.ReadAllBytes(Path.Combine(root, VerificationPath));
            JsonObject subject = JsonNode.Parse(subjectBytes)!.AsObject();
            JsonObject result = JsonNode.Parse(resultBytes)!.AsObject();
            string verificationSidecar = File.ReadAllText(Path.Combine(root, VerificationChecksumPath));
            bool chronologyIsValid =
                DateTimeOffset.TryParse(
                    result["completed_at"]!.GetValue<string>(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset completedAt)
                && DateTimeOffset.TryParse(
                    verification["verified_at"]!.GetValue<string>(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset verifiedAt)
                && verifiedAt >= completedAt;
            if (!ValidateSubject(subject, root)
                || !ValidateResult(result, root, ComputeSha256(subjectBytes))
                || !ValidateChecksumSidecar(
                    verificationSidecar,
                    verificationBytes,
                    "test-architect-verification.json")
                || !HasCanonicalLfBytes(verificationBytes)
                || !chronologyIsValid
                || !HasExactProperties(
                    verification,
                    "schema",
                    "verifier",
                    "verified_at",
                    "subject_sha256",
                    "result_sha256",
                    "focused_test",
                    "manifest_checks",
                    "package_availability",
                    "outcome",
                    "conclusion")
                || verification["schema"]!.GetValue<string>() !=
                    "hexalith.eventstore.story-1-21-test-architect-verification/v1"
                || verification["verifier"]!.GetValue<string>() != "bmad:murat"
                || verification["subject_sha256"]!.GetValue<string>() != ComputeSha256(subjectBytes)
                || verification["result_sha256"]!.GetValue<string>() != ComputeSha256(resultBytes)
                || verification["outcome"]!.GetValue<string>() != "pass"
                || verification["conclusion"]!.GetValue<string>() != VerificationConclusion)
            {
                return false;
            }

            JsonObject focusedTest = verification["focused_test"]!.AsObject();
            return HasExactProperties(focusedTest, "class", "passed", "failed", "skipped")
                && focusedTest["class"]!.GetValue<string>() ==
                    "Hexalith.EventStore.Contracts.Tests.Packaging.FrozenStory120EvidenceIntegrityRepairTests"
                && focusedTest["passed"]!.GetValue<int>() == 7
                && focusedTest["failed"]!.GetValue<int>() == 0
                && focusedTest["skipped"]!.GetValue<int>() == 0
                && ValidateVerificationRows(verification["manifest_checks"]!.AsArray(), 33, 33, 0)
                && ValidateAvailabilityRows(verification["package_availability"]!.AsArray(), 14, 0, 14);
        }
        catch (Exception exception) when (IsValidationException(exception))
        {
            return false;
        }
    }

    private static (int Count, int Passed, string[] Failures) CheckManifest(
        string treeRoot,
        string manifestName)
    {
        (string Hash, string RelativePath)[] entries =
            ParseManifest(File.ReadAllText(Path.Combine(treeRoot, manifestName)));
        string[] failures = entries
            .Where(entry =>
            {
                string path = Path.Combine(treeRoot, entry.RelativePath);
                return !File.Exists(path)
                    || ComputeSha256(File.ReadAllBytes(path)) != entry.Hash;
            })
            .Select(entry => entry.RelativePath)
            .ToArray();
        return (entries.Length, entries.Length - failures.Length, failures);
    }

    private static string ComputeSha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] ExtractFrozenBlockCanonicalBytes(string root)
    {
        string spec = File.ReadAllText(Path.Combine(root, SpecPath))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        const string opening =
            "<frozen-after-approval reason=\"human-owned intent — do not modify unless human renegotiates\">";
        const string closing = "</frozen-after-approval>";
        int start = spec.IndexOf(opening, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidDataException("The approved frozen Story 1.21 block is missing.");
        }

        int end = spec.IndexOf(closing, start, StringComparison.Ordinal);
        if (end < start)
        {
            throw new InvalidDataException("The approved frozen Story 1.21 block is missing.");
        }

        return Encoding.UTF8.GetBytes(spec[start..(end + closing.Length)] + "\n");
    }

    private static string FindRepositoryRoot()
    {
        foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            .Distinct(StringComparer.Ordinal))
        {
            DirectoryInfo? directory = new(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                    && Directory.Exists(Path.Combine(directory.FullName, "src", "Hexalith.EventStore.Contracts")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the EventStore repository root.");
    }

    private static bool HasExactProperties(JsonObject value, params string[] names)
        => value.Select(property => property.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .SequenceEqual(names.OrderBy(name => name, StringComparer.Ordinal), StringComparer.Ordinal);

    private static bool HasCanonicalLfBytes(byte[] bytes)
        => bytes.Length > 0
            && bytes[^1] == (byte)'\n'
            && !bytes.Contains((byte)'\r')
            && !(bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf);

    private static bool IsValidationException(Exception exception)
        => exception is IOException
            or JsonException
            or InvalidOperationException
            or NullReferenceException
            or FormatException
            or ArgumentException
            or UnauthorizedAccessException
            or TimeoutException;

    private static JsonObject LoadObject(string root, string relativePath)
        => JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, relativePath)))!.AsObject();

    private static (string Hash, string RelativePath)[] ParseManifest(string content)
        => content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                int separator = line.IndexOf("  ", StringComparison.Ordinal);
                if (separator != 64 || line.Length <= separator + 2)
                {
                    throw new InvalidDataException($"Invalid checksum manifest row: {line}");
                }

                _ = Convert.FromHexString(line[..separator]);
                return (line[..separator], line[(separator + 2)..]);
            })
            .ToArray();

    private static byte[] ReadGitBlob(string root, string blob)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = root,
            },
        };
        process.StartInfo.ArgumentList.Add("cat-file");
        process.StartInfo.ArgumentList.Add("blob");
        process.StartInfo.ArgumentList.Add(blob);
        process.Start().ShouldBeTrue();
        WaitForExitOrKill(process, "git cat-file");
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        string error = process.StandardError.ReadToEnd();
        process.ExitCode.ShouldBe(0, error);
        return output.ToArray();
    }

    private static string RunGit(string root, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = root,
            },
        };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start().ShouldBeTrue();
        WaitForExitOrKill(process, "git command");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.ExitCode.ShouldBe(0, error);
        return output.TrimEnd('\r', '\n');
    }

    private static bool RunGitSucceeds(string root, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = root,
            },
        };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start().ShouldBeTrue();
        WaitForExitOrKill(process, "git command");
        _ = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        return process.ExitCode == 0;
    }

    private static bool ValidateChecksumSidecar(
        string sidecar,
        byte[] content,
        string expectedFileName)
        => sidecar == $"{ComputeSha256(content)}  {expectedFileName}\n";

    private static void WaitForExitOrKill(Process process, string description)
    {
        if (process.WaitForExit(30_000))
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited between the timeout and kill request.
        }

        process.WaitForExit();
        throw new TimeoutException($"{description} timed out and was terminated.");
    }

    private static bool ValidateAuthorizationReceipt(
        JsonObject receipt,
        string root,
        string subjectSha256)
    {
        try
        {
            byte[] receiptBytes = File.ReadAllBytes(Path.Combine(root, AuthorizationReceiptPath));
            string sidecar = File.ReadAllText(Path.Combine(root, AuthorizationReceiptChecksumPath));
            JsonObject source = receipt["source"]!.AsObject();
            JsonObject subject = receipt["subject"]!.AsObject();
            JsonObject frozenBlock = receipt["frozen_spec_block"]!.AsObject();
            return HasExactProperties(
                    receipt,
                    "schema",
                    "actor",
                    "role",
                    "decision",
                    "source",
                    "subject",
                    "frozen_spec_block",
                    "scope")
                && receipt["schema"]!.GetValue<string>() ==
                    "hexalith.eventstore.story-1-21-evidence-owner-authorization/v1"
                && receipt["actor"]!.GetValue<string>() == "Administrator"
                && receipt["role"]!.GetValue<string>() == "Hexalith.EventStore evidence owner"
                && receipt["decision"]!.GetValue<string>() == "[A] Approve"
                && receipt["scope"]!.GetValue<string>() == AuthorizationScope
                && HasExactProperties(source, "kind", "record", "timestamp_status")
                && source["kind"]!.GetValue<string>() == "interactive-workflow-decision"
                && source["record"]!.GetValue<string>() ==
                    "Administrator's [A] Approve observed before the implementation handoff for the frozen Story 1.21 spec"
                && source["timestamp_status"]!.GetValue<string>() == "not recorded"
                && HasExactProperties(subject, "path", "sha256")
                && subject["path"]!.GetValue<string>() == RepairSubjectPath
                && subject["sha256"]!.GetValue<string>() == subjectSha256
                && HasExactProperties(frozenBlock, "path", "canonicalization", "sha256")
                && frozenBlock["path"]!.GetValue<string>() == SpecPath
                && frozenBlock["canonicalization"]!.GetValue<string>() ==
                    "UTF-8 without BOM; CRLF normalized to LF; opening frozen-after-approval tag through closing tag inclusive; one trailing LF"
                && frozenBlock["sha256"]!.GetValue<string>() == FrozenSpecBlockSha256
                && ComputeSha256(ExtractFrozenBlockCanonicalBytes(root)) == FrozenSpecBlockSha256
                && ValidateChecksumSidecar(sidecar, receiptBytes, "evidence-owner-authorization.json")
                && HasCanonicalLfBytes(receiptBytes)
                && HasCanonicalLfBytes(File.ReadAllBytes(Path.Combine(root, AuthorizationReceiptChecksumPath)));
        }
        catch (Exception exception) when (IsValidationException(exception))
        {
            return false;
        }
    }

    private static bool ValidateResult(JsonObject result, string root, string subjectSha256)
    {
        try
        {
            byte[] resultBytes = File.ReadAllBytes(Path.Combine(root, RepairResultPath));
            if (!HasExactProperties(
                    result,
                    "schema",
                    "subject",
                    "completed_at",
                    "outcome",
                    "repaired_paths",
                    "critical_manifests",
                    "package_availability",
                    "broader_drift",
                    "protected_tree")
                || result["schema"]!.GetValue<string>() !=
                    "hexalith.eventstore.story-1-21-repair-result/v1"
                || result["outcome"]!.GetValue<string>() != "pass"
                || !DateTimeOffset.TryParse(
                    result["completed_at"]!.GetValue<string>(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _)
                || !HasCanonicalLfBytes(resultBytes))
            {
                return false;
            }

            JsonObject subject = result["subject"]!.AsObject();
            JsonArray repairedPaths = result["repaired_paths"]!.AsArray();
            JsonObject broaderDrift = result["broader_drift"]!.AsObject();
            JsonObject protectedTree = result["protected_tree"]!.AsObject();
            if (!HasExactProperties(subject, "path", "sha256")
                || subject["path"]!.GetValue<string>() != RepairSubjectPath
                || subject["sha256"]!.GetValue<string>() != subjectSha256
                || repairedPaths.Count != _expectedRepairs.Length
                || !HasExactProperties(broaderDrift, "reference_commit", "unexpected_paths")
                || broaderDrift["reference_commit"]!.GetValue<string>() != IntroducingParent
                || broaderDrift["unexpected_paths"]!.AsArray().Count != 0
                || !HasExactProperties(protectedTree, "path", "git_blob", "sha256", "outcome")
                || protectedTree["path"]!.GetValue<string>() != ProtectedPath
                || protectedTree["git_blob"]!.GetValue<string>() != ProtectedBlob
                || protectedTree["sha256"]!.GetValue<string>() != ProtectedSha256
                || protectedTree["outcome"]!.GetValue<string>() != "unchanged"
                || !ValidateVerificationRows(result["critical_manifests"]!.AsArray(), 33, 33, 0)
                || !ValidateAvailabilityRows(result["package_availability"]!.AsArray(), 14, 0, 14)
                || !ValidateLiveObservations(
                    root,
                    result["critical_manifests"]!.AsArray(),
                    result["package_availability"]!.AsArray())
                || !NoBroaderStory120Drift(root)
                || RunGit(root, "hash-object", ProtectedPath) != ProtectedBlob
                || ComputeSha256(File.ReadAllBytes(Path.Combine(root, ProtectedPath))) != ProtectedSha256)
            {
                return false;
            }

            for (int index = 0; index < _expectedRepairs.Length; index++)
            {
                var expected = _expectedRepairs[index];
                JsonObject repaired = repairedPaths[index]!.AsObject();
                if (!HasExactProperties(
                        repaired,
                        "path",
                        "mode",
                        "preimage_git_blob",
                        "restored_git_blob",
                        "restored_sha256")
                    || repaired["path"]!.GetValue<string>() != expected.Path
                    || repaired["mode"]!.GetValue<string>() != "100644"
                    || repaired["preimage_git_blob"]!.GetValue<string>() != expected.PreimageBlob
                    || repaired["restored_git_blob"]!.GetValue<string>() != expected.TargetBlob
                    || repaired["restored_sha256"]!.GetValue<string>() != expected.TargetSha256
                    || RunGit(root, "hash-object", expected.Path) != expected.TargetBlob)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception) when (IsValidationException(exception))
        {
            return false;
        }
    }

    private static bool ValidateSubject(JsonObject subject, string root)
    {
        try
        {
            byte[] subjectBytes = File.ReadAllBytes(Path.Combine(root, RepairSubjectPath));
            string subjectSidecar = File.ReadAllText(Path.Combine(root, RepairSubjectChecksumPath));
            string subjectSha256 = ComputeSha256(subjectBytes);
            if (!HasExactProperties(
                    subject,
                    "schema",
                    "story_id",
                    "baseline_commit",
                    "authorization",
                    "introducing_commit",
                    "introducing_parent",
                    "repair_scope",
                    "already_restored_exclusions",
                    "completion_contract")
                || subject["schema"]!.GetValue<string>() !=
                    "hexalith.eventstore.story-1-21-repair-subject/v1"
                || subject["story_id"]!.GetValue<string>() != "1.21"
                || subject["baseline_commit"]!.GetValue<string>() != BaselineCommit
                || subject["introducing_commit"]!.GetValue<string>() != IntroducingCommit
                || subject["introducing_parent"]!.GetValue<string>() != IntroducingParent
                || !ValidateChecksumSidecar(subjectSidecar, subjectBytes, "repair-subject.json")
                || !HasCanonicalLfBytes(subjectBytes)
                || !HasCanonicalLfBytes(File.ReadAllBytes(Path.Combine(root, RepairSubjectChecksumPath)))
                || RunGit(root, "cat-file", "-t", BaselineCommit) != "commit"
                || !RunGitSucceeds(root, "merge-base", "--is-ancestor", BaselineCommit, "HEAD")
                || RunGit(root, "rev-list", "--parents", "-n", "1", IntroducingCommit) !=
                    $"{IntroducingCommit} {IntroducingParent}")
            {
                return false;
            }

            JsonObject authorization = subject["authorization"]!.AsObject();
            JsonArray scope = subject["repair_scope"]!.AsArray();
            JsonArray exclusions = subject["already_restored_exclusions"]!.AsArray();
            JsonObject completion = subject["completion_contract"]!.AsObject();
            if (!HasExactProperties(
                    authorization,
                    "receipt_path",
                    "receipt_sha256_path",
                    "scope")
                || authorization["receipt_path"]!.GetValue<string>() != AuthorizationReceiptPath
                || authorization["receipt_sha256_path"]!.GetValue<string>() !=
                    AuthorizationReceiptChecksumPath
                || authorization["scope"]!.GetValue<string>() != AuthorizationScope
                || !ValidateAuthorizationReceipt(
                    LoadObject(root, AuthorizationReceiptPath),
                    root,
                    subjectSha256)
                || scope.Count != _expectedRepairs.Length
                || exclusions.Count != 1
                || !HasExactProperties(
                    completion,
                    "post_repair_parent_drift_paths",
                    "critical_manifest_entry_count_per_tree",
                    "proof_package_manifest_entry_count_per_tree",
                    "expected_available_proof_packages_per_tree",
                    "test_architect")
                || completion["post_repair_parent_drift_paths"]!.AsArray().Count != 0
                || completion["critical_manifest_entry_count_per_tree"]!.GetValue<int>() != 33
                || completion["proof_package_manifest_entry_count_per_tree"]!.GetValue<int>() != 14
                || completion["expected_available_proof_packages_per_tree"]!.GetValue<int>() != 0
                || completion["test_architect"]!.GetValue<string>() != "bmad:murat")
            {
                return false;
            }

            for (int index = 0; index < _expectedRepairs.Length; index++)
            {
                var expected = _expectedRepairs[index];
                JsonObject item = scope[index]!.AsObject();
                if (!HasExactProperties(
                        item,
                        "path",
                        "mode",
                        "preimage_git_blob",
                        "preimage_sha256",
                        "target_git_blob",
                        "target_sha256")
                    || item["path"]!.GetValue<string>() != expected.Path
                    || item["mode"]!.GetValue<string>() != "100644"
                    || item["preimage_git_blob"]!.GetValue<string>() != expected.PreimageBlob
                    || item["preimage_sha256"]!.GetValue<string>() != expected.PreimageSha256
                    || item["target_git_blob"]!.GetValue<string>() != expected.TargetBlob
                    || item["target_sha256"]!.GetValue<string>() != expected.TargetSha256
                    || RunGit(root, "ls-tree", IntroducingCommit, "--", expected.Path) !=
                        $"100644 blob {expected.PreimageBlob}\t{expected.Path}"
                    || RunGit(root, "ls-tree", IntroducingParent, "--", expected.Path) !=
                        $"100644 blob {expected.TargetBlob}\t{expected.Path}"
                    || ComputeSha256(ReadGitBlob(root, expected.PreimageBlob)) != expected.PreimageSha256
                    || ComputeSha256(ReadGitBlob(root, expected.TargetBlob)) != expected.TargetSha256)
                {
                    return false;
                }
            }

            JsonObject exclusion = exclusions[0]!.AsObject();
            string excludedPath = ProtectedPath;
            if (!HasExactProperties(
                    exclusion,
                    "path",
                    "mode",
                    "introducing_preimage_git_blob",
                    "protected_git_blob",
                    "protected_sha256",
                    "reason")
                || exclusion["path"]!.GetValue<string>() != excludedPath
                || exclusion["mode"]!.GetValue<string>() != "100644"
                || exclusion["introducing_preimage_git_blob"]!.GetValue<string>() !=
                    "5fa3fd3bb8970832db4e30d2db1109b862e17289"
                || exclusion["protected_git_blob"]!.GetValue<string>() !=
                    "32f5728dbbd38bfa0a9ee846a57f7e3aec85abc4"
                || exclusion["protected_sha256"]!.GetValue<string>() !=
                    "fc1c214a90dd2d892e3cc10e5b487ca0b53354b2ad2027f137e5f53d66899c4d"
                || RunGit(root, "ls-tree", IntroducingCommit, "--", excludedPath) !=
                    $"100644 blob 5fa3fd3bb8970832db4e30d2db1109b862e17289\t{excludedPath}"
                || RunGit(root, "ls-tree", IntroducingParent, "--", excludedPath) !=
                    $"100644 blob 32f5728dbbd38bfa0a9ee846a57f7e3aec85abc4\t{excludedPath}")
            {
                return false;
            }

            string[] introducingPaths = RunGit(
                    root,
                    "diff-tree",
                    "--no-commit-id",
                    "--name-only",
                    "-r",
                    IntroducingCommit,
                    "--",
                    EvidenceRoot)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string[] expectedIntroducingPaths = _expectedRepairs.Select(item => item.Path)
                .Append(excludedPath)
                .ToArray();
            return introducingPaths.SequenceEqual(expectedIntroducingPaths, StringComparer.Ordinal);
        }
        catch (Exception exception) when (IsValidationException(exception))
        {
            return false;
        }
    }

    private static bool NoBroaderStory120Drift(string root)
        => string.IsNullOrEmpty(RunGit(root, "diff", "--name-only", IntroducingParent, "--", EvidenceRoot))
            && string.IsNullOrEmpty(
                RunGit(root, "ls-files", "--others", "--exclude-standard", "--", EvidenceRoot))
            && string.IsNullOrEmpty(
                RunGit(
                    root,
                    "ls-files",
                    "--others",
                    "--ignored",
                    "--exclude-standard",
                    "--",
                    EvidenceRoot));

    private static bool ValidateAvailabilityRows(
        JsonArray rows,
        int expectedEntries,
        int expectedAvailable,
        int expectedMissing)
    {
        if (rows.Count != _expectedRepairs.Length)
        {
            return false;
        }

        for (int index = 0; index < rows.Count; index++)
        {
            JsonObject row = rows[index]!.AsObject();
            if (!HasExactProperties(row, "tree", "entries", "available", "missing")
                || row["tree"]!.GetValue<string>() != _expectedRepairs[index].Tree
                || row["entries"]!.GetValue<int>() != expectedEntries
                || row["available"]!.GetValue<int>() != expectedAvailable
                || row["missing"]!.GetValue<int>() != expectedMissing)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateLiveObservations(
        string root,
        JsonArray criticalRows,
        JsonArray availabilityRows)
    {
        if (criticalRows.Count != _expectedRepairs.Length
            || availabilityRows.Count != _expectedRepairs.Length)
        {
            return false;
        }

        foreach (var expected in _expectedRepairs)
        {
            string treeRoot = Path.Combine(root, EvidenceRoot, expected.Tree);
            (int count, int passed, string[] failures) =
                CheckManifest(treeRoot, "critical-evidence-sha256.txt");
            (string Hash, string RelativePath)[] packages =
                ParseManifest(File.ReadAllText(Path.Combine(treeRoot, "nuget-sha256.txt")));
            int available = packages.Count(package =>
                File.Exists(Path.Combine(treeRoot, package.RelativePath)));
            if (count != 33
                || passed != 33
                || failures.Length != 0
                || packages.Length != 14
                || packages.Any(package =>
                    !package.RelativePath.EndsWith(".nupkg", StringComparison.Ordinal))
                || available != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateVerificationRows(
        JsonArray rows,
        int expectedEntries,
        int expectedSuccesses,
        int expectedFailures)
    {
        if (rows.Count != _expectedRepairs.Length)
        {
            return false;
        }

        for (int index = 0; index < rows.Count; index++)
        {
            JsonObject row = rows[index]!.AsObject();
            if (!HasExactProperties(row, "tree", "entries", "passed", "failed")
                || row["tree"]!.GetValue<string>() != _expectedRepairs[index].Tree
                || row["entries"]!.GetValue<int>() != expectedEntries
                || row["passed"]!.GetValue<int>() != expectedSuccesses
                || row["failed"]!.GetValue<int>() != expectedFailures)
            {
                return false;
            }
        }

        return true;
    }
}

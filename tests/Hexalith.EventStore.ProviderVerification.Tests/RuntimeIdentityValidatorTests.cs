using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.ProviderVerification.Tests;

public sealed class RuntimeIdentityValidatorTests
{
    private const string ExpectedHashDomain =
        "Exact NuGet.org v3-flatcontainer response body after redirects, including the NuGet.org repository signature";
    private const string ExpectedInventoryHash = "6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae";
    private const string ExpectedSource = "bb94d93e9b84132cff83a38fba84f25455820d31";

    [Fact]
    public void Validate_AuthorizingSuccessor_RecordsApprovalsAndRuntimeDrift()
    {
        string root = FindRepositoryRoot();
        string identity = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "frontcomposer-11-24-runtime-identity-successor.md");
        string evidence = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "frontcomposer-story-11-24",
            "bb94d93e9b84132cff83a38fba84f25455820d31");
        var hashes = new List<InputHash>();

        IdentityEvidence result = RuntimeIdentityValidator.Validate(identity, evidence, root, hashes);

        result.ApprovalAuthorized.ShouldBeTrue();
        result.ApprovalCount.ShouldBe(2);
        result.ReasonCodes.ShouldNotContain("identity.approval.unavailable");
        result.ExpectedSourceSha.ShouldBe("bb94d93e9b84132cff83a38fba84f25455820d31");
        result.ReasonCodes.ShouldContain("identity.source.mismatch");
        result.ObservedBuildsSha.ShouldNotBe(
            result.ExpectedBuildsSha,
            "The frozen FrontComposer successor remains bound to its historical Builds identity after the Dapr bootstrap pointer advances.");
        result.ReasonCodes.ShouldContain("identity.builds.mismatch");
        result.RuntimeMatches.ShouldBeFalse();
        hashes.Count.ShouldBe(7);
    }

    [Fact]
    public void Validate_ExactPostFreezeReceipts_AuthorizeOnlyTheBoundSubject()
    {
        string root = FindRepositoryRoot();
        string sourceEvidence = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "frontcomposer-story-11-24",
            "bb94d93e9b84132cff83a38fba84f25455820d31");
        string directory = Path.Combine(Path.GetTempPath(), $"eventstore-identity-tests-{Guid.NewGuid():N}");
        string evidence = Path.Combine(directory, "evidence");
        Directory.CreateDirectory(evidence);
        try
        {
            foreach (string source in Directory.GetFiles(sourceEvidence, "*", SearchOption.TopDirectoryOnly))
            {
                File.Copy(source, Path.Combine(evidence, Path.GetFileName(source)));
            }

            string identity = Path.Combine(directory, "decision.md");
            string decision = File.ReadAllText(Path.Combine(
                root,
                "_bmad-output",
                "implementation-artifacts",
                "frontcomposer-11-24-runtime-identity-successor.md"));
            File.WriteAllText(
                identity,
                decision.Replace("final_decision: unavailable", "final_decision: available", StringComparison.Ordinal)
                    .Replace("authorize_consumer_migration: false", "authorize_consumer_migration: true", StringComparison.Ordinal));
            string receiptDirectory = Path.Combine(
                evidence,
                "acceptances",
                "9d074dfd0758a8934f122aab18659627dff1cf5d4c3e548b222cc0d79a881065");
            Directory.CreateDirectory(receiptDirectory);
            WriteReceipt(receiptDirectory, "eventstore-owner", 1);
            WriteReceipt(receiptDirectory, "release-owner", 2);

            IdentityEvidence valid = RuntimeIdentityValidator.Validate(identity, evidence, root, []);

            valid.ApprovalAuthorized.ShouldBeTrue();
            valid.ApprovalCount.ShouldBe(2);

            string driftedPath = Path.Combine(receiptDirectory, "release-owner.json");
            string drifted = File.ReadAllText(driftedPath)
                .Replace("Hexalith.FrontComposer Story 11.24", "Hexalith.Tenants Story 2.12", StringComparison.Ordinal);
            File.WriteAllText(driftedPath, drifted);

            RuntimeIdentityValidator.Validate(identity, evidence, root, []).ApprovalAuthorized.ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Validate_UppercaseDecisionHash_IsRejected()
    {
        string root = FindRepositoryRoot();
        string sourceIdentity = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "frontcomposer-11-24-runtime-identity-successor.md");
        string evidence = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "frontcomposer-story-11-24",
            "bb94d93e9b84132cff83a38fba84f25455820d31");
        string directory = Path.Combine(Path.GetTempPath(), $"eventstore-identity-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string identity = Path.Combine(directory, "decision.md");
        try
        {
            File.WriteAllText(
                identity,
                File.ReadAllText(sourceIdentity).Replace(
                    "bb94d93e9b84132cff83a38fba84f25455820d31",
                    "BB94D93E9B84132CFF83A38FBA84F25455820D31",
                    StringComparison.Ordinal));

            Should.Throw<ProviderVerificationInputException>(
                () => RuntimeIdentityValidator.Validate(identity, evidence, root, []))
                .Code.ShouldBe("identity.decision.hash-invalid");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("https://github.com/Hexalith/Hexalith.EventStore/issues/not-a-number#issuecomment-also-not-a-number")]
    [InlineData("https://github.com/Hexalith/Hexalith.EventStore/issues/1/#issuecomment-2")]
    [InlineData("https://github.com:443/Hexalith/Hexalith.EventStore/issues/1#issuecomment-2")]
    [InlineData("https://github.com/Hexalith/Hexalith.EventStore/issues/1?view=all#issuecomment-2")]
    [InlineData("https://github.com/Hexalith/Hexalith.EventStore/issues/0#issuecomment-2")]
    [InlineData("https://github.com/Hexalith/Hexalith.EventStore/issues/1#issuecomment-02")]
    public void Validate_NonCanonicalIssueCommentReceipt_DoesNotAuthorize(string durableSource)
    {
        (string root, string directory, string identity, string evidence, string receiptDirectory) =
            CreateAuthorizingFixture();
        try
        {
            string receiptPath = Path.Combine(receiptDirectory, "release-owner.json");
            File.WriteAllText(
                receiptPath,
                File.ReadAllText(receiptPath).Replace(
                    "https://github.com/Hexalith/Hexalith.EventStore/issues/1#issuecomment-2",
                    durableSource,
                    StringComparison.Ordinal));

            RuntimeIdentityValidator.Validate(identity, evidence, root, []).ApprovalAuthorized.ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("2026-08-10T08:00:00")]
    [InlineData("2026-08-10T07:06:11Z")]
    [InlineData("2026-08-10T07:06:10Z")]
    [InlineData("2999-08-10T08:00:00Z")]
    public void Validate_AcceptanceTimestampWithoutOffsetOrPostFreezeOrdering_DoesNotAuthorize(string acceptedAt)
    {
        (string root, string directory, string identity, string evidence, string receiptDirectory) =
            CreateAuthorizingFixture();
        try
        {
            string receiptPath = Path.Combine(receiptDirectory, "release-owner.json");
            File.WriteAllText(
                receiptPath,
                File.ReadAllText(receiptPath).Replace(
                    "2026-08-10T08:00:00Z",
                    acceptedAt,
                    StringComparison.Ordinal));

            RuntimeIdentityValidator.Validate(identity, evidence, root, []).ApprovalAuthorized.ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("3.91.1", "bb94d93e9b84132cff83a38fba84f25455820d31", true)]
    [InlineData("3.91.1+bb94d93e9b84132cff83a38fba84f25455820d31", "bb94d93e9b84132cff83a38fba84f25455820d31", true)]
    [InlineData("3.91.1+8358ffc399bdb1f1574bd049f17b3b6ebf907619", "bb94d93e9b84132cff83a38fba84f25455820d31", false)]
    [InlineData("3.91.1+bb94d93e9b84132cff83a38fba84f25455820d31.extra", "bb94d93e9b84132cff83a38fba84f25455820d31", false)]
    public void VersionMatches_SourceSuffix_IsExactWhenPresent(string observed, string source, bool expected)
    {
        RuntimeIdentityValidator.VersionMatches("3.91.1", observed, source).ShouldBe(expected);
    }

    [Theory]
    [InlineData("src/provider.cs")]
    [InlineData("Directory.Build.props")]
    [InlineData("Directory.Build.targets")]
    [InlineData("Directory.Packages.props")]
    [InlineData("global.json")]
    public void IsProviderWorktreeClean_ProviderAffectingChange_IsDirty(string relativePath)
    {
        string repository = CreateCleanGitRepository();
        try
        {
            RuntimeIdentityValidator.IsProviderWorktreeClean(repository).ShouldBeTrue();

            File.AppendAllText(Path.Combine(repository, relativePath), "dirty");

            RuntimeIdentityValidator.IsProviderWorktreeClean(repository).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void IsGitWorktreeClean_BuildsCheckoutChange_IsDirty()
    {
        string repository = CreateCleanGitRepository();
        try
        {
            RuntimeIdentityValidator.IsGitWorktreeClean(repository).ShouldBeTrue();

            File.AppendAllText(Path.Combine(repository, "Directory.Build.props"), "dirty");

            RuntimeIdentityValidator.IsGitWorktreeClean(repository).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void IsRuntimeMatch_DirtyProviderOrBuildsCheckout_FailsClosed(
        bool providerWorktreeClean,
        bool buildsWorktreeClean)
    {
        RuntimeIdentityValidator.IsRuntimeMatch(
            ExpectedSource,
            ExpectedSource,
            "3.91.1",
            $"3.91.1+{ExpectedSource}",
            "a8a50859fa2f27f511a9470dfe1e3ae54d0ebc1a",
            "a8a50859fa2f27f511a9470dfe1e3ae54d0ebc1a",
            ExpectedInventoryHash,
            ExpectedInventoryHash,
            providerWorktreeClean,
            buildsWorktreeClean).ShouldBeFalse();
    }

    [Fact]
    public void ValidatePackageManifest_ApprovedTupleAndEmbeddedCommits_AreRequired()
    {
        string path = EvidencePath("package-manifest.json");
        using JsonDocument validDocument = JsonDocument.Parse(File.ReadAllBytes(path));

        RuntimeIdentityValidator.ValidatePackageManifest(
            validDocument.RootElement,
            ExpectedSource,
            "v3.91.1",
            "3.91.1",
            14,
            ExpectedHashDomain).ShouldBe(ExpectedInventoryHash);

        JsonNode drifted = JsonNode.Parse(File.ReadAllText(path))!;
        drifted["packages"]![0]!["embedded_repository_commit"] = "8358ffc399bdb1f1574bd049f17b3b6ebf907619";
        using JsonDocument driftedDocument = JsonDocument.Parse(drifted.ToJsonString());

        Should.Throw<ProviderVerificationInputException>(() => RuntimeIdentityValidator.ValidatePackageManifest(
            driftedDocument.RootElement,
            ExpectedSource,
            "v3.91.1",
            "3.91.1",
            14,
            ExpectedHashDomain)).Code.ShouldBe("identity.package.entry-invalid");
    }

    [Fact]
    public void ValidateProvenance_ApprovedReleaseAndBuildsTuple_IsRequired()
    {
        string path = EvidencePath("release-catalog-provenance.json");
        using JsonDocument validDocument = JsonDocument.Parse(File.ReadAllBytes(path));
        RuntimeIdentityValidator.ValidateProvenance(
            validDocument.RootElement,
            ExpectedSource,
            "v3.91.1",
            "3.91.1",
            ExpectedInventoryHash,
            "a8a50859fa2f27f511a9470dfe1e3ae54d0ebc1a",
            "f75daebd4c522c081a6f62e274cf25e07971de69",
            "824d7ef100455423aabbcd399c8364074000b2e0",
            14);

        JsonNode drifted = JsonNode.Parse(File.ReadAllText(path))!;
        drifted["exact_source_release"]!["builds_execution_sha"] = "8358ffc399bdb1f1574bd049f17b3b6ebf907619";
        using JsonDocument driftedDocument = JsonDocument.Parse(drifted.ToJsonString());

        Should.Throw<ProviderVerificationInputException>(() => RuntimeIdentityValidator.ValidateProvenance(
            driftedDocument.RootElement,
            ExpectedSource,
            "v3.91.1",
            "3.91.1",
            ExpectedInventoryHash,
            "a8a50859fa2f27f511a9470dfe1e3ae54d0ebc1a",
            "f75daebd4c522c081a6f62e274cf25e07971de69",
            "824d7ef100455423aabbcd399c8364074000b2e0",
            14)).Code.ShouldBe("identity.provenance.tuple-mismatch");
    }

    private static (string Root, string Directory, string Identity, string Evidence, string ReceiptDirectory)
        CreateAuthorizingFixture()
    {
        string root = FindRepositoryRoot();
        string sourceEvidence = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "frontcomposer-story-11-24",
            "bb94d93e9b84132cff83a38fba84f25455820d31");
        string directory = Path.Combine(Path.GetTempPath(), $"eventstore-identity-tests-{Guid.NewGuid():N}");
        string evidence = Path.Combine(directory, "evidence");
        Directory.CreateDirectory(evidence);
        foreach (string source in Directory.GetFiles(sourceEvidence, "*", SearchOption.TopDirectoryOnly))
        {
            File.Copy(source, Path.Combine(evidence, Path.GetFileName(source)));
        }

        string identity = Path.Combine(directory, "decision.md");
        string decision = File.ReadAllText(Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "frontcomposer-11-24-runtime-identity-successor.md"));
        File.WriteAllText(
            identity,
            decision.Replace("final_decision: unavailable", "final_decision: available", StringComparison.Ordinal)
                .Replace("authorize_consumer_migration: false", "authorize_consumer_migration: true", StringComparison.Ordinal));
        string receiptDirectory = Path.Combine(
            evidence,
            "acceptances",
            "9d074dfd0758a8934f122aab18659627dff1cf5d4c3e548b222cc0d79a881065");
        Directory.CreateDirectory(receiptDirectory);
        WriteReceipt(receiptDirectory, "eventstore-owner", 1);
        WriteReceipt(receiptDirectory, "release-owner", 2);
        return (root, directory, identity, evidence, receiptDirectory);
    }

    private static void WriteReceipt(string directory, string role, int commentId)
    {
        string statement = role == "eventstore-owner"
            ? "I accept this exact EventStore source and signed NuGet.org package identity for Hexalith.FrontComposer Story 11.24 only."
            : "I authorize this exact EventStore source and signed NuGet.org package identity for migration by Hexalith.FrontComposer Story 11.24 only.";
        var receipt = new
        {
            schema = "hexalith.eventstore.frontcomposer-runtime-acceptance.v1",
            subject_sha256 = "9d074dfd0758a8934f122aab18659627dff1cf5d4c3e548b222cc0d79a881065",
            subject_frozen_at = "2026-08-10T07:06:11Z",
            actor = "github:jpiquot",
            role,
            decision = "accepted",
            source_sha = "bb94d93e9b84132cff83a38fba84f25455820d31",
            version = "3.91.1",
            consumer_scope = "Hexalith.FrontComposer Story 11.24",
            accepted_at = "2026-08-10T08:00:00Z",
            durable_source = $"https://github.com/Hexalith/Hexalith.EventStore/issues/1#issuecomment-{commentId}",
            statement,
        };
        File.WriteAllText(
            Path.Combine(directory, $"{role}.json"),
            JsonSerializer.Serialize(receipt));
    }

    private static string CreateCleanGitRepository()
    {
        string repository = Path.Combine(Path.GetTempPath(), $"eventstore-identity-git-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repository, "src"));
        File.WriteAllText(Path.Combine(repository, "src", "provider.cs"), "clean");
        foreach (string name in new[]
        {
            "Directory.Build.props",
            "Directory.Build.targets",
            "Directory.Packages.props",
            "global.json",
        })
        {
            File.WriteAllText(Path.Combine(repository, name), "clean");
        }

        RunGit(repository, "init", "--quiet");
        RunGit(repository, "config", "user.email", "provider-verification@example.invalid");
        RunGit(repository, "config", "user.name", "Provider Verification Tests");
        RunGit(repository, "add", ".");
        RunGit(repository, "commit", "--quiet", "-m", "test: seed identity fixture");
        return repository;
    }

    private static string EvidencePath(string name)
        => Path.Combine(
            FindRepositoryRoot(),
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "frontcomposer-story-11-24",
            ExpectedSource,
            name);

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo)!;
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, process.StandardError.ReadToEnd());
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("EventStore repository root was not found.");
    }
}

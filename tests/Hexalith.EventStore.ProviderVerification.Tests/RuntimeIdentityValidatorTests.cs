using Shouldly;

namespace Hexalith.EventStore.ProviderVerification.Tests;

public sealed class RuntimeIdentityValidatorTests
{
    private const string ExpectedInventoryHash = "6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae";
    private const string ExpectedSource = "bb94d93e9b84132cff83a38fba84f25455820d31";

    [Fact]
    public void ObserveLive_RecordsCurrentProvenanceWithoutInventingApproval()
    {
        IdentityEvidence result = RuntimeIdentityValidator.ObserveLive(FindRepositoryRoot());

        result.VerificationMode.ShouldBe("live-compatibility");
        result.ObservedSourceSha.ShouldBe(result.ExpectedSourceSha);
        result.ObservedBuildsSha.ShouldBe(result.ExpectedBuildsSha);
        result.ObservedReleaseInventorySha256.ShouldBe(result.ReleaseInventorySha256);
        result.ApprovalAuthorized.ShouldBeFalse();
        result.ApprovalCount.ShouldBe(0);
        result.RuntimeMatches.ShouldBeTrue();
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

            // The decision record is rejected before any evidence directory is read.
            Should.Throw<ProviderVerificationInputException>(
                () => RuntimeIdentityValidator.Validate(identity, directory, root, []))
                .Code.ShouldBe("identity.decision.hash-invalid");
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

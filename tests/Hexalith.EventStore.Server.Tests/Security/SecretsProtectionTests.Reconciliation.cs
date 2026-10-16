using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

public sealed partial class SecretsProtectionTests
{
    /// <summary>
    /// Scans the complete source-only reconciliation, including delivery files not staged in Git.
    /// Uses the canonical decoder and secret rules without changing tracked-file ingestion.
    /// </summary>
    [Fact]
    public void CanonicalReconciliationDelivery_DoesNotContainUsableSecrets()
    {
        string[] deliveredPaths =
        [
            ".github/workflows/p1r-qualification.yml",
            "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-blocker-repair-20261014/independent-repair-audit.py",
            "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-blocker-repair-20261014/verify-and-audit.py",
            "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-blocker-repair-20261014/verify-repair.py",
            "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-published-run/review-round-1/independent-audit-next.py",
            "tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs",
            "tools/p1r-isolated-runner.py",
            "tools/p1r-published-consumers/Directory.Build.props",
            "tools/p1r-published-consumers/FixtureSnapshotQualification.cs",
            "tools/p1r-published-consumers/README.md",
            "tools/p1r-published-consumers/domain/CounterSnapshotParity.cs",
            "tools/p1r-published-consumers/domain/Domain.csproj",
            "tools/p1r-published-consumers/domain/Program.cs",
            "tools/p1r-published-consumers/host/Host.csproj",
            "tools/p1r-published-consumers/host/Program.cs",
            "tools/p1r-published-consumers/probe/Program.cs",
            "tools/p1r-published-executor.py",
            "tools/p1r_check_witnesses.py",
            "tools/p1r_isolation.py",
            "tools/p1r_isolation_evidence.py",
            "tools/p1r_published_executor.py",
            "tools/p1r_published_qualification.py",
            "tools/tests/p1r_isolation_fixture.py",
            "tools/tests/test_p1r_isolation.py",
            "tools/tests/test_p1r_isolation_hardening.py",
            "tools/tests/test_p1r_published_executor.py",
            ".github/workflows/release.yml",
            "scripts/validate-publication-preflight.sh",
            "tools/release-publication-identity.py",
            "tools/tests/release_publication_fixture.py",
            "tools/tests/test_release_publication_authority.py",
            "tests/Hexalith.EventStore.Contracts.Tests/Packaging/TrustedPublishingReleaseTests.cs",
            "tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs",
            "tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.Reconciliation.cs",
            "tools/p1r-published-consumers/registration-probe/RegistrationProbe.csproj",
            "tools/p1r-published-consumers/registration-probe/StartupHook.cs",
            "tools/tests/test_p1r_snapshot_registration.py",
        ];
        foreach (string path in deliveredPaths)
        {
            ReadTrackedText(path).ShouldNotBeNull($"Delivered text is unavailable: {path}");
            ScanTrackedFile(path).ShouldBeEmpty($"Delivered source contains secret material: {path}");
        }
    }
}

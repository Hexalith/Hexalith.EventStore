using Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for AC #9 / AC #10 — toolchain and CI fit.
/// Either the validator is wired into <c>scripts/validate-docs.{ps1,sh}</c>
/// and <c>.github/workflows/docs-validation.yml</c>, OR a documented
/// CI-deferred reason marker exists in the corresponding entrypoint.
/// </summary>
public class Dw4DocsValidationWiringAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";
    private const string _ciDeferredMarker = "DW4-CI-DEFERRED:";

    [Fact(Skip = _baseSkip + "AC#9 — validator entrypoint is recorded in entrypoint.txt. Remove Skip when wiring.")]
    public void Toolchain_EntrypointDeclarationFile_IsCommitted() {
        string repoRoot = LocateRepoRoot();
        string fullPath = Path.Combine(repoRoot, Dw4ValidatorInvokerFactory.EntrypointFilePath);

        File.Exists(fullPath).ShouldBeTrue(
            $"Dev must commit '{Dw4ValidatorInvokerFactory.EntrypointFilePath}' declaring the chosen " +
            "validator shape (pwsh:, sh:, or dotnet:). AC #9 / story Task 0.3.");

        string content = File.ReadAllText(fullPath).Trim();
        content.ShouldNotBeNullOrWhiteSpace();
        (content.StartsWith("pwsh:", StringComparison.Ordinal)
            || content.StartsWith("sh:", StringComparison.Ordinal)
            || content.StartsWith("dotnet:", StringComparison.Ordinal))
            .ShouldBeTrue($"Entrypoint '{content}' must use a known scheme.");
    }

    [Fact(Skip = _baseSkip + "AC#10 — scripts/validate-docs.ps1 must invoke the validator OR carry a CI-deferred reason. Remove Skip when wiring.")]
    public void CiIntegration_PowerShellScript_InvokesValidatorOrDeclaresDeferral() => AssertScriptIntegration("scripts/validate-docs.ps1");

    [Fact(Skip = _baseSkip + "AC#10 — scripts/validate-docs.sh must invoke the validator OR carry a CI-deferred reason. Remove Skip when wiring.")]
    public void CiIntegration_ShellScript_InvokesValidatorOrDeclaresDeferral() => AssertScriptIntegration("scripts/validate-docs.sh");

    [Fact(Skip = _baseSkip + "AC#10 — docs-validation.yml must invoke the validator OR carry a CI-deferred reason. Remove Skip when wiring.")]
    public void CiIntegration_GitHubWorkflow_InvokesValidatorOrDeclaresDeferral() => AssertScriptIntegration(".github/workflows/docs-validation.yml");

    [Fact(Skip = _baseSkip + "AC#10 — smoke: shelling out to the validator with a known-bad fixture must exit non-zero. Remove Skip when wiring.")]
    public void CiIntegration_SmokeRun_KnownBadFixtureExitsNonZero() {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();
        string knownBadFixture = Path.Combine(
            Dw4FixtureCatalog.FixtureRoot,
            "query-invalid-missing-metadata.md");

        Dw4ValidationOutcome outcome = invoker.Validate([knownBadFixture]);

        outcome.ExitCode.ShouldNotBe(0,
            "Smoke: invoking the chosen validator entrypoint with a known-bad fixture " +
            "must exit non-zero. This proves end-to-end wiring (script → validator → diagnostic).");
    }

    private static void AssertScriptIntegration(string repoRelativePath) {
        string repoRoot = LocateRepoRoot();
        string fullPath = Path.Combine(repoRoot, repoRelativePath);

        File.Exists(fullPath).ShouldBeTrue($"'{repoRelativePath}' must exist.");
        string content = File.ReadAllText(fullPath);

        bool hasValidatorReference =
            content.Contains("validate-evidence", StringComparison.OrdinalIgnoreCase)
            || content.Contains("OperationalEvidence", StringComparison.Ordinal)
            || content.Contains("operational-evidence-validator", StringComparison.Ordinal);

        bool hasCiDeferredMarker = content.Contains(_ciDeferredMarker, StringComparison.Ordinal);

        (hasValidatorReference || hasCiDeferredMarker).ShouldBeTrue(
            $"'{repoRelativePath}' must either invoke the new evidence-schema validator " +
            $"or carry a documented '{_ciDeferredMarker} <reason>' marker. AC #10 forbids " +
            "silent CI deferral.");
    }

    private static string LocateRepoRoot() {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null) {
            if (File.Exists(Path.Combine(dir, "Hexalith.EventStore.slnx"))) {
                return dir;
            }

            DirectoryInfo? parent = Directory.GetParent(dir);
            if (parent is null) {
                break;
            }

            dir = parent.FullName;
        }

        throw new InvalidOperationException("Repo root (Hexalith.EventStore.slnx) not found.");
    }
}

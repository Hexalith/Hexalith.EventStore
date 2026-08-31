using Shouldly;

namespace Hexalith.EventStore.ProviderVerification.Tests;

public sealed class ProviderVerificationOptionsTests
{
    private static readonly string[] _validArguments =
    [
        "--pact-directory", "/tmp/pacts",
        "--manifest", "/tmp/manifest.json",
        "--provider-state-catalog", "/tmp/catalog.json",
        "--identity-record", "/tmp/identity.md",
        "--identity-evidence-directory", "/tmp/evidence",
        "--report-output", "/tmp/report.json",
    ];

    [Fact]
    public void TryParse_CompleteArguments_ReturnsBoundedDefaults()
    {
        bool result = ProviderVerificationOptions.TryParse(_validArguments, out ProviderVerificationOptions? options, out string code);

        result.ShouldBeTrue();
        code.ShouldBeEmpty();
        options.ShouldNotBeNull();
        options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void TryParse_DuplicateOption_FailsClosed()
    {
        string[] arguments = [.. _validArguments, "--manifest", "/tmp/other.json"];

        ProviderVerificationOptions.TryParse(arguments, out _, out string code).ShouldBeFalse();

        code.ShouldBe("input.cli.invalid");
    }

    [Fact]
    public void TryParse_TimeoutOutsideBound_FailsClosed()
    {
        string[] arguments = [.. _validArguments, "--request-timeout-seconds", "121"];

        ProviderVerificationOptions.TryParse(arguments, out _, out string code).ShouldBeFalse();

        code.ShouldBe("input.cli.timeout-invalid");
    }

    [Fact]
    public void TryParse_LiveCompatibility_DoesNotRequireHistoricalApprovalInputs()
    {
        string[] arguments =
        [
            "--verification-mode", "live-compatibility",
            "--pact-directory", "/tmp/pacts",
            "--manifest", "/tmp/manifest.json",
            "--provider-state-catalog", "/tmp/catalog.json",
            "--report-output", "/tmp/report.json",
        ];

        ProviderVerificationOptions.TryParse(arguments, out ProviderVerificationOptions? options, out string code)
            .ShouldBeTrue();

        code.ShouldBeEmpty();
        options.ShouldNotBeNull();
        options.Mode.ShouldBe(ProviderVerificationMode.LiveCompatibility);
        options.IdentityRecordPath.ShouldBeEmpty();
        options.IdentityEvidenceDirectory.ShouldBeEmpty();
    }

    [Fact]
    public void TryParse_UnknownMode_FailsClosed()
    {
        string[] arguments = [.. _validArguments, "--verification-mode", "approval-bypass"];

        ProviderVerificationOptions.TryParse(arguments, out _, out string code).ShouldBeFalse();

        code.ShouldBe("input.cli.mode-invalid");
    }
}

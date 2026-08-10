using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.ProviderVerification.Tests;

public sealed class ReportSafetyTests
{
    [Theory]
    [InlineData("Bearer secret-token")]
    [InlineData("https://internal.example.test/resource")]
    [InlineData("System.Exception: leaked")]
    [InlineData("StackTrace")]
    [InlineData("/home/operator/private")]
    [InlineData("/opt/operator/private")]
    [InlineData("D:\\operator\\private")]
    [InlineData("\\\\server\\share\\private")]
    [InlineData("127.0.0.1:49152")]
    [InlineData("10.42.1.8:8080")]
    [InlineData("172.31.4.2:443")]
    [InlineData("192.168.0.4:5000")]
    [InlineData("localhost:1234")]
    public void IsRedactionClean_AdversarialLeak_IsRejected(string leak)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { value = leak }));

        SafeReportWriter.IsRedactionClean(bytes).ShouldBeFalse();
    }

    [Fact]
    public void IsRedactionClean_SafeBearerRequirementPhrase_IsAllowed()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { value = "bearer requirement" }));

        SafeReportWriter.IsRedactionClean(bytes).ShouldBeTrue();
    }

    [Fact]
    public void TryDeleteTemporaryFile_UndeletableTarget_ReturnsStableCode()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"eventstore-report-cleanup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            SafeReportWriter.TryDeleteTemporaryFile(directory, out string code).ShouldBeFalse();

            code.ShouldBe("report.temporary-cleanup.failed");
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void TryWrite_CompleteSafeReport_WritesOneBoundedDocument()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"eventstore-report-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "report.json");
        try
        {
            ProviderVerificationReport report = CreateReport();

            SafeReportWriter.TryWrite(path, report, out string code).ShouldBeTrue();

            code.ShouldBeEmpty();
            new FileInfo(path).Length.ShouldBeLessThanOrEqualTo(SafeReportWriter.MaximumReportBytes);
            Directory.GetFiles(directory, "*.tmp").ShouldBeEmpty();
            JsonElement root = JsonDocument.Parse(File.ReadAllBytes(path)).RootElement;
            root.GetProperty("complete").GetBoolean().ShouldBeTrue();
            root.GetProperty("host").GetProperty("server").GetString().ShouldBe("Kestrel");
            root.GetProperty("timing").GetProperty("run").GetProperty("startedAt").GetString().ShouldNotBeNullOrWhiteSpace();
            root.GetProperty("interactions")[0].GetProperty("description").GetString().ShouldBe("known interaction");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void VerificationCompleteness_MissingResultOrTeardown_IsRejected()
    {
        ProviderStateEvent setup = new("known", "setup", "state.setup.succeeded", 1);
        InteractionVerificationResult incomplete = new(
            1,
            "known interaction",
            "pact.json",
            "known",
            "interaction.passed",
            1,
            [setup]);

        VerificationCompleteness.IsComplete(1, [incomplete]).ShouldBeFalse();
        VerificationCompleteness.IsComplete(2, [incomplete]).ShouldBeFalse();
    }

    [Fact]
    public void VerificationCompleteness_DuplicateMisorderedOrWrongStateCallbacks_AreRejected()
    {
        ProviderStateEvent setup = new("known", "setup", "state.setup.succeeded", 1);
        ProviderStateEvent teardown = new("known", "teardown", "state.teardown.succeeded", 1);
        InteractionVerificationResult valid = Result(1, [setup, teardown]);
        InteractionVerificationResult duplicateSetup = Result(1, [setup, setup]);
        InteractionVerificationResult reversed = Result(1, [teardown, setup]);
        InteractionVerificationResult wrongState = Result(
            1,
            [new("other", "setup", "state.setup.succeeded", 1), teardown]);

        VerificationCompleteness.IsComplete(1, [duplicateSetup]).ShouldBeFalse();
        VerificationCompleteness.IsComplete(1, [reversed]).ShouldBeFalse();
        VerificationCompleteness.IsComplete(1, [wrongState]).ShouldBeFalse();
        VerificationCompleteness.IsComplete(2, [valid, valid]).ShouldBeFalse();
    }

    [Fact]
    public void AppendNotRunResults_FatalPostInputFailure_ReconcilesEveryRemainingInteraction()
    {
        InteractionDefinition[] requested =
        [
            new("first interaction", "first-state", "GET", "/first", "pact.json", new string('a', 64)),
            new("second interaction", "second-state", "GET", "/second", "pact.json", new string('a', 64)),
        ];
        var reported = new List<InteractionVerificationResult>();

        ProviderVerificationApplication.AppendNotRunResults(requested, reported);

        reported.Count.ShouldBe(2);
        reported.ShouldAllBe(result => result.ResultCode == "interaction.not-run");
        reported.ShouldAllBe(result => result.StateEvents.Count == 2);
        reported.SelectMany(result => result.StateEvents).Select(item => item.ResultCode).ShouldBe(
        [
            "state.setup.not-run",
            "state.teardown.not-run",
            "state.setup.not-run",
            "state.teardown.not-run",
        ]);
        VerificationCompleteness.IsComplete(2, reported).ShouldBeFalse();
    }

    private static ProviderVerificationReport CreateReport()
    {
        ProviderStateEvent setup = new("known", "setup", "state.setup.succeeded", 1);
        ProviderStateEvent teardown = new("known", "teardown", "state.teardown.succeeded", 1);
        InteractionVerificationResult interaction = new(
            1,
            "known interaction",
            "pact.json",
            "known",
            "interaction.passed",
            2,
            [setup, teardown]);
        var timeline = new ProviderVerificationTimeline();
        timeline.BeginStartup();
        timeline.CompleteStartup("startup.succeeded");
        timeline.BeginReadiness();
        timeline.CompleteReadiness("readiness.succeeded");
        timeline.BeginCleanup();
        timeline.CompleteCleanup("cleanup.succeeded");
        return new ProviderVerificationReport(
            "hexalith.eventstore.provider-verification.v1",
            "passed",
            [],
            1,
            1,
            1,
            1,
            1,
            true,
            true,
            true,
            true,
            true,
            new ProviderHostMetadata(
                "Kestrel",
                "production-gateway",
                "http",
                "IPv4",
                "loopback",
                "os-assigned-ephemeral"),
            timeline.CompleteRun("run.succeeded"),
            null,
            [],
            [interaction]);
    }

    private static InteractionVerificationResult Result(int index, IReadOnlyList<ProviderStateEvent> events)
        => new(index, "known interaction", "pact.json", "known", "interaction.passed", 2, events);
}

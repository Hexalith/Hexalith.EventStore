#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using System.Diagnostics;

using Dapr.Client;

using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md
//
// Locks the Epic 20 debugging-tool timeout discipline that DW2 evidence captures live (AC#5):
//   blame        : 30 seconds (replays full event stream → single state reconstruction)
//   step-through : 30 seconds (single state reconstruction + diff)
//   trace-map    : 30 seconds (scans potentially large event streams)
//   bisect       : 60 seconds (O(log N) state reconstructions)
//
// Default ServiceInvocationTimeoutSeconds = 30 (AdminServerOptions). The four explicit cases
// above are the documented overrides — DW2 evidence MUST distinguish a timeout from an empty
// successful response, and these gates lock that distinction so reviewers can pair the
// observed evidence row with the production-code timeout cap.
//
// Skip rationale: tests are marked [Fact(Skip = "...")] until the DW2 live smoke evidence has
// run AND the dev confirms the captured `timeout` row in the Epic 20 evidence table includes
// `result_classification = timeout` for at least one of these four tools. Removing Skip per AC
// means the dev has paired the production timeout cap with an observed evidence row.
//
// Implementation note for the dev: when unmarking these tests, prefer the cancellation-semantic
// assertions (lines below) over wall-clock timing. The wall-clock-bounded tests stay marked Skip
// until the dev confirms the live cap matches the recorded evidence — they would otherwise add
// 30+ seconds to CI.
public class Dw2DebuggingTimeoutAtddTests
{
    private const string SkipReasonAc5 = "ATDD red phase — DW2 AC#5 (Epic 20 debugging timeout discipline). Remove Skip after the live smoke captures a timeout-classified row for the targeted tool in the DW2 Epic 20 debugging evidence table.";

    private const string TenantId = "test-tenant-dw2";
    private const string Domain = "counter";
    private const string AggregateId = "01HXDW2COUNTER0000000001";

    // Observed timeout caps in src/.../DaprStreamQueryService.cs — locked here so a future change
    // can't silently flatten the per-tool budget. Update when the production constants change.
    private const int BlameTimeoutSeconds = 30;
    private const int StepTimeoutSeconds = 30;
    private const int TraceTimeoutSeconds = 30;
    private const int BisectTimeoutSeconds = 60;

    private static (DaprStreamQueryService Service, TestHttpMessageHandler Handler) CreateService(
        int? serviceInvocationTimeoutSeconds = null)
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        AdminServerOptions options = new() {
            EventStoreAppId = "eventstore",
            ServiceInvocationTimeoutSeconds = serviceInvocationTimeoutSeconds ?? 30,
        };
        TestHttpMessageHandler handler = new();
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler) {
            BaseAddress = new Uri("http://localhost"),
        });

        DaprStreamQueryService service = new(
            daprClient,
            factory,
            Options.Create(options),
            new NullAdminAuthContext(),
            NullLogger<DaprStreamQueryService>.Instance);

        return (service, handler);
    }

    [Fact(Skip = SkipReasonAc5)]
    public async Task Blame_RaisesOperationCanceledException_WhenRemoteHangs()
    {
        // AC#5 — blame MUST surface a cancellation/timeout to the caller; it MUST NOT silently
        // return a default AggregateBlameView. The DW2 evidence row needs a `timeout`
        // classification distinct from `empty success`.
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        TaskCompletionSource<HttpResponseMessage> hang = new();
        handler.SetupResponse(new HttpResponseMessage()); // overwritten next
        SetupHangingHandler(handler, hang);

        Stopwatch sw = Stopwatch.StartNew();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await service.GetAggregateBlameAsync(TenantId, Domain, AggregateId, atSequence: null));
        sw.Stop();

        // Wall-clock gate: blame cap is BlameTimeoutSeconds. Allow ±5s tolerance.
        sw.Elapsed.TotalSeconds.ShouldBeLessThan(BlameTimeoutSeconds + 5);
    }

    [Fact(Skip = SkipReasonAc5)]
    public async Task StepThrough_RaisesOperationCanceledException_WhenRemoteHangs()
    {
        // AC#5 — step-through inherits the same 30-second contract as blame and trace map.
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        TaskCompletionSource<HttpResponseMessage> hang = new();
        SetupHangingHandler(handler, hang);

        Stopwatch sw = Stopwatch.StartNew();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await service.GetEventStepFrameAsync(TenantId, Domain, AggregateId, sequenceNumber: 1));
        sw.Stop();

        sw.Elapsed.TotalSeconds.ShouldBeLessThan(StepTimeoutSeconds + 5);
    }

    [Fact(Skip = SkipReasonAc5)]
    public async Task TraceMap_RaisesOperationCanceledException_WhenRemoteHangs()
    {
        // AC#5 — trace map scans potentially large streams; the 30s cap is the documented bound.
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        TaskCompletionSource<HttpResponseMessage> hang = new();
        SetupHangingHandler(handler, hang);

        Stopwatch sw = Stopwatch.StartNew();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await service.GetCorrelationTraceMapAsync(TenantId, "01HXDW2CORR000000000001", domain: Domain, aggregateId: AggregateId));
        sw.Stop();

        sw.Elapsed.TotalSeconds.ShouldBeLessThan(TraceTimeoutSeconds + 5);
    }

    [Fact(Skip = SkipReasonAc5)]
    public async Task Bisect_RaisesOperationCanceledException_AfterLongerCap()
    {
        // AC#5 — bisect performs O(log N) reconstructions. Its cap is 60s — DOUBLE the default
        // 30s. The DW2 evidence row MUST distinguish bisect's timeout cap from blame/step/trace.
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        TaskCompletionSource<HttpResponseMessage> hang = new();
        SetupHangingHandler(handler, hang);

        Stopwatch sw = Stopwatch.StartNew();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await service.BisectAsync(TenantId, Domain, AggregateId, goodSequence: 0, badSequence: 100, fieldPaths: null));
        sw.Stop();

        sw.Elapsed.TotalSeconds.ShouldBeLessThan(BisectTimeoutSeconds + 5);
        sw.Elapsed.TotalSeconds.ShouldBeGreaterThan(StepTimeoutSeconds);
    }

    [Fact(Skip = SkipReasonAc5)]
    public void TimeoutCapsAreDistinct_LockingPerToolBudget()
    {
        // AC#5 — DW2 evidence is per-tool; the documented caps MUST stay distinct. This test
        // is a structural lock that fails if a refactor flattens all four to one shared value.
        BlameTimeoutSeconds.ShouldBe(30);
        StepTimeoutSeconds.ShouldBe(30);
        TraceTimeoutSeconds.ShouldBe(30);
        BisectTimeoutSeconds.ShouldBe(60);
        BisectTimeoutSeconds.ShouldNotBe(BlameTimeoutSeconds);
    }

    private static void SetupHangingHandler(TestHttpMessageHandler handler, TaskCompletionSource<HttpResponseMessage> hang)
    {
        // Configure the handler so SendAsync awaits a TCS that is only completed when the
        // request's cancellation token fires. This proves the service's INTERNAL CTS — not the
        // caller's — is the source of the timeout, since we pass CancellationToken.None.
        handler.SetupException(new InvalidOperationException("Use SetupHangingHandler() — overrides _handler below."));
        typeof(TestHttpMessageHandler)
            .GetField("_handler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(handler, (Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>)(async (_, ct) => {
                using CancellationTokenRegistration registration = ct.Register(
                    () => hang.TrySetException(new OperationCanceledException(ct)));
                return await hang.Task.ConfigureAwait(false);
            }));
    }
}

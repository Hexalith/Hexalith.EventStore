using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Client.Tests.Indexes;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

public sealed class GatewayNativeSourceMetadataReaderTests
{
    private static readonly AggregateIdentity Identity = new("tenant-a", "conversations", "conversation-a");

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1)]
    [InlineData(5, 3)]
    [InlineData(5, 6)]
    public async Task ExistingGatewayWorkloadHttpTransportCarriesExactHeadRequestAndNativeResponse(int head, int retainedFloor)
    {
        StreamReadRequest? submitted = null;
        string? path = null;
        string? assertion = null;
        using var handler = new AdminOperationalIndexHttpMessageHandler(request =>
        {
            path = request.RequestUri!.AbsolutePath;
            assertion = request.Headers.GetValues("X-Hexalith-Workload-Assertion").Single();
            submitted = JsonSerializer.Deserialize<StreamReadRequest>(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult(),
                EventStoreGatewayClient.JsonOptions);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Page(head, retainedFloor)) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.test/") };
        var assertions = Substitute.For<IEventStoreGatewayWorkloadAssertionSource>();
        assertions.IssueAsync(EventStoreGatewayWorkloadOperations.StreamRead, Identity.TenantId, Identity.Domain, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("test-workload-assertion"));
        var gateway = new EventStoreGatewayClient(http,
            Options.Create(new EventStoreGatewayClientOptions { UseWorkloadTransport = true }), assertions);

        NativeSourceObservation? observation = await new GatewayNativeSourceMetadataReader(gateway, TimeProvider.System)
            .ReadAsync(Identity, TestContext.Current.CancellationToken);

        observation.ShouldNotBeNull().Proof.ShouldBe(Page(head, retainedFloor).Metadata.NativeProof);
        path.ShouldBe("/api/v1/streams/read/workload");
        assertion.ShouldBe("test-workload-assertion");
        submitted.ShouldBe(new StreamReadRequest(Identity.TenantId, Identity.Domain, Identity.AggregateId,
            FromSequence: 0, ToSequence: 0, PageSize: 1) { RequireNativeProof = true });
    }

    [Fact]
    public async Task ExistingThreeArgumentAssertionImplementerRetainsDomainAwareClientCompatibility()
    {
        using var handler = new AdminOperationalIndexHttpMessageHandler(request =>
        {
            request.Headers.GetValues("X-Hexalith-Workload-Assertion").Single().ShouldBe("legacy-fixed-domain-assertion");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Page(0)) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.test/") };
        var gateway = new EventStoreGatewayClient(http, Options.Create(new EventStoreGatewayClientOptions { UseWorkloadTransport = true }), new LegacyAssertionSource());
        (await new GatewayNativeSourceMetadataReader(gateway, TimeProvider.System).ReadAsync(Identity, TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
    }

    private sealed class LegacyAssertionSource : IEventStoreGatewayWorkloadAssertionSource
    {
        public ValueTask<string?> IssueAsync(string operation, string tenant, CancellationToken cancellationToken)
            => ValueTask.FromResult<string?>("legacy-fixed-domain-assertion");
    }

    [Theory]
    [InlineData("malformed-json")]
    [InlineData("forbidden")]
    [InlineData("erased")]
    [InlineData("omitted-erased")]
    public async Task ExistingGatewayHttpFailuresCannotReleaseNativeEvidence(string failure)
    {
        using var handler = new AdminOperationalIndexHttpMessageHandler(() => failure switch
        {
            "malformed-json" => new(HttpStatusCode.OK) { Content = new StringContent("{invalid", Encoding.UTF8, "application/json") },
            "forbidden" => new(HttpStatusCode.Forbidden),
            "omitted-erased" => new(HttpStatusCode.OK) { Content = new StringContent(
                JsonSerializer.Serialize(Page(5), JsonSerializerOptions.Web).Replace(
                    ",\"erased\":false", string.Empty, StringComparison.Ordinal), Encoding.UTF8, "application/json") },
            _ => new(HttpStatusCode.OK) { Content = JsonContent.Create(Page(5) with
                { Metadata = Page(5).Metadata with { NativeProof = Page(5).Metadata.NativeProof! with { Erased = true } } }) },
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.test/") };
        var assertions = Substitute.For<IEventStoreGatewayWorkloadAssertionSource>();
        assertions.IssueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("test-workload-assertion"));
        var gateway = new EventStoreGatewayClient(http,
            Options.Create(new EventStoreGatewayClientOptions { UseWorkloadTransport = true }), assertions);

        (await new GatewayNativeSourceMetadataReader(gateway, TimeProvider.System)
            .ReadAsync(Identity, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1)]
    [InlineData(5, 3)]
    [InlineData(5, 6)]
    public async Task ExactHeadOnlyRequestReturnsNativeRevisionAndInjectedObservationTime(int head, int retainedFloor)
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        StreamReadPage page = Page(head, retainedFloor);
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(page);
        var clock = new AuthoritativeReadTimeProvider();
        clock.Advance(TimeSpan.FromHours(1));

        NativeSourceObservation? result = await new GatewayNativeSourceMetadataReader(gateway, clock)
            .ReadAsync(Identity, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Proof.ShouldBe(page.Metadata.NativeProof);
        result.ObservedAt.ShouldBe(clock.GetUtcNow());
        var expected = new StreamReadRequest(Identity.TenantId, Identity.Domain, Identity.AggregateId,
            FromSequence: 0, ToSequence: 0, PageSize: 1) { RequireNativeProof = true };
        await gateway.Received(1).ReadStreamAsync(Arg.Is<StreamReadRequest>(request => request == expected), Arg.Any<CancellationToken>());
        gateway.ReceivedCalls().Count().ShouldBe(1);
    }

    [Theory]
    [InlineData("null-page")]
    [InlineData("null-events")]
    [InlineData("null-metadata")]
    [InlineData("events")]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    [InlineData("from")]
    [InlineData("to")]
    [InlineData("unbounded-to")]
    [InlineData("last")]
    [InlineData("count")]
    [InlineData("truncated")]
    [InlineData("continuation")]
    [InlineData("missing-proof")]
    [InlineData("proof-tenant")]
    [InlineData("proof-domain")]
    [InlineData("proof-aggregate")]
    [InlineData("head-mismatch")]
    [InlineData("negative-head")]
    [InlineData("codec")]
    [InlineData("erased")]
    [InlineData("incarnation")]
    [InlineData("digest")]
    [InlineData("floor")]
    [InlineData("floor-after-head")]
    [InlineData("missing-message")]
    [InlineData("long-message")]
    [InlineData("padded-message")]
    [InlineData("control-message")]
    [InlineData("empty-seed-digest")]
    [InlineData("empty-seed-message")]
    public async Task MalformedOrForeignGatewayEvidenceFailsClosedWithoutRetries(string change)
    {
        StreamReadPage page = Page(5);
        StreamReadMetadata metadata = page.Metadata;
        NativeSourceProof proof = metadata.NativeProof!;
        page = change switch
        {
            "null-page" => null!,
            "null-events" => page with { Events = null! },
            "null-metadata" => page with { Metadata = null! },
            "events" => page with { Events = [null!] },
            "tenant" => page with { Tenant = "other-tenant" },
            "domain" => page with { Domain = "other-domain" },
            "aggregate" => page with { AggregateId = "other-aggregate" },
            "from" => page with { Metadata = metadata with { FromSequence = 1 } },
            "to" => page with { Metadata = metadata with { ToSequence = 1 } },
            "unbounded-to" => page with { Metadata = metadata with { ToSequence = null } },
            "last" => page with { Metadata = metadata with { LastSequenceReturned = 0 } },
            "count" => page with { Metadata = metadata with { EventCount = 1 } },
            "truncated" => page with { Metadata = metadata with { IsTruncated = true } },
            "continuation" => page with { Metadata = metadata with { NextContinuationToken = new("unexpected") } },
            "missing-proof" => page with { Metadata = metadata with { NativeProof = null } },
            "proof-tenant" => page with { Metadata = metadata with { NativeProof = proof with { TenantId = "other-tenant" } } },
            "proof-domain" => page with { Metadata = metadata with { NativeProof = proof with { Domain = "other-domain" } } },
            "proof-aggregate" => page with { Metadata = metadata with { NativeProof = proof with { AggregateId = "other-aggregate" } } },
            "head-mismatch" => page with { Metadata = metadata with { LatestSequence = 4 } },
            "negative-head" => page with { Metadata = metadata with { LatestSequence = -1, NativeProof = proof with { HeadSequence = -1 } } },
            "codec" => page with { Metadata = metadata with { NativeProof = proof with { CodecVersion = 2 } } },
            "erased" => page with { Metadata = metadata with { NativeProof = proof with { Erased = true } } },
            "incarnation" => page with { Metadata = metadata with { NativeProof = proof with { SourceIncarnation = "invalid" } } },
            "digest" => page with { Metadata = metadata with { NativeProof = proof with { CumulativeDigest = new string('A', 64) } } },
            "floor" => page with { Metadata = metadata with { NativeProof = proof with { RetainedFloor = 0 } } },
            "floor-after-head" => page with { Metadata = metadata with { NativeProof = proof with { RetainedFloor = 7 } } },
            "missing-message" => page with { Metadata = metadata with { NativeProof = proof with { HeadMessageId = null } } },
            "long-message" => page with { Metadata = metadata with { NativeProof = proof with { HeadMessageId = new string('m', 257) } } },
            "padded-message" => page with { Metadata = metadata with { NativeProof = proof with { HeadMessageId = " message " } } },
            "control-message" => page with { Metadata = metadata with { NativeProof = proof with { HeadMessageId = "message\nidentity" } } },
            "empty-seed-digest" => Page(0) with { Metadata = Page(0).Metadata with { NativeProof = Page(0).Metadata.NativeProof! with { CumulativeDigest = new string('a', 64) } } },
            _ => Page(0) with { Metadata = Page(0).Metadata with { NativeProof = Page(0).Metadata.NativeProof! with { HeadMessageId = "unexpected" } } },
        };
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(page);

        (await new GatewayNativeSourceMetadataReader(gateway, TimeProvider.System)
            .ReadAsync(Identity, TestContext.Current.CancellationToken)).ShouldBeNull();
        gateway.ReceivedCalls().Count().ShouldBe(1);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(503)]
    [InlineData(500)]
    public async Task GatewayRefusalsReturnUnavailableWithoutRetries(int status)
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EventStoreGatewayException(status, "unavailable"));
        (await new GatewayNativeSourceMetadataReader(gateway, TimeProvider.System)
            .ReadAsync(Identity, TestContext.Current.CancellationToken)).ShouldBeNull();
        gateway.ReceivedCalls().Count().ShouldBe(1);
    }

    [Fact]
    public async Task ProviderCancellationWithoutCallerCancellationReturnsUnavailable()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());
        (await new GatewayNativeSourceMetadataReader(gateway, TimeProvider.System)
            .ReadAsync(Identity, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Theory]
    [InlineData("pending", false)]
    [InlineData("validation", false)]
    [InlineData("invocation", false)]
    [InlineData("invocation", true)]
    public async Task NoncooperativeGatewayOrResponseValidationCannotExceedTwoSecondDeadline(string blockedStage, bool cancelCaller)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new TaskCompletionSource<StreamReadPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var caller = new CancellationTokenSource();
        var events = Substitute.For<IReadOnlyList<StreamReadEvent>>();
        events.Count.Returns(_ => { entered.TrySetResult(); release.Wait(); return 0; });
        StreamReadPage page = Page(5) with { Events = events };
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (blockedStage == "validation") return Task.FromResult(page);
            entered.TrySetResult();
            if (blockedStage == "invocation") { release.Wait(); return Task.FromResult(Page(5)); }
            return provider.Task;
        });
        var clock = new AuthoritativeReadTimeProvider();
        Task<NativeSourceObservation?> pending = new GatewayNativeSourceMetadataReader(gateway, clock)
            .ReadAsync(Identity, caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (cancelCaller) caller.Cancel();
            clock.Advance(TimeSpan.FromSeconds(2));
            if (cancelCaller)
            {
                OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() =>
                    pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
                exception.CancellationToken.ShouldBe(caller.Token);
            }
            else (await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).ShouldBeNull();
            gateway.ReceivedCalls().Count().ShouldBe(1);
        }
        finally
        {
            release.Set();
            provider.TrySetResult(Page(5));
        }
    }

    [Fact]
    public async Task MonotonicDeadlineRejectsLateProofEvenWhenTimerDeliveryIsDelayed()
    {
        var clock = new AuthoritativeReadTimeProvider();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            clock.Advance(TimeSpan.FromSeconds(2), fireTimers: false);
            return Page(5);
        });
        (await new GatewayNativeSourceMetadataReader(gateway, clock)
            .ReadAsync(Identity, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Theory]
    [InlineData("before")]
    [InlineData("pending")]
    [InlineData("fault")]
    [InlineData("deadline")]
    public async Task CallerCancellationTakesPrecedenceOverDependencyFailureOrDeadline(string when)
    {
        using var canceled = new CancellationTokenSource();
        var clock = new AuthoritativeReadTimeProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new TaskCompletionSource<StreamReadPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            entered.TrySetResult();
            if (when == "fault") { canceled.Cancel(); throw new HttpRequestException("fault"); }
            return provider.Task;
        });
        if (when == "before") canceled.Cancel();
        Task<NativeSourceObservation?> pending = new GatewayNativeSourceMetadataReader(gateway, clock).ReadAsync(Identity, canceled.Token);
        try
        {
            if (when is "pending" or "deadline")
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                canceled.Cancel();
                if (when == "deadline") clock.Advance(TimeSpan.FromSeconds(2));
            }
            OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() => pending);
            exception.CancellationToken.ShouldBe(canceled.Token);
            gateway.ReceivedCalls().Count().ShouldBe(when == "before" ? 0 : 1);
        }
        finally { provider.TrySetResult(Page(5)); }
    }

    private static StreamReadPage Page(int head, int retainedFloor = 1)
    {
        NativeSourceProof proof = NativeSourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        for (int sequence = 1; sequence <= head; sequence++)
            proof = NativeSourceProofCodec.ExtendDigest(proof, Identity, sequence, "native-message-" + sequence, new string('a', 64));
        proof = proof with { RetainedFloor = retainedFloor };
        return new(Identity.TenantId, Identity.Domain, Identity.AggregateId, [],
            new(0, 0, null, head, 0, false, null) { NativeProof = proof });
    }
}

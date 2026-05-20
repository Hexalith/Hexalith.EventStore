using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>
/// Tier 1 coverage of the Dapr-backed adapter that fronts the canonical replay path.
/// Verifies the failure-category mapping for the categories the adapter owns
/// (UnknownAggregateType when no registration, defensive guards on inputs).
/// Apply-level failure-category mapping is owned by AggregateReplayer and covered in
/// AggregateReplayerTests in Hexalith.EventStore.Client.Tests.
/// HTTP transport-failure paths require a live Dapr sidecar and are validated by Tier
/// 2/3 ST3 integration coverage.
/// </summary>
public class DaprAggregateStateReconstructorTests {
    private static readonly AggregateIdentity Identity = new("tenant-a", "counter", "counter-1");

    private static ServerEventEnvelope BuildEnvelope(long seq)
        => new(
            MessageId: $"msg-{seq}",
            AggregateId: "counter-1",
            AggregateType: "Counter",
            TenantId: "tenant-a",
            Domain: "counter",
            SequenceNumber: seq,
            GlobalPosition: seq,
            Timestamp: new DateTimeOffset(2026, 05, 07, 12, 0, 0, TimeSpan.Zero).AddSeconds(seq),
            CorrelationId: $"corr-{seq}",
            CausationId: string.Empty,
            UserId: "test-user",
            DomainServiceVersion: "v1",
            EventTypeName: "CounterIncremented",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: Encoding.UTF8.GetBytes("{}"),
            Extensions: null);

    [Fact]
    public async Task ReconstructAsync_NoDomainRegistration_ReturnsFailedUnknownAggregateType() {
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver.ResolveAsync("tenant-a", "counter", "v1", Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);
        DaprClient daprClient = Substitute.For<DaprClient>();
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        var reconstructor = new DaprAggregateStateReconstructor(daprClient, factory, resolver, NullLogger<DaprAggregateStateReconstructor>.Instance);

        AggregateReconstructionResult result = await reconstructor.ReconstructAsync(
            Identity, "Counter", [BuildEnvelope(1)], upToSequence: 1, includeTimeline: false);

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnknownAggregateType);
        result.LastAppliedSequenceNumber.ShouldBe(0);
    }

    [Fact]
    public async Task ReconstructAsync_ResolvesDomainServiceVersionFromLatestEligibleEvent() {
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver.ResolveAsync("tenant-a", "counter", "v2", Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);
        var reconstructor = new DaprAggregateStateReconstructor(
            Substitute.For<DaprClient>(),
            Substitute.For<IHttpClientFactory>(),
            resolver,
            NullLogger<DaprAggregateStateReconstructor>.Instance);
        ServerEventEnvelope v1 = BuildEnvelope(1);
        ServerEventEnvelope v2 = v1 with { SequenceNumber = 2, DomainServiceVersion = "v2" };

        _ = await reconstructor.ReconstructAsync(Identity, "Counter", [v1, v2], upToSequence: 2);

        _ = await resolver.Received(1).ResolveAsync("tenant-a", "counter", "v2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconstructAsync_InvalidDomainServiceVersion_FailsBeforeResolution() {
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        var reconstructor = new DaprAggregateStateReconstructor(
            Substitute.For<DaprClient>(),
            Substitute.For<IHttpClientFactory>(),
            resolver,
            NullLogger<DaprAggregateStateReconstructor>.Instance);
        ServerEventEnvelope invalid = BuildEnvelope(1) with { DomainServiceVersion = "latest" };

        AggregateReconstructionResult result = await reconstructor.ReconstructAsync(
            Identity, "Counter", [invalid], upToSequence: 1);

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnsupportedVersion);
        _ = await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task ReconstructAsync_NegativeUpToSequence_FailsBeforeResolution() {
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        var reconstructor = new DaprAggregateStateReconstructor(
            Substitute.For<DaprClient>(),
            Substitute.For<IHttpClientFactory>(),
            resolver,
            NullLogger<DaprAggregateStateReconstructor>.Instance);

        AggregateReconstructionResult result = await reconstructor.ReconstructAsync(
            Identity, "Counter", [], upToSequence: -1);

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        _ = await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task ReconstructAsync_NullIdentity_Throws() {
        var reconstructor = new DaprAggregateStateReconstructor(
            Substitute.For<DaprClient>(),
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<IDomainServiceResolver>(),
            NullLogger<DaprAggregateStateReconstructor>.Instance);

        _ = await Should.ThrowAsync<ArgumentNullException>(() => reconstructor.ReconstructAsync(
            null!, "Counter", [], upToSequence: 0));
    }

    [Fact]
    public async Task ReconstructAsync_NullEvents_Throws() {
        var reconstructor = new DaprAggregateStateReconstructor(
            Substitute.For<DaprClient>(),
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<IDomainServiceResolver>(),
            NullLogger<DaprAggregateStateReconstructor>.Instance);

        _ = await Should.ThrowAsync<ArgumentNullException>(() => reconstructor.ReconstructAsync(
            Identity, "Counter", null!, upToSequence: 0));
    }

    [Fact]
    public void ReplayStateMethodName_HasCanonicalValue() =>
        // The Sample's Program.cs and any non-sample domain service router must register
        // POST /replay-state. This constant is the contract string consumed by both sides.
        DaprAggregateStateReconstructor.ReplayStateMethodName.ShouldBe("replay-state");
}

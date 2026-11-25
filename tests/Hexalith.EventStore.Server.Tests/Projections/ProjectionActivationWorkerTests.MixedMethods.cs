using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class ProjectionActivationWorkerTests
{
    [Fact]
    public async Task MalformedNamedHeadRetainsDurableActivationUntilSourceCanBeRead()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(1);
        fixture.UseOlderSource();
        fixture.UseProductionNamedCoordinator(hasNamedRoute: true);
        EventEnvelope head = fixture.Events.Single();
        string eventKey = fixture.Identity.EventStreamKeyPrefix + head.SequenceNumber;
        await fixture.SourceState.SetStateAsync(eventKey, head with { MessageId = " " });
        await fixture.SourceState.SaveStateAsync();
        ProjectionActivationWorkItem original = (await fixture.Outbox.GetAsync(fixture.Identity)).ShouldNotBeNull();
        var authority = Substitute.For<IRouteCatalogAuthority>();
        CommittedRouteCatalogSnapshot snapshot = MixedSnapshot("counter-named", includeLegacy: false);
        authority.AcquireAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new RouteCatalogDispatchLease(snapshot, () => Task.CompletedTask));
        var options = new ProjectionDispatchOptions();
        using var worker = new ProjectionActivationWorker(fixture.Outbox, fixture.CreateOrchestrator(authority),
            Options.Create(options), fixture.Clock, catalogAuthority: authority);

        await worker.RunOnceAsync(TestContext.Current.CancellationToken);

        ProjectionActivationWorkItem deferred = (await fixture.Outbox.GetAsync(fixture.Identity)).ShouldNotBeNull();
        deferred.ActivationId.ShouldBe(original.ActivationId);
        deferred.Revision.ShouldBe(original.Revision);
        deferred.Attempt.ShouldBe(original.Attempt + 1);
        (await fixture.NamedScheduler!.GetDueAsync(fixture.Clock.GetUtcNow().AddHours(1), 64)).ShouldBeEmpty();

        // Restore the original committed head. The existing due item is sufficient to retry.
        await fixture.SourceState.SetStateAsync(eventKey, head);
        await fixture.SourceState.SaveStateAsync();
        fixture.Clock.Advance(options.RetryBaseDelay);
        await worker.RunOnceAsync(TestContext.Current.CancellationToken);

        (await fixture.Outbox.GetAsync(fixture.Identity)).ShouldBeNull();
        ProjectionDeliveryRetryWorkItem retry = (await fixture.NamedScheduler.GetDueAsync(fixture.Clock.GetUtcNow().AddHours(1), 64))
            .ShouldHaveSingleItem();
        retry.HeadMessageId.ShouldBe(head.MessageId);
    }

    [Fact]
    public async Task MixedDeliveryDoesNotBindAnActivationRearmedAfterNamedProofWasSampled()
    {
        using var legacy = new LegacyReplayProductionHarness();
        await legacy.InitializeAsync(2);
        ProjectionActivationWorkItem original = (await legacy.Outbox.GetAsync(legacy.Identity)).ShouldNotBeNull();
        var guard = Substitute.For<IProjectionSourceGuardDelivery>();
        guard.InspectCurrentAsync(legacy.Identity, "counter", Arg.Any<CancellationToken>())
            .Returns(new ProjectionSourceGuardDeliveryResult(false, false, null));
        guard.TryDeliverAsync(legacy.Identity, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await legacy.AppendAsync(1);
            await legacy.Outbox.EnsureAsync(legacy.Identity);
            return new ProjectionSourceGuardDeliveryResult(true, true, null) { FinalizedCleanupProven = true };
        });
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new RouteCatalogDispatchLease(MixedSnapshot("counter-named"), () => Task.CompletedTask));
        legacy.NamedCoordinator = Substitute.For<INamedProjectionDispatchCoordinator>();

        await legacy.CreateOrchestrator(authority, guard).DeliverProjectionAsync(legacy.Identity, TestContext.Current.CancellationToken);

        ProjectionActivationWorkItem rearmed = (await legacy.Outbox.GetAsync(legacy.Identity)).ShouldNotBeNull();
        rearmed.Revision.ShouldBeGreaterThan(original.Revision);
        rearmed.LegacyDelivery.ShouldBeNull();
        (await legacy.Model.ReadProjectionStateAsync()).ShouldBeNull();
    }

    [Theory]
    [InlineData(false, "single-event-v1")]
    [InlineData(true, "single-event-v1")]
    [InlineData(false, "full-replay-v1")]
    [InlineData(true, "full-replay-v1")]
    public async Task GuardedNamedAndLegacyCommittedMethodsBothPersistBeforeCleanupAndForeignLegacyOutputRetainsWork(bool refuseLegacy, string semantics)
    {
        using GuardedTailProductionHarness named = await GuardedTailProductionHarness.CreateAsync(2, 1, handlerSemantics: semantics);
        await named.UseProductionOwnersAsync();
        using var legacy = new LegacyReplayProductionHarness(backend: named.Backend);
        await legacy.SeedSourceAsync(named.Events);
        legacy.Head.ShouldBe(named.Head);
        legacy.NamedCoordinator = Substitute.For<INamedProjectionDispatchCoordinator>();
        if (refuseLegacy) legacy.Http.ResponseProjectionTypeOverride = "undeclared-output";
        string route = named.Catalog.OrdinaryProjectionTypes.Single();
        CommittedRouteCatalogSnapshot snapshot = MixedSnapshot(route);
        var authority = Substitute.For<IRouteCatalogAuthority>();
        int acquisitions = 0, releases = 0;
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            acquisitions++;
            return new RouteCatalogDispatchLease(snapshot, () => { releases++; return Task.CompletedTask; });
        });
        // Hold the public legacy HTTP carrier after the actual named model/row has completed.
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        legacy.Http.BeforePreparation = async () => { entered.TrySetResult(); await resume.Task; };
        using var worker = new ProjectionActivationWorker(legacy.Outbox, legacy.CreateOrchestrator(authority, named.Probe),
            Options.Create(new ProjectionDispatchOptions()), named.Clock, named.Probe, authority);
        Task attempt = worker.RunOnceAsync(CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            acquisitions.ShouldBe(1);
            releases.ShouldBe(0);
            attempt.IsCompleted.ShouldBeFalse();
            var namedRow = new DaprProjectionDeliveryStateStore(named.Backend, Options.Create(new ProjectionOptions()));
            (await namedRow.ReadAsync(named.Identity, route)).State.ShouldNotBeNull().LastDeliveredSequence.ShouldBe(2);
            (await named.Models.GetAsync<GuardedTailModel>("models", named.Identity.ActorId + ":" + route)).Value.ShouldNotBeNull().Count.ShouldBe(2);
            (await legacy.Outbox.GetAsync(legacy.Identity)).ShouldNotBeNull().LegacyDelivery.ShouldNotBeNull();
            (await legacy.Model.ReadProjectionStateAsync()).ShouldBeNull();
            resume.SetResult();
            await attempt.WaitAsync(TimeSpan.FromSeconds(5));
            acquisitions.ShouldBe(1);
            legacy.Http.Preparations.ShouldHaveSingleItem().ShouldBe([1L, 2L]);
            if (refuseLegacy)
            {
                (await legacy.Outbox.GetAsync(legacy.Identity)).ShouldNotBeNull().LegacyDelivery.ShouldNotBeNull();
                (await legacy.Model.ReadProjectionStateAsync()).ShouldBeNull();
                (await legacy.Rows.ReadAsync(legacy.Identity, "counter")).State.ShouldBeNull();
            }
            else
            {
                releases.ShouldBe(1);
                (await legacy.Outbox.GetAsync(legacy.Identity)).ShouldBeNull();
                (await legacy.Model.ReadProjectionStateAsync()).ShouldNotBeNull().GetState().GetProperty("count").GetInt32().ShouldBe(2);
                ProjectionDeliveryState row = (await legacy.Rows.ReadAsync(legacy.Identity, "counter")).State.ShouldNotBeNull();
                row.LastDeliveredSequence.ShouldBe(2);
                row.ActiveReservation.ShouldBeNull();
                (await namedRow.ReadAsync(named.Identity, route)).State.ShouldNotBeNull().ActiveReservation.ShouldBeNull();
            }
        }
        finally { resume.TrySetResult(); await attempt.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    private static CommittedRouteCatalogSnapshot MixedSnapshot(string namedOutput, bool includeLegacy = true)
    {
        var fixture = new RouteCatalogTestFixture();
        RouteContractDeclaration legacy = fixture.Envelope.Declarations.Single(declaration => declaration.RouteId == "counter-projection");
        RouteContractDeclaration named = legacy with
        {
            RouteId = "counter-named-projection", ProjectionType = namedOutput,
            Execution = legacy.Execution! with { HandlerType = typeof(GuardedTailProjectionHandler).FullName!, Aliases = [namedOutput], ProjectionOutputs = [namedOutput] },
        };
        RouteCatalogEntry target = fixture.Envelope.Routes.Single(entry => entry.RouteId == legacy.RouteId);
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Declarations = [.. fixture.Envelope.Declarations.Where(declaration => includeLegacy || declaration != legacy), named],
            Routes = [.. fixture.Envelope.Routes.Where(route => includeLegacy || route != target), target with { RouteId = named.RouteId,
                DeclarationDigest = RouteCatalogCodec.ComputeDeclarationDigest(named), Method = "project/v2" }],
        });
        return new(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1);
    }
}

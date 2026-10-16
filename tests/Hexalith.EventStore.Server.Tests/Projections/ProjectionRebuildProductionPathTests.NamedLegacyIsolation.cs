using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Projections;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class ProjectionRebuildProductionPathTests
{
    [Fact]
    public async Task NamedDelivery_AdvancedHeadProgressesWhileUnsuccessfulLegacyReplayKeepsItsOriginalLease()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(1).ConfigureAwait(true);
        INamedProjectionDispatchCoordinator named = Substitute.For<INamedProjectionDispatchCoordinator>();
        named.TryDispatchHeadAsync(fixture.Identity, Arg.Any<DomainServiceRegistration>(), Arg.Any<long>(),
            Arg.Any<string>(), Arg.Any<ProjectionEventDto[]>(), Arg.Any<CancellationToken>()).Returns(true);
        fixture.NamedCoordinator = named;
        fixture.Http.BeforePreparation = () => throw new IOException("synthetic circuit-open legacy endpoint");
        await fixture.DeliverAsync().ConfigureAwait(true);
        ProjectionLegacyDeliveryIntent original = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true))!.LegacyDelivery.ShouldNotBeNull();
        original.HeadSequence.ShouldBe(1);
        original.InvocationToken.ShouldNotBeNull();
        await fixture.AppendAsync(1).ConfigureAwait(true);
        await fixture.Outbox.EnsureAsync(fixture.Identity).ConfigureAwait(true);

        await fixture.DeliverAsync().ConfigureAwait(true);

        await named.Received(1).TryDispatchHeadAsync(fixture.Identity, Arg.Any<DomainServiceRegistration>(), 2,
            fixture.Events[1].MessageId, Arg.Is<ProjectionEventDto[]>(events => events.Length == 2
                && events[0].SequenceNumber == 1 && events[1].SequenceNumber == 2), Arg.Any<CancellationToken>()).ConfigureAwait(true);
        ProjectionActivationWorkItem retained = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldNotBeNull();
        retained.LegacyDelivery.ShouldBe(original);
        retained.Revision.ShouldBeGreaterThan(original.FrozenActivationRevision);
        fixture.ModelState.CommittedState.ShouldBeEmpty();
        fixture.Http.Preparations.ShouldBeEmpty();
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NamedDelivery_UnchangedHeadRetriesIndependentlyWithoutRebindingLegacyIntent(bool namedCompleted)
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(1).ConfigureAwait(true);
        INamedProjectionDispatchCoordinator named = Substitute.For<INamedProjectionDispatchCoordinator>();
        named.TryDispatchHeadAsync(fixture.Identity, Arg.Any<DomainServiceRegistration>(), Arg.Any<long>(),
            Arg.Any<string>(), Arg.Any<ProjectionEventDto[]>(), Arg.Any<CancellationToken>()).Returns(namedCompleted);
        fixture.NamedCoordinator = named;
        fixture.Http.BeforePreparation = () => throw new IOException("synthetic circuit-open legacy endpoint");
        await fixture.DeliverAsync().ConfigureAwait(true);
        ProjectionActivationWorkItem original = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldNotBeNull();
        original.LegacyDelivery.ShouldNotBeNull().InvocationToken.ShouldNotBeNull();
        named.ClearReceivedCalls();

        await fixture.DeliverAsync().ConfigureAwait(true);

        await named.Received(1).TryDispatchHeadAsync(fixture.Identity, Arg.Any<DomainServiceRegistration>(), 1,
            fixture.Events[0].MessageId, Arg.Is<ProjectionEventDto[]>(events => events.Length == 1 && events[0].SequenceNumber == 1),
            Arg.Any<CancellationToken>()).ConfigureAwait(true);
        ProjectionActivationWorkItem retained = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldNotBeNull();
        retained.LegacyDelivery.ShouldBe(original.LegacyDelivery);
        retained.Revision.ShouldBe(original.Revision);
        fixture.ModelState.CommittedState.ShouldBeEmpty();
        fixture.Http.Preparations.ShouldBeEmpty();
    }

}

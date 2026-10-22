using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Builders;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Testing.Tests.Fakes;

public class FakeEventPersisterTests {
    [Fact]
    public async Task PersistEventsAsync_DifferentAggregates_AssignsNonZeroCrossAggregateGlobalPositions() {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var sut = new FakeEventPersister();
        AggregateIdentity firstIdentity = new AggregateIdentityBuilder().WithAggregateId("aggregate-1").Build();
        AggregateIdentity secondIdentity = new AggregateIdentityBuilder().WithAggregateId("aggregate-2").Build();
        CommandEnvelope firstCommand = new CommandEnvelopeBuilder().WithAggregateId("aggregate-1").Build();
        CommandEnvelope secondCommand = new CommandEnvelopeBuilder().WithAggregateId("aggregate-2").Build();
        var domainResult = DomainResult.Success([new FakeEventPersisterTestEvent("persisted")]);

        // Act
        EventPersistResult firstResult = await sut.PersistEventsAsync(
            firstIdentity,
            "test-aggregate",
            firstCommand,
            domainResult,
            "v1",
            cancellationToken);
        EventPersistResult secondResult = await sut.PersistEventsAsync(
            secondIdentity,
            "test-aggregate",
            secondCommand,
            domainResult,
            "v1",
            cancellationToken);

        // Assert
        EventEnvelope firstEnvelope = firstResult.PersistedEnvelopes.ShouldHaveSingleItem();
        EventEnvelope secondEnvelope = secondResult.PersistedEnvelopes.ShouldHaveSingleItem();
        firstEnvelope.GlobalPosition.ShouldBe(1);
        secondEnvelope.GlobalPosition.ShouldBe(2);
        firstEnvelope.GlobalPosition.ShouldBeGreaterThan(0);
        secondEnvelope.GlobalPosition.ShouldBeGreaterThan(firstEnvelope.GlobalPosition);
        firstEnvelope.SequenceNumber.ShouldBe(1);
        secondEnvelope.SequenceNumber.ShouldBe(1);
    }
}

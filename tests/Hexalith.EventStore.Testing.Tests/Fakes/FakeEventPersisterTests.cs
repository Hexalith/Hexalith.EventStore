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
    public async Task PersistEventsAsync_MatchesTheWireWriterForTypedAndSerializedPayloads() {
        var persister = new FakeEventPersister();
        AggregateIdentity identity = new AggregateIdentityBuilder().Build();
        CommandEnvelope command = new CommandEnvelopeBuilder().Build();
        byte[] original = [1, 2, 3];
        DomainResult domainResult = DomainResult.Success([
            new FakeEventPersisterTestEvent("typed"),
            new FakeSerializedEventPayload("supplied-event", original, "application/octet-stream"),
        ]);

        EventPersistResult result = await persister.PersistEventsAsync(
            identity, "test-aggregate", command, domainResult, "v1", TestContext.Current.CancellationToken);
        DomainServiceWireResult wire = DomainServiceWireResult.FromDomainResult(domainResult);

        result.PersistedEnvelopes.Count.ShouldBe(wire.Events.Count);
        for (int index = 0; index < wire.Events.Count; index++) {
            EventEnvelope envelope = result.PersistedEnvelopes[index];
            envelope.Payload.ShouldBe(wire.Events[index].Payload);
            envelope.EventTypeName.ShouldBe(wire.Events[index].EventTypeName);
            envelope.SerializationFormat.ShouldBe(wire.Events[index].SerializationFormat);
            envelope.PayloadVersion.ShouldBe(wire.Events[index].PayloadVersion);
        }

        original[0] = 42;
        persister.PersistedEvents[1].Payload.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task PersistEventsAsync_RejectsProtectedInputWithoutPublishingPartOfTheBatch() {
        var persister = new FakeEventPersister();
        AggregateIdentity identity = new AggregateIdentityBuilder().Build();
        CommandEnvelope command = new CommandEnvelopeBuilder().Build();
        var valid = new FakeEventPersisterTestEvent("typed");
        DomainResult batch = DomainResult.Success([valid, new FakeSerializedEventPayload("protected-event", [1], "json+pdenc-v2")]);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => persister.PersistEventsAsync(
            identity, "test-aggregate", command, batch, "v1", TestContext.Current.CancellationToken));
        persister.PersistedEvents.ShouldBeEmpty();

        EventPersistResult retry = await persister.PersistEventsAsync(
            identity, "test-aggregate", command, DomainResult.Success([valid]), "v1", TestContext.Current.CancellationToken);
        EventEnvelope persisted = retry.PersistedEnvelopes.ShouldHaveSingleItem();
        persisted.SequenceNumber.ShouldBe(1);
        persisted.GlobalPosition.ShouldBe(1);
    }

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

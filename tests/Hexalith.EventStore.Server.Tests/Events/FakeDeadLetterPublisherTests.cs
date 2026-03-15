
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

public class FakeDeadLetterPublisherTests {
    private static CommandEnvelope CreateTestEnvelope(
        string tenantId = "test-tenant",
        string domain = "test-domain",
        string aggregateId = "agg-001") => new(
        MessageId: Guid.NewGuid().ToString(),
        TenantId: tenantId,
        Domain: domain,
        AggregateId: aggregateId,
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: Guid.NewGuid().ToString(),
        CausationId: null,
        UserId: "system",
        Extensions: null);

    private static DeadLetterMessage CreateTestDeadLetterMessage(CommandEnvelope? command = null) {
        CommandEnvelope cmd = command ?? CreateTestEnvelope();
        return DeadLetterMessage.FromException(
            cmd,
            CommandStatus.Processing,
            new InvalidOperationException("Test error"));
    }

    [Fact]
    public async Task PublishDeadLetter_CapturesMessage() {
        // Arrange
        var publisher = new FakeDeadLetterPublisher();
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        DeadLetterMessage message = CreateTestDeadLetterMessage();

        // Act
        bool result = await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        result.ShouldBeTrue();
        IReadOnlyList<(AggregateIdentity Identity, DeadLetterMessage Message)> messages = publisher.GetDeadLetterMessages();
        messages.Count.ShouldBe(1);
        messages[0].Identity.ShouldBe(identity);
        messages[0].Message.ShouldBe(message);
    }

    [Fact]
    public async Task GetDeadLetterMessages_ReturnsAll() {
        // Arrange
        var publisher = new FakeDeadLetterPublisher();
        var identity1 = new AggregateIdentity("tenant-a", "orders", "agg-001");
        DeadLetterMessage message1 = CreateTestDeadLetterMessage(CreateTestEnvelope("tenant-a", "orders", "agg-001"));

        var identity2 = new AggregateIdentity("tenant-b", "inventory", "agg-002");
        DeadLetterMessage message2 = CreateTestDeadLetterMessage(CreateTestEnvelope("tenant-b", "inventory", "agg-002"));

        var identity3 = new AggregateIdentity("tenant-a", "orders", "agg-003");
        DeadLetterMessage message3 = CreateTestDeadLetterMessage(CreateTestEnvelope("tenant-a", "orders", "agg-003"));

        // Act
        _ = await publisher.PublishDeadLetterAsync(identity1, message1);
        _ = await publisher.PublishDeadLetterAsync(identity2, message2);
        _ = await publisher.PublishDeadLetterAsync(identity3, message3);

        // Assert
        IReadOnlyList<(AggregateIdentity Identity, DeadLetterMessage Message)> messages = publisher.GetDeadLetterMessages();
        messages.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetDeadLetterMessagesForTenant_FiltersByTenant() {
        // Arrange
        var publisher = new FakeDeadLetterPublisher();
        var identity1 = new AggregateIdentity("tenant-a", "orders", "agg-001");
        DeadLetterMessage message1 = CreateTestDeadLetterMessage(CreateTestEnvelope("tenant-a", "orders", "agg-001"));

        var identity2 = new AggregateIdentity("tenant-b", "inventory", "agg-002");
        DeadLetterMessage message2 = CreateTestDeadLetterMessage(CreateTestEnvelope("tenant-b", "inventory", "agg-002"));

        var identity3 = new AggregateIdentity("tenant-a", "orders", "agg-003");
        DeadLetterMessage message3 = CreateTestDeadLetterMessage(CreateTestEnvelope("tenant-a", "orders", "agg-003"));

        _ = await publisher.PublishDeadLetterAsync(identity1, message1);
        _ = await publisher.PublishDeadLetterAsync(identity2, message2);
        _ = await publisher.PublishDeadLetterAsync(identity3, message3);

        // Act
        IReadOnlyList<(AggregateIdentity Identity, DeadLetterMessage Message)> tenantAMessages = publisher.GetDeadLetterMessagesForTenant("tenant-a");
        IReadOnlyList<(AggregateIdentity Identity, DeadLetterMessage Message)> tenantBMessages = publisher.GetDeadLetterMessagesForTenant("tenant-b");

        // Assert
        tenantAMessages.Count.ShouldBe(2);
        tenantAMessages.ShouldAllBe(m => m.Identity.TenantId == "tenant-a");
        tenantBMessages.Count.ShouldBe(1);
        tenantBMessages[0].Identity.TenantId.ShouldBe("tenant-b");
    }

    [Fact]
    public async Task GetDeadLetterMessageByCorrelationId_FindsCorrect() {
        // Arrange
        var publisher = new FakeDeadLetterPublisher();
        string targetCorrelationId = Guid.NewGuid().ToString();

        CommandEnvelope envelope1 = CreateTestEnvelope();
        var identity1 = new AggregateIdentity(envelope1.TenantId, envelope1.Domain, envelope1.AggregateId);
        DeadLetterMessage message1 = CreateTestDeadLetterMessage(envelope1);

        var envelope2 = new CommandEnvelope(
            MessageId: "msg-dlq-correlation-1",
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-002",
            CommandType: "UpdateOrder",
            Payload: [4, 5, 6],
            CorrelationId: targetCorrelationId,
            CausationId: null,
            UserId: "system",
            Extensions: null);
        var identity2 = new AggregateIdentity(envelope2.TenantId, envelope2.Domain, envelope2.AggregateId);
        DeadLetterMessage message2 = CreateTestDeadLetterMessage(envelope2);

        CommandEnvelope envelope3 = CreateTestEnvelope();
        var identity3 = new AggregateIdentity(envelope3.TenantId, envelope3.Domain, envelope3.AggregateId);
        DeadLetterMessage message3 = CreateTestDeadLetterMessage(envelope3);

        _ = await publisher.PublishDeadLetterAsync(identity1, message1);
        _ = await publisher.PublishDeadLetterAsync(identity2, message2);
        _ = await publisher.PublishDeadLetterAsync(identity3, message3);

        // Act
        (AggregateIdentity Identity, DeadLetterMessage Message)? found = publisher.GetDeadLetterMessageByCorrelationId(targetCorrelationId);

        // Assert
        _ = found.ShouldNotBeNull();
        found.Value.Message.CorrelationId.ShouldBe(targetCorrelationId);
        found.Value.Identity.ShouldBe(identity2);
    }

    [Fact]
    public async Task SetupFailure_AllCallsReturnFalse() {
        // Arrange
        var publisher = new FakeDeadLetterPublisher();
        publisher.SetupFailure("Test failure");
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        DeadLetterMessage message = CreateTestDeadLetterMessage();

        // Act
        bool result1 = await publisher.PublishDeadLetterAsync(identity, message);
        bool result2 = await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        result1.ShouldBeFalse();
        result2.ShouldBeFalse();
        publisher.GetDeadLetterMessages().Count.ShouldBe(0);
    }

    [Fact]
    public async Task PublishDeadLetter_CanceledToken_ThrowsOperationCanceledException() {
        // Arrange
        var publisher = new FakeDeadLetterPublisher();
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        DeadLetterMessage message = CreateTestDeadLetterMessage();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => publisher.PublishDeadLetterAsync(identity, message, cts.Token));
    }

    [Fact]
    public async Task AssertNoDeadLetters_ThrowsWhenMessagesExist() {
        // Arrange
        var publisher = new FakeDeadLetterPublisher();
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        DeadLetterMessage message = CreateTestDeadLetterMessage();
        _ = await publisher.PublishDeadLetterAsync(identity, message);

        // Act & Assert
        Should.Throw<InvalidOperationException>(publisher.AssertNoDeadLetters)
            .Message.ShouldContain("Expected no dead-letter messages");
    }

    [Fact]
    public async Task Reset_ClearsAllState() {
        // Arrange
        var publisher = new FakeDeadLetterPublisher();
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        DeadLetterMessage message = CreateTestDeadLetterMessage();

        _ = await publisher.PublishDeadLetterAsync(identity, message);
        publisher.SetupFailure("Test failure");

        // Act
        publisher.Reset();

        // Assert
        publisher.GetDeadLetterMessages().Count.ShouldBe(0);
        bool result = await publisher.PublishDeadLetterAsync(identity, message);
        result.ShouldBeTrue();
        publisher.GetDeadLetterMessages().Count.ShouldBe(1);
    }
}

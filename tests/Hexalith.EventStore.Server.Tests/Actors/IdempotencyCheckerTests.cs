using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class IdempotencyCheckerTests
{
    private readonly IActorStateManager _stateManager = Substitute.For<IActorStateManager>();
    private readonly ILogger<IdempotencyChecker> _logger = Substitute.For<ILogger<IdempotencyChecker>>();

    private static CommandProcessingIdentity Identity(
        string messageId = "message-123",
        string causationId = "cause-123",
        string commandType = "TestCommand")
        => new(messageId, causationId, commandType);

    private IdempotencyChecker CreateChecker() => new(_stateManager, _logger);

    [Fact]
    public async Task CheckAsync_NoExistingRecord_ReturnsMiss()
    {
        _ = _stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        IdempotencyChecker checker = CreateChecker();

        IdempotencyCheckResult result = await checker.CheckAsync(Identity());

        result.Outcome.ShouldBe(IdempotencyCheckOutcome.Miss);
        result.Result.ShouldBeNull();
    }

    [Fact]
    public async Task CheckAsync_ExactTerminalRecord_ReturnsCompleteCachedResult()
    {
        CommandProcessingIdentity identity = Identity();
        var record = new IdempotencyRecord(
            identity.CausationId,
            "corr-456",
            true,
            null,
            DateTimeOffset.UtcNow,
            EventCount: 2,
            ResultPayload: """{"status":"ok"}""",
            BackpressureExceeded: true,
            BackpressurePendingCount: 9,
            BackpressureThreshold: 8,
            MessageId: identity.MessageId,
            CommandType: identity.CommandType,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            Disposition: IdempotencyRecordDisposition.Terminal);
        _ = _stateManager.TryGetStateAsync<IdempotencyRecord>("idempotency:message-123", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));
        IdempotencyChecker checker = CreateChecker();

        IdempotencyCheckResult check = await checker.CheckAsync(identity);

        check.Outcome.ShouldBe(IdempotencyCheckOutcome.ExactTerminalDuplicate);
        CommandProcessingResult result = check.Result.ShouldNotBeNull();
        result.Accepted.ShouldBeTrue();
        result.CorrelationId.ShouldBe("corr-456");
        result.EventCount.ShouldBe(2);
        result.ResultPayload.ShouldBe("""{"status":"ok"}""");
        result.BackpressureExceeded.ShouldBeTrue();
        result.BackpressurePendingCount.ShouldBe(9);
        result.BackpressureThreshold.ShouldBe(8);
    }

    [Theory]
    [InlineData("different-message", "cause-123", "TestCommand")]
    [InlineData("message-123", "different-cause", "TestCommand")]
    [InlineData("message-123", "cause-123", "DifferentCommand")]
    public async Task CheckAsync_StoredIdentityMismatch_ReturnsConflictWithoutMutation(
        string storedMessageId,
        string storedCausationId,
        string storedCommandType)
    {
        var record = new IdempotencyRecord(
            storedCausationId,
            "corr-456",
            true,
            null,
            DateTimeOffset.UtcNow,
            MessageId: storedMessageId,
            CommandType: storedCommandType,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            Disposition: IdempotencyRecordDisposition.Terminal);
        _ = _stateManager.TryGetStateAsync<IdempotencyRecord>("idempotency:message-123", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));
        IdempotencyChecker checker = CreateChecker();

        IdempotencyCheckResult result = await checker.CheckAsync(Identity());

        result.Outcome.ShouldBe(IdempotencyCheckOutcome.IdentityConflict);
        result.Result.ShouldBeNull();
        await _stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyRecord>(),
            Arg.Any<CancellationToken>());
        _ = await _stateManager.DidNotReceive().TryRemoveStateAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_ExactLegacyKeyRecord_StagesAtomicMigration()
    {
        CommandProcessingIdentity identity = Identity();
        var record = new IdempotencyRecord(
            identity.CausationId,
            "corr-456",
            true,
            null,
            DateTimeOffset.UtcNow,
            MessageId: identity.MessageId,
            CommandType: identity.CommandType,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            Disposition: IdempotencyRecordDisposition.Terminal);
        _ = _stateManager.TryGetStateAsync<IdempotencyRecord>("idempotency:message-123", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        _ = _stateManager.TryGetStateAsync<IdempotencyRecord>("idempotency:cause-123", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));
        IdempotencyChecker checker = CreateChecker();

        IdempotencyCheckResult result = await checker.CheckAsync(identity);

        result.Outcome.ShouldBe(IdempotencyCheckOutcome.LegacyMigration);
        _ = result.Result.ShouldNotBeNull();
        await _stateManager.Received(1).SetStateAsync(
            "idempotency:message-123",
            record,
            Arg.Any<CancellationToken>());
        _ = await _stateManager.Received(1).TryRemoveStateAsync(
            "idempotency:cause-123",
            Arg.Any<CancellationToken>());
        await _stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_UnverifiableLegacyRecord_ReturnsConflictAndPreservesRecord()
    {
        var legacyRecord = new IdempotencyRecord(
            "cause-123",
            "corr-456",
            true,
            null,
            DateTimeOffset.UtcNow);
        _ = _stateManager.TryGetStateAsync<IdempotencyRecord>("idempotency:message-123", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        _ = _stateManager.TryGetStateAsync<IdempotencyRecord>("idempotency:cause-123", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, legacyRecord));
        IdempotencyChecker checker = CreateChecker();

        IdempotencyCheckResult result = await checker.CheckAsync(Identity());

        result.Outcome.ShouldBe(IdempotencyCheckOutcome.IdentityConflict);
        await _stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyRecord>(),
            Arg.Any<CancellationToken>());
        _ = await _stateManager.DidNotReceive().TryRemoveStateAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_ExpiredExactRecord_PreservesConsumedEvidenceAndReturnsExpired()
    {
        var now = new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        CommandProcessingIdentity identity = Identity();
        var record = new IdempotencyRecord(
            identity.CausationId,
            "corr-456",
            true,
            null,
            now.AddHours(-24),
            MessageId: identity.MessageId,
            CommandType: identity.CommandType,
            ExpiresAt: now.AddSeconds(-1),
            Disposition: IdempotencyRecordDisposition.Terminal);
        _ = _stateManager.TryGetStateAsync<IdempotencyRecord>("idempotency:message-123", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));
        var checker = new IdempotencyChecker(_stateManager, _logger, timeProvider);

        IdempotencyCheckResult result = await checker.CheckAsync(identity);

        result.Outcome.ShouldBe(IdempotencyCheckOutcome.Expired);
        result.StateMutationStaged.ShouldBeFalse();
        result.Result.ShouldBeNull();
        _ = await _stateManager.DidNotReceive().TryRemoveStateAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_WhenMessageAndCausationMatch_PerformsOnlyMessageLookup()
    {
        _ = _stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        IdempotencyChecker checker = CreateChecker();
        CommandProcessingIdentity identity = Identity(causationId: "message-123");

        IdempotencyCheckResult result = await checker.CheckAsync(identity);

        result.Outcome.ShouldBe(IdempotencyCheckOutcome.Miss);
        _ = await _stateManager.Received(1).TryGetStateAsync<IdempotencyRecord>(
            "idempotency:message-123",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InspectAsync_ExactTerminalRecord_IsReadOnlyAndReturnsAuthoritativeResult()
    {
        CommandProcessingIdentity identity = Identity(causationId: "message-123");
        var record = new IdempotencyRecord(
            identity.CausationId,
            "corr-original",
            true,
            null,
            DateTimeOffset.UtcNow,
            EventCount: 2,
            ResultPayload: "{\"status\":\"done\"}",
            MessageId: identity.MessageId,
            CommandType: identity.CommandType,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            Disposition: IdempotencyRecordDisposition.Terminal);
        _ = _stateManager.TryGetStateAsync<IdempotencyRecord>(
                "idempotency:message-123",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));
        IdempotencyChecker checker = CreateChecker();

        IdempotencyCheckResult result = await checker.InspectAsync(identity);

        result.Outcome.ShouldBe(IdempotencyCheckOutcome.ExactTerminalDuplicate);
        result.Result.ShouldNotBeNull().ShouldBe(record.ToResult());
        await _stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyRecord>(),
            Arg.Any<CancellationToken>());
        _ = await _stateManager.DidNotReceive().TryRemoveStateAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await _stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_StoresCompleteIdentityAtMessageKey()
    {
        IdempotencyChecker checker = CreateChecker();
        CommandProcessingIdentity identity = Identity();
        var processingResult = new CommandProcessingResult(
            Accepted: true,
            CorrelationId: "corr-789",
            EventCount: 3,
            ResultPayload: """{"events":3}""");
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddHours(24);

        await checker.RecordAsync(
            identity,
            processingResult,
            expiresAt,
            IdempotencyRecordDisposition.Terminal);

        await _stateManager.Received(1).SetStateAsync(
            "idempotency:message-123",
            Arg.Is<IdempotencyRecord>(r =>
                r.MessageId == identity.MessageId &&
                r.CausationId == identity.CausationId &&
                r.CommandType == identity.CommandType &&
                r.CorrelationId == "corr-789" &&
                r.Accepted &&
                r.EventCount == 3 &&
                r.ResultPayload == """{"events":3}""" &&
                r.ExpiresAt == expiresAt &&
                r.Disposition == IdempotencyRecordDisposition.Terminal),
            Arg.Any<CancellationToken>());
        await _stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_NullIdentity_ThrowsArgumentNullException()
    {
        IdempotencyChecker checker = CreateChecker();

        _ = await Should.ThrowAsync<ArgumentNullException>(() => checker.CheckAsync(null!));
    }

    [Fact]
    public async Task RecordAsync_NullIdentity_ThrowsArgumentNullException()
    {
        IdempotencyChecker checker = CreateChecker();
        var result = new CommandProcessingResult(Accepted: true);

        _ = await Should.ThrowAsync<ArgumentNullException>(() => checker.RecordAsync(
            null!,
            result,
            DateTimeOffset.UtcNow.AddHours(1),
            IdempotencyRecordDisposition.Terminal));
    }

    [Fact]
    public async Task RecordAsync_NullResult_ThrowsArgumentNullException()
    {
        IdempotencyChecker checker = CreateChecker();

        _ = await Should.ThrowAsync<ArgumentNullException>(() => checker.RecordAsync(
            Identity(),
            null!,
            DateTimeOffset.UtcNow.AddHours(1),
            IdempotencyRecordDisposition.Terminal));
    }
}

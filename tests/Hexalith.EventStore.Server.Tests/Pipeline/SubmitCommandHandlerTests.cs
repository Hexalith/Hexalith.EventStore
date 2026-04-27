
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline;

public class SubmitCommandHandlerTests {
    private static IBackpressureTracker CreateMockTracker() {
        IBackpressureTracker tracker = Substitute.For<IBackpressureTracker>();
        _ = tracker.TryAcquire(Arg.Any<string>()).Returns(true);
        return tracker;
    }

    private static ICommandRouter CreateMockRouter() {
        ICommandRouter router = Substitute.For<ICommandRouter>();
        _ = router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(true));
        return router;
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsCorrelationId() {
        // Arrange
        string expectedCorrelationId = Guid.NewGuid().ToString();
        var statusStore = new InMemoryCommandStatusStore();
        var archiveStore = new InMemoryCommandArchiveStore();
        var handler = new SubmitCommandHandler(statusStore, archiveStore, CreateMockRouter(), CreateMockTracker(), NullLogger<SubmitCommandHandler>.Instance);
        var command = new SubmitCommand(
            MessageId: "msg-1",
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: expectedCorrelationId,
            UserId: "test-user");

        // Act
        SubmitCommandResult result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.CorrelationId.ShouldBe(expectedCorrelationId);
    }

    /// <summary>
    /// Structural pin: <see cref="SubmitCommandHandler.Handle"/> with the 5-arg constructor
    /// (status store + archive store + router + backpressure tracker + logger) and unconfigured
    /// <c>NSubstitute</c> mocks for the two state stores must complete without a
    /// <see cref="NullReferenceException"/> and return a <see cref="SubmitCommandResult"/>
    /// carrying the *command's* correlation identifier (per
    /// <see cref="SubmitCommandHandler"/> contract: the result wraps <c>request.CorrelationId</c>,
    /// not the router result's <c>CorrelationId</c>). Distinct sentinel values for command
    /// and router pin which side wins the round-trip.
    ///
    /// Why this test exists (post-Epic-2 R2-A8 closure-as-superseded, 2026-04-27):
    /// The Epic 2 retrospective documented 4 stable Tier 2 failures
    /// (<c>PayloadProtectionTests.SubmitCommandHandler_NeverLogsPayloadData</c>,
    /// <c>CausationIdLoggingTests.SubmitCommandHandler_IncludesCausationId</c>,
    /// <c>LogLevelConventionTests.CommandReceived_UsesInformationLevel</c>,
    /// <c>StructuredLoggingCompletenessTests.CommandReceived_LogContainsAllRequiredFieldsAsync</c>)
    /// that crashed with <see cref="NullReferenceException"/> in this exact construction shape.
    /// On HEAD <c>f803e9a</c> the failures no longer reproduce; R2-A8 was closed as
    /// superseded (see <c>_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md</c> §6).
    /// This test is intentionally NOT a regression test for the historical bug — there is no
    /// reproducing failure to anchor against. Instead it is a forward-looking structural pin
    /// for Epic 3 Story 3.1 (Command Submission Endpoint), which exercises the same handler
    /// with the same fixture shape downstream of <c>POST /api/v1/commands</c>. Pinning the
    /// all-mocks-no-trackers contract here is cheaper than re-discovering it during 3.1's
    /// review. The 4 logging tests cited above assert orthogonal contracts (payload redaction,
    /// causation-id presence, log-level convention, structured-field completeness); they cannot
    /// stand in as the load-bearing guard for the null-path class on their own.
    /// </summary>
    [Fact]
    public async Task Handle_ValidCommand_DoesNotThrowNullReferenceWhenStoresAreNSubstituteMocks() {
        // Arrange — 5-arg ctor with unconfigured NSubstitute mocks for both state stores.
        // Unconfigured Task-returning mock methods auto-stub to Task.CompletedTask; for
        // Task<T?>-returning methods (ReadStatusAsync) they auto-stub to Task.FromResult(null).
        // The handler's null-conditional chains and try/catch wrappers must absorb both shapes.
        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        ICommandArchiveStore archiveStore = Substitute.For<ICommandArchiveStore>();
        ICommandRouter router = Substitute.For<ICommandRouter>();

        // Distinct sentinels make the round-trip unambiguous. SubmitCommandHandler.Handle
        // wraps request.CorrelationId in the result (handler.cs L56), so a future refactor
        // that accidentally surfaces the router's id instead would fail this assertion.
        const string CommandCorrelationId = "corr-from-cmd-1";
        const string RouterCorrelationId = "corr-from-router-1";

        _ = router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(Accepted: true, CorrelationId: RouterCorrelationId));

        // 5-arg ctor at SubmitCommandHandler.cs:32-38 validates IBackpressureTracker non-null
        // then forwards (null, null) for both activity-trackers. The handler never invokes any
        // method on the tracker — leaving it unconfigured documents that the stub is structural
        // (non-null requirement only), not behavioural.
        IBackpressureTracker tracker = Substitute.For<IBackpressureTracker>();

        var handler = new SubmitCommandHandler(statusStore, archiveStore, router, tracker, NullLogger<SubmitCommandHandler>.Instance);
        var command = new SubmitCommand(
            MessageId: "msg-pin-1",
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [0x01],
            CorrelationId: CommandCorrelationId,
            UserId: "test-user",
            Extensions: null);

        // Act — a NullReferenceException would bubble up and fail the test, which is the
        // contract this pin enforces.
        SubmitCommandResult result = await handler.Handle(command, CancellationToken.None);

        // Assert — round-trip the *command's* correlation id (not the router's).
        result.ShouldNotBeNull();
        result.CorrelationId.ShouldBe(CommandCorrelationId);

        // Pin the success-path store-call shape so a future refactor that drops a write
        // surfaces here rather than degrading the pin to vacuous.
        await statusStore.Received(1).WriteStatusAsync(
            command.Tenant,
            command.CorrelationId,
            Arg.Any<CommandStatusRecord>(),
            Arg.Any<CancellationToken>());
        await archiveStore.Received(1).WriteCommandAsync(
            command.Tenant,
            command.CorrelationId,
            Arg.Any<ArchivedCommand>(),
            Arg.Any<CancellationToken>());
        _ = await router.Received(1).RouteCommandAsync(command, Arg.Any<CancellationToken>());

        // Both activity-trackers are null in the 5-arg ctor path — the tracker-gated read-back
        // at SubmitCommandHandler.cs:97-110 must NOT fire here. Locking DidNotReceive pins the
        // no-trackers shape: a future change that adds a non-null default tracker would surface
        // as a test failure rather than a silent behaviour shift.
        _ = await statusStore.DidNotReceive().ReadStatusAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

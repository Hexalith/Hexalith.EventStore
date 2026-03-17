using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline;

public class SubmitCommandHandlerBackpressureTests {
    private static SubmitCommand CreateTestCommand(string? correlationId = null) => new(
        MessageId: Guid.NewGuid().ToString(),
        Tenant: "test-tenant",
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        UserId: "test-user");

    private static ICommandRouter CreateMockRouter() {
        ICommandRouter router = Substitute.For<ICommandRouter>();
        _ = router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(true));
        return router;
    }

    private static string GetExpectedActorId(SubmitCommand command) =>
        new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId).ActorId;

    [Fact]
    public async Task Handle_BackpressureExceeded_ThrowsBackpressureExceededException() {
        // Arrange
        IBackpressureTracker tracker = Substitute.For<IBackpressureTracker>();
        _ = tracker.TryAcquire(Arg.Any<string>()).Returns(false);

        var handler = new SubmitCommandHandler(
            new InMemoryCommandStatusStore(),
            new InMemoryCommandArchiveStore(),
            CreateMockRouter(),
            tracker,
            NullLogger<SubmitCommandHandler>.Instance);

        SubmitCommand command = CreateTestCommand();

        // Act & Assert
        _ = await Should.ThrowAsync<BackpressureExceededException>(
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_BackpressureExceeded_DoesNotCallCommandRouter() {
        // Arrange
        IBackpressureTracker tracker = Substitute.For<IBackpressureTracker>();
        _ = tracker.TryAcquire(Arg.Any<string>()).Returns(false);
        ICommandRouter router = Substitute.For<ICommandRouter>();

        var handler = new SubmitCommandHandler(
            new InMemoryCommandStatusStore(),
            new InMemoryCommandArchiveStore(),
            router,
            tracker,
            NullLogger<SubmitCommandHandler>.Instance);

        SubmitCommand command = CreateTestCommand();

        // Act
        _ = await Should.ThrowAsync<BackpressureExceededException>(
            () => handler.Handle(command, CancellationToken.None));

        // Assert — router should never be called
        _ = await router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BackpressureExceeded_DoesNotWriteStatus() {
        // Arrange
        IBackpressureTracker tracker = Substitute.For<IBackpressureTracker>();
        _ = tracker.TryAcquire(Arg.Any<string>()).Returns(false);
        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();

        var handler = new SubmitCommandHandler(
            statusStore,
            new InMemoryCommandArchiveStore(),
            CreateMockRouter(),
            tracker,
            NullLogger<SubmitCommandHandler>.Instance);

        SubmitCommand command = CreateTestCommand();

        // Act
        _ = await Should.ThrowAsync<BackpressureExceededException>(
            () => handler.Handle(command, CancellationToken.None));

        // Assert — status store should never be called (AC #7)
        await statusStore.DidNotReceive().WriteStatusAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommandStatusRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BackpressureExceeded_DoesNotArchiveCommand() {
        // Arrange
        IBackpressureTracker tracker = Substitute.For<IBackpressureTracker>();
        _ = tracker.TryAcquire(Arg.Any<string>()).Returns(false);
        ICommandArchiveStore archiveStore = Substitute.For<ICommandArchiveStore>();

        var handler = new SubmitCommandHandler(
            new InMemoryCommandStatusStore(),
            archiveStore,
            CreateMockRouter(),
            tracker,
            NullLogger<SubmitCommandHandler>.Instance);

        SubmitCommand command = CreateTestCommand();

        // Act
        _ = await Should.ThrowAsync<BackpressureExceededException>(
            () => handler.Handle(command, CancellationToken.None));

        // Assert — archive should never be called
        await archiveStore.DidNotReceive().WriteCommandAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ArchivedCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_ReleasesBackpressure() {
        // Arrange
        IBackpressureTracker tracker = Substitute.For<IBackpressureTracker>();
        _ = tracker.TryAcquire(Arg.Any<string>()).Returns(true);

        var handler = new SubmitCommandHandler(
            new InMemoryCommandStatusStore(),
            new InMemoryCommandArchiveStore(),
            CreateMockRouter(),
            tracker,
            NullLogger<SubmitCommandHandler>.Instance);

        SubmitCommand command = CreateTestCommand();
        string expectedActorId = GetExpectedActorId(command);

        // Act
        _ = await handler.Handle(command, CancellationToken.None);

        // Assert — Acquire and release should use same actor key
        tracker.Received(1).TryAcquire(expectedActorId);
        tracker.Received(1).Release(expectedActorId);
    }

    [Fact]
    public async Task Handle_RouterThrows_ReleasesBackpressure() {
        // Arrange
        IBackpressureTracker tracker = Substitute.For<IBackpressureTracker>();
        _ = tracker.TryAcquire(Arg.Any<string>()).Returns(true);
        ICommandRouter router = Substitute.For<ICommandRouter>();
        _ = router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Actor failed"));

        var handler = new SubmitCommandHandler(
            new InMemoryCommandStatusStore(),
            new InMemoryCommandArchiveStore(),
            router,
            tracker,
            NullLogger<SubmitCommandHandler>.Instance);

        SubmitCommand command = CreateTestCommand();
        string expectedActorId = GetExpectedActorId(command);

        // Act
        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => handler.Handle(command, CancellationToken.None));

        // Assert — Release should still be called even on failure with exact actor key
        tracker.Received(1).TryAcquire(expectedActorId);
        tracker.Received(1).Release(expectedActorId);
    }

    [Fact]
    public async Task Handle_UnderThreshold_CallsRouter() {
        // Arrange
        IBackpressureTracker tracker = Substitute.For<IBackpressureTracker>();
        _ = tracker.TryAcquire(Arg.Any<string>()).Returns(true);
        ICommandRouter router = CreateMockRouter();

        var handler = new SubmitCommandHandler(
            new InMemoryCommandStatusStore(),
            new InMemoryCommandArchiveStore(),
            router,
            tracker,
            NullLogger<SubmitCommandHandler>.Instance);

        SubmitCommand command = CreateTestCommand();

        // Act
        _ = await handler.Handle(command, CancellationToken.None);

        // Assert — normal flow proceeds
        _ = await router.Received(1).RouteCommandAsync(
            Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Cancelled_ReleasesBackpressure() {
        // Arrange
        IBackpressureTracker tracker = Substitute.For<IBackpressureTracker>();
        _ = tracker.TryAcquire(Arg.Any<string>()).Returns(true);
        ICommandRouter router = Substitute.For<ICommandRouter>();
        _ = router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("Cancelled"));

        var handler = new SubmitCommandHandler(
            new InMemoryCommandStatusStore(),
            new InMemoryCommandArchiveStore(),
            router,
            tracker,
            NullLogger<SubmitCommandHandler>.Instance);

        SubmitCommand command = CreateTestCommand();
        string expectedActorId = GetExpectedActorId(command);

        // Act
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => handler.Handle(command, CancellationToken.None));

        // Assert — Release should be called even on cancellation with exact actor key
        tracker.Received(1).TryAcquire(expectedActorId);
        tracker.Received(1).Release(expectedActorId);
    }
}


using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class SubmitCommandHandlerStatusTests {
    private static ICommandRouter CreateMockRouter() {
        ICommandRouter router = Substitute.For<ICommandRouter>();
        _ = router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(true));
        return router;
    }

    private static SubmitCommand CreateTestCommand(string? correlationId = null) => new(
        MessageId: Guid.NewGuid().ToString(),
        Tenant: "test-tenant",
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        UserId: "test-user");

    [Fact]
    public async Task Handle_ValidCommand_WritesReceivedStatusToStore() {
        // Arrange
        var statusStore = new InMemoryCommandStatusStore();
        var archiveStore = new InMemoryCommandArchiveStore();
        var handler = new SubmitCommandHandler(statusStore, archiveStore, CreateMockRouter(), NullLogger<SubmitCommandHandler>.Instance);
        SubmitCommand command = CreateTestCommand();

        // Act
        _ = await handler.Handle(command, CancellationToken.None);

        // Assert
        CommandStatusRecord? status = await statusStore.ReadStatusAsync(
            command.Tenant, command.CorrelationId, CancellationToken.None);

        _ = status.ShouldNotBeNull();
        status.Status.ShouldBe(CommandStatus.Received);
        status.AggregateId.ShouldBe(command.AggregateId);
    }

    [Fact]
    public async Task Handle_StatusWriteFails_StillReturnsResult() {
        // Arrange
        ICommandStatusStore mockStore = Substitute.For<ICommandStatusStore>();
        _ = mockStore.WriteStatusAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CommandStatusRecord>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Store unavailable"));

        var archiveStore = new InMemoryCommandArchiveStore();
        var handler = new SubmitCommandHandler(mockStore, archiveStore, CreateMockRouter(), NullLogger<SubmitCommandHandler>.Instance);
        SubmitCommand command = CreateTestCommand();

        // Act
        SubmitCommandResult result = await handler.Handle(command, CancellationToken.None);

        // Assert - handler should still return result (rule #12)
        _ = result.ShouldNotBeNull();
        result.CorrelationId.ShouldBe(command.CorrelationId);
    }

    [Fact]
    public async Task Handle_StatusWriteFails_LogsWarning() {
        // Arrange
        ICommandStatusStore mockStore = Substitute.For<ICommandStatusStore>();
        _ = mockStore.WriteStatusAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CommandStatusRecord>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Store unavailable"));

        var archiveStore = new InMemoryCommandArchiveStore();
        ILogger<SubmitCommandHandler> logger = Substitute.For<ILogger<SubmitCommandHandler>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var handler = new SubmitCommandHandler(mockStore, archiveStore, CreateMockRouter(), logger);
        SubmitCommand command = CreateTestCommand();

        // Act
        _ = await handler.Handle(command, CancellationToken.None);

        // Assert - warning should have been logged
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex is InvalidOperationException),
            Arg.Any<Func<object, Exception?, string>>());
    }
}

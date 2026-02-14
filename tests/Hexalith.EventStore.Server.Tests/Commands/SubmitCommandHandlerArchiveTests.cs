namespace Hexalith.EventStore.Server.Tests.Commands;

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

public class SubmitCommandHandlerArchiveTests
{
    private static ICommandRouter CreateMockRouter()
    {
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(true));
        return router;
    }

    private static SubmitCommand CreateTestCommand(string? correlationId = null) => new(
        Tenant: "test-tenant",
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        UserId: "test-user",
        Extensions: new Dictionary<string, string> { ["key1"] = "val1" });

    [Fact]
    public async Task Handle_ValidCommand_WritesArchivedCommandToStore()
    {
        // Arrange
        var statusStore = new InMemoryCommandStatusStore();
        var archiveStore = new InMemoryCommandArchiveStore();
        var handler = new SubmitCommandHandler(statusStore, archiveStore, CreateMockRouter(), NullLogger<SubmitCommandHandler>.Instance);
        SubmitCommand command = CreateTestCommand();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        ArchivedCommand? archived = await archiveStore.ReadCommandAsync(
            command.Tenant, command.CorrelationId, CancellationToken.None);

        archived.ShouldNotBeNull();
        archived.Tenant.ShouldBe(command.Tenant);
        archived.CommandType.ShouldBe(command.CommandType);
    }

    [Fact]
    public async Task Handle_ArchiveWriteFails_StillReturnsResult()
    {
        // Arrange
        var statusStore = new InMemoryCommandStatusStore();
        var mockArchiveStore = Substitute.For<ICommandArchiveStore>();
        mockArchiveStore.WriteCommandAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ArchivedCommand>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Archive store unavailable"));

        var handler = new SubmitCommandHandler(statusStore, mockArchiveStore, CreateMockRouter(), NullLogger<SubmitCommandHandler>.Instance);
        SubmitCommand command = CreateTestCommand();

        // Act
        SubmitCommandResult result = await handler.Handle(command, CancellationToken.None);

        // Assert - handler should still return result (rule #12)
        result.ShouldNotBeNull();
        result.CorrelationId.ShouldBe(command.CorrelationId);
    }

    [Fact]
    public async Task Handle_ArchiveWriteFails_LogsWarning()
    {
        // Arrange
        var statusStore = new InMemoryCommandStatusStore();
        var mockArchiveStore = Substitute.For<ICommandArchiveStore>();
        mockArchiveStore.WriteCommandAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ArchivedCommand>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Archive store unavailable"));

        var logger = Substitute.For<ILogger<SubmitCommandHandler>>();
        var handler = new SubmitCommandHandler(statusStore, mockArchiveStore, CreateMockRouter(), logger);
        SubmitCommand command = CreateTestCommand();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert - warning should have been logged
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex is InvalidOperationException),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_ArchivedCommand_ContainsAllOriginalFields()
    {
        // Arrange
        var statusStore = new InMemoryCommandStatusStore();
        var archiveStore = new InMemoryCommandArchiveStore();
        var handler = new SubmitCommandHandler(statusStore, archiveStore, CreateMockRouter(), NullLogger<SubmitCommandHandler>.Instance);
        SubmitCommand command = CreateTestCommand();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        ArchivedCommand? archived = await archiveStore.ReadCommandAsync(
            command.Tenant, command.CorrelationId, CancellationToken.None);

        archived.ShouldNotBeNull();
        archived.Tenant.ShouldBe(command.Tenant);
        archived.Domain.ShouldBe(command.Domain);
        archived.AggregateId.ShouldBe(command.AggregateId);
        archived.CommandType.ShouldBe(command.CommandType);
        archived.Payload.ShouldBe(command.Payload);
        archived.Extensions.ShouldNotBeNull();
        archived.Extensions["key1"].ShouldBe("val1");
    }
}

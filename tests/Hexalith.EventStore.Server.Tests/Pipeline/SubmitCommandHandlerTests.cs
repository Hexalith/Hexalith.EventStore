namespace Hexalith.EventStore.Server.Tests.Pipeline;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

public class SubmitCommandHandlerTests
{
    private static ICommandRouter CreateMockRouter()
    {
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(true));
        return router;
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsCorrelationId()
    {
        // Arrange
        string expectedCorrelationId = Guid.NewGuid().ToString();
        var statusStore = new InMemoryCommandStatusStore();
        var archiveStore = new InMemoryCommandArchiveStore();
        var handler = new SubmitCommandHandler(statusStore, archiveStore, CreateMockRouter(), NullLogger<SubmitCommandHandler>.Instance);
        var command = new SubmitCommand(
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
}

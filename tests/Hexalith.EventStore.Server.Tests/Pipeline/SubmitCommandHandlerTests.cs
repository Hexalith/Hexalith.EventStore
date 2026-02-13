namespace Hexalith.EventStore.Server.Tests.Pipeline;

using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

public class SubmitCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_ReturnsCorrelationId()
    {
        // Arrange
        string expectedCorrelationId = Guid.NewGuid().ToString();
        var statusStore = new InMemoryCommandStatusStore();
        var archiveStore = new InMemoryCommandArchiveStore();
        var handler = new SubmitCommandHandler(statusStore, archiveStore, NullLogger<SubmitCommandHandler>.Instance);
        var command = new SubmitCommand(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: expectedCorrelationId);

        // Act
        SubmitCommandResult result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.CorrelationId.ShouldBe(expectedCorrelationId);
    }
}

namespace Hexalith.EventStore.Server.Tests.Pipeline;

using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

public class SubmitCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_ReturnsCorrelationId()
    {
        // Arrange
        string expectedCorrelationId = Guid.NewGuid().ToString();
        var handler = new SubmitCommandHandler(NullLogger<SubmitCommandHandler>.Instance);
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

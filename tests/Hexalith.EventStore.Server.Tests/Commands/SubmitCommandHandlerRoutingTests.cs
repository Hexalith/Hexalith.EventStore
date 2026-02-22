namespace Hexalith.EventStore.Server.Tests.Commands;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

public class SubmitCommandHandlerRoutingTests {
    private static SubmitCommand CreateTestCommand(string? correlationId = null) => new(
        Tenant: "test-tenant",
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        UserId: "test-user");

    [Fact]
    public async Task Handle_ValidCommand_RoutesToActor() {
        // Arrange
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(true));

        var handler = new SubmitCommandHandler(
            new InMemoryCommandStatusStore(),
            new InMemoryCommandArchiveStore(),
            router,
            NullLogger<SubmitCommandHandler>.Instance);

        SubmitCommand command = CreateTestCommand();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        await router.Received(1).RouteCommandAsync(
            Arg.Is<SubmitCommand>(c => c.CorrelationId == command.CorrelationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RouterThrows_PropagatesException() {
        // Arrange
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Actor failed"));

        var handler = new SubmitCommandHandler(
            new InMemoryCommandStatusStore(),
            new InMemoryCommandArchiveStore(),
            router,
            NullLogger<SubmitCommandHandler>.Instance);

        SubmitCommand command = CreateTestCommand();

        // Act & Assert - exception should NOT be swallowed (unlike status/archive writes)
        await Should.ThrowAsync<InvalidOperationException>(
            () => handler.Handle(command, CancellationToken.None));
    }
}

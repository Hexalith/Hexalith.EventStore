using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public sealed class ConcurrencyConflictStatusPersistenceE2ETests(
    AspireContractTestFixture fixture) {
    [Fact]
    public async Task ConflictHandlerPersistsRejectedStatusThroughLiveDaprStateAsync() {
        string tenantId = $"conflict-proof-{Guid.NewGuid():N}";
        string messageId = Guid.NewGuid().ToString("N");
        string correlationId = Guid.NewGuid().ToString("N");
        var options = new CommandStatusOptions();
        string stateKey = CommandStatusConstants.BuildKey(tenantId, messageId);
        using DaprClient writeClient = new DaprClientBuilder()
            .UseHttpEndpoint(fixture.EventStoreDaprHttpEndpoint.ToString())
            .UseGrpcEndpoint(fixture.EventStoreDaprGrpcEndpoint.ToString())
            .Build();
        using DaprClient readClient = new DaprClientBuilder()
            .UseHttpEndpoint(fixture.EventStoreDaprHttpEndpoint.ToString())
            .UseGrpcEndpoint(fixture.EventStoreDaprGrpcEndpoint.ToString())
            .Build();
        var statusStore = new DaprCommandStatusStore(
            writeClient,
            Options.Create(options),
            NullLogger<DaprCommandStatusStore>.Instance);
        var handler = new ConcurrencyConflictExceptionHandler(
            statusStore,
            NullLogger<ConcurrencyConflictExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/commands";
        context.Response.Body = new MemoryStream();
        var conflict = new ConcurrencyConflictException(
            correlationId,
            "aggregate-live-dapr",
            tenantId,
            messageId: messageId);

        try {
            bool handled = await handler
                .TryHandleAsync(context, conflict, CancellationToken.None)
                .ConfigureAwait(true);

            handled.ShouldBeTrue();
            context.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
            CommandStatusRecord? persisted = await readClient
                .GetStateAsync<CommandStatusRecord>(options.StateStoreName, stateKey)
                .ConfigureAwait(true);
            _ = persisted.ShouldNotBeNull();
            persisted.Status.ShouldBe(CommandStatus.Rejected);
            persisted.FailureReason.ShouldBe("ConcurrencyConflict");
            persisted.MessageId.ShouldBe(messageId);
            persisted.CorrelationId.ShouldBe(correlationId);
        }
        finally {
            await readClient
                .DeleteStateAsync(options.StateStoreName, stateKey)
                .ConfigureAwait(true);
        }
    }
}

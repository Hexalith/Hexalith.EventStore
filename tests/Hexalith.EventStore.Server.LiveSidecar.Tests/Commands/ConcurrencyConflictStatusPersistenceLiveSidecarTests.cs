using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Commands;

/// <summary>
/// Verifies that HTTP conflict handling persists its terminal status through the production DAPR store.
/// </summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public class ConcurrencyConflictStatusPersistenceLiveSidecarTests {
    private readonly DaprTestContainerFixture _fixture;

    public ConcurrencyConflictStatusPersistenceLiveSidecarTests(DaprTestContainerFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task TryHandleAsync_ConcurrencyConflict_PersistsRejectedStatusThroughDaprAsync() {
        string tenantId = "tenant-a";
        string messageId = $"conflict-status-{Guid.NewGuid():N}";
        string aggregateId = $"counter-{Guid.NewGuid():N}";
        DaprClient daprClient = _fixture.Services.GetRequiredService<DaprClient>();
        var options = Options.Create(new CommandStatusOptions());
        var statusStore = new DaprCommandStatusStore(
            daprClient,
            options,
            NullLogger<DaprCommandStatusStore>.Instance);
        var handler = new ConcurrencyConflictExceptionHandler(
            statusStore,
            NullLogger<ConcurrencyConflictExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/commands";
        context.Response.Body = new MemoryStream();
        var conflict = new ConcurrencyConflictException(
            correlationId: Guid.NewGuid().ToString("N"),
            aggregateId,
            tenantId,
            messageId: messageId);

        try {
            bool handled = await handler
                .TryHandleAsync(context, conflict, CancellationToken.None)
                .ConfigureAwait(true);

            handled.ShouldBeTrue();
            context.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
            CommandStatusRecord? persisted = await statusStore
                .ReadStatusAsync(tenantId, messageId)
                .ConfigureAwait(true);
            _ = persisted.ShouldNotBeNull();
            persisted.Status.ShouldBe(CommandStatus.Rejected);
            persisted.FailureReason.ShouldBe("ConcurrencyConflict");
            persisted.MessageId.ShouldBe(messageId);
            persisted.AggregateId.ShouldBe(aggregateId);
        }
        finally {
            await daprClient
                .DeleteStateAsync(
                    options.Value.StateStoreName,
                    CommandStatusConstants.BuildKey(tenantId, messageId))
                .ConfigureAwait(true);
        }
    }
}

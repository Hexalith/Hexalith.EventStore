
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorTenantValidationTests {
    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_ReturnsRejection() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant");

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("TenantMismatch");
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_DoesNotExecuteSteps3Through5() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant");

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- Step 3 logs "State rehydrated" at Information level; should NOT appear after tenant mismatch
        ctx.Logger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_StoresRejectionInIdempotencyCache() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant", causationId: "cause-mismatch");

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        await ctx.StateManager.Received(1).SetStateAsync(
            "idempotency:cause-mismatch",
            Arg.Is<IdempotencyRecord>(r => r.Accepted == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_CallsSaveStateAsync() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant");

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        await ctx.StateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_MatchingTenant_ProceedsToStep3() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope(); // test-tenant matches actor ID test-tenant:test-domain:agg-001

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- Step 3 should have logged state rehydration at Information level
        ctx.Logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_RejectionContainsBothTenants() {
        // Arrange (F-SA6)
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "tenant-b");

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        _ = result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("tenant-b");
        result.ErrorMessage.ShouldContain("test-tenant");
    }
}

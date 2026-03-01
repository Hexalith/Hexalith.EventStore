
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;
/// <summary>
/// Story 7.4 / AC #4: Tenant isolation tests.
/// Validates tenant isolation at the actor and storage level.
/// SEC-2: Tenant validation occurs BEFORE state rehydration.
/// </summary>
[Collection("DaprTestContainer")]
public class ActorTenantIsolationTests {
    private readonly DaprTestContainerFixture _fixture;

    public ActorTenantIsolationTests(DaprTestContainerFixture fixture) {
        _fixture = fixture;
        _fixture.SetupCounterDomain();
    }

    /// <summary>
    /// Task 4.1: Test tenant A commands never access tenant B state.
    /// Commands for different tenants use different actor IDs, ensuring state isolation.
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_DifferentTenants_HaveIsolatedState() {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"shared-counter-{Guid.NewGuid():N}";

        // Tenant A: send 3 increments
        IAggregateActor tenantAProxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId($"tenant-a:counter:{aggregateId}"),
            nameof(AggregateActor));

        for (int i = 0; i < 3; i++) {
            CommandEnvelope command = new CommandEnvelopeBuilder()
                .WithTenantId("tenant-a")
                .WithDomain("counter")
                .WithAggregateId(aggregateId)
                .WithCommandType("IncrementCounter")
                .Build();

            CommandProcessingResult result = await tenantAProxy.ProcessCommandAsync(command);
            result.Accepted.ShouldBeTrue();
        }

        // Tenant B: send 1 increment (should start fresh, not see tenant A's state)
        IAggregateActor tenantBProxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId($"tenant-b:counter:{aggregateId}"),
            nameof(AggregateActor));

        CommandEnvelope tenantBCommand = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-b")
            .WithDomain("counter")
            .WithAggregateId(aggregateId)
            .WithCommandType("IncrementCounter")
            .Build();

        CommandProcessingResult tenantBResult = await tenantBProxy.ProcessCommandAsync(tenantBCommand);

        // Assert - tenant B's command should succeed independently
        tenantBResult.Accepted.ShouldBeTrue();
        tenantBResult.EventCount.ShouldBe(1, "Tenant B should have its own isolated state");
    }

    /// <summary>
    /// Task 4.3: Test tenant validation executes BEFORE state rehydration (SEC-2).
    /// A command with wrong tenant sent to an actor should be rejected before any state access.
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_RejectedBeforeStateAccess() {
        // Arrange - create a command for tenant-b but route to tenant-a's actor
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        // Actor ID is for tenant-a, but command claims tenant-b
        CommandEnvelope command = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-b")
            .WithDomain("counter")
            .WithAggregateId("tenant-mismatch-test")
            .Build();

        // Route to tenant-a's actor (mismatch)
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId("tenant-a:counter:tenant-mismatch-test"),
            nameof(AggregateActor));

        // Act
        CommandProcessingResult result = await proxy.ProcessCommandAsync(command);

        // Assert - should be rejected due to tenant mismatch
        result.Accepted.ShouldBeFalse("Tenant mismatch should be rejected");
        _ = result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("tenant", Case.Insensitive);
    }

    /// <summary>
    /// Task 4.2: Test storage keys are structurally disjoint per tenant (D1 key pattern).
    /// Verifies that different tenants produce non-overlapping key prefixes,
    /// ensuring no tenant can access another tenant's event stream, metadata, or snapshot.
    /// </summary>
    [Fact]
    public void AggregateIdentity_DifferentTenants_ProduceDisjointKeyPrefixes() {
        // Arrange
        var identityA = new AggregateIdentity("tenant-a", "counter", "agg-001");
        var identityB = new AggregateIdentity("tenant-b", "counter", "agg-001");

        // Act
        string eventsA = identityA.EventStreamKeyPrefix;
        string eventsB = identityB.EventStreamKeyPrefix;
        string metaA = identityA.MetadataKey;
        string metaB = identityB.MetadataKey;
        string snapA = identityA.SnapshotKey;
        string snapB = identityB.SnapshotKey;

        // Assert - keys must not overlap (no prefix relationship)
        eventsA.ShouldNotStartWith(eventsB);
        eventsB.ShouldNotStartWith(eventsA);
        metaA.ShouldNotBe(metaB);
        snapA.ShouldNotBe(snapB);

        // Verify D1 key pattern structure
        eventsA.ShouldBe("tenant-a:counter:agg-001:events:");
        eventsB.ShouldBe("tenant-b:counter:agg-001:events:");
        metaA.ShouldBe("tenant-a:counter:agg-001:metadata");
        metaB.ShouldBe("tenant-b:counter:agg-001:metadata");
        snapA.ShouldBe("tenant-a:counter:agg-001:snapshot");
        snapB.ShouldBe("tenant-b:counter:agg-001:snapshot");

        // Verify actor IDs are also disjoint
        identityA.ActorId.ShouldNotBe(identityB.ActorId);
        identityA.ActorId.ShouldBe("tenant-a:counter:agg-001");
        identityB.ActorId.ShouldBe("tenant-b:counter:agg-001");
    }
}

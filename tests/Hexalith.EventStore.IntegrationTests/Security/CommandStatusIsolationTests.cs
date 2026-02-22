namespace Hexalith.EventStore.IntegrationTests.Security;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Testing.Assertions;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

/// <summary>
/// Component-level integration tests verifying command status key isolation between tenants (AC: #8).
/// JWT tenant matching (SEC-3) is enforced at the API layer (Story 2.6) and not re-tested here.
/// These tests verify the structural key isolation that prevents cross-tenant status leakage.
/// Uses in-memory fakes to exercise CommandStatusStore and CommandStatusConstants together.
/// Classified as integration tests because they validate cross-component behavior.
/// </summary>
public class CommandStatusIsolationTests {
    // --- 4.2: Command status for tenant-a is not retrievable with tenant-b's credentials ---

    [Fact]
    public async Task CommandStatus_ForTenantA_NotRetrievableBy_TenantB() {
        // Arrange - single InMemoryCommandStatusStore (simulates shared state store)
        var store = new InMemoryCommandStatusStore();
        string sharedCorrelationId = "shared-corr-001";
        var statusRecord = new CommandStatusRecord(
            CommandStatus.Received,
            DateTimeOffset.UtcNow,
            "agg-001",
            null,
            null,
            null,
            null);

        // Write status for tenant-a
        await store.WriteStatusAsync("tenant-a", sharedCorrelationId, statusRecord);

        // Act - tenant-b attempts to read with same correlationId
        CommandStatusRecord? resultFromB = await store.ReadStatusAsync("tenant-b", sharedCorrelationId);

        // Assert - tenant-b cannot see tenant-a's status (different key due to tenant prefix)
        resultFromB.ShouldBeNull();

        // But tenant-a can read its own status
        CommandStatusRecord? resultFromA = await store.ReadStatusAsync("tenant-a", sharedCorrelationId);
        resultFromA.ShouldNotBeNull();
        resultFromA.Status.ShouldBe(CommandStatus.Received);
    }

    // --- 4.3: Status keys for different tenants are structurally disjoint ---

    [Fact]
    public void StatusKey_DifferentTenants_SameCorrelationId_StructurallyDisjoint() {
        // Arrange
        const string correlationId = "corr-12345";

        // Act
        string keyA = CommandStatusConstants.BuildKey("tenant-a", correlationId);
        string keyB = CommandStatusConstants.BuildKey("tenant-b", correlationId);

        // Assert
        keyA.ShouldBe("tenant-a:corr-12345:status");
        keyB.ShouldBe("tenant-b:corr-12345:status");
        keyA.ShouldNotBe(keyB);

        // Verify tenant isolation via assertions
        StorageKeyIsolationAssertions.AssertKeyBelongsToTenant(keyA, "tenant-a");
        StorageKeyIsolationAssertions.AssertKeyBelongsToTenant(keyB, "tenant-b");
        StorageKeyIsolationAssertions.AssertKeysDisjoint(keyA, keyB);
    }

    [Fact]
    public async Task CommandStatus_MultipleTenantsWithSameCorrelationId_IndependentStorage() {
        // Arrange
        var store = new InMemoryCommandStatusStore();
        string correlationId = "corr-shared";

        var recordA = new CommandStatusRecord(
            CommandStatus.Received,
            DateTimeOffset.UtcNow,
            "agg-a",
            null,
            null,
            null,
            null);

        var recordB = new CommandStatusRecord(
            CommandStatus.Completed,
            DateTimeOffset.UtcNow,
            "agg-b",
            null,
            null,
            null,
            null);

        // Act - both tenants write status with same correlationId
        await store.WriteStatusAsync("tenant-a", correlationId, recordA);
        await store.WriteStatusAsync("tenant-b", correlationId, recordB);

        // Assert - each tenant gets its own status back
        CommandStatusRecord? resultA = await store.ReadStatusAsync("tenant-a", correlationId);
        CommandStatusRecord? resultB = await store.ReadStatusAsync("tenant-b", correlationId);

        resultA.ShouldNotBeNull();
        resultA.Status.ShouldBe(CommandStatus.Received);
        resultA.AggregateId.ShouldBe("agg-a");

        resultB.ShouldNotBeNull();
        resultB.Status.ShouldBe(CommandStatus.Completed);
        resultB.AggregateId.ShouldBe("agg-b");

        // Verify 2 distinct entries in the store
        store.GetStatusCount().ShouldBe(2);
    }
}

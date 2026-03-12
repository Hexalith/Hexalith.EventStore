
using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Security;
/// <summary>
/// Structural injection prevention tests verifying AggregateIdentity prevents namespace escape attacks,
/// and tenant mismatch causes zero state access.
/// (AC: #4, #8, #10, #11, #12)
/// </summary>
public class TenantInjectionPreventionTests {
    // --- Task 3.2: AC #10 ---

    [Fact]
    public void AggregateIdentity_ColonInTenantId_Throws() =>
        // Colon injection to escape key namespace
        Should.Throw<ArgumentException>(
            () => new AggregateIdentity("tenant-b:orders", "orders", "order-001"));

    // --- Task 3.3: AC #10 ---

    [Theory]
    [InlineData("tenant\0id")]       // null byte
    [InlineData("tenant\u0001id")]   // control char SOH
    [InlineData("tenant\u001Fid")]   // control char US
    public void AggregateIdentity_ControlCharsInTenantId_Throws(string tenantId) => Should.Throw<ArgumentException>(
            () => new AggregateIdentity(tenantId, "orders", "order-001"));

    // --- Task 3.4: AC #10 ---

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void AggregateIdentity_EmptyOrWhitespaceTenantId_Throws(string tenantId) => Should.Throw<ArgumentException>(
            () => new AggregateIdentity(tenantId, "orders", "order-001"));

    // --- Task 3.5: AC #4, #8 ---

    [Fact]
    public async Task TenantValidator_MismatchDetected_NoStateManagerAccess() {
        // Arrange -- actor with tenant-b, command with tenant-a
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant-b:orders:order-001") });
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), commandStatusStore, eventPublisher, Options.Create(new EventDrainOptions()), deadLetterPublisher);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        // No pipeline state, no duplicate
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        var command = new CommandEnvelope("tenant-a", "orders", "order-001", "CreateOrder", [1],
            Guid.NewGuid().ToString(), "cause-mismatch", "system", null);

        // Act
        _ = await actor.ProcessCommandAsync(command);

        // Assert -- StateManager should NOT have been called for metadata (Step 3) or event reads
        _ = await stateManager.DidNotReceive().TryGetStateAsync<AggregateMetadata>(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = await stateManager.DidNotReceive().TryGetStateAsync<EventEnvelope>(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- Task 3.6: AC #8 ---

    [Fact]
    public async Task TenantValidator_MismatchDetected_RejectionRecordedViaIdempotency() {
        // Arrange
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant-b:orders:order-001") });
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), commandStatusStore, eventPublisher, Options.Create(new EventDrainOptions()), deadLetterPublisher);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        var command = new CommandEnvelope("tenant-a", "orders", "order-001", "CreateOrder", [1],
            Guid.NewGuid().ToString(), "cause-reject", "system", null);

        // Act
        _ = await actor.ProcessCommandAsync(command);

        // Assert -- idempotency record stored with rejection
        await stateManager.Received(1).SetStateAsync(
            "idempotency:cause-reject",
            Arg.Is<IdempotencyRecord>(r => r.Accepted == false),
            Arg.Any<CancellationToken>());

        // SaveStateAsync called exactly once (for the rejection commit)
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    // --- Task 3.7: AC #4 ---

    [Fact]
    public async Task AggregateActor_TenantMismatch_ResultContainsTenantMismatchError() {
        // Arrange
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant-b:orders:order-001") });
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), commandStatusStore, eventPublisher, Options.Create(new EventDrainOptions()), deadLetterPublisher);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        var command = new CommandEnvelope("tenant-a", "orders", "order-001", "CreateOrder", [1],
            Guid.NewGuid().ToString(), "cause-1", "system", null);

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(command);

        // Assert
        result.Accepted.ShouldBeFalse();
        _ = result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("TenantMismatch");
        result.ErrorMessage.ShouldContain("tenant-a");
        result.ErrorMessage.ShouldContain("tenant-b");
    }

    // --- Task 3.8: AC #11, GAP-R1 ---

    [Theory]
    [InlineData("\u0430")]           // Cyrillic 'а' (looks like Latin 'a')
    [InlineData("tenant-\u0430")]    // Cyrillic 'а' embedded
    [InlineData("\uFF11")]           // Fullwidth digit '1'
    [InlineData("tenant-\u0435")]    // Cyrillic 'е' (looks like Latin 'e')
    public void AggregateIdentity_UnicodeHomoglyphInTenantId_Throws(string tenantId) => Should.Throw<ArgumentException>(
            () => new AggregateIdentity(tenantId, "orders", "order-001"));

    // --- Task 3.9: AC #11, GAP-R3 ---

    [Fact]
    public void AggregateIdentity_MaxLengthTenantId_Accepted() {
        // 64-char tenant ID should be accepted
        string maxTenant = new('a', 64);
        var identity = new AggregateIdentity(maxTenant, "orders", "order-001");
        identity.TenantId.Length.ShouldBe(64);
    }

    [Fact]
    public void AggregateIdentity_OverMaxLengthTenantId_Throws() {
        // 65-char tenant ID should be rejected
        string overMaxTenant = new('a', 65);
        _ = Should.Throw<ArgumentException>(
            () => new AggregateIdentity(overMaxTenant, "orders", "order-001"));
    }

    // --- Task 3.10: AC #11, GAP-PM2 ---

    [Fact]
    public void AggregateIdentity_DotInTenantId_Throws() =>
        // Dots could create ambiguous pub/sub topics
        Should.Throw<ArgumentException>(
            () => new AggregateIdentity("tenant.a", "orders", "order-001"));

    // --- Task 3.11: GAP-F1 ---

    [Fact]
    public void TenantValidator_UsesOrdinalStringComparison() {
        // TenantValidator must use Ordinal comparison, not culture-sensitive.
        // This test verifies case-sensitivity which distinguishes Ordinal from
        // OrdinalIgnoreCase, CurrentCultureIgnoreCase, and InvariantCultureIgnoreCase.
        var validator = new TenantValidator(NullLogger<TenantValidator>.Instance);

        // Different tenants should not match
        _ = Should.Throw<TenantMismatchException>(
            () => validator.Validate("tenant-a", "tenant-b:orders:order-001"));

        // Same tenant should pass
        Should.NotThrow(
            () => validator.Validate("tenant-a", "tenant-a:orders:order-001"));

        // Case-sensitive: Ordinal treats "Tenant-A" != "tenant-a".
        // This assertion would NOT catch a regression to OrdinalIgnoreCase without it.
        _ = Should.Throw<TenantMismatchException>(
            () => validator.Validate("Tenant-A", "tenant-a:orders:order-001"));
    }

    // --- Task 3.12: AC #12, GAP-C1 ---

    [Fact]
    public void AggregateActor_TenantMismatch_DemonstratesCrossTenantViolationWithoutValidator() {
        // This test documents WHY TenantValidator is critical:
        // An actor with ID tenant-b:orders:order-001 that receives a command with TenantId=tenant-a
        // would read STATE belonging to tenant-b (from its own ActorId), not tenant-a.

        // Create actor ID for tenant-b
        string actorId = "tenant-b:orders:order-001";

        // Simulate GetAggregateIdentityFromActorId()
        string[] parts = actorId.Split(':', 3);
        var actorIdentity = new AggregateIdentity(parts[0], parts[1], parts[2]);

        // Command arrives for tenant-a
        string commandTenant = "tenant-a";

        // WITHOUT TenantValidator, the actor would use its own identity for state reads
        actorIdentity.TenantId.ShouldBe("tenant-b");
        actorIdentity.TenantId.ShouldNotBe(commandTenant);

        // All state keys derived from the actor's identity (tenant-b), not the command's (tenant-a)
        actorIdentity.EventStreamKeyPrefix.ShouldStartWith("tenant-b:");
        actorIdentity.MetadataKey.ShouldStartWith("tenant-b:");
        actorIdentity.SnapshotKey.ShouldStartWith("tenant-b:");

        // TenantValidator.Validate MUST be called before any state access
        var validator = new TenantValidator(NullLogger<TenantValidator>.Instance);
        _ = Should.Throw<TenantMismatchException>(
            () => validator.Validate(commandTenant, actorId));
    }
}

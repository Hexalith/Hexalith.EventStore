using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>
/// Story 1.9 Task 2 — canonical <see cref="ProjectionReadModelAddress"/> ownership seam: only
/// factory-minted, aggregate-owned addresses are erasable; shared/legacy/reserved targets are denied
/// before any state is resolved.
/// </summary>
public sealed class ProjectionReadModelAddressFactoryTests {
    private static readonly AggregateIdentity Identity = new("tenant-a", "orders", "order-1");

    private static ProjectionReadModelAddressFactory CreateFactory(
        ProjectionSlotRegistry registry,
        string readModelStore = "statestore") =>
        new(registry, Options.Create(new ProjectionOptions { ReadModelStateStoreName = readModelStore }));

    [Fact]
    public void Create_RegisteredAggregateOwnedSlot_ProducesCanonicalKeyAndConfiguredStore() {
        var registry = new ProjectionSlotRegistry();
        registry.Register("summary", "detail", ProjectionReadModelSlotKind.AggregateOwned);
        ProjectionReadModelAddressFactory factory = CreateFactory(registry, "read-models");

        ProjectionReadModelAddress address = factory.Create(Identity, "summary", "detail");

        address.Key.ShouldBe("readmodel:tenant-a:orders:summary:order-1:detail");
        address.StoreName.ShouldBe("read-models");
        address.TenantId.ShouldBe("tenant-a");
        address.Domain.ShouldBe("orders");
        address.ProjectionName.ShouldBe("summary");
        address.AggregateId.ShouldBe("order-1");
        address.Slot.ShouldBe("detail");
    }

    [Fact]
    public void Create_SharedSlot_IsDeniedWithAddressException() {
        var registry = new ProjectionSlotRegistry();
        registry.Register("summary", "index", ProjectionReadModelSlotKind.Shared);
        ProjectionReadModelAddressFactory factory = CreateFactory(registry);

        _ = Should.Throw<ProjectionReadModelAddressException>(
            () => factory.Create(Identity, "summary", "index"));
    }

    [Fact]
    public void Create_UnregisteredSlot_IsDeniedWithAddressException() {
        ProjectionReadModelAddressFactory factory = CreateFactory(new ProjectionSlotRegistry());

        _ = Should.Throw<ProjectionReadModelAddressException>(
            () => factory.Create(Identity, "summary", "detail"));
    }

    [Fact]
    public void CreateForAdmittedManifest_UnregisteredSlot_RecreatesCanonicalAddress() {
        ProjectionReadModelAddressFactory factory = CreateFactory(new ProjectionSlotRegistry(), "read-models");

        ProjectionReadModelAddress address = factory.CreateForAdmittedManifest(Identity, "summary", "detail");

        address.Key.ShouldBe("readmodel:tenant-a:orders:summary:order-1:detail");
        address.StoreName.ShouldBe("read-models");
    }

    [Theory]
    [InlineData("sum:mary", "detail")]
    [InlineData("summary", "de:tail")]
    [InlineData("summary", "de|tail")]
    [InlineData("", "detail")]
    [InlineData("summary", "")]
    public void Create_ReservedOrBlankSegment_ThrowsArgumentException(string projection, string slot) {
        var registry = new ProjectionSlotRegistry();
        ProjectionReadModelAddressFactory factory = CreateFactory(registry);

        _ = Should.Throw<ArgumentException>(() => factory.Create(Identity, projection, slot));
    }

    [Fact]
    public void CreateAggregateOwnedManifest_ReturnsOnlyAggregateOwnedSlots_ExcludingShared() {
        var registry = new ProjectionSlotRegistry();
        registry.Register("summary", "detail", ProjectionReadModelSlotKind.AggregateOwned);
        registry.Register("summary", "audit", ProjectionReadModelSlotKind.AggregateOwned);
        registry.Register("summary", "index", ProjectionReadModelSlotKind.Shared);
        registry.Register("other", "detail", ProjectionReadModelSlotKind.AggregateOwned);
        ProjectionReadModelAddressFactory factory = CreateFactory(registry);

        IReadOnlyList<ProjectionReadModelAddress> manifest = factory.CreateAggregateOwnedManifest(Identity, "summary");

        manifest.Select(a => a.Slot).ShouldBe(["audit", "detail"]);
        manifest.ShouldAllBe(a => a.Key.StartsWith("readmodel:tenant-a:orders:summary:order-1:", StringComparison.Ordinal));
    }

    [Fact]
    public void Registry_ReRegisterWithConflictingKind_Throws() {
        var registry = new ProjectionSlotRegistry();
        registry.Register("summary", "detail", ProjectionReadModelSlotKind.AggregateOwned);

        // Idempotent for the same kind.
        registry.Register("summary", "detail", ProjectionReadModelSlotKind.AggregateOwned);

        _ = Should.Throw<InvalidOperationException>(
            () => registry.Register("summary", "detail", ProjectionReadModelSlotKind.Shared));
    }

    [Fact]
    public void Registry_CanonicalWriterDeclaration_IsDomainScoped() {
        var registry = new ProjectionSlotRegistry();
        registry.Register("summary", "writerless", ProjectionReadModelSlotKind.AggregateOwned);
        registry.Register("summary", "canonical", ProjectionReadModelSlotKind.AggregateOwned);
        registry.RegisterCanonicalWriter("orders", "summary", "canonical");

        registry.DeclaresCanonicalWriter("orders", "summary", "writerless").ShouldBeFalse();
        registry.DeclaresCanonicalWriter("orders", "summary", "canonical").ShouldBeTrue();
        registry.DeclaresCanonicalWriter("billing", "summary", "canonical").ShouldBeFalse();
        registry.DeclaresCanonicalWriter("orders", "summary", "missing").ShouldBeFalse();

        registry.RegisterCanonicalWriter("billing", "summary", "canonical");
        registry.DeclaresCanonicalWriter("billing", "summary", "canonical").ShouldBeTrue();
    }

    [Fact]
    public void Registry_CanonicalWriterDeclaration_RequiresRegisteredAggregateOwnedSlot() {
        var registry = new ProjectionSlotRegistry();
        registry.Register("summary", "shared", ProjectionReadModelSlotKind.Shared);

        _ = Should.Throw<InvalidOperationException>(
            () => registry.RegisterCanonicalWriter("orders", "summary", "missing"));
        _ = Should.Throw<InvalidOperationException>(
            () => registry.RegisterCanonicalWriter("orders", "summary", "shared"));
    }

    [Fact]
    public void ReleasedPublicShapes_RemainAvailable() {
        Type declarationType = typeof(ProjectionReadModelSlotDeclaration);
        declarationType.GetConstructor([
            typeof(string),
            typeof(string),
            typeof(ProjectionReadModelSlotKind),
        ]).ShouldNotBeNull();
        declarationType.GetMethod("Deconstruct", [
            typeof(string).MakeByRefType(),
            typeof(string).MakeByRefType(),
            typeof(ProjectionReadModelSlotKind).MakeByRefType(),
        ]).ShouldNotBeNull();

        typeof(ProjectionSlotServiceCollectionExtensions).GetMethod(
            nameof(ProjectionSlotServiceCollectionExtensions.AddProjectionReadModelSlot),
            [
                typeof(IServiceCollection),
                typeof(string),
                typeof(string),
                typeof(ProjectionReadModelSlotKind),
            ]).ShouldNotBeNull();

        typeof(IProjectionSlotRegistry).GetMethod(
            nameof(IProjectionSlotRegistry.Register),
            [
                typeof(string),
                typeof(string),
                typeof(ProjectionReadModelSlotKind),
            ]).ShouldNotBeNull();
    }
}

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Registration helpers for declaring logical projection read-model slots. Domain modules declare their
/// aggregate-owned vs shared slots through <see cref="AddProjectionReadModelSlot"/> (directly or via the
/// DomainService seam, which registers <see cref="ProjectionReadModelSlotDeclaration"/> entries); they never
/// implement raw DAPR erasure plumbing (AD-2). Every declaration is registered as a DI singleton and the
/// platform <see cref="ProjectionSlotRegistry"/> absorbs them all when it is resolved, so declaration order
/// and the domain/platform package boundary do not matter.
/// </summary>
public static class ProjectionSlotServiceCollectionExtensions {
    /// <summary>
    /// Declares a logical projection read-model slot with the platform slot registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="projectionName">The projection name (colon-free segment).</param>
    /// <param name="slot">The logical slot name (colon-free segment).</param>
    /// <param name="kind">Whether the slot is aggregate-owned (erasable) or shared (excluded).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProjectionReadModelSlot(
        this IServiceCollection services,
        string projectionName,
        string slot,
        ProjectionReadModelSlotKind kind) {
        ArgumentNullException.ThrowIfNull(services);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        ProjectionKeySegments.Validate(slot, nameof(slot));
        _ = services.AddSingleton(new ProjectionReadModelSlotDeclaration(projectionName, slot, kind));
        return services;
    }

    /// <summary>
    /// Builds a <see cref="ProjectionSlotRegistry"/> populated from every registered
    /// <see cref="ProjectionReadModelSlotDeclaration"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The populated registry.</returns>
    internal static ProjectionSlotRegistry BuildSlotRegistry(IServiceProvider serviceProvider) {
        var registry = new ProjectionSlotRegistry();
        foreach (ProjectionReadModelSlotDeclaration declaration in serviceProvider.GetServices<ProjectionReadModelSlotDeclaration>()) {
            registry.Register(declaration.ProjectionName, declaration.Slot, declaration.Kind);
        }

        return registry;
    }
}

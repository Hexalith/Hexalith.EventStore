using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Default <see cref="IProjectionReadModelAddressFactory"/>. Resolves the read-model store component from
/// <see cref="ProjectionOptions"/> (never a caller argument) and emits canonical keys of the form
/// <c>readmodel:{tenant}:{domain}:{projection}:{aggregate}:{slot}</c>, all segments reserved-char-free.
/// </summary>
/// <param name="slotRegistry">The registry of declared logical slots.</param>
/// <param name="options">The projection options carrying the read-model store component name.</param>
public sealed class ProjectionReadModelAddressFactory(
    IProjectionSlotRegistry slotRegistry,
    IOptions<ProjectionOptions> options) : IProjectionReadModelAddressFactory {
    internal const string KeyPrefix = "readmodel:";

    /// <inheritdoc/>
    public ProjectionReadModelAddress Create(AggregateIdentity identity, string projectionName, string slot) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(identity.TenantId, "identity.TenantId");
        ProjectionKeySegments.Validate(identity.Domain, "identity.Domain");
        ProjectionKeySegments.Validate(identity.AggregateId, "identity.AggregateId");
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        ProjectionKeySegments.Validate(slot, nameof(slot));

        if (!slotRegistry.TryGetKind(projectionName, slot, out ProjectionReadModelSlotKind kind)) {
            throw new ProjectionReadModelAddressException(
                $"Read-model slot '{slot}' is not registered for projection '{projectionName}'; legacy or opaque targets are not erasable.");
        }

        if (kind != ProjectionReadModelSlotKind.AggregateOwned) {
            throw new ProjectionReadModelAddressException(
                $"Read-model slot '{slot}' for projection '{projectionName}' is shared and is excluded from whole-key erasure.");
        }

        return Build(identity, projectionName, slot);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ProjectionReadModelAddress> CreateAggregateOwnedManifest(AggregateIdentity identity, string projectionName) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(identity.TenantId, "identity.TenantId");
        ProjectionKeySegments.Validate(identity.Domain, "identity.Domain");
        ProjectionKeySegments.Validate(identity.AggregateId, "identity.AggregateId");
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));

        return [.. slotRegistry
            .GetAggregateOwnedSlots(projectionName)
            .Select(slot => Build(identity, projectionName, slot))];
    }

    private ProjectionReadModelAddress Build(AggregateIdentity identity, string projectionName, string slot) {
        string key = string.Join(
            ':',
            KeyPrefix[..^1],
            identity.TenantId,
            identity.Domain,
            projectionName,
            identity.AggregateId,
            slot);
        return new ProjectionReadModelAddress(
            options.Value.ReadModelStateStoreName,
            key,
            identity.TenantId,
            identity.Domain,
            projectionName,
            identity.AggregateId,
            slot);
    }
}

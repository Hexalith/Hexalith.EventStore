namespace Hexalith.EventStore.DomainService;

/// <summary>The persisted control-namespace registry of tenants that hold reminder candidates.</summary>
/// <param name="Tenants">The canonical tenants, in ordinal order.</param>
internal sealed record ReminderTenantRegistry(IReadOnlyList<string> Tenants);

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Derives every persisted reminder key. Keys are scoped by the reminder actor type; tenant index keys use
/// the canonical tenant, which cannot contain a colon, and the control registry lives in a namespace no
/// tenant can form.
/// </summary>
internal static class ReminderStateKeys
{
    /// <summary>Gets the key of the control-namespace tenant registry.</summary>
    /// <param name="actorType">The reminder actor type name.</param>
    /// <returns>The state key.</returns>
    public static string TenantRegistry(string actorType) => Prefix(actorType) + ":control:tenants";

    /// <summary>Gets the key of one tenant's candidate index.</summary>
    /// <param name="actorType">The reminder actor type name.</param>
    /// <param name="tenant">The canonical tenant.</param>
    /// <returns>The state key.</returns>
    public static string TenantCandidates(string actorType, string tenant) => Prefix(actorType) + ":tenant:" + tenant + ":candidates";

    /// <summary>Gets the key of one item's reminder state.</summary>
    /// <param name="actorType">The reminder actor type name.</param>
    /// <param name="actorId">The <c>wra-</c> actor identifier.</param>
    /// <returns>The state key.</returns>
    public static string Item(string actorType, string actorId) => Prefix(actorType) + ":item:" + actorId;

    /// <summary>Gets the key of one subject's latest audit disposition.</summary>
    /// <param name="actorType">The reminder actor type name.</param>
    /// <param name="actorId">The <c>wra-</c> actor identifier.</param>
    /// <param name="subject">The reminder name or quarantine evidence digest.</param>
    /// <returns>The state key.</returns>
    public static string Disposition(string actorType, string actorId, string subject)
        => Item(actorType, actorId) + ":disposition:" + subject;

    private static string Prefix(string actorType) => "eventstore:reminders:v1:" + actorType;
}

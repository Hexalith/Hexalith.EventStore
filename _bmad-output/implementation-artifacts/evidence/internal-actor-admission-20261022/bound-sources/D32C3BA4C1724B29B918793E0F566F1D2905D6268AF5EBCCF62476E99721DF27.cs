namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Required finite purpose policy; no production default is supplied.</summary>
/// <param name="PolicyId">The approved versioned policy reference.</param>
/// <param name="Retention">The approved finite retention duration.</param>
/// <param name="ExpiryTrigger">The approved irreversible expiry trigger.</param>
public sealed record IdentityHistoryPolicy(string PolicyId, TimeSpan Retention, string ExpiryTrigger)
{
    /// <summary>Gets whether every mandatory policy field is explicitly configured.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(PolicyId)
        && Retention > TimeSpan.Zero && ExpiryTrigger == "binding-effective-at";

    /// <summary>Derives supported expiry without allowing out-of-range policy durations.</summary>
    public DateTimeOffset? DeriveExpiry(DateTimeOffset effectiveAt)
    {
        if (!IsValid)
        {
            return null;
        }

        try
        {
            return effectiveAt + Retention;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}

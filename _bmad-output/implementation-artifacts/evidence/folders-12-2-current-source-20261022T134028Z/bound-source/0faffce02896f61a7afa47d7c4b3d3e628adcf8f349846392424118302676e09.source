using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>
/// Durable anchor store. Admit retains the original without advancing. Lookup starts no effect.
/// Production does not register a store, so the journal denies.
/// </summary>
public interface IProtectionAnchorStore
{
    /// <summary>Durably retains one exact original without advancing the anchor.</summary>
    /// <param name="transition">Original transition.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The admit disposition.</returns>
    Task<ProtectionAnchorDisposition> AdmitAsync(AnchoredStateTransition transition, CancellationToken cancellationToken);

    /// <summary>Reads the retained record without resolving an unknown acknowledgement.</summary>
    /// <param name="scopeId">Scope id.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The retained record, or null.</returns>
    Task<ProtectionAnchorRecord?> ReadAsync(string scopeId, CancellationToken cancellationToken);

    /// <summary>Compares the predecessor anchor and advances the same original.</summary>
    /// <param name="transition">Admitted original.</param>
    /// <param name="expectedAnchor">Expected predecessor revision.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Advanced, Unknown when the acknowledgement is lost, or Conflict.</returns>
    Task<ProtectionAnchorDisposition> AdvanceAsync(AnchoredStateTransition transition, long expectedAnchor, CancellationToken cancellationToken);

    /// <summary>Resolves only the same admitted original. It does not advance the anchor or start an effect.</summary>
    /// <param name="transition">Claimed original.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The recover disposition.</returns>
    Task<ProtectionAnchorDisposition> RecoverAsync(AnchoredStateTransition transition, CancellationToken cancellationToken);

    /// <summary>Reads the retained original. A lost record stays Unknown until this lookup finds it.</summary>
    /// <param name="transition">Claimed original.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The looked-up record, or null.</returns>
    Task<ProtectionAnchorRecord?> LookupAsync(AnchoredStateTransition transition, CancellationToken cancellationToken);

    /// <summary>Reads the current record for a scope. This is a lookup, not an advance.</summary>
    /// <param name="scopeId">Scope id.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The looked-up record, or null.</returns>
    Task<ProtectionAnchorRecord?> LookupScopeAsync(string scopeId, CancellationToken cancellationToken);
}

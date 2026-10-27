using System.Security.Claims;

namespace Hexalith.EventStore.DomainService.Content;

/// <summary>EventStore-owned immutable body persistence, atomic publication and governed disposition.</summary>
public interface IAuthoritativeContentStore
{
    /// <summary>Consumes bounded actual bytes and issues an opaque tenant-scoped receipt after durable verification.</summary>
    Task<ContentReceipt> UploadAsync(ClaimsPrincipal principal, ContentUploadRequest request, Stream bytes, CancellationToken cancellationToken = default);

    /// <summary>Verifies actual persisted upload bytes, tenant/task scope and immutable metadata before command admission.</summary>
    Task<ContentReceipt> VerifyUploadAsync(ClaimsPrincipal principal, ContentUploadVerification request, CancellationToken cancellationToken = default);

    /// <summary>Inspects verified tenant/task upload bytes in memory for trusted mutation preparation after explicit inspection authorization.</summary>
    /// <remarks>Internal caller capability only; never expose this as public upload-body retrieval. Ordinary content-read authority is independent.</remarks>
    Task<ContentReadResult> InspectUploadAsync(ClaimsPrincipal principal, ContentUploadVerification request, CancellationToken cancellationToken = default);

    /// <summary>Protects all verified bodies before event append. Equivalent retries return the same preparation; conflicting retries fail.</summary>
    Task PrepareAsync(ClaimsPrincipal principal, ContentPublicationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reconciles a prepared command after append, rejection or process replacement; pending remains hidden and protected.</summary>
    Task<ContentPublicationOutcome> RecoverAsync(ClaimsPrincipal principal, ContentScope scope, string commandId, CancellationToken cancellationToken = default);

    /// <summary>Reauthorizes, reconciles and verifies the exact visible version; tombstones suppress every version of an incarnation.</summary>
    Task<ContentReadResult> ReadAsync(ClaimsPrincipal principal, ContentReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lists current visible immutable bindings after current authorization and pending reconciliation.</summary>
    Task<IReadOnlyList<ContentBinding>> ListAsync(ClaimsPrincipal principal, ContentScope scope, CancellationToken cancellationToken = default);

    /// <summary>Atomically blocks a clean released lease fence without terminally closing its logical task.</summary>
    Task<bool> TryQuiesceTaskLeaseAsync(ClaimsPrincipal principal, ContentScope scope, string taskId, long leaseFence, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <summary>Atomically seals a source-confirmed task against new uploads/preparations only when no staged or unresolved work remains.</summary>
    /// <remarks>Older providers fail closed until they implement this authoritative capability.</remarks>
    Task<bool> TrySealTaskForCompletionAsync(ClaimsPrincipal principal, ContentScope scope, string taskId, long leaseFence, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <summary>Lists metadata-only upload recovery/disposition candidates, including interrupted reserved uploads.</summary>
    Task<IReadOnlyList<ContentUploadStatus>> ListUploadsAsync(ClaimsPrincipal principal, ContentScope scope, CancellationToken cancellationToken = default);

    /// <summary>Durably applies or releases a content hold; release clears the cleanup epoch. Erasure-sealed receipts reject new holds.</summary>
    Task SetHoldAsync(ClaimsPrincipal principal, ContentScope scope, string receiptId, bool legalHold, CancellationToken cancellationToken = default);

    /// <summary>Durably marks a never-assigned upload abandoned before its cleanup epoch can start.</summary>
    Task AbandonUploadAsync(ClaimsPrincipal principal, ContentScope scope, string receiptId, CancellationToken cancellationToken = default);

    /// <summary>Starts/cancels a P7D epoch or erases bytes only after a fresh sealed disposition. Returns true only after physical erasure.</summary>
    Task<bool> ApplyDispositionAsync(ClaimsPrincipal principal, ContentScope scope, string receiptId, CancellationToken cancellationToken = default);
}

using System.Security.Claims;

namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Domain truth and policy only. Implementations perform no physical body persistence.</summary>
/// <remarks>Every method must fail closed on unavailable current authority. Never resolve from projections or caller assertions.</remarks>
public interface IContentAuthority
{
    /// <summary>Checks current tenant/resource/policy authority before store observation. Resource is the exact request object or receipt identifier.</summary>
    Task AuthorizeAsync(ClaimsPrincipal principal, ContentScope scope, ContentOperation operation, object resource, CancellationToken cancellationToken);

    /// <summary>Examines actual bytes and policy; rejects confidential operational cleartext, sealed values or reconstruction material before persistence.</summary>
    Task ValidateUploadAsync(ClaimsPrincipal principal, ContentUploadRequest request, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);

    /// <summary>Checks current task, incarnation, lock fence, expected version, and policy against authoritative state.</summary>
    Task ValidatePreparationAsync(ClaimsPrincipal principal, ContentPublicationRequest request, CancellationToken cancellationToken);

    /// <summary>Proves the exact prepared batch's outcome from retained authoritative command/event state. Accepted must compare every binding and fence.</summary>
    Task<ContentPublicationOutcome> GetPublicationOutcomeAsync(ContentPublicationRequest request, CancellationToken cancellationToken);

    /// <summary>Reads fresh lifecycle/hold/reference state; generation changes even when a resume/hold cycle occurs between cleanup visits.</summary>
    Task<ContentDisposition> GetDispositionAsync(ContentScope scope, ContentReceipt receipt, CancellationToken cancellationToken);

    /// <summary>Atomically rechecks the exact generation and permanently disposes this receipt against new references; retries require current source proof.</summary>
    /// <remarks>A resume or source change before the grant must reject the stale generation. A successful durable grant permanently precludes references to this receipt, including from later resumed work; it need not prohibit unrelated new tasks. Store-owned holds are fenced by the store claim. A retry before the store commits deletion must re-read current disposition and re-prove the source grant; after its irreversible deletion decision the store may complete physical erasure after replacement.</remarks>
    Task<bool> TryAuthorizeErasureAsync(ContentScope scope, ContentReceipt receipt, long generation, CancellationToken cancellationToken);
}

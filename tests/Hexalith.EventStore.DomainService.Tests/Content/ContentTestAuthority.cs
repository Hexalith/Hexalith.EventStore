using System.Security.Claims;

using Hexalith.EventStore.DomainService.Content;

namespace Hexalith.EventStore.DomainService.Tests.Content;

internal sealed class ContentTestAuthority : IContentAuthority
{
    public bool Deny { get; set; }
    public bool DenyInspection { get; set; }
    public bool DenyOrdinaryRead { get; set; }
    public bool RejectBytes { get; set; }
    public bool RejectPreparation { get; set; }
    public Func<long, CancellationToken, Task<bool>>? SealAsync { get; set; }
    public bool RejectDispositionRead { get; set; }
    public bool SealErasure { get; set; } = true;
    public int AuthorizationCount { get; private set; }
    public ContentPublicationOutcome Outcome { get; set; }
    public ContentDisposition Disposition { get; set; } = new(1, false, true, true, false);
    public Dictionary<string, ContentPublicationOutcome> Outcomes { get; } = new(StringComparer.Ordinal);
    public Task AuthorizeAsync(ClaimsPrincipal principal, ContentScope scope, ContentOperation operation, object resource, CancellationToken cancellationToken)
    {
        AuthorizationCount++;
        if (Deny || operation == ContentOperation.InspectUpload && DenyInspection || operation == ContentOperation.Read && DenyOrdinaryRead
            || principal.FindFirst("tenant")?.Value != scope.TenantId) throw new UnauthorizedAccessException();
        return Task.CompletedTask;
    }
    public Task ValidateUploadAsync(ClaimsPrincipal principal, ContentUploadRequest request, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (RejectBytes) throw new UnauthorizedAccessException();
        return Task.CompletedTask;
    }
    public Task ValidatePreparationAsync(ClaimsPrincipal principal, ContentPublicationRequest request, CancellationToken cancellationToken)
    {
        if (RejectPreparation) throw new ContentConflictException();
        return Task.CompletedTask;
    }
    public Task<ContentPublicationOutcome> GetPublicationOutcomeAsync(ContentPublicationRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Outcomes.GetValueOrDefault(request.CommandId, Outcome));
    public Task<ContentDisposition> GetDispositionAsync(ContentScope scope, ContentReceipt receipt, CancellationToken cancellationToken)
        => RejectDispositionRead ? throw new InvalidOperationException("Disposition unavailable") : Task.FromResult(Disposition);
    public Task<bool> TryAuthorizeErasureAsync(ContentScope scope, ContentReceipt receipt, long generation, CancellationToken cancellationToken)
        => SealAsync?.Invoke(generation, cancellationToken) ?? Task.FromResult(SealErasure && Disposition.Generation == generation && Disposition.Eligible);
}

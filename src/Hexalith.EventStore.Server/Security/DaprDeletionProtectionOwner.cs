using Dapr.Actors.Client;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Bounded exact-tenant private adapter to the existing atomic protection actor. No registration or unrestricted actor client is exposed.</summary>
/// <param name="tenantId">Installed private tenant.</param><param name="proxies">Dedicated authenticated protection-owner transport.</param><param name="clock">Whole-call clock.</param>
public sealed class DaprDeletionProtectionOwner(string tenantId, IActorProxyFactory proxies, TimeProvider clock) : IDeletionProtectionOwner
{
    /// <inheritdoc/>
    public Task<DeletionConsumptionOriginal<DeletionBatchConsumptionRequest>?> ReadRegistrationOriginalAsync(DeletionBatchConsumptionRequest request, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(request); return ReadOriginalAsync(request.Capability.TenantId, actor => actor.ReadRegistrationOriginalAsync(DeletionConsumptionIdentity.Capture(request)), cancellationToken); }
    /// <inheritdoc/>
    public Task<DeletionConsumptionOriginal<DeletionBatchConsumptionRequest>?> ReadReservationOriginalAsync(DeletionBatchConsumptionRequest request, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(request); return ReadOriginalAsync(request.Capability.TenantId, actor => actor.ReadReservationOriginalAsync(DeletionConsumptionIdentity.Capture(request)), cancellationToken); }
    /// <inheritdoc/>
    public Task<DeletionConsumptionOriginal<DeletionBatchBlockRequest>?> ReadBlockOriginalAsync(DeletionBatchBlockRequest request, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(request); return ReadOriginalAsync(request.TenantId, actor => actor.ReadBlockOriginalAsync(request), cancellationToken); }
    /// <inheritdoc/>
    public Task<DeletionConsumptionOriginal<DeletionReattestationActivation>?> ReadActivationOriginalAsync(DeletionReattestationActivation activation, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(activation); return ReadOriginalAsync(activation.Replacement.Capability.TenantId,
        actor => actor.ReadActivationOriginalAsync(activation with { Replacement = DeletionConsumptionIdentity.Capture(activation.Replacement) }), cancellationToken); }
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> RegisterAsync(DeletionBatchConsumptionRequest request, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(request); return InvokeAsync(request.Capability.TenantId, actor => actor.RegisterAsync(request), cancellationToken); }
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> ReserveAndConsumeAsync(DeletionBatchConsumptionRequest request, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(request); return InvokeAsync(request.Capability.TenantId, actor => actor.ReserveAndConsumeAsync(request), cancellationToken); }
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> ActivateAsync(DeletionReattestationActivation activation, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(activation); return InvokeAsync(activation.Replacement.Capability.TenantId, actor => actor.ActivateAsync(activation), cancellationToken); }
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> ReconcileBlockedReplacementAsync(DeletionBlockedReplacementReconciliation request, CancellationToken cancellationToken = default)
        { ArgumentNullException.ThrowIfNull(request); return InvokeAsync(request.Capability.TenantId, actor => actor.ReconcileBlockedReplacementAsync(request), cancellationToken); }
    /// <inheritdoc/>
    public async Task<DeletionBlockedReplacementResult?> ReadBlockedReplacementAsync(DeletionBatchCapabilityV1 capability, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capability);
        if (capability.TenantId != tenantId) { throw new ArgumentException("Private protection tenant mismatch."); }
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        var result = await deadline.ReadAsync(_ => proxies.CreateActorProxy<IDeletionConsumptionActor>(new(DeletionConsumptionActor.GetActorId(tenantId)), DeletionConsumptionActor.ActorTypeName)
            .ReadBlockedReplacementAsync(capability)).ConfigureAwait(false);
        deadline.ThrowIfCancellationRequested(); return result;
    }
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> LookupAsync(string requestedTenant, string batchId, CancellationToken cancellationToken = default)
        => InvokeAsync(requestedTenant, actor => actor.LookupAsync(requestedTenant, batchId), cancellationToken);
    /// <inheritdoc/>
    public async Task<DeletionReservedManifestOriginal?> ReadReservedManifestOriginalAsync(string requestedTenant, string batchId, string reservationReceiptId, CancellationToken cancellationToken = default)
    {
        if (requestedTenant != tenantId) { throw new ArgumentException("Private protection tenant mismatch."); }
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        var result = await deadline.ReadWithAdmissionAsync(_ => proxies.CreateActorProxy<IDeletionConsumptionActor>(new(DeletionConsumptionActor.GetActorId(tenantId)), DeletionConsumptionActor.ActorTypeName)
            .ReadReservedManifestOriginalAsync(requestedTenant, batchId, reservationReceiptId)).ConfigureAwait(false);
        deadline.ThrowIfCancellationRequested(); return result;
    }
    /// <inheritdoc/>
    public async Task<DeletionActivationComparison?> ReadActivationComparisonAsync(string requestedTenant, string batchId, string replacementKeyVersion, CancellationToken cancellationToken = default)
    {
        if (requestedTenant != tenantId) { throw new ArgumentException("Private protection tenant mismatch."); }
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        var result = await deadline.ReadAsync(_ => proxies.CreateActorProxy<IDeletionConsumptionActor>(new(DeletionConsumptionActor.GetActorId(tenantId)), DeletionConsumptionActor.ActorTypeName)
            .ReadActivationComparisonAsync(requestedTenant, batchId, replacementKeyVersion)).ConfigureAwait(false);
        deadline.ThrowIfCancellationRequested(); return result;
    }
    private async Task<DeletionConsumptionOutcome> InvokeAsync(string requestedTenant, Func<IDeletionConsumptionActor, Task<DeletionConsumptionOutcome>> operation, CancellationToken cancellationToken)
    {
        if (requestedTenant != tenantId) { throw new ArgumentException("Private protection tenant mismatch."); }
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        var result = await deadline.ReadAsync(_ => operation(proxies.CreateActorProxy<IDeletionConsumptionActor>(new(DeletionConsumptionActor.GetActorId(tenantId)), DeletionConsumptionActor.ActorTypeName))).ConfigureAwait(false);
        deadline.ThrowIfCancellationRequested(); return result;
    }
    private async Task<DeletionConsumptionOriginal<TRequest>?> ReadOriginalAsync<TRequest>(string requestedTenant,
        Func<IDeletionConsumptionActor, Task<DeletionConsumptionOriginal<TRequest>?>> operation, CancellationToken cancellationToken)
    {
        if (requestedTenant != tenantId) { throw new ArgumentException("Private protection tenant mismatch."); }
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        try
        {
            var result = await deadline.ReadWithAdmissionAsync(_ => operation(proxies.CreateActorProxy<IDeletionConsumptionActor>(
                new(DeletionConsumptionActor.GetActorId(tenantId)), DeletionConsumptionActor.ActorTypeName))).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); deadline.ThrowIfCancellationRequested(); return result;
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested(); throw;
        }
    }
}

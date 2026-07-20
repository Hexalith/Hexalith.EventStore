using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Routes trusted descriptors to the protected tenant/key admission actor.</summary>
public sealed class IdempotencyAdmissionCoordinator(
    IActorProxyFactory actorProxyFactory,
    IdempotencyKeyProtector keyProtector) : IIdempotencyAdmissionCoordinator
{
    /// <inheritdoc/>
    public async Task<IdempotencyAdmissionSession?> AdmitAsync(
        SubmitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        if (command.Idempotency is null)
        {
            return null;
        }

        IdempotencyProtectedIdentity identity = keyProtector.Protect(
            command.Tenant,
            command.MessageId,
            command.Idempotency);
        IIdempotencyAdmissionActor actor = CreateActor(identity.ActorId);
        var request = new IdempotencyAdmissionRequest(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            identity.TenantPartition,
            identity.DigestKeyVersion,
            identity.KeyDigest,
            identity.VerificationTag,
            identity.IntentDigest,
            identity.RetentionTier);
        IdempotencyAdmissionResult result = await actor.AdmitAsync(request).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return new IdempotencyAdmissionSession(
            identity.ActorId,
            result.FencingToken,
            result.Decision,
            result.ReplayResult,
            result.Decision == IdempotencyAdmissionDecision.Execute
                ? Guid.NewGuid().ToString("N")
                : null);
    }

    /// <inheritdoc/>
    public async Task BeginAsync(
        IdempotencyAdmissionSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        await CreateActor(session.ActorId)
            .BeginAsync(new IdempotencyAdmissionTransitionRequest(session.FencingToken))
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <inheritdoc/>
    public async Task CompleteAsync(
        IdempotencyAdmissionSession session,
        CommandProcessingResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        await CreateActor(session.ActorId)
            .CompleteAsync(new IdempotencyAdmissionCompletionRequest(session.FencingToken, result))
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <inheritdoc/>
    public async Task MarkRecoveryAsync(
        IdempotencyAdmissionSession session,
        IdempotencyAdmissionState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        await CreateActor(session.ActorId)
            .MarkRecoveryAsync(new IdempotencyAdmissionRecoveryRequest(session.FencingToken, state))
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private IIdempotencyAdmissionActor CreateActor(string actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        return actorProxyFactory.CreateActorProxy<IIdempotencyAdmissionActor>(
            new ActorId(actorId),
            IdempotencyAdmissionActor.ActorTypeName);
    }
}

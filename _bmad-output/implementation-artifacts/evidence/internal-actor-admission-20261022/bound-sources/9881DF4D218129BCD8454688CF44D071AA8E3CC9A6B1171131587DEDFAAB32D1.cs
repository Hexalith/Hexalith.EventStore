using System.Text.Json;

using Dapr.Actors.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Identity;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Durable proof-gated stable actor registry in the existing EventStore state store.</summary>
public sealed class IdentityActorRegistryActor(ActorHost host,
    [FromKeyedServices("identity-registry")] IIdentityAdmissionProof proofService,
    IOptions<ActorRegistryTrustOptions> trustOptions)
    : Actor(host), IIdentityActorRegistryActor
{
    /// <summary>The fixed registry actor type.</summary>
    public const string ActorTypeName = nameof(IdentityActorRegistryActor);

    /// <summary>The atomic namespace state key.</summary>
    public const string StateName = "identity-registry-v1";

    /// <summary>The single global namespace owning actor identity independently of tenants.</summary>
    public const string RegistryNamespace = "global-actors-v1";

    /// <inheritdoc/>
    public async Task<ActorRegistryEntry?> ReadAsync(string lookup, bool byAlias, string messageId, string proof)
    {
        if (Host.Id.GetId() != RegistryNamespace)
        {
            throw new InvalidOperationException("Registry scope is invalid.");
        }

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { lookup, byAlias });
        Verify("RegistryRead", messageId, messageId, payload, proof, requireOperator: false);
        ActorRegistryState state = await ReadStateAsync().ConfigureAwait(false);
        ActorRegistryAlias? alias = byAlias ? state.Aliases.GetValueOrDefault(lookup) : null;
        string? actor = byAlias ? alias is { Active: true } ? alias.ActorId : null : lookup;
        return actor is null ? null : state.Actors.GetValueOrDefault(actor);
    }

    /// <inheritdoc/>
    public async Task<ActorRegistryEntry> MutateAsync(ActorRegistryMutation mutation, string proof)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (Host.Id.GetId() != RegistryNamespace || mutation.RegistryNamespace != RegistryNamespace)
        {
            throw new InvalidOperationException("Registry scope is invalid.");
        }

        IdentityAdmissionEvidence admitted = Verify("RegistryMutation", mutation.OperationId, mutation.OperationId,
            JsonSerializer.SerializeToUtf8Bytes(mutation), proof, requireOperator: true);
        if (mutation.ProvenanceId != admitted.OperatorActorId)
        {
            throw new InvalidOperationException("Registry provenance differs from verified admission.");
        }

        ActorRegistryState current = await ReadStateAsync().ConfigureAwait(false);
        (ActorRegistryState next, ActorRegistryEntry result) = ActorRegistryTransitions.Apply(current, mutation);
        if (!ReferenceEquals(current, next))
        {
            await StateManager.SetStateAsync(StateName, next).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }

        return result;
    }

    private IdentityAdmissionEvidence Verify(string operation, string messageId, string logicalId, byte[] payload, string proof, bool requireOperator)
    {
        var scope = new IdentityAdmissionScope("identity-authority", "identity-registry", Host.Id.GetId(),
            operation, messageId, logicalId, IdentityAdmissionProof.Digest(payload));
        IdentityAdmissionEvidence? evidence = proofService.Verify(proof, scope);
        if (evidence is null
            || !(requireOperator ? trustOptions.Value.WriterSources : trustOptions.Value.ReaderSources)
                .Contains(evidence.SourceId, StringComparer.Ordinal)
            || (requireOperator && string.IsNullOrWhiteSpace(evidence.OperatorActorId)))
        {
            throw new InvalidOperationException("Registry admission is unavailable.");
        }

        return evidence;
    }

    private async Task<ActorRegistryState> ReadStateAsync()
    {
        ConditionalValue<ActorRegistryState> stored = await StateManager.TryGetStateAsync<ActorRegistryState>(StateName).ConfigureAwait(false);
        return stored.HasValue ? stored.Value : new(new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal));
    }
}

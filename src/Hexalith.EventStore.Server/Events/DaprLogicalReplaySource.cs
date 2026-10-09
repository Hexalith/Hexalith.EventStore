using System.Security.Cryptography;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Composes owning-actor logical readback, shared evolution and current separately scoped logical signatures.</summary>
/// <remarks>Unregistered trusted-host preparation. It grants no production source, peer, key or catalog readiness.</remarks>
internal sealed class DaprLogicalReplaySource
{
    private readonly IActorStateManager _stateManager;
    private readonly DaprLogicalEventReader _reader;
    private readonly string _application;
    private readonly string _namespace;
    private readonly string _actorType;
    private readonly AggregateIdentity _identity;
    private readonly byte[] _configurationHash;
    private readonly EventEvolutionService _evolution;

    /// <summary>Fixes actual owner context before any request; no request supplies a state key or actor identity.</summary>
    internal DaprLogicalReplaySource(IActorStateManager stateManager, IEventPayloadProtectionService protection,
        EventEvolutionService evolution, AggregateIdentity identity, string applicationId, string actorNamespace,
        string actorType, ReadOnlySpan<byte> pinnedSourceConfiguration)
    {
        ArgumentNullException.ThrowIfNull(stateManager); ArgumentNullException.ThrowIfNull(identity);
        foreach (string value in new[] { applicationId, actorNamespace, actorType }) { ArgumentException.ThrowIfNullOrWhiteSpace(value); }
        if (pinnedSourceConfiguration.Length is < 1 or > 64 * 1024) { throw new ArgumentException("Source configuration must be bounded and explicitly pinned."); }
        _stateManager = stateManager; _identity = identity; _application = applicationId; _namespace = actorNamespace;
        _actorType = actorType; _configurationHash = SHA256.HashData(pinnedSourceConfiguration);
        _evolution = evolution; _reader = new DaprLogicalEventReader(stateManager, protection, evolution);
    }

    /// <summary>Checks that an additive owner uses this exact actual actor state-manager instance.</summary>
    internal bool OwnsStateManager(IActorStateManager stateManager) => ReferenceEquals(_stateManager, stateManager);

    /// <summary>Captures the immutable complete-prefix source from actual actor metadata with the originating token.</summary>
    internal async Task<DaprLogicalSourceBinding> CaptureBindingAsync(string aggregateType, long? target,
        CancellationToken cancellationToken)
    {
        _evolution.RequireActive(cancellationToken);
        ConditionalValue<AggregateMetadata> metadata = await _stateManager.TryGetStateAsync<AggregateMetadata>(
            _identity.MetadataKey, cancellationToken).ConfigureAwait(false);
        if (metadata.HasValue && metadata.Value is null) { throw new InvalidOperationException("ReplayRestartRequired: present actor metadata has no logical value."); }
        long head = metadata.HasValue ? metadata.Value.CurrentSequence : 0;
        var binding = new DaprLogicalSourceBinding(_application, _namespace, _actorType, _identity, aggregateType, head,
            target ?? head, metadata.HasValue ? metadata.Value.RetainedFloor : 1, _configurationHash.ToArray(),
            metadata.HasValue ? metadata.Value.ETag : null, metadata.HasValue ? metadata.Value.LastModified : DateTimeOffset.UnixEpoch, metadata.HasValue);
        _ = DaprLogicalClaimCodec.ComputeSourceBindingHash(binding);
        _evolution.RequireActive(cancellationToken); return binding;
    }

    /// <summary>Checks actual host, identity, pinned configuration, immutable source observations and capability.</summary>
    internal async Task RequireCurrentAsync(DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust,
        CancellationToken cancellationToken)
    {
        _evolution.RequireActive(cancellationToken);
        if (binding.ApplicationId != _application || binding.Namespace != _namespace || binding.ActorType != _actorType
            || binding.Identity != _identity || !CryptographicOperations.FixedTimeEquals(binding.SourceConfigurationHash.Span, _configurationHash)) { throw new InvalidOperationException("AddressMismatch: logical source differs from its actual owning actor context."); }
        if (trust.Domain != _identity.Domain || !ReferenceEquals(trust.CapabilityLoss, _evolution.CapabilityLoss)
            || !trust.RegistryFingerprint.Span.SequenceEqual(Convert.FromHexString(_evolution.RegistryFingerprint))) { throw new InvalidOperationException("CapabilityMismatch: logical source/trust domain, registry or loss scope disagree."); }
        trust.RequireCurrent(cancellationToken);
        await _reader.RequireSourceBindingAsync(binding, cancellationToken).ConfigureAwait(false);
        trust.RequireCurrent(cancellationToken);
        _evolution.RequireActive(cancellationToken);
    }

    /// <summary>Builds page one from genesis without admitting a caller accumulator or token.</summary>
    internal Task<DaprLogicalReplayPage> ReadFirstPageAsync(DaprLogicalSourceBinding binding, int maxCount,
        DaprLogicalClaimTrust trust, ECDsa key, EventBufferBudget budget, CancellationToken cancellationToken,
        Func<CancellationToken, Task>? operationFence = null)
    {
        byte[] sourceHash = DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, budget);
        byte[] genesis = DaprLogicalClaimCodec.ComputeGenesis(sourceHash, trust.RegistryFingerprint);
        return ReadPageCoreAsync(binding, 1, maxCount, genesis, trust, key, budget, cancellationToken, operationFence);
    }

    /// <summary>Builds a successor only from the dedicated owner's freshly admitted committed ledger progress.</summary>
    internal Task<DaprLogicalReplayPage> ReadNextPageAsync(DaprLogicalSourceBinding binding, int maxCount,
        DaprReplayCommittedProgress progress, DaprLogicalClaimTrust trust, ECDsa key,
        EventBufferBudget budget, CancellationToken cancellationToken, Func<CancellationToken, Task>? operationFence = null)
    {
        if (progress.CompletedSequence == long.MaxValue || progress.CompletedSequence >= binding.TargetSequence
            || !progress.SourceBindingHash.AsSpan().SequenceEqual(DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, budget))
            || !progress.RegistryFingerprint.AsSpan().SequenceEqual(trust.RegistryFingerprint.Span)) { throw new InvalidOperationException("ReplayRestartRequired: no admitted logical successor exists."); }
        return ReadPageCoreAsync(binding, progress.CompletedSequence + 1, maxCount, progress.Accumulator,
            trust, key, budget, cancellationToken, operationFence);
    }

    /// <summary>Reads only the owning operation's separately admitted anchored successor, including its eligible zero tail.</summary>
    internal Task<DaprLogicalReplayPage> ReadAnchoredPageAsync(DaprLogicalSourceBinding binding, int maxCount,
        long completedSequence, ReadOnlyMemory<byte> accumulator, DaprLogicalClaimTrust trust, ECDsa key,
        EventBufferBudget budget, CancellationToken token, DaprLogicalAnchoredIntake anchored,
        Func<CancellationToken, Task> operationFence)
    {
        token.ThrowIfCancellationRequested();
        anchored.Trust.RequireCurrent(trust, token);
        if (completedSequence < anchored.Selection.CoveredSequence || completedSequence > binding.TargetSequence
            || completedSequence == binding.TargetSequence && completedSequence != anchored.Selection.CoveredSequence
            || accumulator.Length != 32)
        {
            throw new InvalidOperationException("AnchorCapabilityHold: no admitted anchored successor exists.");
        }
        long start = completedSequence == long.MaxValue ? long.MaxValue : completedSequence + 1;
        return ReadPageCoreAsync(binding, start, maxCount, accumulator, trust, key, budget, token, operationFence, anchored);
    }

    private async Task<DaprLogicalReplayPage> ReadPageCoreAsync(DaprLogicalSourceBinding binding, long start, int maxCount,
        ReadOnlyMemory<byte> admittedAccumulator, DaprLogicalClaimTrust trust, ECDsa key,
        EventBufferBudget budget, CancellationToken cancellationToken, Func<CancellationToken, Task>? operationFence,
        DaprLogicalAnchoredIntake? anchored = null)
    {
        async Task RequirePageCurrentAsync(CancellationToken token)
        {
            _evolution.RequireActive(token);
            if (operationFence is not null) { await operationFence(token).ConfigureAwait(false); }
            await RequireCurrentAsync(binding, trust, token).ConfigureAwait(false);
            if (operationFence is not null) { await operationFence(token).ConfigureAwait(false); }
            _evolution.RequireActive(token);
            trust.RequireCurrent(token);
            anchored?.Trust.RequireCurrent(trust, token);
        }
        await RequirePageCurrentAsync(cancellationToken).ConfigureAwait(false);
        bool emptyTail = anchored is not null && anchored.Selection.CoveredSequence == binding.TargetSequence;
        DaprLogicalEventPage page = emptyTail
            ? new DaprLogicalEventPage(start, binding.ActorHead, [], binding.RetainedFloor)
            : await _reader.ReadPageAsync(_identity, binding.AggregateType, start, maxCount,
                cancellationToken, sharedBudget: budget, expectedSourceBinding: binding,
                sourceFence: RequirePageCurrentAsync).ConfigureAwait(false);
        var routes = new List<DaprLogicalSignedClaim>(); DaprLogicalSignedClaim? signedPrefix = null;
        try
        {
            byte[] sourceHash = DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, budget);
            byte[] registry = Convert.FromHexString(GetRegistryFingerprint());
            if (!registry.AsSpan().SequenceEqual(trust.RegistryFingerprint.Span)) { throw new InvalidOperationException("CapabilityMismatch: source evolution and logical trust use different registries."); }
            byte[] accumulator = admittedAccumulator.ToArray(); var entries = new List<DaprLogicalDigestEntry>();
            foreach (DaprLogicalEventView view in page.Events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] logical = Convert.FromHexString(view.ApplicationLogicalDigest
                    ?? throw new InvalidOperationException("LogicalDigestMismatch: reader supplied no admitted logical digest."));
                var entry = new DaprLogicalDigestEntry(view.SequenceNumber, logical); entries.Add(entry);
                accumulator = DaprLogicalClaimCodec.ComputeAccumulatorStep(sourceHash, registry, accumulator, entry);
                EventEnvelope e = view.Source;
                var metadata = new DaprLogicalConsumedMetadata(e.MessageId, e.AggregateId, e.AggregateType, e.TenantId,
                    e.Domain, e.SequenceNumber, e.GlobalPosition, e.Timestamp, e.CorrelationId, e.CausationId, e.UserId,
                    e.DomainServiceVersion, e.EventTypeName, e.MetadataVersion, e.SerializationFormat, e.EventContractType,
                    e.PayloadVersion, e.ApplicationPayloadDigest, e.Extensions as IReadOnlyDictionary<string, string>, view.ReadableFormat);
                byte[] consumed = DaprLogicalClaimCodec.ComputeConsumedMetadataHash(metadata, logical, sourceHash, budget);
                byte[] effectiveHash = ((ImmutablePayload)view.Resolved.Payload).ComputeSha256();
                var fields = new DaprLogicalRouteClaim(logical, e.TenantId, e.Domain, e.AggregateId, e.AggregateType,
                    e.SequenceNumber, e.MessageId, e.EventTypeName, e.MetadataVersion, e.EventContractType, e.PayloadVersion,
                    e.SerializationFormat, view.Resolved.CanonicalType, view.Resolved.CurrentVersion, registry, effectiveHash,
                    view.Resolved.SerializationFormat, consumed, sourceHash);
                using var encodedRoute = new DaprLogicalEncodedClaim(() => DaprLogicalClaimCodec.EncodeRoute(fields), budget, cancellationToken);
                routes.Add(trust.Sign(1, encodedRoute.Bytes.Span, key, budget, cancellationToken));
            }
            long end = page.Events.Count == 0 ? anchored?.Selection.CoveredSequence ?? 0 : page.Events[^1].SequenceNumber;
            var prefix = new DaprLogicalPrefixClaim(_identity.TenantId, _identity.Domain, _identity.AggregateId,
                binding.AggregateType, start, end, binding.ActorHead, binding.TargetSequence, page.Events.Count,
                DaprLogicalClaimCodec.ComputeOrderedList(sourceHash, entries), accumulator, registry, sourceHash,
                anchored is null ? DaprLogicalSourceBinding.ModelId : DaprLogicalReplayAnchorCodec.ModelId);
            await RequirePageCurrentAsync(cancellationToken).ConfigureAwait(false);
            using var encodedPrefix = new DaprLogicalEncodedClaim(() => anchored is null ? DaprLogicalClaimCodec.EncodePrefix(prefix)
                : DaprLogicalAnchoredPrefixCodec.Encode(new DaprLogicalAnchoredPrefixClaim(prefix,
                    anchored.SelectionHash, anchored.Selection.CoveredSequence, anchored.Selection.CanonicalStateHash)), budget, cancellationToken);
            signedPrefix = anchored is null ? trust.Sign(3, encodedPrefix.Bytes.Span, key, budget, cancellationToken)
                : anchored.Trust.SignPrefix(encodedPrefix.Bytes.Span, key, budget, cancellationToken);
            await RequirePageCurrentAsync(cancellationToken).ConfigureAwait(false);
            return new DaprLogicalReplayPage(page, routes.ToArray(), signedPrefix, prefix);
        }
        catch { foreach (DaprLogicalSignedClaim route in routes) { route.Dispose(); } signedPrefix?.Dispose(); page.Dispose(); throw; }
    }

    private string GetRegistryFingerprint() => _evolution.RegistryFingerprint;
}

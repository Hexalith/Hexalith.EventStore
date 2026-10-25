using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Occurrence-authenticated field protection behind EventStore hooks. Custody, binding, source-writer and nonrollback authorities are mandatory infrastructure dependencies.</summary>
public sealed class InteractionPayloadProtectionService : IEventPayloadProtectionService
{
    private const string Scheme = "hexalith-pdenc-v2";
    private const string ContentPath = "/ProtectedContent/Value";
    private readonly IInteractionOccurrenceRegistry _registry;
    private readonly IInteractionProtectionBindingProvider _bindings;
    private readonly InteractionOccurrenceProtector _protector;

    /// <summary>Creates the engine without manufacturing any custody or authority implementation.</summary>
    /// <param name="registry">Authenticated durable occurrence owner.</param>
    /// <param name="keys">Scoped infrastructure key custody and exclusive encryption lease.</param>
    /// <param name="bindings">Independent target enrollment, completion and erasure authority.</param>
    /// <param name="clock">Operational deadline clock.</param>
    public InteractionPayloadProtectionService(IInteractionOccurrenceRegistry registry, IInteractionOccurrenceKeyProvider keys,
        IInteractionProtectionBindingProvider bindings, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(clock);
        _registry = registry;
        _bindings = bindings;
        _protector = new(registry, keys, clock);
    }

    /// <summary>Reports the loaded binary and exact compiled revision, never a configured or caller-supplied target.</summary>
    /// <returns>The actual loaded engine identity.</returns>
    public InteractionProtectionEngineIdentity GetIdentity()
    {
        Assembly assembly = GetType().Assembly;
        string informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        string source = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(static attribute => attribute.Key == "SourceRevisionId")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(source) && informational.LastIndexOf('+') is int separator && separator >= 0)
        {
            // The SDK/MinVer build stamps its exact source identifier into this compiled attribute.
            // It is read from the binary, never supplied by a runtime profile.
            source = informational[(separator + 1)..];
        }
        if (source.Length != 40 || source.Any(static c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) { source = string.Empty; }
        return new(assembly.GetName().Name!, assembly.GetName().Version?.ToString() ?? string.Empty, informational, source);
    }

    /// <inheritdoc/>
    public Task<PayloadProtectionResult> ProtectEventPayloadAsync(AggregateIdentity identity, IEventPayload eventPayload,
        string eventTypeName, byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default)
        => throw MissingOccurrence();

    /// <inheritdoc/>
    public async Task<PayloadProtectionWriteResult> ProtectEventPayloadAsync(AggregateIdentity identity, IEventPayload eventPayload,
        string eventTypeName, byte[] payloadBytes, string serializationFormat, PayloadProtectionOccurrenceContext occurrenceContext,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOccurrence(identity, occurrenceContext, PayloadProtectionPayloadKind.Event, eventTypeName);
        if (serializationFormat != "json") { throw new InvalidOperationException("ProtectionFormatUnsupported"); }
        ProtectionTarget? target = ReadTarget(payloadBytes);
        if (target is null)
        {
            return new(new(payloadBytes.ToArray(), serializationFormat, EventStorePayloadProtectionMetadata.Unprotected()), null);
        }

        InteractionProtectionBinding binding = await ResolveAsync(identity, occurrenceContext, target, true, cancellationToken).ConfigureAwait(false);
        InteractionOccurrenceSealedResult result = await _protector.ProtectEventAsync(payloadBytes, [ContentPath], binding.Identity,
            binding.DigestKeyVersion, binding.ReservationAttemptOrdinal, binding.KeyReference, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("ProtectionAuthorityUnavailable");
        RequireReference(binding, result);
        return new(new(result.PayloadBytes, result.SerializationFormat, Metadata(result.KeyReference)), Completion(binding, occurrenceContext));
    }

    /// <inheritdoc/>
    public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(AggregateIdentity identity, string eventTypeName,
        byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default)
        => throw MissingOccurrence();

    /// <inheritdoc/>
    public Task<PayloadUnprotectionOutcome> TryUnprotectEventPayloadAsync(AggregateIdentity identity, string eventTypeName,
        byte[] payloadBytes, string serializationFormat, EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default)
        => Task.FromResult(PayloadUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation, metadata));

    /// <inheritdoc/>
    public async Task<PayloadUnprotectionOutcome> TryUnprotectEventPayloadAsync(AggregateIdentity identity, string eventTypeName,
        byte[] payloadBytes, string serializationFormat, EventStorePayloadProtectionMetadata? metadata,
        PayloadProtectionOccurrenceContext occurrenceContext, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOccurrence(identity, occurrenceContext, PayloadProtectionPayloadKind.Event, eventTypeName);
        if (IsPlain(serializationFormat, metadata))
        {
            return ReadTarget(payloadBytes) is null
                ? PayloadUnprotectionOutcome.Readable(payloadBytes.ToArray(), serializationFormat, EventStorePayloadProtectionMetadata.Unprotected())
                : PayloadUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch, metadata);
        }
        try
        {
            InteractionProtectionBinding? binding = await AuthenticateStoredAsync(identity, occurrenceContext, payloadBytes,
                serializationFormat, metadata, cancellationToken).ConfigureAwait(false);
            if (binding is null) { return Mismatch(metadata); }
            CoreUnprotectionResult result = await _protector.UnprotectAsync(binding.Identity, cancellationToken).ConfigureAwait(false);
            return result.IsReadable
                ? PayloadUnprotectionOutcome.Readable(result.PayloadBytes!, "json", metadata!)
                : PayloadUnprotectionOutcome.Unreadable(result.UnreadableReason!.Value, metadata);
        }
        catch (OperationCanceledException) { throw; }
        catch { return PayloadUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable, metadata); }
    }

    /// <inheritdoc/>
    public async Task<PayloadDistributionResult> PrepareEventForDistributionAsync(AggregateIdentity identity, string eventTypeName,
        byte[] payloadBytes, string serializationFormat, EventStorePayloadProtectionMetadata? metadata,
        PayloadProtectionOccurrenceContext occurrenceContext, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOccurrence(identity, occurrenceContext, PayloadProtectionPayloadKind.Event, eventTypeName);
        if (IsPlain(serializationFormat, metadata))
        {
            return new(await TryUnprotectEventPayloadAsync(identity, eventTypeName, payloadBytes, serializationFormat,
                metadata, occurrenceContext, cancellationToken).ConfigureAwait(false));
        }
        try
        {
            InteractionProtectionBinding? binding = await AuthenticateStoredAsync(identity, occurrenceContext, payloadBytes,
                serializationFormat, metadata, cancellationToken).ConfigureAwait(false);
            return binding is null ? new(Mismatch(metadata))
                : new(PayloadUnprotectionOutcome.Readable(payloadBytes.ToArray(), serializationFormat, metadata!), true);
        }
        catch (OperationCanceledException) { throw; }
        catch { return new(PayloadUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable, metadata)); }
    }

    /// <inheritdoc/>
    public async Task<PayloadReplayResult> TryReplayEventPayloadAsync(AggregateIdentity identity, string eventTypeName,
        byte[] payloadBytes, string serializationFormat, EventStorePayloadProtectionMetadata? metadata,
        PayloadProtectionOccurrenceContext occurrenceContext, CancellationToken cancellationToken = default)
    {
        PayloadUnprotectionOutcome outcome = await TryUnprotectEventPayloadAsync(identity, eventTypeName, payloadBytes,
            serializationFormat, metadata, occurrenceContext, cancellationToken).ConfigureAwait(false);
        if (outcome.UnreadableReason != UnreadableProtectedDataReason.KeyInvalidatedOrDeleted) { return new(outcome); }
        try
        {
            InteractionProtectionBinding? binding = await AuthenticateStoredAsync(identity, occurrenceContext, payloadBytes,
                serializationFormat, metadata, cancellationToken).ConfigureAwait(false);
            if (binding is null || !await _bindings.IsIrreversiblyErasedAsync(binding, cancellationToken).ConfigureAwait(false)) { return new(outcome); }
            using BoundedJsonDocument document = BoundedJsonDocument.Inspect(payloadBytes, cancellationToken);
            BoundedJsonNode node = document.Resolve(ContentPath, cancellationToken: cancellationToken);
            byte[] erased = document.Rewrite([new JsonReplacement(node.Start, node.Length, "{\"State\":\"Erased\"}"u8.ToArray())], cancellationToken);
            return new(PayloadUnprotectionOutcome.Readable(erased, "json", metadata!), true);
        }
        catch (OperationCanceledException) { throw; }
        catch { return new(PayloadUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable, metadata)); }
    }

    /// <inheritdoc/>
    public Task<object> ProtectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default)
        => throw MissingOccurrence();

    /// <inheritdoc/>
    public Task<object> UnprotectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default)
        => throw MissingOccurrence();

    /// <inheritdoc/>
    public async Task<SnapshotProtectionWriteResult> ProtectSnapshotAsync(AggregateIdentity identity, object state, JsonTypeInfo stateTypeInfo,
        PayloadProtectionOccurrenceContext occurrenceContext, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOccurrence(identity, occurrenceContext, PayloadProtectionPayloadKind.Snapshot);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(state, stateTypeInfo);
        try
        {
            ProtectionTarget? target = SnapshotProtectionTargetReader.Read(bytes, identity.TenantId, cancellationToken);
            InteractionProtectionBinding binding = await ResolveAsync(identity, occurrenceContext, target, true, cancellationToken).ConfigureAwait(false);
            InteractionOccurrenceSealedResult result = await _protector.ProtectSnapshotAsync(bytes, binding.Identity,
                binding.DigestKeyVersion, binding.ReservationAttemptOrdinal, binding.KeyReference, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("ProtectionAuthorityUnavailable");
            RequireReference(binding, result);
            return new(new(ReadSnapshot(result.PayloadBytes), Metadata(result.KeyReference)), Completion(binding, occurrenceContext));
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <inheritdoc/>
    public async Task<SnapshotUnprotectionOutcome> TryUnprotectSnapshotAsync(AggregateIdentity identity, object state,
        EventStorePayloadProtectionMetadata? metadata, JsonTypeInfo stateTypeInfo, PayloadProtectionOccurrenceContext occurrenceContext,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOccurrence(identity, occurrenceContext, PayloadProtectionPayloadKind.Snapshot);
        try
        {
            ProtectedSnapshotPayloadV2 snapshot = state is ProtectedSnapshotPayloadV2 typed ? typed
                : state is JsonElement json ? ReadSnapshot(json) : throw new InvalidOperationException("ProtectionSnapshotInvalid");
            InteractionProtectionBinding binding = await ResolveAsync(identity, occurrenceContext, null, false, cancellationToken).ConfigureAwait(false);
            InteractionOccurrenceReservationResult retained = await _registry.LookupAsync(binding.Identity, cancellationToken).ConfigureAwait(false);
            if (!IsActive(retained, binding, metadata) || ReadSnapshot(retained.Record!.Sealed!.PayloadBytes) != snapshot)
            { return SnapshotUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch, metadata); }
            CoreUnprotectionResult result = await _protector.UnprotectAsync(binding.Identity, cancellationToken).ConfigureAwait(false);
            if (!result.IsReadable) { return SnapshotUnprotectionOutcome.Unreadable(result.UnreadableReason!.Value, metadata); }
            try
            {
                ProtectionTarget? target = SnapshotProtectionTargetReader.Read(result.PayloadBytes!, identity.TenantId, cancellationToken);
                if (target is not null && target != binding.Identity.Target)
                { return SnapshotUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch, metadata); }
                object unprotected = JsonSerializer.Deserialize(result.PayloadBytes!, stateTypeInfo)
                    ?? throw new InvalidOperationException("ProtectionSnapshotInvalid");
                cancellationToken.ThrowIfCancellationRequested();
                return SnapshotUnprotectionOutcome.Readable(unprotected, metadata!);
            }
            finally { CryptographicOperations.ZeroMemory(result.PayloadBytes!); }
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            cancellationToken.ThrowIfCancellationRequested();
            return SnapshotUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable, metadata);
        }
    }

    /// <inheritdoc/>
    public async Task AcquirePayloadProtectionCompletionLeaseAsync(PayloadProtectionCompletionContext completionContext,
        CancellationToken cancellationToken = default)
    {
        InteractionProtectionBinding binding = await ResolveCompletionAsync(completionContext, cancellationToken).ConfigureAwait(false);
        if (!await _bindings.AcquireCompletionLeaseAsync(binding, cancellationToken).ConfigureAwait(false))
        { throw new InvalidOperationException("ProtectionCompletionUnavailable"); }
    }

    /// <inheritdoc/>
    public async Task CompletePayloadProtectionAsync(PayloadProtectionCompletionContext completionContext,
        PayloadProtectionPersistenceOutcome persistenceOutcome, CancellationToken cancellationToken = default)
    {
        if (persistenceOutcome is not (PayloadProtectionPersistenceOutcome.Persisted or PayloadProtectionPersistenceOutcome.NotPersisted
            or PayloadProtectionPersistenceOutcome.Unknown)) { throw new ArgumentOutOfRangeException(nameof(persistenceOutcome)); }
        InteractionProtectionBinding binding = await ResolveCompletionAsync(completionContext, cancellationToken).ConfigureAwait(false);
        // An ambiguous save retains the original pending record. Only independent exact writer proof may activate or abort it.
        if (persistenceOutcome == PayloadProtectionPersistenceOutcome.Unknown) { return; }
        bool persisted = persistenceOutcome == PayloadProtectionPersistenceOutcome.Persisted;
        InteractionOccurrenceReservationResult result = await _protector.CompleteWriterAsync(binding.Identity,
            completionContext.KeyReference, completionContext.OperationId, persisted, cancellationToken).ConfigureAwait(false);
        if (result.Record?.Request.Identity != binding.Identity || result.Record.KeyReference != completionContext.KeyReference
            || result.Record.WriterProofId != completionContext.OperationId
            || result.Record.WriterState != (persisted ? InteractionOccurrenceWriterState.Active : InteractionOccurrenceWriterState.Aborted))
        { throw new InvalidOperationException("ProtectionCompletionUnavailable"); }
    }

    private async Task<InteractionProtectionBinding> ResolveCompletionAsync(PayloadProtectionCompletionContext context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);
        InteractionProtectionBinding binding = await ResolveAsync(context.Identity, context.Occurrence, null, true, token).ConfigureAwait(false);
        if (context != Completion(binding, context.Occurrence)) { throw new InvalidOperationException("ProtectionCompletionMismatch"); }
        return binding;
    }

    /// <summary>Recovers only the independently retained original writer identity after a caller lost its preparation result. No new reference, attempt, or never-written claim is created.</summary>
    internal async Task RecoverOriginalCommittedOccurrenceAsync(AggregateIdentity owner,
        PayloadProtectionOccurrenceContext occurrence, CancellationToken cancellationToken)
    {
        InteractionProtectionBinding binding = await ResolveAsync(owner, occurrence, null, false, cancellationToken).ConfigureAwait(false);
        await CompletePayloadProtectionAsync(Completion(binding, occurrence), PayloadProtectionPersistenceOutcome.Persisted,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<InteractionProtectionBinding?> AuthenticateStoredAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence,
        byte[] bytes, string format, EventStorePayloadProtectionMetadata? metadata, CancellationToken token)
    {
        if (format != PayloadProtectionWireFormat.ProtectedSerializationFormat || metadata?.Scheme != Scheme) { return null; }
        ProtectionTarget? target = ReadTarget(bytes);
        if (target is null) { return null; }
        InteractionProtectionBinding binding = await ResolveAsync(owner, occurrence, target, false, token).ConfigureAwait(false);
        InteractionOccurrenceReservationResult result = await _registry.LookupAsync(binding.Identity, token).ConfigureAwait(false);
        return IsActive(result, binding, metadata) && result.Record!.Sealed!.PayloadBytes.AsSpan().SequenceEqual(bytes) ? binding : null;
    }

    private async Task<InteractionProtectionBinding> ResolveAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence,
        ProtectionTarget? target, bool writing, CancellationToken token)
    {
        InteractionProtectionBinding? binding = await _bindings.ResolveAsync(owner, occurrence, target, writing, token).ConfigureAwait(false);
        if (binding?.Identity is null || binding.Identity.Owner != owner || binding.Identity.Target.TenantId != owner.TenantId
            || target is not null && binding.Identity.Target != target || binding.Identity.Sequence != occurrence.RecordSequence
            || binding.Identity.Kind != occurrence.PayloadKind || binding.Identity.PayloadTypeId != occurrence.PayloadTypeId
            || !CanonicalUlid.IsValid(binding.KeyReference) || !CanonicalUlid.IsValid(binding.OperationId)
            || binding.ReservationAttemptOrdinal < 1 || string.IsNullOrWhiteSpace(binding.DigestKeyVersion))
        { throw new InvalidOperationException("ProtectionAuthorityUnavailable"); }
        return binding;
    }

    private static bool IsActive(InteractionOccurrenceReservationResult result, InteractionProtectionBinding binding,
        EventStorePayloadProtectionMetadata? metadata)
        => result.Status == InteractionOccurrenceReservationStatus.Sealed && result.Record?.Request.Identity == binding.Identity
            && result.Record.WriterState == InteractionOccurrenceWriterState.Active && result.Record.Sealed is not null
            && result.Record.WriterProofId == binding.OperationId
            && result.Record.KeyReference == binding.KeyReference && result.Record.Sealed.KeyReference == binding.KeyReference
            && result.Record.Sealed.SerializationFormat == PayloadProtectionWireFormat.ProtectedSerializationFormat
            && metadata == Metadata(binding.KeyReference);

    private static void RequireReference(InteractionProtectionBinding binding, InteractionOccurrenceSealedResult result)
    {
        if (result.KeyReference != binding.KeyReference) { throw new InvalidOperationException("ProtectionReferenceMismatch"); }
    }

    private static ProtectionTarget? ReadTarget(byte[] bytes)
    {
        using BoundedJsonDocument bounded = BoundedJsonDocument.Inspect(bytes, CancellationToken.None);
        using JsonDocument document = JsonDocument.Parse(bytes);
        if (document.RootElement.ValueKind != JsonValueKind.Object) { throw new InvalidOperationException("ProtectionEnvelopeInvalid"); }
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, "ProtectedContent", StringComparison.OrdinalIgnoreCase) && property.Name != "ProtectedContent")
            { throw new InvalidOperationException("ProtectionEnvelopeInvalid"); }
        }
        if (!document.RootElement.TryGetProperty("ProtectedContent", out JsonElement envelope))
        {
            if (bounded.ContainsProtectedMember || HasNestedContent(document.RootElement)) { throw new InvalidOperationException("ProtectionEnvelopeInvalid"); }
            return null;
        }
        if (envelope.ValueKind != JsonValueKind.Object || envelope.EnumerateObject().Count() != 2
            || !envelope.TryGetProperty("Value", out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || !envelope.TryGetProperty("Target", out JsonElement target) || target.ValueKind != JsonValueKind.Object
            || target.EnumerateObject().Count() != 3) { throw new InvalidOperationException("ProtectionEnvelopeInvalid"); }
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (property.Name != "ProtectedContent" && HasNestedContent(property.Value))
            { throw new InvalidOperationException("ProtectionEnvelopeInvalid"); }
        }
        string tenant = target.GetProperty("TenantId").GetString()!;
        string interaction = target.GetProperty("AgentInteractionId").GetString()!;
        string alias = target.GetProperty("TargetProtectionKeyAlias").GetString()!;
        if (string.IsNullOrWhiteSpace(tenant) || string.IsNullOrWhiteSpace(interaction) || string.IsNullOrWhiteSpace(alias))
        { throw new InvalidOperationException("ProtectionEnvelopeInvalid"); }
        return new(tenant, interaction, alias);
    }

    private static ProtectedSnapshotPayloadV2 ReadSnapshot(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        return ReadSnapshot(document.RootElement);
    }

    private static ProtectedSnapshotPayloadV2 ReadSnapshot(JsonElement snapshot)
    {
        if (snapshot.ValueKind != JsonValueKind.Object || snapshot.EnumerateObject().Count() != 3)
        { throw new InvalidOperationException("ProtectionSnapshotInvalid"); }
        return new(SnapshotProperty(snapshot, "Format", "format"), SnapshotProperty(snapshot, "SnapshotTypeId", "snapshotTypeId"),
            SnapshotProperty(snapshot, "Envelope", "envelope"));
    }

    private static string SnapshotProperty(JsonElement snapshot, string canonical, string transported)
    {
        bool first = snapshot.TryGetProperty(canonical, out JsonElement value);
        bool second = snapshot.TryGetProperty(transported, out JsonElement webValue);
        if (first == second) { throw new InvalidOperationException("ProtectionSnapshotInvalid"); }
        return (first ? value : webValue).GetString() ?? throw new InvalidOperationException("ProtectionSnapshotInvalid");
    }

    private static bool HasNestedContent(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Object => value.EnumerateObject().Any(static property =>
                property.Name.Equals("ProtectedContent", StringComparison.OrdinalIgnoreCase) || HasNestedContent(property.Value)),
            JsonValueKind.Array => value.EnumerateArray().Any(static item => HasNestedContent(item)),
            _ => false,
        };

    private static EventStorePayloadProtectionMetadata Metadata(string reference) => new(PayloadProtectionState.Protected, 1, Scheme, reference, "application/json", null);
    private static PayloadProtectionCompletionContext Completion(InteractionProtectionBinding binding, PayloadProtectionOccurrenceContext occurrence)
        => new(binding.Identity.Owner, occurrence, binding.OperationId, binding.KeyReference, 1, binding.LifecycleEpoch);
    private static PayloadUnprotectionOutcome Mismatch(EventStorePayloadProtectionMetadata? metadata)
        => PayloadUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch, metadata);
    private static bool IsPlain(string format, EventStorePayloadProtectionMetadata? metadata)
        => format == "json" && (metadata is null || metadata == EventStorePayloadProtectionMetadata.Unprotected()
            || metadata == EventStorePayloadProtectionMetadataCarrier.Legacy());
    private static NotSupportedException MissingOccurrence() => new("Authenticated protection occurrence is required.");
    private static void ValidateOccurrence(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence,
        PayloadProtectionPayloadKind kind, string? type = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(occurrence);
        if (occurrence.RecordSequence == 0 || occurrence.PayloadKind != kind || string.IsNullOrWhiteSpace(occurrence.PayloadTypeId)
            || type is not null && occurrence.PayloadTypeId != type) { throw new ArgumentException("ProtectionOccurrenceMismatch"); }
    }
}

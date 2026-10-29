using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Mixed-root snapshot engine using unchanged V2 codecs independently for each protected value.</summary>
internal sealed class InteractionSnapshotEntriesProtector(IInteractionOccurrenceRegistry registry, IInteractionProtectionBindingProvider bindings,
    InteractionOccurrenceProtector protector)
{
    internal const string Format = "json+pdenc-v2-entries";
    internal static bool IsMixed(object state) => state is ProtectedSnapshotEntriesV2 || state is JsonElement element
        && element.ValueKind == JsonValueKind.Object && (element.TryGetProperty("Format", out var format) || element.TryGetProperty("format", out format))
        && format.ValueKind == JsonValueKind.String && format.GetString() == Format;

    internal async Task<SnapshotProtectionWriteResult> ProtectAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext parent,
        SnapshotEntryLayout layout, CancellationToken token)
    {
        var resolved = await ResolveAsync(owner, parent, layout.ContainerDigest, layout.Entries, true, token).ConfigureAwait(false);
        var entries = new List<ProtectedSnapshotEntryV2>(); var completions = new List<PayloadProtectionCompletionContext>();
        for (int index = 0; index < layout.Entries.Length; index++)
        {
            var binding = resolved[index]; var entry = layout.Entries[index];
            byte[] fragment = JsonSerializer.SerializeToUtf8Bytes(new SnapshotEntryFragment(layout.ContainerDigest, layout.Values[index]), InteractionSnapshotJsonContext.Default.SnapshotEntryFragment);
            try
            {
                var sealedValue = await protector.ProtectSnapshotAsync(fragment, binding.Identity, binding.DigestKeyVersion,
                    binding.ReservationAttemptOrdinal, binding.KeyReference, token).ConfigureAwait(false) ?? throw new InvalidOperationException("ProtectionAuthorityUnavailable");
                if (sealedValue.KeyReference != binding.KeyReference || sealedValue.ProtectedPathCount != 1) { throw new PayloadProtectionFormatException(); }
                var snapshot = JsonSerializer.Deserialize(sealedValue.PayloadBytes, InteractionSnapshotJsonContext.Default.ProtectedSnapshotPayloadV2) ?? throw new PayloadProtectionFormatException();
                entries.Add(new(entry.Path, entry.Target, binding.KeyReference, snapshot));
                completions.Add(Completion(binding));
            }
            finally { CryptographicOperations.ZeroMemory(fragment); }
        }
        var carrier = new ProtectedSnapshotEntriesV2(Format, parent.PayloadTypeId, layout.Skeleton.ToArray(), entries);
        var composite = completions[0] with { Occurrence = parent, SnapshotEntries = completions.ToArray(), SnapshotContainerDigest = layout.ContainerDigest,
            SnapshotEntryManifest = layout.Entries.ToArray() };
        var metadata = new EventStorePayloadProtectionMetadata(PayloadProtectionState.Protected, 1, "hexalith-pdenc-v2", composite.KeyReference, "application/json",
            new Dictionary<string, string> { ["snapshot-layout"] = "entries-v2" });
        return new(new(carrier, metadata), composite);
    }

    internal async Task<SnapshotUnprotectionOutcome> ReadAsync(AggregateIdentity owner, object state, EventStorePayloadProtectionMetadata? metadata,
        JsonTypeInfo type, PayloadProtectionOccurrenceContext parent, bool replay, CancellationToken token)
    {
        try
        {
            if (state is JsonElement transport) { RejectCaseDuplicates(transport, 0); }
            var carrier = state as ProtectedSnapshotEntriesV2 ?? ((JsonElement)state).Deserialize(InteractionSnapshotJsonContext.Default.ProtectedSnapshotEntriesV2) ?? throw new PayloadProtectionFormatException();
            if (carrier.Format != Format || carrier.SnapshotTypeId != parent.PayloadTypeId || carrier.Entries is not { Count: > 1 and <= 1000 }
                || carrier.SkeletonBytes is null || metadata is not { State: PayloadProtectionState.Protected, Scheme: "hexalith-pdenc-v2" })
            { return Unreadable(metadata); }
            var layout = SnapshotEntryLayout.Read(carrier.SkeletonBytes, owner.TenantId, true, token);
            if (layout.Entries.Length != carrier.Entries.Count || layout.Values.Any(value => value is not null)
                || !layout.Entries.SequenceEqual(carrier.Entries.Select(entry => new SnapshotProtectionEntry(entry.Path, entry.Target)))) { return Unreadable(metadata); }
            var resolved = await ResolveAsync(owner, parent, layout.ContainerDigest, layout.Entries, false, token).ConfigureAwait(false);
            if (metadata.KeyAlias != resolved[0].KeyReference) { return Unreadable(metadata); }
            var restored = JsonNode.Parse(layout.Skeleton) ?? throw new PayloadProtectionFormatException();
            for (int index = 0; index < resolved.Count; index++)
            {
                var binding = resolved[index]; var entry = carrier.Entries[index];
                var lost = await bindings.ResolveErasedStoredAsync(owner, InteractionSnapshotEntries.EntryOccurrence(parent, entry.Path),
                    JsonSerializer.SerializeToUtf8Bytes(entry.Protected, InteractionSnapshotJsonContext.Default.ProtectedSnapshotPayloadV2), entry.KeyReference, token).ConfigureAwait(false);
                if (lost is not null)
                {
                    if (lost != binding || !replay || !await bindings.IsIrreversiblyErasedAsync(binding, token).ConfigureAwait(false))
                    { return SnapshotUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted, metadata); }
                    SnapshotEntryLayout.Replace(restored, entry.Path, JsonSerializer.SerializeToNode(new Erased(), InteractionSnapshotJsonContext.Default.Erased)); continue;
                }
                var original = await registry.LookupAsync(binding.Identity, token).ConfigureAwait(false);
                if (entry.KeyReference != binding.KeyReference || original.Status != InteractionOccurrenceReservationStatus.Sealed
                    || original.Record is not { WriterState: InteractionOccurrenceWriterState.Active, Sealed: { } sealedValue } retained
                    || retained.Request.Identity != binding.Identity || retained.KeyReference != binding.KeyReference || retained.WriterProofId != binding.OperationId
                    || sealedValue.KeyReference != binding.KeyReference || sealedValue.SerializationFormat != "json+pdenc-v2"
                    || JsonSerializer.Deserialize(sealedValue.PayloadBytes, InteractionSnapshotJsonContext.Default.ProtectedSnapshotPayloadV2) != entry.Protected) { return Unreadable(metadata); }
                var opened = await protector.UnprotectAsync(binding.Identity, token).ConfigureAwait(false);
                if (!opened.IsReadable)
                {
                    if (!replay || !await bindings.IsIrreversiblyErasedAsync(binding, token).ConfigureAwait(false))
                    { return SnapshotUnprotectionOutcome.Unreadable(opened.UnreadableReason ?? UnreadableProtectedDataReason.ProviderUnavailable, metadata); }
                    SnapshotEntryLayout.Replace(restored, entry.Path, JsonSerializer.SerializeToNode(new Erased(), InteractionSnapshotJsonContext.Default.Erased)); continue;
                }
                try
                {
                    var fragment = JsonNode.Parse(opened.PayloadBytes!) as JsonObject ?? throw new PayloadProtectionFormatException();
                    if (fragment.Count != 2 || fragment["ContainerDigest"]?.GetValue<string>() != layout.ContainerDigest || !fragment.ContainsKey("Value")) { return Unreadable(metadata); }
                    SnapshotEntryLayout.Replace(restored, entry.Path, fragment["Value"]?.DeepClone());
                }
                finally { CryptographicOperations.ZeroMemory(opened.PayloadBytes!); }
            }
            byte[] clear = JsonSerializer.SerializeToUtf8Bytes(restored, InteractionSnapshotJsonContext.Default.JsonNode);
            try
            {
                // Authentication binds the historical bytes, not their ownership geometry. Reapply the
                // writer's bounded rules before exposing values that may predate those rules.
                var restoredLayout = SnapshotEntryLayout.Read(clear, owner.TenantId, false, token);
                if (!restoredLayout.Skeleton.AsSpan().SequenceEqual(layout.Skeleton) || !restoredLayout.Entries.SequenceEqual(layout.Entries))
                { return Unreadable(metadata); }
                object unprotected = JsonSerializer.Deserialize(clear, type) ?? throw new PayloadProtectionFormatException();
                token.ThrowIfCancellationRequested();
                return SnapshotUnprotectionOutcome.Readable(unprotected, metadata);
            }
            finally { CryptographicOperations.ZeroMemory(clear); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { token.ThrowIfCancellationRequested(); return SnapshotUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable, metadata); }
    }

    internal async Task<IReadOnlyList<InteractionProtectionBinding>> ResolveCompletionAsync(PayloadProtectionCompletionContext context, CancellationToken token)
    {
        if (context.SnapshotEntries is not { Length: > 1 and <= 1000 } children || context.SnapshotEntryManifest is not { Length: > 1 and <= 1000 } manifest
            || children.Length != manifest.Length || context.SnapshotContainerDigest is not { Length: 64 } digest || children.Any(child => child.SnapshotEntries is not null
                || child.SnapshotContainerDigest is not null || child.SnapshotEntryManifest is not null)) { throw new InvalidOperationException("ProtectionCompletionMismatch"); }
        var resolved = await ResolveAsync(context.Identity, context.Occurrence, digest, manifest, true, token).ConfigureAwait(false);
        for (int index = 0; index < resolved.Count; index++)
        { if (children[index] != Completion(resolved[index])) { throw new InvalidOperationException("ProtectionCompletionMismatch"); } }
        if (context.OperationId != children[0].OperationId || context.KeyReference != children[0].KeyReference || context.DekVersion != children[0].DekVersion
            || context.LifecycleEpoch != children[0].LifecycleEpoch) { throw new InvalidOperationException("ProtectionCompletionMismatch"); }
        return resolved;
    }
    private async Task<IReadOnlyList<InteractionProtectionBinding>> ResolveAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext parent, string digest,
        IReadOnlyList<SnapshotProtectionEntry> entries, bool writing, CancellationToken token)
    {
        var resolved = await bindings.ResolveSnapshotEntriesAsync(owner, parent, digest, entries, writing, token).ConfigureAwait(false);
        if (resolved is null || resolved.Count != entries.Count || entries.Count is < 2 or > 1000 || entries.Select(entry => entry.Path).Distinct(StringComparer.Ordinal).Count() != entries.Count)
        { throw new InvalidOperationException("ProtectionAuthorityUnavailable"); }
        for (int index = 0; index < entries.Count; index++)
        {
            var expected = InteractionSnapshotEntries.EntryOccurrence(parent, entries[index].Path); var binding = resolved[index];
            if (binding.Identity.Owner != owner || binding.Identity.Target != entries[index].Target || entries[index].Target.TenantId != owner.TenantId
                || binding.Identity.Sequence != expected.RecordSequence || binding.Identity.Kind != PayloadProtectionPayloadKind.Snapshot || binding.Identity.PayloadTypeId != expected.PayloadTypeId
                || !CanonicalUlid.IsValid(binding.KeyReference) || !CanonicalUlid.IsValid(binding.OperationId)) { throw new InvalidOperationException("ProtectionAuthorityUnavailable"); }
        }
        return resolved;
    }
    private static PayloadProtectionCompletionContext Completion(InteractionProtectionBinding binding) => new(binding.Identity.Owner,
        new(binding.Identity.Sequence, binding.Identity.Kind, binding.Identity.PayloadTypeId), binding.OperationId, binding.KeyReference, 1, binding.LifecycleEpoch);
    private static void RejectCaseDuplicates(JsonElement element, int depth)
    {
        if (depth > 64) { throw new PayloadProtectionFormatException(); }
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            { if (!names.Add(property.Name)) { throw new PayloadProtectionFormatException(); } RejectCaseDuplicates(property.Value, depth + 1); }
        }
        else if (element.ValueKind == JsonValueKind.Array) { foreach (var child in element.EnumerateArray()) { RejectCaseDuplicates(child, depth + 1); } }
    }
    private static SnapshotUnprotectionOutcome Unreadable(EventStorePayloadProtectionMetadata? metadata)
        => SnapshotUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch, metadata);
}

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Real mixed-entry encryption with separately retained synthetic occurrence and custody authorities; no installed qualification.</summary>
internal sealed class InteractionMixedSnapshotFixture
{
    private readonly InteractionOccurrenceProtectorFixture[] _originals;

    /// <summary>Gets the independently fixed directory owner.</summary>
    internal AggregateIdentity Owner { get; } = new("tenant-a", "agents", "directory");

    /// <summary>Gets the original parent occurrence.</summary>
    internal PayloadProtectionOccurrenceContext Occurrence { get; } = new(77, PayloadProtectionPayloadKind.Snapshot, "hx-snapshot-v1:directory");

    /// <summary>Gets the exact source state containing two interaction roots.</summary>
    internal JsonElement Source { get; } = JsonSerializer.Deserialize<JsonElement>("""
        {"Safe":7,"Entries":[
          {"ProtectedContent":{"Target":{"TenantId":"tenant-a","AgentInteractionId":"interaction-a","TargetProtectionKeyAlias":"alias-a"},"Value":{"Secret":"secret-a"}}},
          {"ProtectedContent":{"Target":{"TenantId":"tenant-a","AgentInteractionId":"interaction-b","TargetProtectionKeyAlias":"alias-b"},"Value":{"Secret":"secret-b"}}}
        ]}
        """);

    /// <summary>Gets the JSON state metadata.</summary>
    internal JsonTypeInfo StateType { get; } = JsonSerializerOptions.Default.GetTypeInfo(typeof(JsonElement));

    /// <summary>Gets the actual shipped engine.</summary>
    internal InteractionPayloadProtectionService Service { get; }

    /// <summary>Gets the independently retained synthetic binding and erasure authority.</summary>
    internal IInteractionProtectionBindingProvider Authority { get; } = Substitute.For<IInteractionProtectionBindingProvider>();

    /// <summary>Gets the fixed original bindings.</summary>
    internal InteractionProtectionBinding[] Bindings { get; }

    /// <summary>Gets independently retained occurrence records.</summary>
    internal IReadOnlyList<InteractionOccurrenceRecord> Records => _originals.Select(original => original.Record!).ToArray();

    /// <summary>Gets the reader-owned key arrays for zeroing checks.</summary>
    internal List<byte[]> OwnedBuffers { get; } = [];

    /// <summary>Creates exact synthetic authorities around the real occurrence codecs and engine.</summary>
    internal InteractionMixedSnapshotFixture()
    {
        var layout = SnapshotEntryLayout.Read(JsonSerializer.SerializeToUtf8Bytes(Source), Owner.TenantId, false, CancellationToken.None);
        string[] references = ["01ARZ3NDEKTSV4RRFFQ69G5FAV", "01ARZ3NDEKTSV4RRFFQ69G5FAW"];
        _originals = layout.Entries.Select(_ => new InteractionOccurrenceProtectorFixture()).ToArray();
        Bindings = layout.Entries.Select((entry, index) => new InteractionProtectionBinding(_originals[index].Identity with
        {
            Target = entry.Target,
            Owner = Owner,
            Kind = PayloadProtectionPayloadKind.Snapshot,
            Sequence = Occurrence.RecordSequence,
            PayloadTypeId = InteractionSnapshotEntries.EntryOccurrence(Occurrence, entry.Path).PayloadTypeId,
        }, "digest-v1", 1, references[index], references[index], 1)).ToArray();

        InteractionOccurrenceProtectorFixture Original(InteractionOccurrenceIdentity identity)
            => _originals[Array.FindIndex(Bindings, binding => binding.Identity == identity)];
        var registry = Substitute.For<IInteractionOccurrenceRegistry>();
        registry.ReserveAsync(Arg.Any<InteractionOccurrenceRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            Original(call.ArgAt<InteractionOccurrenceRequest>(0).Identity).Registry.ReserveAsync(call.ArgAt<InteractionOccurrenceRequest>(0), call.ArgAt<CancellationToken>(1)));
        registry.RetainSealedAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<InteractionOccurrenceSealedResult>(), Arg.Any<CancellationToken>()).Returns(call =>
            Original(call.ArgAt<InteractionOccurrenceIdentity>(0)).Registry.RetainSealedAsync(call.ArgAt<InteractionOccurrenceIdentity>(0), call.ArgAt<InteractionOccurrenceSealedResult>(1), call.ArgAt<CancellationToken>(2)));
        registry.CompleteWriterAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call =>
            Original(call.ArgAt<InteractionOccurrenceIdentity>(0)).Registry.CompleteWriterAsync(call.ArgAt<InteractionOccurrenceIdentity>(0), call.ArgAt<string>(1), call.ArgAt<string>(2), call.ArgAt<bool>(3), call.ArgAt<CancellationToken>(4)));
        registry.LookupAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<CancellationToken>()).Returns(call =>
            Original(call.ArgAt<InteractionOccurrenceIdentity>(0)).Registry.LookupAsync(call.ArgAt<InteractionOccurrenceIdentity>(0), call.ArgAt<CancellationToken>(1)));
        var authority = Authority;
        authority.ResolveSnapshotEntriesAsync(Owner, Occurrence, layout.ContainerDigest, Arg.Any<IReadOnlyList<SnapshotProtectionEntry>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyList<SnapshotProtectionEntry>>(3).SequenceEqual(layout.Entries) ? Bindings : null);
        authority.ResolveAsync(Owner, Arg.Any<PayloadProtectionOccurrenceContext>(), null, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => Bindings.SingleOrDefault(binding => new PayloadProtectionOccurrenceContext(binding.Identity.Sequence, binding.Identity.Kind, binding.Identity.PayloadTypeId) == call.ArgAt<PayloadProtectionOccurrenceContext>(1)));
        authority.AcquireSnapshotCompletionLeasesAsync(Arg.Any<IReadOnlyList<InteractionProtectionBinding>>(), Arg.Any<CancellationToken>()).Returns(true);
        var keys = new InteractionOccurrenceTestKeys
        {
            Allocate = (identity, purpose, version, seed) =>
            {
                byte[] bytes = Enumerable.Repeat(seed, 32).ToArray(); OwnedBuffers.Add(bytes);
                return new(identity.Target, purpose, version, bytes);
            },
        };
        Service = new(registry, keys, authority, TimeProvider.System);
    }

    /// <summary>Seals an authenticated historical entry value directly through the real occurrence protector, retaining the original binding and container.</summary>
    internal async Task<SnapshotProtectionResult> HistoricalAsync(JsonElement firstValue, CancellationToken token)
    {
        var layout = SnapshotEntryLayout.Read(JsonSerializer.SerializeToUtf8Bytes(Source), Owner.TenantId, false, token);
        for (int index = 0; index < Bindings.Length; index++)
        {
            var binding = Bindings[index];
            byte[] clear = JsonSerializer.SerializeToUtf8Bytes(new
            {
                layout.ContainerDigest,
                Value = index == 0 ? firstValue : JsonSerializer.SerializeToElement(layout.Values[index]),
            });
            try
            {
                await _originals[index].Adapter.ProtectSnapshotAsync(clear, binding.Identity, binding.DigestKeyVersion, 1, binding.KeyReference, token);
                await _originals[index].Adapter.CompleteWriterAsync(binding.Identity, binding.KeyReference, binding.OperationId, true, token);
            }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(clear); }
        }
        var carrier = new ProtectedSnapshotEntriesV2("json+pdenc-v2-entries", Occurrence.PayloadTypeId, layout.Skeleton,
            layout.Entries.Select((entry, index) => new ProtectedSnapshotEntryV2(entry.Path, entry.Target, Bindings[index].KeyReference,
                JsonSerializer.Deserialize<ProtectedSnapshotPayloadV2>(_originals[index].Record!.Sealed!.PayloadBytes)!)).ToArray());
        return new(carrier, new(PayloadProtectionState.Protected, 1, "hexalith-pdenc-v2", Bindings[0].KeyReference, "application/json", null));
    }
}

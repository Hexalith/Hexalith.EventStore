using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.PayloadProtection.Tests;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Independently fixed two-target bindings around real snapshot encryption and retained synthetic occurrence state; no installed qualification.</summary>
internal sealed class InteractionMixedSnapshotPersistenceFixture
{
    private readonly InteractionOccurrenceProtectorFixture[] _originals = [new(), new()];

    /// <summary>Gets the original directory owner.</summary>
    internal AggregateIdentity Owner { get; } = new("tenant-a", "agents", "directory");

    /// <summary>Gets the exact Server snapshot occurrence.</summary>
    internal PayloadProtectionOccurrenceContext Occurrence { get; } = new(1, PayloadProtectionPayloadKind.Snapshot, "hx-snapshot-v1:folded-state");

    /// <summary>Gets the source state with content belonging to two distinct roots.</summary>
    internal JsonElement Source { get; } = JsonSerializer.Deserialize<JsonElement>("""
        {"Safe":7,"Entries":[
          {"ProtectedContent":{"Target":{"TenantId":"tenant-a","AgentInteractionId":"interaction-a","TargetProtectionKeyAlias":"alias-a"},"Value":{"Secret":"secret-a"}}},
          {"ProtectedContent":{"Target":{"TenantId":"tenant-a","AgentInteractionId":"interaction-b","TargetProtectionKeyAlias":"alias-b"},"Value":{"Secret":"secret-b"}}}
        ]}
        """);

    /// <summary>Gets the independently fixed technical skeleton.</summary>
    internal byte[] Skeleton { get; } = """
        {"Safe":7,"Entries":[{"ProtectedContent":{"Target":{"TenantId":"tenant-a","AgentInteractionId":"interaction-a","TargetProtectionKeyAlias":"alias-a"},"Value":null}},{"ProtectedContent":{"Target":{"TenantId":"tenant-a","AgentInteractionId":"interaction-b","TargetProtectionKeyAlias":"alias-b"},"Value":null}}]}
        """u8.ToArray();

    /// <summary>Gets the independently fixed complete entry manifest.</summary>
    internal SnapshotProtectionEntry[] Entries { get; } =
    [
        new("/Entries/0/ProtectedContent/Value", new("tenant-a", "interaction-a", "alias-a")),
        new("/Entries/1/ProtectedContent/Value", new("tenant-a", "interaction-b", "alias-b")),
    ];

    /// <summary>Gets the fixed original bindings.</summary>
    internal InteractionProtectionBinding[] Bindings { get; }

    /// <summary>Gets the actual shipped hook engine.</summary>
    internal InteractionPayloadProtectionService Service { get; }

    /// <summary>Gets the tracked synthetic custody provider.</summary>
    internal InteractionOccurrenceTestKeys Keys { get; } = new();

    /// <summary>Gets the retained occurrence records.</summary>
    internal IReadOnlyList<InteractionOccurrenceRecord> Records => _originals.Select(original => original.Record!).ToArray();

    /// <summary>Gets transferred key buffers for zeroing checks.</summary>
    internal IEnumerable<byte[]> OwnedBuffers => _originals.SelectMany(original => original.OwnedBuffers);

    /// <summary>Gets or sets the independently proved first-root tombstone.</summary>
    internal bool EraseFirst { get; set; }

    /// <summary>Creates fixed exact manifest enrollment and independent synthetic custody/registry authorities.</summary>
    internal InteractionMixedSnapshotPersistenceFixture()
    {
        string[] references = ["01ARZ3NDEKTSV4RRFFQ69G5FAV", "01ARZ3NDEKTSV4RRFFQ69G5FAW"];
        Bindings = Entries.Select((entry, index) => new InteractionProtectionBinding(_originals[index].Identity with
        {
            Target = entry.Target,
            Owner = Owner,
            Kind = PayloadProtectionPayloadKind.Snapshot,
            Sequence = Occurrence.RecordSequence,
            PayloadTypeId = InteractionSnapshotEntries.EntryOccurrence(Occurrence, entry.Path).PayloadTypeId,
        }, "digest-v1", 1, references[index], references[index], 1)).ToArray();
        var registry = Substitute.For<IInteractionOccurrenceRegistry>();
        registry.ReserveAsync(Arg.Any<InteractionOccurrenceRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            Original(call.ArgAt<InteractionOccurrenceRequest>(0).Identity).Registry.ReserveAsync(call.ArgAt<InteractionOccurrenceRequest>(0), call.ArgAt<CancellationToken>(1)));
        registry.RetainSealedAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<InteractionOccurrenceSealedResult>(), Arg.Any<CancellationToken>()).Returns(call =>
            Original(call.ArgAt<InteractionOccurrenceIdentity>(0)).Registry.RetainSealedAsync(call.ArgAt<InteractionOccurrenceIdentity>(0), call.ArgAt<InteractionOccurrenceSealedResult>(1), call.ArgAt<CancellationToken>(2)));
        registry.CompleteWriterAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call =>
            Original(call.ArgAt<InteractionOccurrenceIdentity>(0)).Registry.CompleteWriterAsync(call.ArgAt<InteractionOccurrenceIdentity>(0), call.ArgAt<string>(1), call.ArgAt<string>(2), call.ArgAt<bool>(3), call.ArgAt<CancellationToken>(4)));
        registry.LookupAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<CancellationToken>()).Returns(call =>
            Original(call.ArgAt<InteractionOccurrenceIdentity>(0)).Registry.LookupAsync(call.ArgAt<InteractionOccurrenceIdentity>(0), call.ArgAt<CancellationToken>(1)));
        var authority = Substitute.For<IInteractionProtectionBindingProvider>();
        authority.ResolveSnapshotEntriesAsync(Owner, Occurrence, Convert.ToHexString(SHA256.HashData(Skeleton)),
            Arg.Is<IReadOnlyList<SnapshotProtectionEntry>>(entries => entries.SequenceEqual(Entries)), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Bindings);
        authority.ResolveAsync(Owner, Arg.Any<PayloadProtectionOccurrenceContext>(), null, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call =>
            Bindings.SingleOrDefault(binding => new PayloadProtectionOccurrenceContext(binding.Identity.Sequence, binding.Identity.Kind, binding.Identity.PayloadTypeId)
                == call.ArgAt<PayloadProtectionOccurrenceContext>(1)));
        authority.AcquireSnapshotCompletionLeasesAsync(Arg.Is<IReadOnlyList<InteractionProtectionBinding>>(entries => entries.SequenceEqual(Bindings)),
            Arg.Any<CancellationToken>()).Returns(true);
        authority.ResolveErasedStoredAsync(Owner, InteractionSnapshotEntries.EntryOccurrence(Occurrence, Entries[0].Path),
            Arg.Any<byte[]>(), references[0], Arg.Any<CancellationToken>()).Returns(call => EraseFirst
                && _originals[0].Record?.Sealed is { } original && call.ArgAt<byte[]>(2).SequenceEqual(original.PayloadBytes) ? Bindings[0] : null);
        authority.IsIrreversiblyErasedAsync(Bindings[0], Arg.Any<CancellationToken>()).Returns(_ => EraseFirst);
        Keys.Allocate = (identity, purpose, version, seed) => Original(identity).Owned(identity, purpose, version, seed);
        Keys.RootResolver = identity => Task.FromResult<InteractionOccurrenceOwnedKey?>(EraseFirst && identity == Bindings[0].Identity
            ? null : Original(identity).Owned(identity, "interaction-root", identity.RootKeyVersion, 9));
        Service = new(registry, Keys, authority, TimeProvider.System);
    }

    private InteractionOccurrenceProtectorFixture Original(InteractionOccurrenceIdentity identity)
        => _originals[Array.FindIndex(Bindings, binding => binding.Identity == identity)];
}

using System.Security.Cryptography;
using System.Text.Json;

namespace P1R.Counter;

/// <summary>Measures detached state round-trip and canonical increment parity before fixture enrollment.</summary>
internal static class CounterSnapshotParity
{
    /// <summary>Returns measured operands and hashes, refusing enrollment if parity fails.</summary>
    internal static object Measure()
    {
        CounterState original = new() { Count = 9 };
        byte[] stateBytes = JsonSerializer.SerializeToUtf8Bytes(original, JsonSerializerOptions.Web);
        CounterState detached = JsonSerializer.Deserialize<CounterState>(stateBytes, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Counter state round-trip returned null.");
        int roundtripCount = detached.Count;
        CounterIncremented domainEvent = (CounterIncremented)CounterAggregate
            .Handle(new IncrementCounter(), original).Events.Single();
        byte[] eventBytes = JsonSerializer.SerializeToUtf8Bytes(domainEvent, JsonSerializerOptions.Web);
        CounterIncremented replayedEvent = JsonSerializer.Deserialize<CounterIncremented>(eventBytes, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Counter event round-trip returned null.");
        bool stateRoundtrip = detached.Count == original.Count && !ReferenceEquals(original, detached);
        bool canonicalEventParity = eventBytes.AsSpan().SequenceEqual("{}"u8);
        detached.Apply(replayedEvent);
        bool detachedApply = original.Count == 9 && detached.Count == 10;
        if (!stateRoundtrip || !canonicalEventParity || !detachedApply)
        {
            throw new InvalidOperationException("Counter snapshot fixture parity failed.");
        }

        return new
        {
            registered = true,
            test_only = true,
            profile_id = FixtureSnapshotQualification.ProfileId,
            parity_evidence_id = FixtureSnapshotQualification.ParityEvidenceId,
            state_roundtrip = stateRoundtrip,
            canonical_event_parity = canonicalEventParity,
            detached_apply = detachedApply,
            original_count = original.Count,
            roundtrip_count = roundtripCount,
            applied_count = detached.Count,
            state_json = JsonSerializer.Deserialize<JsonElement>(stateBytes),
            state_sha256 = Convert.ToHexStringLower(SHA256.HashData(stateBytes)),
            canonical_event_sha256 = Convert.ToHexStringLower(SHA256.HashData(eventBytes)),
            limits = FixtureSnapshotQualification.Limits,
        };
    }
}

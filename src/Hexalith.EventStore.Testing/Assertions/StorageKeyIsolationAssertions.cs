namespace Hexalith.EventStore.Testing.Assertions;

using Hexalith.EventStore.Contracts.Identity;

using Shouldly;

/// <summary>
/// Static assertion helpers for verifying storage key isolation between tenants.
/// These are test-only utilities -- NOT production runtime guards.
/// Isolation is guaranteed by construction (AggregateIdentity validation), not by runtime checking.
/// </summary>
public static class StorageKeyIsolationAssertions
{
    /// <summary>
    /// Asserts that the given key starts with the expected tenant prefix ("{expectedTenant}:").
    /// </summary>
    /// <param name="key">The state store key to verify.</param>
    /// <param name="expectedTenant">The expected tenant identifier.</param>
    public static void AssertKeyBelongsToTenant(string key, string expectedTenant)
    {
        key.ShouldNotBeNullOrEmpty();
        expectedTenant.ShouldNotBeNullOrEmpty();

        string expectedPrefix = $"{expectedTenant}:";
        key.ShouldStartWith(expectedPrefix);
    }

    /// <summary>
    /// Asserts that two keys are structurally disjoint -- they share no common prefix up to the first segment (tenant).
    /// Two keys are disjoint if their first colon-delimited segment differs.
    /// </summary>
    /// <param name="keyA">The first key.</param>
    /// <param name="keyB">The second key.</param>
    public static void AssertKeysDisjoint(string keyA, string keyB)
    {
        keyA.ShouldNotBeNullOrEmpty();
        keyB.ShouldNotBeNullOrEmpty();

        string tenantA = GetFirstSegment(keyA);
        string tenantB = GetFirstSegment(keyB);

        tenantA.ShouldNotBe(tenantB);
    }

    /// <summary>
    /// Validates that a full event stream key matches the expected structure derived from the given identity.
    /// Verifies the key matches the pattern: {tenant}:{domain}:{aggId}:events:{seq}.
    /// </summary>
    /// <param name="key">The event stream key to validate.</param>
    /// <param name="identity">The AggregateIdentity that should have produced this key.</param>
    public static void AssertEventStreamKey(string key, AggregateIdentity identity)
    {
        key.ShouldNotBeNullOrEmpty();
        identity.ShouldNotBeNull();

        key.ShouldStartWith(identity.EventStreamKeyPrefix);

        // Verify the portion after the prefix is a valid sequence number
        string sequencePart = key[identity.EventStreamKeyPrefix.Length..];
        long.TryParse(sequencePart, out long seq)
            .ShouldBeTrue($"Expected numeric sequence number after prefix, got '{sequencePart}'.");
        seq.ShouldBeGreaterThan(0L);
    }

    private static string GetFirstSegment(string key)
    {
        int colonIndex = key.IndexOf(':');
        return colonIndex >= 0 ? key[..colonIndex] : key;
    }
}

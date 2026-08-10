namespace Hexalith.EventStore.Server.Actors;

/// <summary>Builds the canonical protected admission actor identifier.</summary>
internal static class IdempotencyAdmissionActorIdentity
{
    public static string Build(string tenant, string digestKeyVersion, string keyDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(digestKeyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyDigest);
        return string.Concat(tenant, ":", digestKeyVersion, ":", keyDigest);
    }
}

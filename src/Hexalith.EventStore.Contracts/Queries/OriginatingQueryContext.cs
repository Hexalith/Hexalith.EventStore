using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>Immutable originating identity captured from a verified query, separate from its owner-query hop. Untrusted until the receiving SDK verifies the envelope digest.</summary>
[DataContract]
[KnownType(typeof(string[]))]
public sealed record OriginatingQueryContext
{
    private readonly string[]? _scopes;
    private readonly string[]? _audience;

    /// <summary>Captures the authenticated original query's identity without retaining mutable collections.</summary>
    /// <param name="originalActorId">The authenticated original sub.</param>
    /// <param name="authenticatedWorkloadId">The original azp/client_id/aud workload resolution, if present.</param>
    /// <param name="isDelegated">The original authenticated delegation signal.</param>
    /// <param name="delegationId">The original authenticated act.sub, if present.</param>
    /// <param name="scopes">The original scope/scp values, preserving absence.</param>
    /// <param name="audience">The original aud values, preserving absence.</param>
    [JsonConstructor]
    public OriginatingQueryContext(string originalActorId, string? authenticatedWorkloadId, bool isDelegated,
        string? delegationId, IReadOnlyList<string>? scopes, IReadOnlyList<string>? audience)
    {
        OriginalActorId = originalActorId;
        AuthenticatedWorkloadId = authenticatedWorkloadId;
        IsDelegated = isDelegated;
        DelegationId = delegationId;
        Scopes = scopes;
        Audience = audience;
    }

    /// <summary>Gets the authenticated original actor.</summary>
    [DataMember]
    public string OriginalActorId { get; init; }
    /// <summary>Gets the authenticated original workload, when present.</summary>
    [DataMember]
    public string? AuthenticatedWorkloadId { get; init; }
    /// <summary>Gets the original delegation signal.</summary>
    [DataMember]
    public bool IsDelegated { get; init; }
    /// <summary>Gets the authenticated original act.sub, when present.</summary>
    [DataMember]
    public string? DelegationId { get; init; }
    /// <summary>Gets a defensive copy of the original scopes, preserving absence.</summary>
    [DataMember]
    public IReadOnlyList<string>? Scopes { get => _scopes?.ToArray(); init => _scopes = value?.ToArray(); }
    /// <summary>Gets a defensive copy of the original audiences, preserving absence.</summary>
    [DataMember]
    public IReadOnlyList<string>? Audience { get => _audience?.ToArray(); init => _audience = value?.ToArray(); }

    /// <summary>Compares the originating context by ordinal sequence content, including absent collections.</summary>
    public bool Equals(OriginatingQueryContext? other) => other is not null
        && OriginalActorId == other.OriginalActorId && AuthenticatedWorkloadId == other.AuthenticatedWorkloadId
        && IsDelegated == other.IsDelegated && DelegationId == other.DelegationId
        && Same(_scopes, other._scopes) && Same(_audience, other._audience);
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(OriginalActorId, StringComparer.Ordinal);
        hash.Add(AuthenticatedWorkloadId, StringComparer.Ordinal);
        hash.Add(IsDelegated);
        hash.Add(DelegationId, StringComparer.Ordinal);
        Add(ref hash, _scopes);
        Add(ref hash, _audience);
        return hash.ToHashCode();
    }

    private static bool Same(string[]? left, string[]? right) => left is null || right is null
        ? left is null && right is null : left.SequenceEqual(right, StringComparer.Ordinal);
    private static void Add(ref HashCode hash, string[]? values)
    {
        hash.Add(values?.Length ?? -1);
        if (values is not null) foreach (string value in values) hash.Add(value, StringComparer.Ordinal);
    }
}

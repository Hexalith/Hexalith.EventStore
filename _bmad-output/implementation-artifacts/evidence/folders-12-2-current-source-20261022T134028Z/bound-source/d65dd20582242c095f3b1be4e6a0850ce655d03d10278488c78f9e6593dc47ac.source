
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Request sent to a domain service's /project endpoint with per-aggregate granularity.
/// Contains the aggregate identity and the events to project.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="Events">The events to project, in sequence order.</param>
public record ProjectionRequest(
    string TenantId,
    string Domain,
    string AggregateId,
    ProjectionEventDto[] Events) {
    private NativeSourceProof? _verifiedNativeSourceProof;
    private byte[]? _verifiedNativeRequestDigest;

    /// <summary>Gets the producer's original native prefix hint; transport binding alone supplies no authority.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSourceProof? NativeSourceProof { get; init; }

    /// <summary>Gets native evidence only while this exact request still matches SDK verification before evolution.</summary>
    [JsonIgnore]
    public NativeSourceProof? VerifiedNativeSourceProof => _verifiedNativeRequestDigest is not null
        && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(_verifiedNativeRequestDigest, NativeRequestDigest())
            ? _verifiedNativeSourceProof : null;

    /// <summary>Binds verified evidence to this exact immutable input snapshot.</summary>
    internal void AdmitNativeSourceProof(NativeSourceProof proof)
    {
        _verifiedNativeSourceProof = proof;
        _verifiedNativeRequestDigest = NativeRequestDigest();
    }

    /// <summary>Removes all inherited private admission before a new verification attempt.</summary>
    internal void ClearNativeSourceAdmission()
    {
        _verifiedNativeSourceProof = null;
        _verifiedNativeRequestDigest = null;
    }

    private byte[] NativeRequestDigest()
    {
        // Hash bounded metadata and raw payload segments separately. Encoding the whole request
        // as JSON would allocate another complete base64 prefix on every mutation check.
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        void Append(byte[]? bytes)
        {
            Span<byte> length = stackalloc byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes?.Length ?? -1);
            hash.AppendData(length);
            if (bytes is not null) hash.AppendData(bytes);
        }
        Append(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this with
        { Events = [], EventEvolutionProof = null, VerifiedEffectiveEvents = null }, System.Text.Json.JsonSerializerOptions.Web));
        Append(EventEvolutionProof);
        // Evolution hints cannot acquire native admission; mutation still changes the digest.
        if (VerifiedEffectiveEvents is not null) Append([1]);
        foreach (ProjectionEventDto item in Events)
        {
            Append(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(item with
            { Payload = [], EffectivePayload = null, StoredDigest = null }, System.Text.Json.JsonSerializerOptions.Web));
            Append(item.Payload);
            Append(item.EffectivePayload);
            Append(item.StoredDigest);
        }
        return hash.GetHashAndReset();
    }

    /// <summary>Gets untrusted effective-view transport hints that require verification before handler dispatch.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VerifiedEffectiveEventView[]? VerifiedEffectiveEvents { get; init; }

    /// <summary>Gets the untrusted event-evolution proof hint that requires route and complete-prefix verification.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? EventEvolutionProof { get; init; }

    /// <summary>
    /// Gets the owner stream head observed by EventStore while preparing a mapped full replay.
    /// This transport value is admitted only on the protected rebuild route; handlers must still
    /// compare it with the supplied contiguous owner prefix and selected read model.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? RebuildSourceHeadSequence { get; init; }

    /// <summary>Gets the frozen persisted head identity for an explicitly guarded staged replay.</summary>
    [JsonIgnore]
    internal string? RebuildSourceHeadMessageId { get; init; }

    /// <summary>Gets the admitted selected generations that a guarded immutable replay manifest must preserve.</summary>
    [JsonIgnore]
    internal IReadOnlyDictionary<string, string>? RebuildOwnerGenerations { get; init; }

    /// <summary>Gets the exact admitted owner identities paired with guarded replay generations.</summary>
    [JsonIgnore]
    internal IReadOnlyDictionary<string, string>? RebuildOwnerIdentities { get; init; }

    /// <summary>Gets the private compact attestation of the complete admitted owner bindings.</summary>
    [JsonIgnore]
    internal string? RebuildOwnerBindingFingerprint { get; init; }

    /// <summary>
    /// Gets the aggregate actor's completed-erasure observation for a protected, empty full replay.
    /// The mapped rebuild route accepts this only with an empty prefix and a zero source head.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RebuildSourceErased { get; init; }
}

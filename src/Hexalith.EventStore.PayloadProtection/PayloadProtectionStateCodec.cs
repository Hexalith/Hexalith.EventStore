// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10.2; amendment sections 1-2 and 5-6; approved review correction section 8.
// Approved Dapr Kubernetes cryptography supplement: 14eb081c36c1122b337c75ee1d341afc7db0a01ea107ed83596817ddcfd33881; sections 5-6.
using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Encodes explicitly enrolled closed state records and verifies their semantic integrity.</summary>
internal static class PayloadProtectionStateCodec
{
    /// <summary>Gets the maximum UTF-8 size of one durable value.</summary>
    internal const int MaximumBytes = 65_536;
    private static readonly FrozenDictionary<Type, FrozenDictionary<string, string>> FieldProfiles = PayloadProtectionStateSchema.Fields
        .ToFrozenDictionary(schema => schema.Key, schema => schema.Value.Split(' ')
            .Select(field => field.Split(':', 2)).ToFrozenDictionary(field => field[0], field => field[1], StringComparer.Ordinal));
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
    private static readonly HashSet<string> ErasureReasons = new(StringComparer.Ordinal)
    {
        "none", "domain-pending", "domain-invalidating", "domain-invalidated", "domain-deleted", "state-unavailable",
        "state-denied", "state-unknown", "epoch-invalid", "epoch-regressed",
    };
    private static readonly HashSet<string> Reasons = new(ErasureReasons, StringComparer.Ordinal)
    {
        "missing-key", "key-invalidated", "provider-unavailable", "provider-denied", "consistency-mismatch",
        "malformed-metadata", "unknown-metadata-version", "provider-opaque-unsupported", "bytes-metadata-mismatch",
        "cancelled", "not-persisted", "ambiguous-outcome", "orphan-quarantined", "fingerprint-collision-observed",
    };

    /// <summary>Writes schemaVersion first, ordered camelCase members and no insignificant whitespace.</summary>
    internal static byte[] Encode<T>(T record, PayloadProtectionAuthorityProfile? authorityProfile = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(record);
        // A V2 key travels through callers typed as its V1 base; its own closed metadata and profile follow its runtime schema.
        bool v2 = record is WrappedDekV2;
        byte[] bytes = v2 ? JsonSerializer.SerializeToUtf8Bytes(record, Metadata(typeof(WrappedDekV2)))
            : JsonSerializer.SerializeToUtf8Bytes(record, Metadata<T>());
        _ = Decode<T>(bytes, authorityProfile ?? (v2 ? PayloadProtectionAuthorityProfile.DaprKubernetesCrypto : null));
        return bytes;
    }

    /// <summary>Validates every member before deserializing, accepting legal JSON member reordering.</summary>
    internal static T Decode<T>(ReadOnlyMemory<byte> bytes, PayloadProtectionAuthorityProfile? authorityProfile = null) where T : class
    {
        try
        {
            if (bytes.Length is < 2 or > MaximumBytes)
            {
                throw new PayloadProtectionFormatException();
            }

            CanonicalText.ValidateUtf8(bytes.Span);
            PayloadProtectionAuthorityProfile authority = authorityProfile ?? PayloadProtectionAuthorityProfile.Predecessor;
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            Type schema = Schema(typeof(T), document.RootElement, authority);
            ValidateObject(document.RootElement, schema, root: true);
            T record = JsonSerializer.Deserialize(bytes.Span, Metadata(schema)) as T ?? throw new PayloadProtectionFormatException();
            ValidateSemantics(record, authority);
            return record;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new PayloadProtectionFormatException();
        }
    }

    /// <summary>Recomputes both corruption checks after a wrapped-key record transition.</summary>
    internal static WrappedDekV1 Seal(WrappedDekV1 record)
    {
        byte[] wrapped = record is WrappedDekV2 ? DecodeEnvelope(record.WrappedDek) : DecodeWrapped(record.WrappedDek);
        try
        {
            record = record with { WrappedDekSha256 = Convert.ToHexStringLower(SHA256.HashData(wrapped)) };
            return record with { CanonicalFieldsSha256 = FieldsHash(record) };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrapped);
        }
    }

    /// <summary>Creates an unsealed draft of the backend-declared closed key schema; it never converts an existing record.</summary>
    internal static WrappedDekV1 CreateKey(int schemaVersion, string tenantId, string domain, string aggregateId, string scopeDigest,
        string keyRef, uint dekVersion, string state, string backendId, string providerKeyVersionId, string wrapAlgorithmId,
        string wrappedDek, ulong lifecycleEpoch, ulong fenceApprovalEpoch, ulong highestWrittenEpoch, string creatingOperationId,
        string createdAtUtc, string updatedAtUtc) => schemaVersion switch
        {
            1 => new WrappedDekV1(tenantId, domain, aggregateId, scopeDigest, keyRef, dekVersion, state, backendId, providerKeyVersionId,
                wrapAlgorithmId, wrappedDek, lifecycleEpoch, fenceApprovalEpoch, highestWrittenEpoch, creatingOperationId, createdAtUtc,
                updatedAtUtc, new('0', 64), new('0', 64)),
            2 => new WrappedDekV2(tenantId, domain, aggregateId, scopeDigest, keyRef, dekVersion, state, backendId, providerKeyVersionId,
                wrapAlgorithmId, wrappedDek, lifecycleEpoch, fenceApprovalEpoch, highestWrittenEpoch, creatingOperationId, createdAtUtc,
                updatedAtUtc, new('0', 64), new('0', 64)),
            _ => throw new PayloadProtectionFormatException(),
        };

    /// <summary>Formats canonical UTC timestamps with exactly seven fractional digits.</summary>
    internal static string Timestamp(DateTimeOffset value) => value.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);

    /// <summary>Parses only the exact durable timestamp spelling.</summary>
    internal static DateTimeOffset ParseTimestamp(string value)
    {
        if (!DateTimeOffset.TryParseExact(value, TimestampFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed)
            || Timestamp(parsed) != value)
        {
            throw new PayloadProtectionFormatException();
        }

        return parsed;
    }

    /// <summary>Validates the approved closed operation/reconciliation/audit reason union.</summary>
    internal static void ValidateReason(string reason)
    {
        if (!Reasons.Contains(reason)) { throw new PayloadProtectionFormatException(); }
    }

    /// <summary>Formats the scope-index entry identity that orders a page and seeds its hash-bucket placement.</summary>
    internal static string ScopeEntryIdentity(string keyRef, uint dekVersion)
        => keyRef + ":" + dekVersion.ToString("D10", CultureInfo.InvariantCulture);

    /// <summary>Formats the KEK-inventory entry identity that orders a page and seeds its hash-bucket placement.</summary>
    internal static string KekEntryIdentity(string scopeDigest, string keyRef, uint dekVersion)
        => scopeDigest + ":" + ScopeEntryIdentity(keyRef, dekVersion);

    /// <summary>Derives the only legal exact address for an enrolled durable root.</summary>
    internal static string KeyFor<T>(T value) where T : class => value switch
    {
        WrappedDekV1 v => PayloadProtectionStateKeys.Key(v.ScopeDigest, v.KeyRef, v.DekVersion),
        ScopeIndexHeadV1 v => PayloadProtectionStateKeys.IndexHead(v.ScopeDigest),
        ScopeIndexPageV1 v => PayloadProtectionStateKeys.IndexPage(v.ScopeDigest, v.Page),
        KekInventoryHeadV1 v => PayloadProtectionStateKeys.KekHead(v.BackendIdDigest, v.ProviderVersionDigest),
        KekInventoryPageV1 v => PayloadProtectionStateKeys.KekPage(v.BackendIdDigest, v.ProviderVersionDigest, v.Page),
        LifecycleV1 v => PayloadProtectionStateKeys.Lifecycle(v.ScopeDigest),
        FenceV1 v => PayloadProtectionStateKeys.Fence(v.TenantDomainDigest),
        DeploymentGenerationV1 v => PayloadProtectionStateKeys.Deployment(v.TenantDomainDigest),
        CapabilityLeaseV1 v => PayloadProtectionStateKeys.Capability(v.TenantDomainDigest, v.Generation, v.InstanceId),
        OperationV1 v => PayloadProtectionStateKeys.Operation(v.OperationId),
        ReadLeaseV1 v => PayloadProtectionStateKeys.Lease(v.ScopeDigest, v.OperationId),
        CompletionLeaseV1 v => PayloadProtectionStateKeys.Completion(v.ScopeDigest, v.KeyRef, v.DekVersion, v.OperationId),
        ReconciliationV1 v => PayloadProtectionStateKeys.Reconciliation(v.OperationId),
        MemberHeadV1 v => PayloadProtectionStateKeys.MemberHead(v.TenantDomainDigest),
        MemberPageV1 v => PayloadProtectionStateKeys.MemberPage(v.TenantDomainDigest, v.Page),
        WorkHeadV1 v => PayloadProtectionStateKeys.WorkHead(v.TenantDomainDigest),
        WorkPageV1 v => PayloadProtectionStateKeys.WorkPage(v.TenantDomainDigest, v.Page),
        InvalidationDrainV1 v => PayloadProtectionStateKeys.Drain(v.ScopeDigest, v.OperationId),
        CacheClearAckV1 v => PayloadProtectionStateKeys.ClearAck(v.ScopeDigest, v.LifecycleEpoch, v.InstanceId),
        PayloadProtectionAuditRecord v => PayloadProtectionStateKeys.Audit(v.WorkflowId ?? v.OperationId, v.AuditId),
        _ => throw new PayloadProtectionFormatException(),
    };

    private static JsonTypeInfo<T> Metadata<T>() where T : class
        => PayloadProtectionStateJsonContext.Default.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new PayloadProtectionFormatException();

    private static JsonTypeInfo Metadata(Type type)
        => PayloadProtectionStateJsonContext.Default.GetTypeInfo(type) ?? throw new PayloadProtectionFormatException();

    private static Type Schema(Type requested, JsonElement root, PayloadProtectionAuthorityProfile authority)
    {
        if (requested != typeof(WrappedDekV1) && requested != typeof(WrappedDekV2)) { return requested; }
        // Supplement section 5: the closed key schema is selected by its own schemaVersion and must match the process authority.
        // A V2 record under another profile, or a V1 record under the Dapr Kubernetes profile, is refused and never converted.
        Type schema = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("schemaVersion", out JsonElement version)
            && version.ValueKind == JsonValueKind.Number && version.GetRawText() == "2" ? typeof(WrappedDekV2) : typeof(WrappedDekV1);
        if ((schema == typeof(WrappedDekV2)) != (authority.KeyRecordSchemaVersion == 2) || !requested.IsAssignableFrom(schema))
        {
            throw new PayloadProtectionFormatException();
        }

        return schema;
    }

    private static void ValidateObject(JsonElement value, Type type, bool root)
    {
        if (value.ValueKind != JsonValueKind.Object || !FieldProfiles.TryGetValue(type, out FrozenDictionary<string, string>? fields))
        {
            throw new PayloadProtectionFormatException();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name)) { throw new PayloadProtectionFormatException(); }
            if (root && property.Name == "schemaVersion")
            {
                if (property.Value.GetRawText() != (type == typeof(WrappedDekV2) ? "2" : "1")) { throw new PayloadProtectionFormatException(); }
                continue;
            }

            if (!fields.TryGetValue(property.Name, out string? profile)) { throw new PayloadProtectionFormatException(); }
            ValidateField(property.Value, profile.TrimEnd('?'));
        }

        if ((root && !seen.Contains("schemaVersion")) || fields.Any(p => !p.Value.EndsWith('?') && !seen.Contains(p.Key)))
        {
            throw new PayloadProtectionFormatException();
        }
    }

    private static void ValidateField(JsonElement value, string profile)
    {
        if (profile is "u32" or "u64" or "positive")
        {
            if (value.ValueKind != JsonValueKind.Number) { throw new PayloadProtectionFormatException(); }
            string raw = value.GetRawText();
            if (raw.Length == 0 || raw.Any(c => c is < '0' or > '9') || (raw.Length > 1 && raw[0] == '0')
                || !value.TryGetUInt64(out ulong n) || (profile != "u64" && n > uint.MaxValue) || (profile == "positive" && n == 0))
            {
                throw new PayloadProtectionFormatException();
            }

            return;
        }

        if (profile.StartsWith('['))
        {
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 256) { throw new PayloadProtectionFormatException(); }
            string itemProfile = profile[1..^1];
            string? previous = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (PayloadProtectionStateSchema.Elements.TryGetValue(itemProfile, out Type? type))
                {
                    ValidateObject(item, type, root: false);
                    string identity = itemProfile switch
                    {
                        "ScopeIndexEntryV1" => ScopeEntryIdentity(item.GetProperty("keyRef").GetString()!, item.GetProperty("dekVersion").GetUInt32()),
                        "KekInventoryEntryV1" => KekEntryIdentity(item.GetProperty("scopeDigest").GetString()!, item.GetProperty("keyRef").GetString()!, item.GetProperty("dekVersion").GetUInt32()),
                        "MemberEntryV1" => item.GetProperty("ordinal").GetUInt32().ToString("D10", CultureInfo.InvariantCulture),
                        _ => item.GetProperty("operationId").GetString()!,
                    };
                    if (!seen.Add(identity) || (previous is not null && StringComparer.Ordinal.Compare(previous, identity) >= 0))
                    {
                        throw new PayloadProtectionFormatException();
                    }

                    previous = identity;
                }
                else
                {
                    ValidateField(item, itemProfile == "format" ? "json|json-redacted|json+pdenc-v1|json+pdenc-v2|wrapped-dek-v2" : "Disabled|ReadOnly|CompatibilityLegacy|V2|RollbackLegacy");
                    string text = item.GetString()!;
                    if (!seen.Add(text) || (previous is not null && StringComparer.Ordinal.Compare(previous, text) >= 0))
                    {
                        throw new PayloadProtectionFormatException();
                    }

                    previous = text;
                }
            }

            return;
        }

        if (value.ValueKind != JsonValueKind.String) { throw new PayloadProtectionFormatException(); }
        string textValue = value.GetString()!;
        if (profile.Contains('|') || profile is "RSA-OAEP-256" or "v2" or WrappedDekV2.ClosedBackendId or WrappedDekV2.ClosedWrapAlgorithmId)
        {
            if (!profile.Split('|').Contains(textValue, StringComparer.Ordinal)) { throw new PayloadProtectionFormatException(); }
            return;
        }

        switch (profile)
        {
            case "shaOrEmpty": if (textValue.Length == 0) { break; } goto case "sha";
            case "sha": _ = PayloadProtectionStateKeys.Sha(textValue); break;
            case "sha1": if (!PayloadProtectionStateKeys.IsHex(textValue, 40)) { throw new PayloadProtectionFormatException(); } break;
            case "fingerprint": if (!PayloadProtectionStateKeys.IsHex(textValue, 16)) { throw new PayloadProtectionFormatException(); } break;
            case "ulidOrEmpty": if (textValue.Length == 0) { break; } goto case "ulid";
            case "ulid": if (!CanonicalUlid.IsValid(textValue)) { throw new PayloadProtectionFormatException(); } break;
            case "id": PersonalDataPolicyRegistry.ValidateId(textValue); break;
            case "tenant": _ = CanonicalText.GetByteCount(textValue, 1, 256); break;
            case "domain": _ = CanonicalText.GetByteCount(textValue, 1, 128); break;
            case "aggregate": case "actor": _ = CanonicalText.GetByteCount(textValue, 1, 256); break;
            case "provider": _ = CanonicalText.GetByteCount(textValue, 1, MaximumBytes); break;
            case "timestamp": _ = ParseTimestamp(textValue); break;
            case "binary": CryptographicOperations.ZeroMemory(DecodeWrapped(textValue)); break;
            case "envelope": CryptographicOperations.ZeroMemory(DecodeEnvelope(textValue)); break;
            case "keyAddress": DaprCryptographyPayloadProtectionOptions.ValidateKeyAddress(textValue); break;
            case "erasure": if (!ErasureReasons.Contains(textValue)) { throw new PayloadProtectionFormatException(); } break;
            case "reason": ValidateReason(textValue); break;
            default: throw new PayloadProtectionFormatException();
        }
    }

    private static byte[] DecodeWrapped(string value)
    {
        if (value.Length is < 2 or > 683) { throw new PayloadProtectionFormatException(); }
        byte[] bytes = Base64UrlCodec.Decode(value);
        if (bytes.Length is < 1 or > 512)
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw new PayloadProtectionFormatException();
        }

        return bytes;
    }

    private static byte[] DecodeEnvelope(string value)
    {
        // 2,048 decoded bytes are at most 2,731 canonical unpadded base64url characters.
        if (value.Length is < 2 or > 2731) { throw new PayloadProtectionFormatException(); }
        byte[] bytes = Base64UrlCodec.Decode(value);
        if (!WrappedDekV2.IsEnvelope(bytes))
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw new PayloadProtectionFormatException();
        }

        return bytes;
    }

    private static void ValidateSemantics<T>(T value, PayloadProtectionAuthorityProfile authorityProfile)
    {
        switch (value)
        {
            case WrappedDekV1 key:
                ValidateIdentity(key.TenantId, key.Domain, key.AggregateId, key.ScopeDigest);
                WrappedDekV1 expected = Seal(key);
                if (key.WrappedDekSha256 != expected.WrappedDekSha256 || key.CanonicalFieldsSha256 != expected.CanonicalFieldsSha256
                    || ParseTimestamp(key.UpdatedAtUtc) < ParseTimestamp(key.CreatedAtUtc)) { throw new PayloadProtectionFormatException(); }
                break;
            case LifecycleV1 lifecycle:
                ValidateIdentity(lifecycle.TenantId, lifecycle.Domain, lifecycle.AggregateId, lifecycle.ScopeDigest);
                string reason = lifecycle.State switch { "Active" => "none", "Pending" => "domain-pending", "Invalidating" => "domain-invalidating", "Invalidated" => "domain-invalidated", _ => "domain-deleted" };
                if (lifecycle.ReasonCode != reason) { throw new PayloadProtectionFormatException(); }
                break;
            case FenceV1 fence:
                ValidateRoot(fence.TenantId, fence.Domain, fence.TenantDomainDigest);
                if (fence.ApprovedSpecDigest != authorityProfile.Digest) { throw new PayloadProtectionFormatException(); }
                break;
            case DeploymentGenerationV1 deployment:
                ValidateRoot(deployment.TenantId, deployment.Domain, deployment.TenantDomainDigest);
                if (deployment.ApprovedSpecDigest != authorityProfile.Digest) { throw new PayloadProtectionFormatException(); }
                break;
            case CapabilityLeaseV1 lease:
                ValidateInterval(lease.IssuedAtUtc, lease.ExpiresAtUtc, TimeSpan.FromSeconds(60), exact: true);
                if (lease.SpecDigest != authorityProfile.Digest || lease.ReadFormats.Length == 0 || lease.WriteModes.Length == 0) { throw new PayloadProtectionFormatException(); }
                // Supplement section 6: the V2 reader token exists only under its own authority, where it and json+pdenc-v2 are mandatory.
                bool readsV2Keys = lease.ReadFormats.Contains("wrapped-dek-v2", StringComparer.Ordinal);
                if (readsV2Keys != (authorityProfile.KeyRecordSchemaVersion == 2)
                    || (readsV2Keys && !lease.ReadFormats.Contains("json+pdenc-v2", StringComparer.Ordinal))) { throw new PayloadProtectionFormatException(); }
                break;
            case ReconciliationV1 work:
                if ((work.Kind == "Rewrap") != (work.TargetProviderVersionDigest.Length != 0)) { throw new PayloadProtectionFormatException(); }
                if ((work.KeyRef.Length == 0) != (work.DekVersion == 0)
                    || (work.KeyRef.Length == 0 && work.Kind is not ("Invalidate" or "Delete"))) { throw new PayloadProtectionFormatException(); }
                break;
            case OperationV1 operation when operation.Operation != "Reconcile":
                if ((operation.KeyRef.Length == 0) != (operation.DekVersion == 0)
                    || (operation.KeyRef.Length == 0 && operation.Operation is not ("Invalidate" or "Delete"))) { throw new PayloadProtectionFormatException(); }
                break;
            case ReadLeaseV1 lease: ValidateInterval(lease.IssuedAtUtc, lease.ExpiresAtUtc, TimeSpan.FromSeconds(45), exact: false); break;
            case CompletionLeaseV1 lease: ValidateInterval(lease.IssuedAtUtc, lease.ExpiresAtUtc, TimeSpan.FromSeconds(60), exact: true); break;
            case MemberHeadV1 head:
                if (head.PageCount > 256 || head.EntryCount > 65536 || head.PageCount != (head.EntryCount + 255) / 256) { throw new PayloadProtectionFormatException(); }
                break;
            case WorkHeadV1 head:
                if (head.PageCount > 256 || head.EntryCount > head.PageCount * 256 || (head.PageCount == 0 && head.EntryCount != 0)) { throw new PayloadProtectionFormatException(); }
                break;
            case MemberPageV1 page:
                if (page.Page >= 256 || page.Entries.Length == 0 || page.Entries.Where((e, i) => e.Ordinal != page.Page * 256 + i + 1).Any()
                    || page.Entries.Select(e => e.InstanceId).Distinct(StringComparer.Ordinal).Count() != page.Entries.Length) { throw new PayloadProtectionFormatException(); }
                break;
            case WorkPageV1 page: if (page.Page >= 256) { throw new PayloadProtectionFormatException(); } break;
            case InvalidationDrainV1 drain: if (drain.MemberCutoff > 65536) { throw new PayloadProtectionFormatException(); } break;
            case PayloadProtectionAuditRecord audit:
                ValidateRoot(audit.TenantId, audit.Domain, PayloadProtectionStateKeys.TenantDomainDigest(audit.TenantId, audit.Domain));
                if (audit.AggregateId is not null) { ValidateIdentity(audit.TenantId, audit.Domain, audit.AggregateId, PayloadProtectionStateKeys.ScopeDigest(new(audit.TenantId, audit.Domain, audit.AggregateId))); }
                break;
        }
    }

    private static void ValidateInterval(string issued, string expires, TimeSpan maximum, bool exact)
    {
        TimeSpan interval = ParseTimestamp(expires) - ParseTimestamp(issued);
        if (interval <= TimeSpan.Zero || interval > maximum || (exact && interval != maximum)) { throw new PayloadProtectionFormatException(); }
    }

    private static void ValidateRoot(string tenant, string domain, string root)
    {
        var identity = new AggregateIdentity(tenant, domain, "validation");
        if (identity.TenantId != tenant || identity.Domain != domain || PayloadProtectionStateKeys.TenantDomainDigest(tenant, domain) != root)
        {
            throw new PayloadProtectionFormatException();
        }
    }

    private static void ValidateIdentity(string tenant, string domain, string aggregate, string scope)
    {
        var identity = new AggregateIdentity(tenant, domain, aggregate);
        if (identity.TenantId != tenant || identity.Domain != domain || PayloadProtectionStateKeys.ScopeDigest(identity) != scope)
        {
            throw new PayloadProtectionFormatException();
        }
    }

    private static string FieldsHash(WrappedDekV1 record)
    {
        using var bytes = new MemoryStream();
        // HXKF, schema byte (01 for V1, 02 for V2), field count 0x11 and two zero reserved bytes; field identifiers are shared.
        bytes.Write([0x48, 0x58, 0x4b, 0x46, record is WrappedDekV2 ? (byte)0x02 : (byte)0x01, 0x11, 0, 0]);
        Text(1, record.TenantId); Text(2, record.Domain); Text(3, record.AggregateId);
        Field(4, 4, Convert.FromHexString(record.ScopeDigest)); Text(5, record.KeyRef);
        Span<byte> number = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(number, record.DekVersion); Field(6, 2, number[..4]);
        Text(7, record.State); Text(8, record.BackendId); Text(9, record.ProviderKeyVersionId); Text(10, record.WrapAlgorithmId);
        BinaryPrimitives.WriteUInt64BigEndian(number, record.LifecycleEpoch); Field(12, 3, number);
        BinaryPrimitives.WriteUInt64BigEndian(number, record.FenceApprovalEpoch); Field(13, 3, number);
        BinaryPrimitives.WriteUInt64BigEndian(number, record.HighestWrittenEpoch); Field(14, 3, number);
        Text(15, record.CreatingOperationId); Text(16, record.CreatedAtUtc); Text(17, record.UpdatedAtUtc);
        Field(19, 4, Convert.FromHexString(record.WrappedDekSha256));
        return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));

        void Text(byte id, string value) => Field(id, 1, CanonicalText.Encode(value, 1, MaximumBytes));
        void Field(byte id, byte type, ReadOnlySpan<byte> value)
        {
            bytes.WriteByte(id); bytes.WriteByte(type);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)value.Length));
            bytes.Write(length); bytes.Write(value);
        }
    }
}

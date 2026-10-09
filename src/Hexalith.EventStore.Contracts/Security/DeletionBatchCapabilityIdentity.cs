using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Shared closed capability canonical identity; this byte primitive grants no authority.</summary>
public static class DeletionBatchCapabilityIdentity
{
    /// <summary>Exact sorted complete target manifest identity shared by guard and atomic protection owner; aliases alone never identify a target.</summary>
    public static string TargetManifestDigest(IReadOnlyList<ProtectionTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets); var values = new List<string[]>();
        foreach (var target in targets)
        {
            if (values.Count >= 1000 || target is null) { throw new ArgumentException("Malformed bounded target manifest."); }
            foreach (string value in new[] { target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias })
            { if (string.IsNullOrWhiteSpace(value) || value.Length > 2048) { throw new ArgumentException("Malformed target identity."); } _ = StrictUtf8.GetByteCount(value); }
            values.Add([target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias]);
        }
        return Convert.ToHexString(SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(values)));
    }
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private const long MaximumExactInteger = 9007199254740991;
    /// <summary>RFC 8785 canonical bytes for the closed fourteen fields, with exact I-JSON integer and Unicode validation.</summary>
    public static byte[] CanonicalPayload(DeletionBatchCapabilityV1 payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var fields = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["Issuer"] = payload.Issuer, ["Audience"] = payload.Audience, ["TenantId"] = payload.TenantId,
            ["DeletionRequestId"] = payload.DeletionRequestId, ["DestructionSealId"] = payload.DestructionSealId,
            ["BatchKind"] = payload.BatchKind, ["BatchOrdinal"] = payload.BatchOrdinal, ["BatchId"] = payload.BatchId,
            ["ManifestDigest"] = payload.ManifestDigest, ["GuardStreamId"] = payload.GuardStreamId,
            ["IntendedIssuedGuardRevision"] = payload.IntendedIssuedGuardRevision, ["AttestationOrdinal"] = payload.AttestationOrdinal,
            ["SigningAttemptOrdinal"] = payload.SigningAttemptOrdinal, ["CapabilityKeyVersion"] = payload.CapabilityKeyVersion,
        };
        if (payload.BatchOrdinal < 0 || payload.IntendedIssuedGuardRevision <= 0 || payload.AttestationOrdinal <= 0 || payload.SigningAttemptOrdinal <= 0)
        { throw new ArgumentException("Invalid capability ordinal."); }
        var text = new StringBuilder("{"); bool first = true;
        foreach (var field in fields)
        {
            if (!first) { text.Append(','); } first = false;
            String(text, field.Key); text.Append(':');
            if (field.Value is long number)
            {
                if (number > MaximumExactInteger) { throw new ArgumentException("Capability number is not exactly representable in I-JSON."); }
                text.Append(number.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                if (field.Value is not string value || string.IsNullOrWhiteSpace(value) || value.Length > 2048)
                { throw new ArgumentException("Malformed capability identity."); }
                String(text, value);
            }
        }
        text.Append('}'); return StrictUtf8.GetBytes(text.ToString());
    }
    /// <summary>Deterministic exact original signing request identity shared by signer and append guard.</summary>
    public static string SigningRequestId(DeletionBatchCapabilityV1 payload)
        => Convert.ToHexString(SHA256.HashData(CanonicalPayload(payload)));
    private static void String(StringBuilder text, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 2048) { throw new ArgumentException("Capability string exceeds bound."); }
        // Strict encoding rejects isolated surrogates before a serializer can replace them. JCS preserves Unicode without normalization.
        _ = StrictUtf8.GetByteCount(value); text.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"': text.Append("\\\""); break;
                case '\\': text.Append("\\\\"); break;
                case '\b': text.Append("\\b"); break;
                case '\t': text.Append("\\t"); break;
                case '\n': text.Append("\\n"); break;
                case '\f': text.Append("\\f"); break;
                case '\r': text.Append("\\r"); break;
                default:
                    if (character < 0x20) { text.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture)); }
                    else { text.Append(character); }
                    break;
            }
        }
        text.Append('"');
    }
}

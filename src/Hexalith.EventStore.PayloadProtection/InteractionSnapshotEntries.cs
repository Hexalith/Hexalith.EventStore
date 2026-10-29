using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Canonical mixed-root snapshot layout and sub-occurrence identities. Existing single-root identities and authenticated bytes are unchanged.</summary>
public static class InteractionSnapshotEntries
{
    /// <summary>Inspects exact source state and returns only its content-free complete entry manifest for independent source enrollment.</summary>
    public static SnapshotProtectionEntriesDescription Describe(byte[] sourceBytes, string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceBytes); ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var layout = SnapshotEntryLayout.Read(sourceBytes, tenantId, false, cancellationToken);
        return new(layout.ContainerDigest, layout.Entries);
    }
    /// <summary>Derives a snapshot entry storage occurrence from the original parent and canonical path, without changing event or whole-snapshot identities.</summary>
    public static PayloadProtectionOccurrenceContext EntryOccurrence(PayloadProtectionOccurrenceContext parent, string path)
    {
        ArgumentNullException.ThrowIfNull(parent); ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (parent.PayloadKind != PayloadProtectionPayloadKind.Snapshot || !path.StartsWith("/", StringComparison.Ordinal))
        { throw new ArgumentException("Exact bounded snapshot parent and value path required."); }
        AadCodec.ValidateSnapshotTypeId(parent.PayloadTypeId);
        return parent with { PayloadTypeId = "hx-snapshot-v1:entry-" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new SnapshotEntryOccurrenceIdentity(parent.PayloadTypeId, path), InteractionSnapshotJsonContext.Default.SnapshotEntryOccurrenceIdentity))) };
    }
}

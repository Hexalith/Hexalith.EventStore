using Hexalith.EventStore.Contracts.Identity;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Centralizes projection delivery state and control-plane key composition.</summary>
internal static class ProjectionDeliveryStateKeys {
    /// <summary>The store-global writer cutover marker key.</summary>
    public const string WriterProtocol = "projection-delivery-writer-protocol";

    /// <summary>Returns the exact source-v1 length-prefixed per-scope admission key.</summary>
    internal static string GetSourceGuardKey(AggregateIdentity identity, string projectionName)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (string value in new[] { identity.TenantId, identity.Domain, identity.AggregateId, projectionName })
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return "projection-delivery-source-guard:v1:" + Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private const string _reconciliationPrefix = "projection-delivery-reconciliation:";

    /// <summary>Returns the compatible in-place projection delivery row key.</summary>
    public static string GetStateKey(AggregateIdentity identity, string projectionName) =>
        ProjectionCheckpointTracker.GetProjectionScopedStateKey(identity, projectionName);

    /// <summary>Returns the payload-free reconciliation work key for one projection scope.</summary>
    public static string GetReconciliationKey(AggregateIdentity identity, string projectionName) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        return $"{_reconciliationPrefix}{identity.ActorId}:{projectionName}";
    }
}

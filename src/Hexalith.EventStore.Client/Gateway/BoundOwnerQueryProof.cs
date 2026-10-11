using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Gateway;

/// <summary>Binds an owner query's complete payload and requester to a short-lived workload proof.</summary>
public static class BoundOwnerQueryProof
{
    /// <summary>The request header carrying the signed authority query binding.</summary>
    public const string HeaderName = "X-Hexalith-Bound-Owner-Query-Proof";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Signs the complete query and its bound requester.</summary>
    public static string Issue(BoundOwnerQueryRequest request, string caller, string privateKeyPkcs8Base64, DateTimeOffset now)
        => BoundStreamReadProof.Issue(Scope(request), caller, privateKeyPkcs8Base64, now);

    /// <summary>Verifies the exact query and requester against the authenticated caller key.</summary>
    public static bool Verify(BoundOwnerQueryRequest request, string? proof, string caller,
        string? publicKeySpkiBase64, DateTimeOffset now)
    {
        try
        {
            return BoundStreamReadProof.Verify(proof, Scope(request), caller, publicKeySpkiBase64, now);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static StreamReadRequest Scope(BoundOwnerQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Query);
        if (string.IsNullOrWhiteSpace(request.PrincipalId) || request.PrincipalId.Length > 200 ||
            string.IsNullOrWhiteSpace(request.Query.AggregateId) || request.Query.AggregateId.Length > 200 ||
            string.IsNullOrWhiteSpace(request.Query.QueryType) || request.Query.QueryType.Length > 300)
        {
            throw new ArgumentException("Invalid owner query binding.", nameof(request));
        }

        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        return new StreamReadRequest(request.Query.Tenant, request.Query.Domain, Convert.ToHexString(SHA256.HashData(canonical)));
    }
}

using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Gateway;

/// <summary>Signs the full workload command and represented principal as a second resource proof.</summary>
public static class BoundWorkloadCommandProof
{
    /// <summary>The request header carrying the signed command binding.</summary>
    public const string HeaderName = "X-Hexalith-Bound-Workload-Command-Proof";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Issues a short-lived proof for one exact command and represented principal.</summary>
    public static string Issue(BoundWorkloadCommandRequest request, string caller,
        string privateKeyPkcs8Base64, DateTimeOffset now)
        => BoundStreamReadProof.Issue(Scope(request), caller, privateKeyPkcs8Base64, now);

    /// <summary>Verifies the exact command, principal, and authenticated caller.</summary>
    public static bool Verify(BoundWorkloadCommandRequest request, string? proof, string caller,
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

    private static StreamReadRequest Scope(BoundWorkloadCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Command);
        if (string.IsNullOrWhiteSpace(request.PrincipalId) || request.PrincipalId.Length > 200 ||
            string.IsNullOrWhiteSpace(request.Command.MessageId) || request.Command.MessageId.Length > 200 ||
            string.IsNullOrWhiteSpace(request.Command.CommandType) || request.Command.CommandType.Length > 300 ||
            request.Command.Payload.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Invalid bound workload command.", nameof(request));
        }

        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        return new StreamReadRequest(request.Command.Tenant, request.Command.Domain,
            Convert.ToHexString(SHA256.HashData(canonical)));
    }
}

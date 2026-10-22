using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Gateway;

/// <summary>Signs an exact stream read scope as a second proof beside the OIDC workload assertion.</summary>
public static class BoundStreamReadProof
{
    /// <summary>The request header carrying the short-lived resource proof.</summary>
    public const string HeaderName = "X-Hexalith-Bound-Stream-Read-Proof";

    private const int MaximumLength = 8192;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Issues a thirty-second RSA proof for one caller, tenant, domain and aggregate.</summary>
    public static string Issue(StreamReadRequest request, string caller, string privateKeyPkcs8Base64, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPkcs8Base64);
        if (now.Offset != TimeSpan.Zero || !ValidScope(request))
        {
            throw new ArgumentException("Invalid bound stream read scope.", nameof(request));
        }

        using RSA rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyPkcs8Base64), out _);
        if (rsa.KeySize < 2048)
        {
            throw new ArgumentException("The bound stream read key must be at least 2048 bits.", nameof(privateKeyPkcs8Base64));
        }

        var payload = new ProofPayload(1, caller, request.Tenant, request.Domain, request.AggregateId!,
            now.ToUnixTimeSeconds(), now.AddSeconds(30).ToUnixTimeSeconds(),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
        string body = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
        string signature = Base64UrlEncode(rsa.SignData(Encoding.ASCII.GetBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        return $"{body}.{signature}";
    }

    /// <summary>Checks the signature, freshness and every exact request binding.</summary>
    public static bool Verify(string? proof, StreamReadRequest request, string caller, string? publicKeySpkiBase64, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(proof) || proof.Length > MaximumLength || request is null ||
            string.IsNullOrWhiteSpace(caller) || string.IsNullOrWhiteSpace(publicKeySpkiBase64) ||
            now.Offset != TimeSpan.Zero || !ValidScope(request))
        {
            return false;
        }

        try
        {
            string[] parts = proof.Split('.');
            if (parts.Length != 2 || parts.Any(static part => part.Length == 0)) { return false; }
            byte[] body = Base64UrlDecode(parts[0]);
            ProofPayload? payload = JsonSerializer.Deserialize<ProofPayload>(body, JsonOptions);
            if (payload is null || payload.Version != 1 || payload.Caller != caller ||
                payload.Tenant != request.Tenant || payload.Domain != request.Domain ||
                payload.AggregateId != request.AggregateId ||
                payload.Nonce is not { Length: 32 } || !payload.Nonce.All(Uri.IsHexDigit) ||
                payload.ExpiresAt <= payload.IssuedAt || payload.ExpiresAt - payload.IssuedAt > 30 ||
                payload.IssuedAt > now.ToUnixTimeSeconds() + 5 ||
                payload.ExpiresAt <= now.ToUnixTimeSeconds())
            {
                return false;
            }

            using RSA rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeySpkiBase64), out _);
            return rsa.KeySize >= 2048 && rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0]),
                Base64UrlDecode(parts[1]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or JsonException or CryptographicException)
        {
            return false;
        }
    }

    private static bool ValidScope(StreamReadRequest request)
        => !string.IsNullOrWhiteSpace(request.Tenant) && !string.IsNullOrWhiteSpace(request.Domain) &&
            !string.IsNullOrWhiteSpace(request.AggregateId) &&
            request.Tenant.Length <= 200 && request.Domain.Length <= 200 && request.AggregateId.Length <= 256;

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        string base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
    }

    private sealed record ProofPayload(int Version, string Caller, string Tenant, string Domain, string AggregateId,
        long IssuedAt, long ExpiresAt, string Nonce);
}

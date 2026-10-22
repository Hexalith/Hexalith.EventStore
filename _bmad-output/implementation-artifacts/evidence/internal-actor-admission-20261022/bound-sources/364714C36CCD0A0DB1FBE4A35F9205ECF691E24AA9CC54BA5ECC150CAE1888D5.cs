using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Signs and verifies short-lived, command-bound human authority receipts.</summary>
public static class HumanAuthorityReceiptCodec
{
    /// <summary>The only accepted receipt issuer.</summary>
    public const string Issuer = "hexalith-eventstore";

    /// <summary>The Folders domain-service audience.</summary>
    public const string Audience = "folders";

    /// <summary>The maximum receipt lifetime.</summary>
    public const int LifetimeSeconds = 120;

    /// <summary>Signs a receipt after the gateway has authenticated the human caller and assigned the execution message ID.</summary>
    public static string Issue(
        CommandEnvelope command,
        string tenant,
        string subject,
        IReadOnlyCollection<string> permissions,
        string base64SigningKey,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(permissions);
        if (!string.Equals(command.Domain, Audience, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(tenant)
            || string.IsNullOrWhiteSpace(subject)
            || !string.Equals(command.TenantId, tenant, StringComparison.Ordinal)
            || !string.Equals(command.UserId, subject, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Human authority cannot be issued for this command.");
        }

        byte[] key = ReadKey(base64SigningKey);
        var claims = new HumanAuthorityReceiptClaims(
            1,
            Issuer,
            Audience,
            now.ToUnixTimeSeconds(),
            now.AddSeconds(LifetimeSeconds).ToUnixTimeSeconds(),
            Guid.NewGuid().ToString("N"),
            tenant,
            subject,
            [.. permissions.Where(permission => !string.IsNullOrWhiteSpace(permission)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            Digest(command));
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(claims);
        byte[] signature = HMACSHA256.HashData(key, payload);
        return $"{Base64Url(payload)}.{Base64Url(signature)}";
    }

    /// <summary>Verifies signature, scope, lifetime and exact command binding without trusting the command as identity evidence.</summary>
    public static bool TryVerify(
        string? receipt,
        CommandEnvelope command,
        string base64SigningKey,
        DateTimeOffset now,
        out HumanAuthorityReceiptClaims? claims)
    {
        claims = null;
        if (command is null || string.IsNullOrWhiteSpace(receipt) || receipt.Length > 16_384)
        {
            return false;
        }

        int separator = receipt.IndexOf('.');
        if (separator <= 0 || separator != receipt.LastIndexOf('.') || separator == receipt.Length - 1)
        {
            return false;
        }

        try
        {
            byte[] key = ReadKey(base64SigningKey);
            byte[] payload = FromBase64Url(receipt[..separator]);
            byte[] signature = FromBase64Url(receipt[(separator + 1)..]);
            if (signature.Length != 32 || !CryptographicOperations.FixedTimeEquals(signature, HMACSHA256.HashData(key, payload)))
            {
                return false;
            }

            HumanAuthorityReceiptClaims? candidate = JsonSerializer.Deserialize<HumanAuthorityReceiptClaims>(payload);
            long current = now.ToUnixTimeSeconds();
            if (candidate is null
                || candidate.Version != 1
                || !string.Equals(candidate.Issuer, Issuer, StringComparison.Ordinal)
                || !string.Equals(candidate.Audience, Audience, StringComparison.Ordinal)
                || !string.Equals(command.Domain, Audience, StringComparison.OrdinalIgnoreCase)
                || candidate.IssuedAtUnixSeconds > current
                || candidate.ExpiresAtUnixSeconds <= current
                || candidate.ExpiresAtUnixSeconds - candidate.IssuedAtUnixSeconds != LifetimeSeconds
                || string.IsNullOrWhiteSpace(candidate.InvocationId)
                || string.IsNullOrWhiteSpace(candidate.Subject)
                || candidate.Permissions is null
                || !string.Equals(candidate.Tenant, command.TenantId, StringComparison.Ordinal)
                || !string.Equals(candidate.Subject, command.UserId, StringComparison.Ordinal)
                || !string.Equals(candidate.CommandDigest, Digest(command), StringComparison.Ordinal))
            {
                return false;
            }

            claims = candidate;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static byte[] ReadKey(string base64SigningKey)
    {
        byte[] key = Convert.FromBase64String(base64SigningKey);
        if (key.Length < 32)
        {
            throw new InvalidOperationException("Human authority receipt signing key is too short.");
        }

        return key;
    }

    private static string Digest(CommandEnvelope command)
    {
        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(new
        {
            command.MessageId,
            command.TenantId,
            command.Domain,
            command.AggregateId,
            command.CommandType,
            PayloadDigest = Convert.ToHexString(SHA256.HashData(command.Payload)),
            command.CorrelationId,
            command.CausationId,
            command.UserId,
            Extensions = command.Extensions?
                .OrderBy(static extension => extension.Key, StringComparer.Ordinal)
                .Select(static extension => new { extension.Key, extension.Value })
                .ToArray(),
        });
        return Convert.ToHexString(SHA256.HashData(canonical));
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight((padded.Length + 3) / 4 * 4, '=');
        return Convert.FromBase64String(padded);
    }
}

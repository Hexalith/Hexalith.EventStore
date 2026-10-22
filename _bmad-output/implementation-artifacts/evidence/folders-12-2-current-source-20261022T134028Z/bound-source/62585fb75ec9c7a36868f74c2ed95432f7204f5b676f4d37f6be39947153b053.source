using System.Security.Cryptography;

using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Resolves local-development key material from validated application configuration.</summary>
public sealed class ConfigurationIdempotencyDigestKeyProvider(
    IOptions<IdempotencyAdmissionOptions> options) : IIdempotencyDigestKeyProvider
{
    private readonly IdempotencyAdmissionOptions _options = options?.Value
        ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    public ValueTask<IdempotencyDigestKeyRing> GetKeyRingAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled || _options.DigestKeySource != IdempotencyDigestKeySource.Configuration)
        {
            throw new InvalidOperationException("The configured idempotency digest key ring is unavailable.");
        }

        var decoded = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            foreach (string version in new[] { _options.ActiveDigestKeyVersion }
                .Concat(_options.ReaderDigestKeyVersions))
            {
                if (!_options.DigestKeys.TryGetValue(version, out string? encoded))
                {
                    throw new InvalidOperationException("The configured idempotency digest key ring is unavailable.");
                }

                decoded.Add(version, Convert.FromBase64String(encoded));
            }

            return ValueTask.FromResult(new IdempotencyDigestKeyRing(
                _options.ActiveDigestKeyVersion,
                decoded,
                _options.ReaderDigestKeyVersions));
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("The configured idempotency digest key ring is invalid.");
        }
        finally
        {
            foreach (byte[] key in decoded.Values)
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }
}

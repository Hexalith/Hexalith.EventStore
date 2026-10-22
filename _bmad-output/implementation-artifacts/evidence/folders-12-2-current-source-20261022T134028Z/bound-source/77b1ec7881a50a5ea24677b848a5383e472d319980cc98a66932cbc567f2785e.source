using System.Security.Cryptography;

using Dapr.Client;

using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Resolves a runtime-required digest key-ring map through the approved Dapr secret boundary.</summary>
public sealed class DaprSecretIdempotencyDigestKeyProvider(
    DaprClient daprClient,
    IOptions<IdempotencyAdmissionOptions> options) : IIdempotencyDigestKeyProvider
{
    private const string GenerationField = "generation";
    private readonly DaprClient _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
    private readonly IdempotencyAdmissionOptions _options = options?.Value
        ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    public async ValueTask<IdempotencyDigestKeyRing> GetKeyRingAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || _options.DigestKeySource != IdempotencyDigestKeySource.DaprSecret)
        {
            throw new InvalidOperationException("The secret-backed idempotency digest key ring is unavailable.");
        }

        Dictionary<string, string> secret;
        try
        {
            secret = await _daprClient.GetSecretAsync(
                _options.DigestKeySecretStoreName,
                _options.DigestKeySecretName,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidOperationException("The secret-backed idempotency digest key ring is unavailable.");
        }

        var decoded = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            if (!secret.TryGetValue(GenerationField, out string? generation)
                || !string.Equals(generation, _options.DigestKeySecretGeneration, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The secret-backed idempotency digest key ring generation is invalid.");
            }

            foreach (string version in new[] { _options.ActiveDigestKeyVersion }
                .Concat(_options.ReaderDigestKeyVersions))
            {
                if (!secret.TryGetValue(version, out string? encoded))
                {
                    throw new InvalidOperationException("The secret-backed idempotency digest key ring is invalid.");
                }

                decoded.Add(version, Convert.FromBase64String(encoded));
            }

            return new IdempotencyDigestKeyRing(
                _options.ActiveDigestKeyVersion,
                decoded,
                _options.ReaderDigestKeyVersions);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("The secret-backed idempotency digest key ring is invalid.");
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

// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10 and 15.
using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Owns a bounded backend output until explicitly transferred or disposed; never formats key material.</summary>
internal sealed class PayloadBackendResult : IDisposable
{
    private byte[]? _bytes;

    /// <summary>Creates an opaque version-bound backend result, taking ownership of any supplied bytes.</summary>
    internal PayloadBackendResult(PayloadBackendFailure failure, byte[]? bytes = null, string? providerKeyVersionId = null,
        TimeSpan? retryAfter = null)
    {
        _bytes = bytes;
        if (!Enum.IsDefined(failure) || bytes is { Length: > 512 }
            || (failure == PayloadBackendFailure.None && bytes is not { Length: >= 1 }))
        {
            Dispose();
            throw new PayloadProtectionFormatException();
        }

        if (providerKeyVersionId is not null)
        {
            try { _ = CanonicalText.GetByteCount(providerKeyVersionId, 1, PayloadProtectionStateCodec.MaximumBytes); }
            catch { Dispose(); throw; }
        }

        if (failure != PayloadBackendFailure.None) { Dispose(); }
        Failure = failure;
        ProviderKeyVersionId = providerKeyVersionId;
        RetryAfter = retryAfter;
    }

    /// <summary>Gets the closed outcome.</summary>
    internal PayloadBackendFailure Failure { get; }
    /// <summary>Gets the opaque exact provider version, confined to engine/adapter memory and wrapped-key state.</summary>
    internal string? ProviderKeyVersionId { get; }
    /// <summary>Gets the typed retry delay; the executor must validate it against the remaining budget.</summary>
    internal TimeSpan? RetryAfter { get; }
    /// <summary>Transfers the owned mutable output exactly once.</summary>
    internal byte[] TakeBytes() => Interlocked.Exchange(ref _bytes, null) ?? throw new PayloadProtectionFormatException();
    /// <summary>Clears any output not transferred to another explicit owner.</summary>
    public void Dispose()
    {
        byte[]? bytes = Interlocked.Exchange(ref _bytes, null);
        if (bytes is not null) { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <summary>Returns a bounded type name without provider or key information.</summary>
    public override string ToString() => nameof(PayloadBackendResult);
}

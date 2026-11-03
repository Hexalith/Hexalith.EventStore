// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 section 6; no nested authentication retries.
namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Bounds one credential acquisition, including MSAL's hidden retries, without adding provider calls or a result journal.</summary>
internal sealed class AzureKeyVaultTokenAcquisition : IDisposable
{
    private static readonly AsyncLocal<AzureKeyVaultTokenAcquisition?> Current = new();
    private readonly AzureKeyVaultTokenAcquisition? _previous;
    private readonly AzureKeyVaultTokenAcquisition _root;
    private readonly CancellationTokenSource? _stop;
    private int _requests;
    private int _active = 1;
    private int _failure = -1;
    /// <summary>Joins an enclosing acquisition so wrapping the same credential cannot reset its request allowance.</summary>
    internal AzureKeyVaultTokenAcquisition(CancellationToken caller)
    {
        _previous = Current.Value;
        _root = _previous?._root ?? this;
        if (_root == this)
        {
            _stop = CancellationTokenSource.CreateLinkedTokenSource(caller);
        }

        Current.Value = this;
    }

    /// <summary>Gets the exact acquisition token linked to the enclosing operation.</summary>
    internal CancellationToken Token => _root._stop!.Token;
    /// <summary>Gets the first constructive failure, if one has stopped this acquisition.</summary>
    internal PayloadBackendFailure? Failure => Volatile.Read(ref _root._failure)is int value && value >= 0 ? (PayloadBackendFailure)value : null;

    /// <summary>Permits one physical identity request; library-level replay cannot introduce another wire attempt.</summary>
    internal static CancellationToken RequireSingleRequest()
    {
        AzureKeyVaultTokenAcquisition? root = Current.Value?._root;
        if (root is null || Volatile.Read(ref root._active) == 0)
        {
            throw new AzureKeyVaultTransportException(PayloadBackendFailure.Unavailable);
        }

        CancellationToken token = root._stop!.Token;
        token.ThrowIfCancellationRequested();
        if (Interlocked.Increment(ref root._requests) > 1)
        {
            Stop(PayloadBackendFailure.Unavailable);
            throw new AzureKeyVaultTransportException(PayloadBackendFailure.Unavailable);
        }

        return token;
    }

    /// <summary>Stops SDK retry/backoff immediately after an affirmative token transport/status failure.</summary>
    internal static void Stop(PayloadBackendFailure failure)
    {
        AzureKeyVaultTokenAcquisition? root = Current.Value?._root;
        if (root is not null)
        {
            Interlocked.CompareExchange(ref root._failure, (int)failure, -1);
            root._stop!.Cancel();
        }
    }

    /// <summary>Restores the enclosing context after actual credential completion.</summary>
    public void Dispose()
    {
        Current.Value = _previous;
        if (_stop is not null)
        {
            Interlocked.Exchange(ref _active, 0);
            try
            {
                _stop.Cancel();
            }
            finally
            {
                _stop.Dispose();
            }
        }
    }
}

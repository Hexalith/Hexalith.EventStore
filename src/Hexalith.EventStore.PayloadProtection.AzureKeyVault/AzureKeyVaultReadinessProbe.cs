// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 5, 11, 15.
using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Runs fresh metadata and remote wrap/unwrap under one ten-second phase, with fixed-time comparison and clearing.</summary>
internal sealed class AzureKeyVaultReadinessProbe(AzureKeyVaultPayloadProtectionBackend backend, TimeProvider? timeProvider = null, Action<byte[]>? cleared = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    /// <summary>Checks adapter capability; engine PF-02, lifecycle/fence readiness and real-service conformance are separate.</summary>
    internal async ValueTask<AzureKeyVaultReadinessResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        using var phase = new PayloadOperationBudget(_clock, cancellationToken);
        byte[] probe = RandomNumberGenerator.GetBytes(32);
        byte[]? wrapped = null;
        byte[]? unwrapped = null;
        try
        {
            AzureKeyVaultKeyProfile profile = await PayloadOperationBudget.AwaitAsync(token => backend.RefreshAsync(token), phase.Token).ConfigureAwait(false);
            var executor = new PayloadBackendExecutor(_clock);
            using PayloadBackendResult wrap = await executor.ExecuteAsync(backend, token => backend.WrapExactAsync("01J00000000000000000000860", profile.Id, probe, token), phase.Token, allowRetry: false).ConfigureAwait(false);
            if (wrap.Failure != PayloadBackendFailure.None)
            {
                return new(false, wrap.Failure);
            }

            wrapped = wrap.TakeBytes();
            using PayloadBackendResult unwrap = await executor.ExecuteAsync(backend, token => backend.UnwrapAsync("01J00000000000000000000861", profile.Id, wrapped, token), phase.Token, allowRetry: false, operation: PayloadProtectionOperation.Unwrap).ConfigureAwait(false);
            if (unwrap.Failure != PayloadBackendFailure.None)
            {
                return new(false, unwrap.Failure);
            }

            unwrapped = unwrap.TakeBytes();
            PayloadOperationBudget.RequireCurrent();
            if (unwrapped.Length != 32 || !CryptographicOperations.FixedTimeEquals(probe, unwrapped))
            {
                return new(false, PayloadBackendFailure.ConsistencyMismatch);
            }

            return new(true, PayloadBackendFailure.None, PayloadProtectionStateKeys.Digest(profile.Id)[..16]);
        }
        catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (AzureKeyVaultTransportException exception)
        {
            return new(false, exception.Failure);
        }
        catch (Exception exception)
        {
            return new(false, AzureKeyVaultFailureClassifier.Exception(exception, false, false));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(probe);
            cleared?.Invoke(probe);
            if (wrapped is not null)
            {
                CryptographicOperations.ZeroMemory(wrapped);
                cleared?.Invoke(wrapped);
            }

            if (unwrapped is not null)
            {
                CryptographicOperations.ZeroMemory(unwrapped);
                cleared?.Invoke(unwrapped);
            }
        }
    }
}

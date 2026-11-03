// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 5, 11, 15.
using Azure.Security.KeyVault.Keys;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Version-bound opaque engine adapter. Unknown mutation completion stays pending without a result journal or inferred absence.</summary>
internal sealed class AzureKeyVaultPayloadProtectionBackend(AzureKeyVaultPayloadProtectionOptions options, AzureKeyVaultMetadataClient metadata, AzureKeyVaultDataPlane dataPlane, TimeProvider? timeProvider = null) : PayloadProtectionBackend
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _selectionLock = new(1, 1);
    private AzureKeyVaultKeyProfile? _selection;
    private long _selectedAt;
    /// <inheritdoc/>
    internal override string BackendId => "azure-key-vault-rsa-hsm-v1";
    /// <inheritdoc/>
    internal override string EndpointFingerprint => PayloadProtectionStateKeys.Digest(options.VaultUri.GetLeftPart(UriPartial.Authority));

    /// <inheritdoc/>
    internal override ValueTask<PayloadBackendResult> WrapAsync(string operationId, ReadOnlyMemory<byte> dek, CancellationToken cancellationToken) => ExecuteAsync(operationId, null, dek, true, cancellationToken);
    /// <inheritdoc/>
    internal override ValueTask<PayloadBackendResult> WrapExactAsync(string operationId, string providerKeyVersionId, ReadOnlyMemory<byte> dek, CancellationToken cancellationToken) => ExecuteAsync(operationId, providerKeyVersionId, dek, true, cancellationToken);
    /// <inheritdoc/>
    internal override ValueTask<PayloadBackendResult> UnwrapAsync(string operationId, string providerKeyVersionId, ReadOnlyMemory<byte> wrappedDek, CancellationToken cancellationToken) => ExecuteAsync(operationId, providerKeyVersionId, wrappedDek, false, cancellationToken);
    /// <inheritdoc/>
    internal override ValueTask<PayloadBackendResult> ReconcileAsync(string operationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanonicalUlid.IsValid(operationId))
        {
            throw AzureKeyVaultOptionsValidator.Invalid();
        }

        return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Ambiguous));
    }

    /// <summary>Reads fresh metadata for startup without treating a cached probe or local fake as conformance.</summary>
    internal ValueTask<AzureKeyVaultKeyProfile> RefreshAsync(CancellationToken token) => SelectAsync(null, true, token);
    private async ValueTask<PayloadBackendResult> ExecuteAsync(string operation, string? id, ReadOnlyMemory<byte> input, bool wrap, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (input.Length != (wrap ? 32 : 384))
        {
            return new(PayloadBackendFailure.ConsistencyMismatch);
        }

        byte[] owned = input.ToArray();
        try
        {
            if (!CanonicalUlid.IsValid(operation))
            {
                throw AzureKeyVaultOptionsValidator.Invalid();
            }

            AzureKeyVaultKeyProfile profile = await SelectAsync(id, false, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return await dataPlane.ExecuteAsync(profile.Id, owned, wrap, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)when (token.IsCancellationRequested)
        {
            throw new OperationCanceledException(token);
        }
        catch (AzureKeyVaultTransportException exception)
        {
            return new(exception.Failure);
        }
        catch (Exception exception)
        {
            return new(AzureKeyVaultFailureClassifier.Exception(exception, false, wrap));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(owned);
        }
    }

    private async ValueTask<AzureKeyVaultKeyProfile> SelectAsync(string? id, bool force, CancellationToken token)
    {
        if (id is not null)
        {
            string version = AzureKeyVaultKeyProfile.Version(options, id);
            KeyVaultKey key = await metadata.GetAsync(version, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return AzureKeyVaultKeyProfile.Validate(options, key, _clock.GetUtcNow(), id);
        }

        await _selectionLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (!force && _selection is not null && _clock.GetElapsedTime(_selectedAt) < options.KeyVersionRefreshInterval && _selection.IsTimeValid(_clock.GetUtcNow()))
            {
                return _selection;
            }

            KeyVaultKey key = await metadata.GetAsync(null, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            AzureKeyVaultKeyProfile selected = AzureKeyVaultKeyProfile.Validate(options, key, _clock.GetUtcNow(), null);
            _selection = selected;
            _selectedAt = _clock.GetTimestamp();
            return selected;
        }
        finally
        {
            _selectionLock.Release();
        }
    }
}

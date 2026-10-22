using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;

namespace Hexalith.EventStore.Server.Security;

/// <summary>
/// Occurrence authority. Missing installation denies. A restored divergent snapshot denies. Transition calls delegate to the journal and do not mint an epoch.
/// </summary>
public sealed class InteractionOccurrenceAuthority : IInteractionOccurrenceAuthority
{
    private readonly IInteractionOccurrenceInstallation? _installation;
    private readonly IAnchoredStateTransitionAuthority? _journal;

    /// <summary>Creates the authority. Production passes no installation.</summary>
    /// <param name="installation">Injected epoch and occurrence installation.</param>
    /// <param name="journal">Independent transition journal. Null denies every transition.</param>
    public InteractionOccurrenceAuthority(IInteractionOccurrenceInstallation? installation = null, IAnchoredStateTransitionAuthority? journal = null)
    {
        _installation = installation;
        _journal = journal;
    }

    /// <inheritdoc />
    public async Task<bool> AuthorizeOperationAsync(InteractionOccurrenceIdentity identity, string operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_installation is null || identity is null || operation is not ("Reserve" or "RetainSealed" or "CompleteWriter" or "Lookup"))
        {
            return false;
        }

        try
        {
            return await _installation.AuthorizeOperationAsync(identity, operation, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateStateAsync(string tenantId, string epochId, long revision, string exactStateDigest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_installation is null || _journal is null || string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(epochId))
        {
            return false;
        }

        try
        {
            if (revision < 1 || !await _installation.MatchesAsync(tenantId, epochId, revision, exactStateDigest, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            return await _journal.VerifyTransitionAsync(Proof(tenantId, epochId, revision, exactStateDigest), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetInstalledEpochAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_installation is null || string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        try
        {
            string? epoch = await _installation.GetEpochAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(epoch) ? null : epoch;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> AuthorizeReservationAsync(InteractionOccurrenceRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_installation is null || request?.Identity is null)
        {
            return false;
        }

        try
        {
            return await _installation.AuthorizeReservationAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifySealedAsync(InteractionOccurrenceRecord reservation, InteractionOccurrenceSealedResult sealedResult, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_installation is null || reservation is null || sealedResult is null
            || reservation.WriterState != InteractionOccurrenceWriterState.Reserved
            || sealedResult.SerializationFormat != PayloadProtectionWireFormat.ProtectedSerializationFormat
            || sealedResult.KeyReference != reservation.KeyReference
            || sealedResult.PayloadBytes is not { Length: > 0 })
        {
            return false;
        }

        try
        {
            return await _installation.VerifySealedAsync(reservation, sealedResult, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyWriterAsync(InteractionOccurrenceRecord record, string proofId, bool persisted, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_installation is null || record is null || string.IsNullOrWhiteSpace(proofId))
        {
            return false;
        }

        try
        {
            return await _installation.VerifyWriterAsync(record, proofId, persisted, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RecordRevisionAsync(string tenantId, string epochId, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_installation is null || _journal is null || nextRevision < 1 || nextRevision != expectedRevision + 1)
        {
            return false;
        }

        try
        {
            if (!await _installation.MatchesAsync(tenantId, epochId, nextRevision, exactStateDigest, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            return await _journal.VerifyTransitionAsync(Proof(tenantId, epochId, nextRevision, exactStateDigest), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => Forward(transition, _journal is null ? null : token => _journal.AdmitTransitionAsync(transition, token), cancellationToken);

    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => Forward(transition, _journal is null ? null : token => _journal.RecoverTransitionAsync(transition, token), cancellationToken);

    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => Forward(transition, _journal is null ? null : token => _journal.RecordTransitionAsync(transition, token), cancellationToken);

    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => Forward(transition, _journal is null ? null : token => _journal.VerifyTransitionAsync(transition, token), cancellationToken);

    private Task<bool> Forward(AnchoredStateTransition transition, Func<CancellationToken, Task<bool>>? call, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_installation is null || call is null || transition is null)
        {
            return Task.FromResult(false);
        }

        return Invoke(call, cancellationToken);
    }

    private static async Task<bool> Invoke(Func<CancellationToken, Task<bool>> call, CancellationToken cancellationToken)
    {
        try
        {
            return await call(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static AnchoredStateTransition Proof(string tenantId, string epochId, long revision, string exactStateDigest)
    {
        if (revision < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        byte[] bytes = Encoding.UTF8.GetBytes(string.Join('\n', "occurrence", tenantId, epochId, revision.ToString(System.Globalization.CultureInfo.InvariantCulture), exactStateDigest));
        string target = Convert.ToHexString(SHA256.HashData(bytes));
        long expected = checked(revision - 1);
        string predecessor = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("occurrence-predecessor\n" + tenantId + "\n" + expected.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        return new("occurrence|" + tenantId + "|" + epochId, expected, revision, predecessor, target, bytes);
    }
}

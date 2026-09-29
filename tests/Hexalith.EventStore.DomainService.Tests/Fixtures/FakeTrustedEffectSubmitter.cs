using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>
/// A synthetic target receipt authority: the first submission of an effect persists a receipt and later ones
/// replay it, exactly like the Story 4.13 target inbox. Receipts survive simulated reminder-host restarts.
/// </summary>
internal sealed class FakeTrustedEffectSubmitter : ITrustedEffectSubmitter
{
    private readonly Dictionary<string, TrustedEffectResult> _receipts = new(StringComparer.Ordinal);

    /// <summary>Gets every submission received, in order.</summary>
    public List<(TrustedEffectSubmission Submission, TrustedEffectContext Context)> Calls { get; } = [];

    /// <summary>Gets the durable receipts by effect identifier.</summary>
    public IReadOnlyDictionary<string, TrustedEffectResult> Receipts => _receipts;

    /// <summary>Gets or sets how many next submissions end with an uncertain transport failure.</summary>
    public int FailuresRemaining { get; set; }

    /// <summary>Gets or sets a value indicating whether a failing submission still persisted its receipt.</summary>
    public bool PersistBeforeFailure { get; set; }

    /// <summary>Gets or sets the disposition the target records for a first submission.</summary>
    public TrustedEffectDisposition Disposition { get; set; } = TrustedEffectDisposition.Success;

    /// <summary>Gets or sets an override applied to every returned receipt, such as a mismatched effect identifier.</summary>
    public Func<TrustedEffectResult, TrustedEffectResult>? ReceiptOverride { get; set; }

    /// <inheritdoc/>
    public Task<TrustedEffectResult> SubmitAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((submission, context));
        string effectId = EffectIdentityCodec.ComputeEffectId(submission.Identity);
        if (submission.MessageId != "wrk-" + effectId || submission.IdempotencyKey != submission.MessageId)
        {
            throw new InvalidOperationException("The gateway recomputes and rejects a non-deterministic effect key.");
        }

        if (FailuresRemaining > 0)
        {
            FailuresRemaining--;
            if (PersistBeforeFailure)
            {
                _ = _receipts.TryAdd(effectId, new TrustedEffectResult(effectId, TrustedEffectDisposition.Success, false, null));
            }

            throw new HttpRequestException("Synthetic gateway timeout.");
        }

        if (_receipts.TryGetValue(effectId, out TrustedEffectResult? prior))
        {
            return Task.FromResult(Returned(prior with { Replayed = true }));
        }

        var receipt = new TrustedEffectResult(effectId, Disposition, false, null);
        _receipts[effectId] = receipt;
        return Task.FromResult(Returned(receipt));
    }

    private TrustedEffectResult Returned(TrustedEffectResult receipt) => ReceiptOverride?.Invoke(receipt) ?? receipt;
}

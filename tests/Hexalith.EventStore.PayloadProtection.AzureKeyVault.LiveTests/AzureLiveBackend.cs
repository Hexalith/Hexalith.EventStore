// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 5-7; separately authorized isolated environment required.
namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests;
/// <summary>Test-only retained-target selection and explicit boundary fault injection, never reachable from runtime configuration.</summary>
internal sealed class AzureLiveBackend(AzureKeyVaultPayloadProtectionBackend real, string selected) : PayloadProtectionBackend
{
    internal string Selected { get; set; } = selected;
    internal string? Fault { get; set; }
    internal int Calls { get; private set; }
    internal override string BackendId => real.BackendId;
    internal override string EndpointFingerprint => real.EndpointFingerprint;

    internal override async ValueTask<PayloadBackendResult> WrapAsync(string operationId, ReadOnlyMemory<byte> dek, CancellationToken cancellationToken)
    {
        Calls++;
        if (Fault == "lost-wrap-response")
        {
            using var lost = await real.WrapExactAsync(operationId, Selected, dek, cancellationToken);
            lost.Failure.ShouldBe(PayloadBackendFailure.None);
            return new(PayloadBackendFailure.Ambiguous);
        }

        if (Fault is not null)
        {
            return Failure(Fault);
        }

        return await real.WrapExactAsync(operationId, Selected, dek, cancellationToken);
    }

    internal override async ValueTask<PayloadBackendResult> WrapExactAsync(string operationId, string providerKeyVersionId, ReadOnlyMemory<byte> dek, CancellationToken cancellationToken)
    {
        Calls++;
        return Fault is null ? await real.WrapExactAsync(operationId, providerKeyVersionId, dek, cancellationToken) : Failure(Fault);
    }

    internal override async ValueTask<PayloadBackendResult> UnwrapAsync(string operationId, string providerKeyVersionId, ReadOnlyMemory<byte> wrappedDek, CancellationToken cancellationToken)
    {
        Calls++;
        return Fault is null ? await real.UnwrapAsync(operationId, providerKeyVersionId, wrappedDek, cancellationToken) : Failure(Fault);
    }

    internal override ValueTask<PayloadBackendResult> ReconcileAsync(string operationId, CancellationToken cancellationToken) => real.ReconcileAsync(operationId, cancellationToken);
    private static PayloadBackendResult Failure(string fault) => fault switch
    {
        "429" => new(PayloadBackendFailure.Unavailable, retryAfter: TimeSpan.Zero),
        "dns" => new(PayloadBackendFailure.Unavailable),
        "timeout" or "5xx" => new(PayloadBackendFailure.Ambiguous),
        _ => throw new InvalidOperationException("Unknown authorized fault."),
    };
}

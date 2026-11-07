using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>A transport classification fixture only; it cannot qualify Platform authority or production custody.</summary>
internal sealed class PayloadBackendTestAuthority : PayloadKeyBackendTransportAuthority
{
    /// <summary>Gets or sets fixture admission.</summary>
    internal bool Admit { get; set; } = true;
    /// <summary>Gets or sets fixture response authentication.</summary>
    internal bool Authenticate { get; set; } = true;
    /// <summary>Gets or sets separate original-operation absence proof; a missing key never sets it.</summary>
    internal bool OriginalAbsent { get; set; }
    /// <summary>Gets the retained exact encoded owners for post-completion zeroing assertions.</summary>
    internal List<ReadOnlyMemory<byte>> Encoded { get; } = [];
    /// <summary>Gets the retained owned request arrays for post-completion zeroing assertions.</summary>
    internal List<byte[]> Inputs { get; } = [];
    /// <summary>Gets or sets a request-admission callback for cancellation tests.</summary>
    internal Func<PayloadKeyBackendRequest, CancellationToken, ValueTask>? Preparing { get; set; }
    internal override async ValueTask<IReadOnlyDictionary<string, string>?> PrepareAsync(PayloadKeyBackendRequest request, ReadOnlyMemory<byte> encodedRequest, CancellationToken cancellationToken)
    {
        Encoded.Add(encodedRequest);
        if (request.Bytes is not null) { Inputs.Add(request.Bytes); }
        if (Preparing is not null) { await Preparing(request, cancellationToken); }
        return Admit ? new Dictionary<string, string> { ["custody-operation-proof"] = "fixture", ["custody-control-proof"] = "fixture", ["custody-key-admission"] = "fixture" } : null;
    }
    internal override ValueTask<bool> ValidateResponseAsync(PayloadKeyBackendRequest request, ReadOnlyMemory<byte> encodedRequest,
        PayloadKeyBackendResponse response, ReadOnlyMemory<byte> encodedResponse, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        Encoded.Add(encodedResponse);
        return ValueTask.FromResult(Authenticate);
    }
    /// <summary>Gets or sets an explicit proof failure/cancellation hook.</summary>
    internal Func<CancellationToken, ValueTask<bool>>? Confirming { get; set; }
    internal override ValueTask<bool> ConfirmsOriginalMutationAbsentAsync(string operationId, CancellationToken cancellationToken) => Confirming?.Invoke(cancellationToken) ?? ValueTask.FromResult(OriginalAbsent);
}

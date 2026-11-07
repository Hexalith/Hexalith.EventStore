// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// On-premises authority: 835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180; approved supplement sections 2, 4-5.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Installed Platform bridge to its existing current-authority and transport-admission primitives; no default grant exists.</summary>
/// <remarks>The bridge must bind the exact encoded request, current lifecycle/fleet authority and installed provider/key identity before native admission.
/// Response authentication must establish native completion or exact-original absence, never mere Dapr receipt. EventStore invents no substitute signatures.</remarks>
internal abstract class PayloadKeyBackendTransportAuthority
{
    /// <summary>Returns current qualified metadata for this exact request, or null to refuse before transport.</summary>
    internal abstract ValueTask<IReadOnlyDictionary<string, string>?> PrepareAsync(PayloadKeyBackendRequest request,
        ReadOnlyMemory<byte> encodedRequest, CancellationToken cancellationToken);

    /// <summary>Separately proves exact-original native mutation absence; missing keys, local journals and denial never suffice.</summary>
    internal virtual ValueTask<bool> ConfirmsOriginalMutationAbsentAsync(string operationId, CancellationToken cancellationToken)
        => ValueTask.FromResult(false);

    /// <summary>Authenticates the exact response using installed Platform primitives, including native evidence for its closed outcome.</summary>
    internal abstract ValueTask<bool> ValidateResponseAsync(PayloadKeyBackendRequest request, ReadOnlyMemory<byte> encodedRequest,
        PayloadKeyBackendResponse response, ReadOnlyMemory<byte> encodedResponse, IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken);
}

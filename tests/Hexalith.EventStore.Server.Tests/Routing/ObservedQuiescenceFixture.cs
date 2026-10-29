using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Development test owner: signs only registered, observed successful server execution completion, never a client cancellation or elapsed timeout.</summary>
internal sealed class ObservedQuiescenceFixture : IRouteCatalogQuiescenceVerifier
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    private readonly ConcurrentDictionary<string, (RouteCatalogDispatchFence Fence, Task ServerExecution)> _observed = new();

    public void Observe(RouteCatalogDispatchFence fence, Task serverExecution) => _observed[fence.LeaseId] = (fence, serverExecution);

    public RouteCatalogQuiescenceRequest Issue(RouteCatalogDispatchFence fence)
    {
        if (!_observed.TryGetValue(fence.LeaseId, out var observed) || observed.Fence != fence || !observed.ServerExecution.IsCompletedSuccessfully)
            throw new RouteCatalogUnavailableException("Server execution has not been observed quiescent.");
        return new(fence.LeaseId, fence.RootDigest, fence.Epoch, Convert.ToHexStringLower(Sign(fence)));
    }

    public Task VerifyAsync(RouteCatalogQuiescenceRequest request, RouteCatalogDispatchFence fence, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RouteCatalogQuiescenceRequest expected = Issue(fence);
        byte[] supplied;
        try { supplied = Convert.FromHexString(request.Proof); }
        catch (FormatException) { throw new RouteCatalogUnavailableException("Invalid owner completion receipt."); }
        if (request.LeaseId != expected.LeaseId || request.RootDigest != expected.RootDigest || request.Epoch != expected.Epoch
            || !CryptographicOperations.FixedTimeEquals(Sign(fence), supplied))
            throw new RouteCatalogUnavailableException("Invalid owner completion receipt.");
        return Task.CompletedTask;
    }

    private byte[] Sign(RouteCatalogDispatchFence fence) => HMACSHA256.HashData(_key, JsonSerializer.SerializeToUtf8Bytes(fence));
}

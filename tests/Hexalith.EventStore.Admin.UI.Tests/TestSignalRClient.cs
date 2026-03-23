using Hexalith.EventStore.SignalR;

using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.Admin.UI.Tests;

/// <summary>
/// Test-safe wrapper around <see cref="EventStoreSignalRClient"/> that also implements
/// <see cref="IDisposable"/> for bUnit's synchronous disposal.
/// </summary>
internal sealed class TestSignalRClient : IDisposable
{
    private readonly EventStoreSignalRClient _inner;

    public TestSignalRClient()
    {
        _inner = new EventStoreSignalRClient(
            new EventStoreSignalRClientOptions { HubUrl = "https://localhost:9999/hubs/test" },
            NullLogger<EventStoreSignalRClient>.Instance);
    }

    public EventStoreSignalRClient Inner => _inner;

    public void Dispose()
    {
        // Fire-and-forget async dispose — acceptable in test context
        _inner.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}

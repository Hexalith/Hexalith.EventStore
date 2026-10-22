using Hexalith.EventStore.SignalR;

using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.Admin.UI.Tests;

/// <summary>
/// Test-safe wrapper around <see cref="EventStoreSignalRClient"/> that also implements
/// <see cref="IDisposable"/> for bUnit's synchronous disposal.
/// </summary>
internal sealed class TestSignalRClient : IDisposable {
    public TestSignalRClient() => Inner = new EventStoreSignalRClient(
            new EventStoreSignalRClientOptions { HubUrl = "https://localhost:9999/hubs/test" },
            NullLogger<EventStoreSignalRClient>.Instance);

    public EventStoreSignalRClient Inner { get; }

    public void Dispose() =>
        // Fire-and-forget async dispose — acceptable in test context
        Inner.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

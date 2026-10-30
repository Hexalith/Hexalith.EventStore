namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Process-wide gate that serializes code blocks sharing local DAPR/Aspire infrastructure.
/// </summary>
public static class DaprTestExecutionGate {
    private static readonly SemaphoreSlim s_gate = new(1, 1);

    /// <summary>Enters the gate, blocking until it is free, and returns a lease that releases on dispose.</summary>
    public static IDisposable Enter() {
        s_gate.Wait();
        return new Lease();
    }

    private sealed class Lease : IDisposable {
        private bool _disposed;

        public void Dispose() {
            if (_disposed) {
                return;
            }

            _disposed = true;
            _ = s_gate.Release();
        }
    }
}

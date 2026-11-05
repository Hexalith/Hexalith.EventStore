namespace Hexalith.EventStore.ServiceDefaults;

/// <summary>Retains local privacy across path re-execution and asynchronous descendants without a wire field.</summary>
internal sealed class ReadDiagnosticsPrivacyScope : IDisposable
{
    private static readonly AsyncLocal<bool> _active = new();
    private readonly bool _previous;
    private bool _disposed;

    private ReadDiagnosticsPrivacyScope()
    {
        _previous = _active.Value;
        _active.Value = true;
    }

    /// <summary>Gets whether this execution context is processing or draining protected read work.</summary>
    internal static bool IsActive => _active.Value;

    /// <summary>Protects the current execution and descendants, preserving the previous local context.</summary>
    /// <returns>The scope that restores its own caller when disposed.</returns>
    internal static IDisposable Begin() => new ReadDiagnosticsPrivacyScope();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _active.Value = _previous;
            _disposed = true;
        }
    }
}

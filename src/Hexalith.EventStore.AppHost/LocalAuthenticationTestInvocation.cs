namespace Hexalith.EventStore.AppHost;

/// <summary>
/// Caller-held, one-use capability for an explicitly isolated AppHost test invocation.
/// </summary>
internal sealed class LocalAuthenticationTestInvocation : IDisposable
{
    private bool _activated;
    private bool _consumed;
    private bool _disposed;

    internal LocalAuthenticationTestInvocation(LocalAuthenticationCredentials credentials)
    {
        Credentials = credentials;
    }

    /// <summary>Gets the isolated values so a test issuer can use the same ephemeral key.</summary>
    public LocalAuthenticationCredentials Credentials { get; }

    /// <summary>
    /// Activates this capability for the current asynchronous invocation flow.
    /// </summary>
    public void Activate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_activated || _consumed)
        {
            throw new InvalidOperationException("The local authentication test invocation is already active or consumed.");
        }

        LocalAuthenticationCredentials.ActivateTestInvocation(this);
        _activated = true;
    }

    /// <summary>
    /// Consumes the capability exactly once when the AppHost creates its local credential set.
    /// </summary>
    /// <returns>The isolated credential set.</returns>
    internal LocalAuthenticationCredentials Consume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_activated || _consumed)
        {
            throw new InvalidOperationException("The local authentication test invocation is not active or was already consumed.");
        }

        _activated = false;
        _consumed = true;
        return Credentials;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_activated)
        {
            LocalAuthenticationCredentials.DeactivateTestInvocation(this);
            _activated = false;
        }

        _disposed = true;
    }
}

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>
/// Owns the one active append-durability race session for the isolated live-sidecar collection.
/// </summary>
public sealed class AppendDurabilityRaceControl
{
    private readonly object _sync = new();
    private AppendDurabilityRaceSession? _activeSession;

    /// <summary>Starts a session that gates the first global-position allocation.</summary>
    /// <param name="sessionId">The evidence-safe session identifier.</param>
    /// <returns>The active session.</returns>
    public AppendDurabilityRaceSession BeginSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        lock (_sync)
        {
            if (_activeSession is not null)
            {
                throw new InvalidOperationException("An append-durability race session is already active.");
            }

            _activeSession = new AppendDurabilityRaceSession(this, sessionId);
            return _activeSession;
        }
    }

    /// <summary>Releases and clears any session left active by a failed test.</summary>
    public void Reset()
    {
        AppendDurabilityRaceSession? session;
        lock (_sync)
        {
            session = _activeSession;
            _activeSession = null;
        }

        session?.Release();
    }

    /// <summary>Gets the active allocation interceptor, if a race session is running.</summary>
    /// <returns>The active session or <see langword="null"/>.</returns>
    internal AppendDurabilityRaceSession? GetActiveSession()
    {
        lock (_sync)
        {
            return _activeSession;
        }
    }

    /// <summary>Clears the supplied session when it is still the active one.</summary>
    /// <param name="session">The session being disposed.</param>
    internal void Complete(AppendDurabilityRaceSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_sync)
        {
            if (ReferenceEquals(_activeSession, session))
            {
                _activeSession = null;
            }
        }
    }
}

using System.Diagnostics;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class ProviderStateCoordinator(
    IReadOnlySet<string> allowedStates,
    TimeSpan? transitionDelay = null,
    bool failForcedCleanup = false)
{
    private readonly object _sync = new();
    private readonly List<ProviderStateEvent> _events = [];
    private string? _expectedState;
    private string? _state;

    public string? CurrentState
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public void BeginInteraction(string state)
    {
        lock (_sync)
        {
            _expectedState = state;
            _state = null;
            _events.Clear();
        }
    }

    public async Task<string> ApplyAsync(string state, string action, CancellationToken cancellationToken)
    {
        if (transitionDelay is { } delay)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        var stopwatch = Stopwatch.StartNew();
        string resultCode;
        lock (_sync)
        {
            if (!allowedStates.Contains(state) || !string.Equals(state, _expectedState, StringComparison.Ordinal))
            {
                resultCode = "state.unknown-or-unexpected";
            }
            else if (string.Equals(action, "setup", StringComparison.OrdinalIgnoreCase))
            {
                _state = state;
                resultCode = "state.setup.succeeded";
            }
            else if (string.Equals(action, "teardown", StringComparison.OrdinalIgnoreCase))
            {
                _state = null;
                resultCode = "state.teardown.succeeded";
            }
            else
            {
                resultCode = "state.action.invalid";
            }

            _events.Add(new ProviderStateEvent(state, action.ToLowerInvariant(), resultCode, stopwatch.ElapsedMilliseconds));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return resultCode;
    }

    public IReadOnlyList<ProviderStateEvent> SnapshotEvents()
    {
        lock (_sync)
        {
            return _events.ToArray();
        }
    }

    public bool ForceCleanup(string state)
    {
        lock (_sync)
        {
            if (failForcedCleanup)
            {
                _events.Add(new ProviderStateEvent(state, "teardown", "state.teardown.failed", 0));
                return false;
            }

            bool wasClean = _state is null;
            _state = null;
            if (!wasClean)
            {
                _events.Add(new ProviderStateEvent(state, "teardown", "state.teardown.forced", 0));
            }

            return _state is null;
        }
    }
}


using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// In-memory test double for <see cref="IActorStateMachine"/>.
/// Tracks all checkpoint, load, and cleanup calls for test assertions.
/// Supports configurable existing pipeline state for resume testing.
/// </summary>
public sealed class FakeActorStateMachine : IActorStateMachine {
    private readonly Dictionary<string, PipelineState> _states = [];
    private readonly List<(string Key, PipelineState State)> _checkpointCalls = [];
    private readonly List<(string Prefix, string CorrelationId)> _loadCalls = [];
    private readonly List<(string Prefix, string CorrelationId)> _cleanupCalls = [];

    /// <summary>Gets all checkpoint calls made.</summary>
    public IReadOnlyList<(string Key, PipelineState State)> CheckpointCalls => _checkpointCalls;

    /// <summary>Gets all load calls made.</summary>
    public IReadOnlyList<(string Prefix, string CorrelationId)> LoadCalls => _loadCalls;

    /// <summary>Gets all cleanup calls made.</summary>
    public IReadOnlyList<(string Prefix, string CorrelationId)> CleanupCalls => _cleanupCalls;

    /// <summary>Gets the current pipeline states (after checkpoints and cleanups).</summary>
    public IReadOnlyDictionary<string, PipelineState> States => _states;

    /// <summary>
    /// Seeds an existing pipeline state for resume testing.
    /// </summary>
    public void SeedPipelineState(string pipelineKeyPrefix, PipelineState state) {
        ArgumentNullException.ThrowIfNull(state);
        string key = $"{pipelineKeyPrefix}{state.CorrelationId}";
        _states[key] = state;
    }

    /// <inheritdoc/>
    public Task CheckpointAsync(string pipelineKeyPrefix, PipelineState state) {
        ArgumentNullException.ThrowIfNull(state);
        string key = $"{pipelineKeyPrefix}{state.CorrelationId}";
        _checkpointCalls.Add((key, state));
        _states[key] = state;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<PipelineState?> LoadPipelineStateAsync(string pipelineKeyPrefix, string correlationId) {
        _loadCalls.Add((pipelineKeyPrefix, correlationId));
        string key = $"{pipelineKeyPrefix}{correlationId}";
        return Task.FromResult(_states.TryGetValue(key, out PipelineState? state) ? state : null);
    }

    /// <inheritdoc/>
    public Task CleanupPipelineAsync(string pipelineKeyPrefix, string correlationId) {
        _cleanupCalls.Add((pipelineKeyPrefix, correlationId));
        string key = $"{pipelineKeyPrefix}{correlationId}";
        _ = _states.Remove(key);
        return Task.CompletedTask;
    }
}

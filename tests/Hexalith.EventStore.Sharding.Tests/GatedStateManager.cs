using System.Reflection;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Sharding.TestHost;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Pauses actual actor state-manager calls at deterministic expiry boundaries.</summary>
public class GatedStateManager : DispatchProxy
{
    /// <summary>Gets the isolated pending/committed state implementation.</summary>
    public InMemoryStateManager Inner { get; } = new();

    /// <summary>Gets or sets the read, stage, or save boundary to pause.</summary>
    public string Boundary { get; set; } = string.Empty;

    /// <summary>Gets or sets the action that waits for the real fence to expire.</summary>
    public Func<Task> WaitForExpiry { get; set; } = () => Task.CompletedTask;

    /// <summary>Gets whether the requested state boundary was actually reached.</summary>
    public bool ReachedBoundary { get; private set; }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        ArgumentNullException.ThrowIfNull(args);
        if (targetMethod.Name == "TryGetStateAsync" && targetMethod.GetGenericArguments()[0] == typeof(ShardState))
            return ReadAsync((Task<ConditionalValue<ShardState>>)targetMethod.Invoke(Inner, args)!);
        if (targetMethod.Name == "SetStateAsync" && targetMethod.GetGenericArguments()[0] == typeof(ShardState))
            return StageAsync((Task)targetMethod.Invoke(Inner, args)!);
        if (targetMethod.Name == "SaveStateAsync")
            return SaveAsync((CancellationToken)args![0]!);
        return targetMethod.Invoke(Inner, args);
    }

    private async Task<ConditionalValue<ShardState>> ReadAsync(Task<ConditionalValue<ShardState>> read)
    {
        ConditionalValue<ShardState> value = await read;
        if (Boundary == "read") { ReachedBoundary = true; await WaitForExpiry(); }
        return value;
    }

    private async Task StageAsync(Task stage)
    {
        await stage;
        if (Boundary == "stage") { ReachedBoundary = true; await WaitForExpiry(); }
    }

    private async Task SaveAsync(CancellationToken token)
    {
        if (Boundary == "save")
        {
            ReachedBoundary = true;
            await WaitForExpiry();
            token.ThrowIfCancellationRequested();
        }

        await Inner.SaveStateAsync(token);
    }
}

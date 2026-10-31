using System.Reflection;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Injects exactly one ambiguous save failure before or after durable commit.</summary>
public class SaveFailureStateManager : DispatchProxy
{
    /// <summary>Gets the independent staged and committed store.</summary>
    public InMemoryStateManager Inner { get; } = new();

    /// <summary>Gets or sets whether the injected failure follows durable commit.</summary>
    public bool CommitBeforeFailure { get; set; }

    /// <summary>Gets or sets whether failure occurs after staging instead of during save.</summary>
    public bool FailDuringStage { get; set; }

    /// <summary>Gets or sets whether the next save should fail.</summary>
    public bool FailNextSave { get; set; } = true;

    /// <summary>Gets the exact original exception retained across diagnostic failures.</summary>
    public IOException SaveFailure { get; } = new("Injected uncertain save outcome.");

    /// <summary>Gets actual save invocations through the actor's state manager.</summary>
    public int SaveAttempts { get; private set; }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        ArgumentNullException.ThrowIfNull(args);
        if (targetMethod.Name == "SetStateAsync" && FailNextSave && FailDuringStage)
        {
            FailNextSave = false;
            return FailStageAsync((Task)targetMethod.Invoke(Inner, args)!);
        }
        if (targetMethod.Name == "SaveStateAsync")
        {
            SaveAttempts++;
            if (FailNextSave)
            {
                FailNextSave = false;
                return FailSaveAsync((CancellationToken)args![0]!);
            }
        }

        return targetMethod.Invoke(Inner, args);
    }

    private static async Task FailStageAsync(Task stage)
    {
        await stage;
        throw new IOException("Injected staging failure.");
    }

    private async Task FailSaveAsync(CancellationToken token)
    {
        if (CommitBeforeFailure) await Inner.SaveStateAsync(token);
        throw SaveFailure;
    }
}

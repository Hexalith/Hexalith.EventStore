using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Injects authority or cancellation at an actual router admission boundary.</summary>
internal sealed class DaprLogicalCommandStateAdmissionStage : IDomainServiceAdmissionStage
{
    /// <summary>Gets or sets an exact callback.</summary>
    internal Action? Hook
    {
        get; set;
    }
    /// <summary>Gets or sets a typed request mutation hook for stage-state isolation controls.</summary>
    internal Action<DomainServiceAdmissionContext>? ContextHook
    {
        get; set;
    }
    /// <summary>Gets invocation count.</summary>
    internal int Calls
    {
        get; private set;
    }
    /// <inheritdoc/>
    public string Name => "logical-command-test";
    /// <inheritdoc/>
    public Task<DomainServiceAdmissionResult> EvaluateAsync(DomainServiceAdmissionContext context, CancellationToken cancellationToken)
    {
        Calls++;
        Hook?.Invoke();
        ContextHook?.Invoke(context);
        return Task.FromResult(DomainServiceAdmissionResult.Accepted());
    }
}

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Positive ownership of one exact outbox by its currently running hosted worker.</summary>
internal sealed class ProjectionActivationWorkerCapability(
    ProjectionActivationWorker worker, IProjectionActivationOutbox outbox, bool enabled,
    IReadOnlySet<string>? responseDomains = null)
{
    /// <summary>Gets the exact worker aliased into the hosted-service registration.</summary>
    internal ProjectionActivationWorker Worker { get; } = worker;

    /// <summary>Gets the explicit qualified-cohort behavior opt-in.</summary>
    internal bool Enabled { get; } = enabled;

    /// <summary>Restricts response deferral to an explicitly qualified domain when configured.</summary>
    internal bool EnabledFor(string domain) => Enabled && (responseDomains is null || responseDomains.Contains(domain));

    /// <summary>Requires the actual hosted lifetime and the identical durable outbox.</summary>
    internal bool CanDefer(IProjectionActivationOutbox candidate, string domain)
        => EnabledFor(domain) && Worker.IsRunning && ReferenceEquals(outbox, candidate) && Worker.OwnsOutbox(candidate);
}

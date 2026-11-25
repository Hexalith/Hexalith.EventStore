namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Result classes for scoped Durable Task admission.</summary>
public enum DurableTaskAdmissionOutcome
{
    /// <summary>The intent resolved to a committed task.</summary>
    Admitted,

    /// <summary>Current authority was not established.</summary>
    Denied,

    /// <summary>The scoped key was already admitted with a different request.</summary>
    Conflict,

    /// <summary>A new intent requires valid current confirmation.</summary>
    RenewConfirmation,
}

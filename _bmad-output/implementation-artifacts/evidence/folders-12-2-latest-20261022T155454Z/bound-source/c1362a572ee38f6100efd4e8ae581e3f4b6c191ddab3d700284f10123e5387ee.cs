namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Exception thrown when a command's tenant does not match the actor's tenant identity.
/// SEC-2: Indicates a potential routing error or security violation.
/// </summary>
public class TenantMismatchException : InvalidOperationException {
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantMismatchException"/> class.
    /// </summary>
    /// <param name="commandTenant">The tenant from the command.</param>
    /// <param name="actorTenant">The tenant from the actor identity.</param>
    public TenantMismatchException(string commandTenant, string actorTenant)
        : base($"TenantMismatch: command tenant '{commandTenant}' does not match actor tenant '{actorTenant}'") {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandTenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorTenant);
        CommandTenant = commandTenant;
        ActorTenant = actorTenant;
    }

    /// <summary>Gets the tenant from the command.</summary>
    public string CommandTenant { get; }

    /// <summary>Gets the tenant from the actor identity.</summary>
    public string ActorTenant { get; }
}

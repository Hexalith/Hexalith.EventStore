namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Outcome resolved from authoritative retained command/event state.</summary>
public enum ContentPublicationOutcome
{
    /// <summary>No terminal append decision is established; bytes stay protected and hidden.</summary>
    Pending,
    /// <summary>The exact prepared batch was durably accepted.</summary>
    Accepted,
    /// <summary>The exact command was durably rejected and cannot later append.</summary>
    Rejected,
}

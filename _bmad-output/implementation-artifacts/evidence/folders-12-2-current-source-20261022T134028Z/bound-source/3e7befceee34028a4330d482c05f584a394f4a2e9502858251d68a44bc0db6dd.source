namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Closed directory/current-epoch writer categories; classification must come from the concrete command contract and authoritative owner facts.</summary>
public enum DirectoryWriteKind
{
    /// <summary>Interaction or failure creation.</summary>
    Create = 1,
    /// <summary>Interaction content append.</summary>
    ContentAppend = 2,
    /// <summary>Directory permit creation.</summary>
    Permit = 3,
    /// <summary>Creation or workflow-start outbox.</summary>
    CreationOutbox = 4,
    /// <summary>Protected user-action intent/outbox.</summary>
    UserActionIntent = 5,
    /// <summary>Effect lease acquisition.</summary>
    LeaseAcquire = 6,
    /// <summary>Effect lease commit.</summary>
    LeaseCommit = 7,
    /// <summary>Rate admission authorization.</summary>
    RateAuthorization = 8,
    /// <summary>Open-interaction authorization.</summary>
    OpenAuthorization = 9,
    /// <summary>Budget reservation authorization.</summary>
    BudgetAuthorization = 10,
    /// <summary>Capacity admission authorization.</summary>
    CapacityAuthorization = 11,
    /// <summary>Only an exact prior manifested result, acknowledgement, cancellation or settlement; cannot create new authority.</summary>
    OriginalRecoveryResult = 12,
}

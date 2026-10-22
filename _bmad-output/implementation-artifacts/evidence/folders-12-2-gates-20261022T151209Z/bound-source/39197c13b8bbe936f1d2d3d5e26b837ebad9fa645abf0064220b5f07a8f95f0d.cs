namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Identifies the durable payload kind supplied to payload-protection policy and occurrence contexts.
/// Implements Story 8.1 sections 7 and 9 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
public enum PayloadProtectionPayloadKind {
    /// <summary>The payload is an event.</summary>
    Event = 1,

    /// <summary>The payload is a snapshot.</summary>
    Snapshot = 2,
}

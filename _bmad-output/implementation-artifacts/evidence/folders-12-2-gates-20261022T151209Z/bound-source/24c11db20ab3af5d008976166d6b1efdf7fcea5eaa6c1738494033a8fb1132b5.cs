namespace Hexalith.EventStore.Server.Security;

/// <summary>Durable journal disposition. Unknown stays unresolved until a later lookup reads the retained original.</summary>
public enum ProtectionAnchorDisposition
{
    /// <summary>No record is installed for the scope.</summary>
    Absent = 0,

    /// <summary>The original transition is retained and the anchor has not advanced.</summary>
    Admitted = 1,

    /// <summary>The same original was compared and the anchor advanced.</summary>
    Advanced = 2,

    /// <summary>The acknowledgement was lost. Lookup must read the retained original before it is treated as known.</summary>
    Unknown = 3,

    /// <summary>The bytes or anchor do not match the retained original.</summary>
    Conflict = 4,
}

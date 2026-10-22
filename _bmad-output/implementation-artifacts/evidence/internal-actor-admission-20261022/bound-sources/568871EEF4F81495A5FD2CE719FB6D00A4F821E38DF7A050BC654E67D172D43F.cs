namespace Hexalith.EventStore.Server.Actors;

/// <summary>Classifies exact supported aggregate-local legacy source evidence.</summary>
internal enum IdempotencyLegacySourceDecision
{
    /// <summary>The exact supported source state matches the closed inventory.</summary>
    Exact,

    /// <summary>The exact source matches but its replay interval has elapsed.</summary>
    Expired,

    /// <summary>The exact source is permanently redirected to the pinned target.</summary>
    Redirected,

    /// <summary>The exact source record is absent and cannot authorize migration or fresh work.</summary>
    Missing,

    /// <summary>The source schema or shape is not supported.</summary>
    Unsupported,

    /// <summary>The exact source state could not be read or durably redirected.</summary>
    Unavailable,

    /// <summary>The source exists but does not match the exact closed evidence.</summary>
    Conflict,
}

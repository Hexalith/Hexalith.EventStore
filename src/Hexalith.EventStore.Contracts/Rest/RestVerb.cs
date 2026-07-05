namespace Hexalith.EventStore.Contracts.Rest;

/// <summary>
/// HTTP verb used by a generated REST endpoint.
/// </summary>
public enum RestVerb
{
    /// <summary>HTTP GET.</summary>
    Get,

    /// <summary>HTTP POST.</summary>
    Post,

    /// <summary>HTTP PUT.</summary>
    Put,

    /// <summary>HTTP PATCH.</summary>
    Patch,

    /// <summary>HTTP DELETE.</summary>
    Delete,
}

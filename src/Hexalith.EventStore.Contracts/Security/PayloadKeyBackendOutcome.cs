// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// On-premises authority: 835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180; approved supplement sections 2, 4-5.
namespace Hexalith.EventStore.Contracts.Security;

/// <summary>The seven neutral backend outcomes; provider messages never cross this boundary.</summary>
public enum PayloadKeyBackendOutcome
{
    /// <summary>The requested operation completed with validated output.</summary>
    Succeeded = 1,
    /// <summary>Service is unavailable; for a mutation, native absence is affirmatively established.</summary>
    Unavailable = 2,
    /// <summary>Current authenticated authorization was explicitly refused.</summary>
    Denied = 3,
    /// <summary>The exact key/version is absent, or Lookup proves the exact original mutation absent.</summary>
    Missing = 4,
    /// <summary>Validated current lifecycle evidence establishes invalidation.</summary>
    Invalidated = 5,
    /// <summary>Identity, profile, native framing or response data disagrees.</summary>
    ConsistencyMismatch = 6,
    /// <summary>A native effect cannot be established or excluded; never blindly replay it.</summary>
    Ambiguous = 7,
}

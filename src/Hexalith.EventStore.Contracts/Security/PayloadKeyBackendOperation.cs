// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// On-premises authority: 835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180; approved supplement sections 2, 4-5.
namespace Hexalith.EventStore.Contracts.Security;

/// <summary>The closed version-one neutral key operations; none grants key administration.</summary>
public enum PayloadKeyBackendOperation
{
    /// <summary>Freshly observes the installed key profile and current exact version.</summary>
    Profile = 1,
    /// <summary>Wraps a new DEK under the explicitly freshly selected version.</summary>
    Wrap = 2,
    /// <summary>Wraps an existing DEK under its retained exact rewrap target.</summary>
    WrapExact = 3,
    /// <summary>Unwraps only the recorded exact version.</summary>
    Unwrap = 4,
    /// <summary>Looks up original durable evidence without resending a mutation or plaintext delivery.</summary>
    Lookup = 5,
}

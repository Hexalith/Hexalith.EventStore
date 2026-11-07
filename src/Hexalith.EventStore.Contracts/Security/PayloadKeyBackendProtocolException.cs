// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// On-premises authority: 835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180; approved supplement sections 2, 4-5.
namespace Hexalith.EventStore.Contracts.Security;

/// <summary>A fixed, non-disclosing neutral protocol refusal.</summary>
public sealed class PayloadKeyBackendProtocolException : Exception
{
    /// <summary>Creates a bounded failure without provider or parser details.</summary>
    public PayloadKeyBackendProtocolException() : base("Payload key backend protocol validation failed.") { }
}

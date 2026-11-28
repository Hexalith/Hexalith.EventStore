using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Requests a fenced transition before crossing the side-effect boundary.</summary>
/// <param name="FencingToken">The reservation fence.</param>
/// <param name="Authority">Optional authority verification request validated atomically with the transition.</param>
[DataContract]
[method: JsonConstructor]
public sealed record IdempotencyAdmissionTransitionRequest(
    [property: DataMember] long FencingToken,
    [property: DataMember] IdempotencyAdmissionAuthorityRequest? Authority = null)
{
    /// <summary>Initializes a transition request using the original fence contract.</summary>
    /// <param name="FencingToken">The reservation fence.</param>
    public IdempotencyAdmissionTransitionRequest(long FencingToken)
        : this(FencingToken, Authority: null)
    {
    }

    /// <summary>Deconstructs the original fence contract.</summary>
    /// <param name="FencingToken">The reservation fence.</param>
    public void Deconstruct(out long FencingToken) => FencingToken = this.FencingToken;
}

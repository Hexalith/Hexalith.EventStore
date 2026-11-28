using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Requests terminal finalization under the active fence.</summary>
/// <param name="FencingToken">The active fence.</param>
/// <param name="Result">The deterministic command result to retain for replay.</param>
/// <param name="Authority">Optional authority verification request validated atomically with finalization.</param>
[DataContract]
[method: JsonConstructor]
public sealed record IdempotencyAdmissionCompletionRequest(
    [property: DataMember] long FencingToken,
    [property: DataMember] CommandProcessingResult Result,
    [property: DataMember] IdempotencyAdmissionAuthorityRequest? Authority = null)
{
    /// <summary>Initializes a completion request using the original fence and result contract.</summary>
    /// <param name="FencingToken">The active fence.</param>
    /// <param name="Result">The deterministic command result to retain for replay.</param>
    public IdempotencyAdmissionCompletionRequest(long FencingToken, CommandProcessingResult Result)
        : this(FencingToken, Result, Authority: null)
    {
    }

    /// <summary>Deconstructs the original fence and result contract.</summary>
    /// <param name="FencingToken">The active fence.</param>
    /// <param name="Result">The deterministic command result to retain for replay.</param>
    public void Deconstruct(out long FencingToken, out CommandProcessingResult Result)
    {
        FencingToken = this.FencingToken;
        Result = this.Result;
    }
}

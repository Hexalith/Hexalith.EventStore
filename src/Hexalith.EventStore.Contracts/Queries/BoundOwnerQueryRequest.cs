using System.Text.Json.Serialization;
namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>An exact owner query and requester carried only on the workload authority route.</summary>
public sealed record BoundOwnerQueryRequest(SubmitQueryRequest Query, string PrincipalId)
{
    /// <summary>Optional original query whose exact digest is covered by the original gateway assertion.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public QueryEnvelope? OriginatingQuery { get; init; }
    /// <summary>Original gateway assertion, verified for the currently authenticated requesting workload audience.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginatingAssertion { get; init; }
    /// <inheritdoc/>
    public override string ToString() => "BoundOwnerQueryRequest { [REDACTED] }";
}

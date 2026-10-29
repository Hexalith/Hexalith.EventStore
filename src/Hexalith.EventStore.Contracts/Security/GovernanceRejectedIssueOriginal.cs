namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Independently retained actual failed replacement-issue attempt and fresh current guard basis. It proves absence of issue, never an active credential or dispatch.</summary>
public sealed record GovernanceRejectedIssueOriginal(GovernanceGuardTransition Transition, string TargetMutationDigest, GovernanceProtocolReceipt Refusal,
    long CurrentGuardRevision, string CurrentGuardStateDigest)
{
    /// <summary>Exact bounded content-free native guard snapshot bytes independently observed at this basis; the digest never substitutes for missing bytes.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? CurrentGuardStateBytes { get; init; }
}

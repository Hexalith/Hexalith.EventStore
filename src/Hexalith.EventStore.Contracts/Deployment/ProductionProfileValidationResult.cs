namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Structural outcome only; authorization and qualification are always unevaluated.</summary>
/// <param name="IsStructurallyValid">Whether the closed document and caller-supplied inventory agree.</param>
/// <param name="InfrastructureDigest">The computed infrastructure digest on success, otherwise null.</param>
/// <param name="ResolvedDigest">The computed complete-document digest on success, otherwise null.</param>
/// <param name="Issues">Deterministic safe path/code issues.</param>
public sealed record ProductionProfileValidationResult(
    bool IsStructurallyValid,
    string? InfrastructureDigest,
    string? ResolvedDigest,
    IReadOnlyList<ProductionProfileIssue> Issues)
{
    /// <summary>Gets the constant absence of an authorization evaluation.</summary>
    public string Authorization => "unevaluated";

    /// <summary>Gets the constant absence of an authenticated qualification evaluation.</summary>
    public string Qualification => "unevaluated";
}

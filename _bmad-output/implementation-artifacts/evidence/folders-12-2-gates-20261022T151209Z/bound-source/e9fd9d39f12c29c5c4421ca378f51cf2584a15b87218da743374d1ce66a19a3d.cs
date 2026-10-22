namespace Hexalith.EventStore.Contracts.Effects;

/// <summary>Gateway request whose workload identity is derived from authentication, not this body.</summary>
/// <param name="Submission">Effect facts and target command.</param>
/// <param name="Purpose">Named delegated purpose.</param>
/// <param name="CausationId">Source event causation identifier.</param>
/// <param name="DelegationToken">Signed workload delegation credential.</param>
public sealed record TrustedEffectSubmitRequest(
    TrustedEffectSubmission Submission,
    string Purpose,
    string CausationId,
    string DelegationToken);

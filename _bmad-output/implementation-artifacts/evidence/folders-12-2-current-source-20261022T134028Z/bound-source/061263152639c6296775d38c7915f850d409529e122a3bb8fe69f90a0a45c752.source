using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Authenticated, server-derived effect data for the target actor.</summary>
/// <param name="Submission">Validated effect request.</param>
/// <param name="Context">Verified workload delegation.</param>
/// <param name="SemanticDigest">Server-derived canonical command digest.</param>
public sealed record TrustedEffectAdmission(
    TrustedEffectSubmission Submission,
    TrustedEffectContext Context,
    string SemanticDigest);

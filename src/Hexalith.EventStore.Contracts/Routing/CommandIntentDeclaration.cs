using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Owner-authored static operation policy, independent of deployment secrets and runtime registration.</summary>
/// <param name="Route">Actual command declaration.</param>
/// <param name="AdapterId">Stable adapter identity.</param>
/// <param name="OperationId">Stable semantic operation identity.</param>
/// <param name="PolicyVersion">Explicit policy version.</param>
/// <param name="DescriptorSchema">Complete target and semantic field schema.</param>
/// <param name="RetentionTier">Explicit replay retention.</param>
/// <param name="RequiresAuthenticatedSubject">Whether intent includes the trusted acting subject.</param>
public sealed record CommandIntentDeclaration(
    RouteContractDeclaration Route,
    string AdapterId,
    string OperationId,
    string PolicyVersion,
    IdempotencyDescriptorSchema DescriptorSchema,
    IdempotencyReplayRetentionTier RetentionTier,
    bool RequiresAuthenticatedSubject);

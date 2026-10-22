namespace Hexalith.EventStore.Server.Projections;

/// <summary>Describes maintenance qualification without asserting runtime or production readiness.</summary>
internal sealed record ProjectionSourceGuardQualificationResult(bool Qualified, bool Activated, string ReasonCode,
    long PreservedSequence);

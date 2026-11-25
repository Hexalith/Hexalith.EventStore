namespace Hexalith.EventStore.Server.Actors;

/// <summary>Admission decision for a projection delivery write.</summary>
/// <param name="Admitted">True when the delivery write may proceed (phase is <see cref="ProjectionLifecyclePhase.Idle"/>).</param>
/// <param name="Phase">The lifecycle phase observed at admission time.</param>
public sealed record ProjectionDeliveryAdmission(bool Admitted, ProjectionLifecyclePhase Phase);

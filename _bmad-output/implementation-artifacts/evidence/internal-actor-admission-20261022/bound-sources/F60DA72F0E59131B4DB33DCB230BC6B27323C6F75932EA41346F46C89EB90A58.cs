namespace Hexalith.EventStore.Server.Projections;

/// <summary>Determines whether one named projection route may invoke its v2 handler.</summary>
internal enum ProjectionDeliveryAdmissionDisposition {
    /// <summary>A durable reservation was acquired and the handler may run.</summary>
    Dispatch = 0,

    /// <summary>The exact delivery was already completed durably.</summary>
    AlreadyCompleted = 1,

    /// <summary>The delivery remains eligible for stable-identity retry.</summary>
    Retryable = 2,

    /// <summary>The delivery failed closed and needs operator action or corrected input.</summary>
    Failed = 3,
}

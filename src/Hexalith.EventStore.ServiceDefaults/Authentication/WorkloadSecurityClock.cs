namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// The dedicated wall clock of workload-assertion security: issuance (<c>iat</c>, <c>nbf</c>, <c>exp</c>), token
/// caching, and receiver-side lifetime evaluation.
/// </summary>
/// <remarks>
/// It is deliberately separate from the host's <see cref="System.TimeProvider"/> registration, which a host may
/// replace for business time (a frozen or simulated clock). Assertions cross process boundaries and are also checked
/// by JwtBearer lifetime validation against the wall clock, so they must always use real time. The default is
/// <see cref="System.TimeProvider.System"/>; only a test registers another instance, before the workload services.
/// </remarks>
public sealed class WorkloadSecurityClock
{
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="WorkloadSecurityClock"/> class.</summary>
    /// <param name="timeProvider">The time source; production hosts use <see cref="System"/>.</param>
    public WorkloadSecurityClock(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Gets the wall-clock instance registered by default.</summary>
    public static WorkloadSecurityClock System { get; } = new(TimeProvider.System);

    /// <summary>Gets the current UTC time.</summary>
    /// <returns>The current UTC time.</returns>
    public DateTimeOffset GetUtcNow() => _timeProvider.GetUtcNow();
}

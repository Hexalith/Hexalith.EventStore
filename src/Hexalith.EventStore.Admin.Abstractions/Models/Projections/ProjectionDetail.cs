namespace Hexalith.EventStore.Admin.Abstractions.Models.Projections;

/// <summary>
/// Detailed information about a projection, extending <see cref="ProjectionStatus"/> with errors, configuration, and subscribed event types.
/// </summary>
/// <param name="Name">The projection name.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Status">The current projection status.</param>
/// <param name="Lag">The number of events behind the current position.</param>
/// <param name="Throughput">The current processing throughput in events per second.</param>
/// <param name="ErrorCount">The total number of errors encountered.</param>
/// <param name="LastProcessedPosition">The last successfully processed event position.</param>
/// <param name="LastProcessedUtc">When the last event was successfully processed.</param>
/// <param name="Errors">The list of structured errors.</param>
/// <param name="Configuration">The projection configuration as opaque JSON.</param>
/// <param name="SubscribedEventTypes">The event types this projection subscribes to.</param>
public record ProjectionDetail(
    string Name,
    string TenantId,
    ProjectionStatusType Status,
    long Lag,
    double Throughput,
    int ErrorCount,
    long LastProcessedPosition,
    DateTimeOffset LastProcessedUtc,
    IReadOnlyList<ProjectionError> Errors,
    string Configuration,
    IReadOnlyList<string> SubscribedEventTypes)
    : ProjectionStatus(Name, TenantId, Status, Lag, Throughput, ErrorCount, LastProcessedPosition, LastProcessedUtc)
{
    /// <summary>Gets the list of structured errors.</summary>
    public IReadOnlyList<ProjectionError> Errors { get; } = Errors ?? throw new ArgumentNullException(nameof(Errors));

    /// <summary>Gets the projection configuration as opaque JSON.</summary>
    public string Configuration { get; } = Configuration ?? throw new ArgumentNullException(nameof(Configuration));

    /// <summary>Gets the event types this projection subscribes to.</summary>
    public IReadOnlyList<string> SubscribedEventTypes { get; } = SubscribedEventTypes ?? throw new ArgumentNullException(nameof(SubscribedEventTypes));

    /// <summary>
    /// Returns a string representation with configuration data redacted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"ProjectionDetail {{ Name = {Name}, TenantId = {TenantId}, Status = {Status}, Lag = {Lag}, Throughput = {Throughput}, ErrorCount = {ErrorCount}, LastProcessedPosition = {LastProcessedPosition}, LastProcessedUtc = {LastProcessedUtc}, Errors = [{Errors.Count} items], Configuration = [REDACTED], SubscribedEventTypes = [{SubscribedEventTypes.Count} items] }}";
}

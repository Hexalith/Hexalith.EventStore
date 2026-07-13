namespace Hexalith.EventStore.Client.Projections;

/// <summary>Identifies one exact canonical named projection route.</summary>
/// <param name="Domain">The canonical domain.</param>
/// <param name="ProjectionType">The canonical projection type.</param>
public sealed record ProjectionDispatchRoute(string Domain, string ProjectionType);

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// One canonical operational domain-service route and the credential it requires.
/// </summary>
/// <param name="Route">The route pattern, with a leading slash.</param>
/// <param name="Operation">The workload operation an assertion must grant.</param>
/// <param name="Policy">The authorization policy the effective endpoint must carry.</param>
public sealed record EventStoreDomainServiceRoute(string Route, string Operation, string Policy);

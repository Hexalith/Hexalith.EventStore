using System.Reflection;

using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Sample;

public static class AdminOperationalIndexMetadata {
    public static Response Create(DiscoveryResult discovery, IReadOnlyList<string>? requestedDomains) {
        ArgumentNullException.ThrowIfNull(discovery);

        HashSet<string> requested = requestedDomains is { Count: > 0 }
            ? new HashSet<string>(requestedDomains, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(discovery.Aggregates.Select(a => a.DomainName), StringComparer.OrdinalIgnoreCase);

        List<DomainMetadata> domains = [.. discovery.Aggregates
            .Where(a => requested.Contains(a.DomainName))
            .Select(a => CreateDomainMetadata(a, discovery))
            .OrderBy(d => d.Domain, StringComparer.OrdinalIgnoreCase)];

        return new Response(domains);
    }

    private static DomainMetadata CreateDomainMetadata(DiscoveredDomain aggregate, DiscoveryResult discovery) {
        Type aggregateType = aggregate.Type;
        Type stateType = aggregate.StateType;
        List<string> commandTypes = [.. aggregateType
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "Handle" && IsDomainResultReturn(m.ReturnType))
            .Select(m => m.GetParameters().FirstOrDefault()?.ParameterType)
            .Where(t => t is not null)
            .Select(t => t!.FullName ?? t.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        List<string> eventTypes = [.. stateType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "Apply" && m.GetParameters().Length == 1)
            .Select(m => m.GetParameters()[0].ParameterType)
            .Where(t => typeof(IEventPayload).IsAssignableFrom(t))
            .Select(t => t.FullName ?? t.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        List<string> rejectionEventTypes = [.. stateType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "Apply" && m.GetParameters().Length == 1)
            .Select(m => m.GetParameters()[0].ParameterType)
            .Where(t => typeof(IRejectionEvent).IsAssignableFrom(t))
            .Select(t => t.FullName ?? t.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        List<string> projectionNames = [.. discovery.Projections
            .Where(p => p.DomainName.Equals(aggregate.DomainName, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.DomainName)
            .Concat(DiscoverProjectionHandlerNames(aggregateType.Assembly, aggregate.DomainName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];

        return new DomainMetadata(
            aggregate.DomainName,
            eventTypes,
            rejectionEventTypes,
            commandTypes,
            [aggregateType.FullName ?? aggregateType.Name],
            projectionNames);
    }

    private static IEnumerable<string> DiscoverProjectionHandlerNames(Assembly assembly, string domain)
        => assembly.GetTypes()
            .Where(t => t.IsClass && t.IsAbstract && t.IsSealed && t.Name.EndsWith("ProjectionHandler", StringComparison.Ordinal))
            .Where(t => (t.Namespace ?? string.Empty).Contains($".{ToPascalCase(domain)}.", StringComparison.Ordinal))
            .Select(_ => domain);

    private static bool IsDomainResultReturn(Type returnType)
        => returnType == typeof(DomainResult)
            || returnType == typeof(Task<DomainResult>);

    private static string ToPascalCase(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : $"{char.ToUpperInvariant(value[0])}{value[1..]}";

    public sealed record Request(IReadOnlyList<string> Domains);

    public sealed record Response(IReadOnlyList<DomainMetadata> Domains);

    public sealed record DomainMetadata(
        string Domain,
        IReadOnlyList<string> EventTypes,
        IReadOnlyList<string> RejectionEventTypes,
        IReadOnlyList<string> CommandTypes,
        IReadOnlyList<string> AggregateTypes,
        IReadOnlyList<string> ProjectionNames);
}


using System.Collections.Concurrent;
using System.Reflection;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Conventions;

namespace Hexalith.EventStore.Client.Discovery;

/// <summary>
/// Scans assemblies for types inheriting from <see cref="EventStoreAggregate{TState}"/> or
/// <see cref="EventStoreProjection{TReadModel}"/> and resolves their domain names via
/// <see cref="NamingConventionEngine"/>.
/// </summary>
internal static class AssemblyScanner {
    private static readonly ConcurrentDictionary<Assembly, DiscoveryResult> _cache = new();

    /// <summary>
    /// Scans an assembly for all concrete types that inherit from <see cref="EventStoreAggregate{TState}"/>.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>A list of discovered aggregate types with resolved domain names.</returns>
    public static IReadOnlyList<DiscoveredDomain> ScanForAggregates(Assembly assembly) {
        ArgumentNullException.ThrowIfNull(assembly);
        return ScanForDomainTypes(assembly).Aggregates;
    }

    /// <summary>
    /// Scans an assembly for all concrete types that inherit from <see cref="EventStoreProjection{TReadModel}"/>.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>A list of discovered projection types with resolved domain names.</returns>
    public static IReadOnlyList<DiscoveredDomain> ScanForProjections(Assembly assembly) {
        ArgumentNullException.ThrowIfNull(assembly);
        return ScanForDomainTypes(assembly).Projections;
    }

    /// <summary>
    /// Scans an assembly for all concrete aggregate and projection types in a single pass.
    /// Results are cached per assembly for subsequent calls.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>A <see cref="DiscoveryResult"/> containing both aggregate and projection types.</returns>
    public static DiscoveryResult ScanForDomainTypes(Assembly assembly) {
        ArgumentNullException.ThrowIfNull(assembly);
        return _cache.GetOrAdd(assembly, static a => ScanForDomainTypes(GetLoadableTypes(a)));
    }

    /// <summary>
    /// Scans multiple assemblies for all concrete aggregate and projection types.
    /// Returns a combined result with de-duplicated types and cross-assembly duplicate domain name detection.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>A combined <see cref="DiscoveryResult"/> with de-duplicated types.</returns>
    public static DiscoveryResult ScanForDomainTypes(IEnumerable<Assembly> assemblies) {
        ArgumentNullException.ThrowIfNull(assemblies);

        var allTypes = new HashSet<Type>();
        foreach (Assembly assembly in assemblies) {
            ArgumentNullException.ThrowIfNull(assembly);
            DiscoveryResult assemblyResult = ScanForDomainTypes(assembly);

            foreach (DiscoveredDomain discovered in assemblyResult.Aggregates) {
                _ = allTypes.Add(discovered.Type);
            }

            foreach (DiscoveredDomain discovered in assemblyResult.Projections) {
                _ = allTypes.Add(discovered.Type);
            }

        }

        return ScanForDomainTypes(allTypes);
    }

    /// <summary>
    /// Scans the provided types for concrete aggregate and projection types.
    /// This internal overload supports unit testing with specific type sets.
    /// </summary>
    /// <param name="types">The types to scan.</param>
    /// <returns>A <see cref="DiscoveryResult"/> containing discovered domain types.</returns>
    internal static DiscoveryResult ScanForDomainTypes(IEnumerable<Type> types) {
        ArgumentNullException.ThrowIfNull(types);

        var aggregates = new List<DiscoveredDomain>();
        var projections = new List<DiscoveredDomain>();

        foreach (Type type in types) {
            ArgumentNullException.ThrowIfNull(type);

            if (type.IsAbstract || type.IsGenericTypeDefinition) {
                continue;
            }

            if (IsSubclassOfOpenGeneric(type, typeof(EventStoreAggregate<>))) {
                string domainName = ResolveDomainNameWrapped(type);
                Type stateType = ExtractGenericArgument(type, typeof(EventStoreAggregate<>));
                aggregates.Add(new DiscoveredDomain(type, domainName, stateType, DomainKind.Aggregate));
            }
            else if (IsSubclassOfOpenGeneric(type, typeof(EventStoreProjection<>))) {
                string domainName = ResolveDomainNameWrapped(type);
                Type stateType = ExtractGenericArgument(type, typeof(EventStoreProjection<>));
                projections.Add(new DiscoveredDomain(type, domainName, stateType, DomainKind.Projection));
            }
        }

        DetectWithinCategoryDuplicates(aggregates, "aggregate");
        DetectWithinCategoryDuplicates(projections, "projection");

        return new DiscoveryResult(aggregates, projections);
    }

    /// <summary>
    /// Clears the assembly scan cache. Intended for test isolation.
    /// </summary>
    internal static void ClearCache() => _cache.Clear();

    private static bool IsSubclassOfOpenGeneric(Type type, Type openGenericBase) {
        Type? current = type.BaseType;
        while (current is not null) {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == openGenericBase) {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static Type ExtractGenericArgument(Type type, Type openGenericBase) {
        Type? current = type.BaseType;
        while (current is not null) {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == openGenericBase) {
                Type result = current.GetGenericArguments()[0];
                if (result.IsGenericParameter) {
                    throw new InvalidOperationException(
                        $"Type '{type.Name}' has an unresolved generic parameter '{result.Name}' " +
                        $"in its '{openGenericBase.Name}' base class. Ensure the type is a concrete (non-open-generic) class.");
                }

                return result;
            }

            current = current.BaseType;
        }

        throw new InvalidOperationException($"Type '{type.Name}' does not inherit from '{openGenericBase.Name}'.");
    }

    private static string ResolveDomainNameWrapped(Type type) {
        try {
            return NamingConventionEngine.GetDomainName(type);
        }
        catch (ArgumentException ex) {
            throw new InvalidOperationException(
                $"Failed to resolve domain name for type '{type.FullName}' in assembly '{type.Assembly.GetName().Name}'. " +
                $"See inner exception for details.",
                ex);
        }
    }

    private static void DetectWithinCategoryDuplicates(List<DiscoveredDomain> discovered, string category) {
        var seen = new Dictionary<string, DiscoveredDomain>(StringComparer.Ordinal);
        foreach (DiscoveredDomain item in discovered) {
            if (seen.TryGetValue(item.DomainName, out DiscoveredDomain? existing)) {
                throw new InvalidOperationException(
                    $"Duplicate {category} domain name '{item.DomainName}' detected. " +
                    $"Type '{existing.Type.FullName}' (assembly '{existing.Type.Assembly.GetName().Name}') " +
                    $"and type '{item.Type.FullName}' (assembly '{item.Type.Assembly.GetName().Name}') " +
                    $"both resolve to the same domain name.");
            }

            seen[item.DomainName] = item;
        }
    }

    private static Type[] GetLoadableTypes(Assembly assembly) {
        try {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException ex) {
            return ex.Types.Where(t => t is not null).ToArray()!;
        }
        catch (NotSupportedException) {
            // Dynamic assemblies may not support GetExportedTypes().
            return assembly.GetTypes();
        }
    }
}

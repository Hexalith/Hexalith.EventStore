using System.Reflection;

using Dapr.Client;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.Tenants.Contracts.Routing;
using Hexalith.Tenants.Server.Aggregates;
using Hexalith.Tenants.Server.Projections;
using Hexalith.Tenants.Server.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.CatalogConstruction;

/// <summary>Repository-local, compiled-owner Development composition. No activation or secret availability is implied.</summary>
public static class DevelopmentCatalogProfile
{
    /// <summary>Gets the complete owner declarations.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Declarations => [.. SampleCatalog.Routes, .. TenantCatalogDeclarations.Routes];

    /// <summary>Verifies the candidate; construction fails while the gateway query fallback remains unbounded.</summary>
    public static async Task<RouteCatalogEnvelope> BuildAsync(string repositoryRoot, CancellationToken cancellationToken = default, DevelopmentCatalogCapabilities? capabilities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        capabilities ??= ObserveCapabilities();
        ValidateCapabilities(Declarations, capabilities);
        string basePath = Path.Combine(repositoryRoot, "src/Hexalith.EventStore/appsettings.json");
        string developmentPath = Path.Combine(repositoryRoot, "src/Hexalith.EventStore/appsettings.Development.json");
        byte[] baseBytes = ReadBounded(basePath);
        byte[] developmentBytes = ReadBounded(developmentPath);
        DevelopmentCatalogInspection inspection = DevelopmentRouteCatalogBuilder.Inspect([baseBytes, developmentBytes]);
        DomainServiceOptions options;
        try
        {
            using var baseStream = new MemoryStream(baseBytes, writable: false);
            using var developmentStream = new MemoryStream(developmentBytes, writable: false);
            IConfiguration config = new ConfigurationBuilder().AddJsonStream(baseStream).AddJsonStream(developmentStream).Build();
            options = config.GetSection("EventStore:DomainServices").Get<DomainServiceOptions>() ?? new();
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            throw new RouteCatalogValidationException([new("$.developmentProfile.configuration", "invalid-configuration")]);
        }
        if (!string.IsNullOrWhiteSpace(options.ConfigStoreName)) Fail("unbounded-dynamic-configuration");

        IReadOnlyList<RouteContractDeclaration> declarations = Declarations;
        IDeclaredIdempotencyIntentAdapter[] adapters = [.. SampleCatalog.CreateAdapters(), .. TenantIntentAdapters.Create()];
        ValidateHostedCommands(adapters, [typeof(CounterAggregate).Assembly, typeof(TenantAggregate).Assembly]);
        string[] domains = ["counter", "greeting", "tenants", "global-administrators"];
        if (inspection.Bindings.Any(binding => !declarations.Any(declaration => declaration.Domain == binding.Domain && declaration.ContractVersion == binding.ContractVersion)))
            Fail("undeclared-configured-contract-version");
        if (inspection.Bindings.Any(binding => binding.CommandMethod != "process"
            || binding.AppId != (binding.Domain is "counter" or "greeting" ? "sample" : "tenants")))
            Fail("configured-binding-target-drift");
        if (inspection.Bindings.Any(b => !domains.Contains(b.Domain, StringComparer.Ordinal))
            || domains.Any(domain => !inspection.Bindings.Any(b => b.Domain == domain))) Fail("configured-domain-inventory-drift");

        using DaprClient dapr = new DaprClientBuilder().Build();
        var resolver = new DomainServiceResolver(dapr, Options.Create(options), NullLogger<DomainServiceResolver>.Instance);
        var bindings = new List<RouteCompositionBinding>();
        foreach (RouteContractDeclaration declaration in declarations)
        {
            string app = declaration.Domain is "counter" or "greeting" ? "sample" : "tenants";
            // Probe every exact configured tenant and an unmatched tenant: within these four domains the
            // remaining convention fallback has a finite, explicitly checked target, never a new handler.
            string tenantPattern = declaration.Domain == "global-administrators" ? "system" : "*";
            string[] tenants = tenantPattern == "system" ? ["system"]
                : ["catalog-unmatched-tenant", "system", .. inspection.Bindings.Where(b => b.Domain == declaration.Domain && b.TenantPattern != "*").Select(b => b.TenantPattern)];
            if (tenantPattern == "system" && inspection.Bindings.Any(b => b.Domain == declaration.Domain && b.TenantPattern != "system"))
                Fail("undeclared-resolver-tenant-scope");
            foreach (string tenant in tenants.Distinct(StringComparer.Ordinal))
            {
                DomainServiceRegistration? actual = await resolver.ResolveAsync(tenant, declaration.Domain, declaration.ContractVersion, cancellationToken).ConfigureAwait(false);
                if (actual is null || actual.AppId != app || actual.MethodName != "process" || actual.Version != declaration.ContractVersion)
                    Fail("effective-resolver-target-drift");
            }
            bool actor = declaration.Execution!.TargetKind == "projection-actor";
            bindings.Add(new(declaration.RouteId, actor ? "eventstore" : app,
                actor ? "QueryAsync" : declaration.Kind switch { "command" => "process", "query" => "query", _ => "project" }, "v1", false, tenantPattern));
        }
        ValidateQueryFallbackBoundary();
        return DevelopmentRouteCatalogBuilder.Build(new(1, false, declarations, bindings,
            adapters.Select(adapter => DevelopmentIdempotencyBindings.Bind(adapter.Declaration, 1)).ToArray()));
    }

    /// <summary>Refuses undeclared or ambiguous actual Handle payloads before constructing a profile.</summary>
    public static void ValidateCommands(IReadOnlyList<IDeclaredIdempotencyIntentAdapter> adapters, IReadOnlyList<Type> aggregates)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        ArgumentNullException.ThrowIfNull(aggregates);
        IReadOnlyList<DiscoveredDomain> discovered;
        try
        {
            discovered = AssemblyScanner.ScanForDomainTypes(aggregates).Aggregates;
        }
        catch (InvalidOperationException)
        {
            throw new RouteCatalogValidationException([new("$.developmentProfile", "actual-command-handler-inventory-drift")]);
        }
        if (discovered.Count != aggregates.Count) Fail("actual-command-handler-inventory-drift");
        var actual = new List<(string Domain, Type Command, string[] Aliases)>();
        foreach (DiscoveredDomain aggregate in discovered)
        {
            Dictionary<string, AggregateCommandHandleMethod> methods;
            try
            {
                methods = AggregateCommandDiscovery.Discover(aggregate.Type, aggregate.StateType);
            }
            catch (InvalidOperationException)
            {
                throw new RouteCatalogValidationException([new("$.developmentProfile", "actual-command-handler-inventory-drift")]);
            }
            if (methods.Count == 0) Fail("actual-command-handler-inventory-drift");
            if (methods.Values.Any(method => method.Method.ContainsGenericParameters)) Fail("non-invocable-command-handler");
            foreach (IGrouping<Type, KeyValuePair<string, AggregateCommandHandleMethod>> handler in methods.GroupBy(pair => pair.Value.CommandType))
                actual.Add((aggregate.DomainName, handler.Key, handler.Select(pair => pair.Key).Order(StringComparer.Ordinal).ToArray()));
        }
        if (actual.Count != adapters.Count) Fail("actual-command-handler-inventory-drift");
        foreach (var handler in actual)
        {
            IDeclaredIdempotencyIntentAdapter[] matching = adapters.Where(adapter => adapter.Declaration.Route.Domain == handler.Domain
                && adapter.Declaration.Route.Execution?.HandlerType == handler.Command.FullName).ToArray();
            if (matching.Length != 1) Fail("actual-command-handler-inventory-drift");
            RouteExecutionDeclaration execution = matching[0].Declaration.Route.Execution!;
            if (execution.AliasPolicy != "clr-short-name-v1"
                || !execution.Aliases.Order(StringComparer.Ordinal).SequenceEqual(handler.Aliases, StringComparer.Ordinal))
                Fail("actual-command-alias-drift");
        }
        foreach (IDeclaredIdempotencyIntentAdapter adapter in adapters)
        {
            if (adapter.CommandType != adapter.Declaration.Route.MessageType || adapter.AdapterId != adapter.Declaration.AdapterId
                || adapter.OperationId != adapter.Declaration.OperationId || adapter.DescriptorVersion != adapter.Declaration.DescriptorSchema.Version
                || adapter.RetentionTier != adapter.Declaration.RetentionTier) Fail("adapter-declaration-drift");
        }
    }

    /// <summary>Checks the complete SDK-discovered inventory of every enrolled aggregate assembly.</summary>
    public static void ValidateHostedCommands(IReadOnlyList<IDeclaredIdempotencyIntentAdapter> adapters, IReadOnlyList<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        Type[] aggregates;
        try
        {
            // Compiled owner assemblies use exported SDK types at runtime. Enumerate every type
            // first so a loader failure cannot turn that discovery into a partial inventory.
            aggregates = AssemblyScanner.ScanForDomainTypes(LoadAllTypes(assemblies).Where(type => type.IsVisible)).Aggregates.Select(domain => domain.Type).ToArray();
        }
        catch (InvalidOperationException)
        {
            throw new RouteCatalogValidationException([new("$.developmentProfile", "actual-command-handler-inventory-drift")]);
        }
        ValidateCommands(adapters, aggregates);
    }

    private static byte[] ReadBounded(string path)
    {
        using FileStream input = File.OpenRead(path);
        byte[] buffer = new byte[RouteCatalogCodec.MaximumByteLength + 1];
        int length = input.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        if (length > RouteCatalogCodec.MaximumByteLength) Fail("size-limit");
        return buffer.AsSpan(0, length).ToArray();
    }

    /// <summary>Combines packaged Tenants owner enrollment with local Sample discovery; Tenants owns executable discovery parity.</summary>
    public static DevelopmentCatalogCapabilities ObserveCapabilities() => new(
        [.. TenantCatalogCapabilities.HandlerQueries, .. DomainCatalogCapabilities.Queries(typeof(CounterAggregate).Assembly)],
        [SampleCatalog.Projection, .. TenantCatalogCapabilities.Projections.Declarations]);

    /// <summary>Rejects missing, extra, ambiguous, or stale capability evidence before any catalog is written.</summary>
    public static void ValidateCapabilities(IReadOnlyList<RouteContractDeclaration> declarations, DevelopmentCatalogCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(capabilities);
        if (capabilities.HandlerQueries is null || capabilities.Projections is null) Fail("missing-capability-evidence");
        string[] Expected(string kind) => declarations.Where(route => route.Kind == kind && route.Execution?.TargetKind == "domain-service")
            .Select(RouteCatalogCodec.ComputeDeclarationDigest).Order(StringComparer.Ordinal).ToArray();
        if (!Expected("query").SequenceEqual(capabilities.HandlerQueries.Select(RouteCatalogCodec.ComputeDeclarationDigest).Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || !Expected("projection").SequenceEqual(capabilities.Projections.Select(RouteCatalogCodec.ComputeDeclarationDigest).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            Fail("missing-ambiguous-or-drifted-capability-evidence");
        ValidateHostedProjections([SampleCatalog.Projection], [typeof(CounterAggregate).Assembly]);
        ValidateHostedSdkProjectionOutputs(TenantCatalogCapabilities.Projections.Declarations,
            [(typeof(TenantProjection), "tenants"), (typeof(TenantIndexProjection), "tenant-index"), (typeof(GlobalAdministratorProjection), "global-administrators")],
            [typeof(TenantAggregate).Assembly]);
    }

    /// <summary>Checks SDK read-model projections behind a custom dispatcher against its declared outputs.</summary>
    public static void ValidateHostedSdkProjectionOutputs(IReadOnlyList<RouteContractDeclaration> declarations,
        IReadOnlyList<(Type Type, string Output)> outputs, IReadOnlyList<Assembly> assemblies)
    {
        Type[] types = LoadAllTypes(assemblies);
        IReadOnlyList<DiscoveredDomain> sdk;
        try { sdk = AssemblyScanner.ScanForDomainTypes(types.Where(type => type.IsVisible)).Projections; }
        catch (InvalidOperationException) { throw new RouteCatalogValidationException([new("$.developmentProfile", "actual-projection-handler-inventory-drift")]); }
        if (!sdk.Select(projection => (projection.Type, projection.DomainName)).OrderBy(value => value.Type.FullName)
            .SequenceEqual(outputs.OrderBy(value => value.Type.FullName))
            || outputs.Any(output => !declarations.Any(route => route.Execution!.ProjectionOutputs.Contains(output.Output, StringComparer.Ordinal)))
            || types.Any(type => type is { IsClass: true, IsAbstract: false }
                && (typeof(IDomainProjectionHandler).IsAssignableFrom(type) || typeof(IAsyncDomainProjectionHandler).IsAssignableFrom(type))))
            Fail("actual-projection-handler-inventory-drift");
    }

    /// <summary>Checks every SDK, legacy and async projection discovered in the enrolled assemblies.</summary>
    public static void ValidateHostedProjections(IReadOnlyList<RouteContractDeclaration> declarations, IReadOnlyList<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(assemblies);
        Type[] types = LoadAllTypes(assemblies);
        IReadOnlyList<DiscoveredDomain> sdk;
        try { sdk = AssemblyScanner.ScanForDomainTypes(types.Where(type => type.IsVisible)).Projections; }
        catch (InvalidOperationException) { throw new RouteCatalogValidationException([new("$.developmentProfile", "actual-projection-handler-inventory-drift")]); }
        Type[] handlers = types.Where(type => type is { IsClass: true, IsAbstract: false }
            && (typeof(IDomainProjectionHandler).IsAssignableFrom(type) || typeof(IAsyncDomainProjectionHandler).IsAssignableFrom(type)))
            .Concat(sdk.Select(projection => projection.Type)).Distinct().ToArray();
        if (!handlers.Select(type => type.FullName).Order(StringComparer.Ordinal)
            .SequenceEqual(declarations.Select(route => route.Execution?.HandlerType).Distinct().Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || declarations.Any(route => route.Kind != "projection")
            || sdk.Any(projection => declarations.Any(route => route.Execution?.HandlerType == projection.Type.FullName && route.Domain != projection.DomainName)))
            Fail("actual-projection-handler-inventory-drift");
    }

    private static Type[] LoadAllTypes(IReadOnlyList<Assembly> assemblies)
    {
        try { return assemblies.SelectMany(assembly => assembly.GetTypes()).Distinct().ToArray(); }
        catch (Exception exception) when (exception is ReflectionTypeLoadException or FileNotFoundException or FileLoadException or TypeLoadException or NotSupportedException)
        {
            throw new RouteCatalogValidationException([new("$.developmentProfile", "unavailable-complete-assembly-inventory")]);
        }
    }

    private static void ValidateQueryFallbackBoundary()
    {
        // HandlerAwareQueryRouter forwards every unknown QueryType to QueryRouter, which accepts
        // caller-supplied ProjectionType and ProjectionActorType. No finite owner declaration
        // currently bounds those targets. Positive declared routes cannot certify completeness.
        Fail("unrepresented-unbounded-query-fallback");
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string code) => throw new RouteCatalogValidationException([new("$.developmentProfile", code)]);
}

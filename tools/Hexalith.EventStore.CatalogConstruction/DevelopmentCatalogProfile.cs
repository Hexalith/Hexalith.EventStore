using System.Reflection;

using Dapr.Client;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Greeting;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.Tenants.Contracts.Routing;
using Hexalith.Tenants.Server.Aggregates;
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

    /// <summary>Constructs and verifies actual static and bounded convention resolution for this explicit profile.</summary>
    public static async Task<RouteCatalogEnvelope> BuildAsync(string repositoryRoot, CancellationToken cancellationToken = default, DevelopmentCatalogCapabilities? capabilities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        capabilities ??= ObserveCapabilities();
        ValidateCapabilities(Declarations, capabilities);
        string basePath = Path.Combine(repositoryRoot, "src/Hexalith.EventStore/appsettings.json");
        string developmentPath = Path.Combine(repositoryRoot, "src/Hexalith.EventStore/appsettings.Development.json");
        DevelopmentCatalogInspection inspection = DevelopmentRouteCatalogBuilder.Inspect([File.ReadAllBytes(basePath), File.ReadAllBytes(developmentPath)]);
        IConfiguration config = new ConfigurationBuilder().AddJsonFile(basePath).AddJsonFile(developmentPath).Build();
        DomainServiceOptions options = config.GetSection("EventStore:DomainServices").Get<DomainServiceOptions>() ?? new();
        if (!string.IsNullOrWhiteSpace(options.ConfigStoreName)) Fail("unbounded-dynamic-configuration");

        IReadOnlyList<RouteContractDeclaration> declarations = Declarations;
        IDeclaredIdempotencyIntentAdapter[] adapters = [.. SampleCatalog.CreateAdapters(), .. TenantIntentAdapters.Create()];
        ValidateCommands(adapters, [typeof(CounterAggregate), typeof(GreetingAggregate), typeof(TenantAggregate), typeof(GlobalAdministratorsAggregate)]);
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
        return DevelopmentRouteCatalogBuilder.Build(new(1, false, declarations, bindings,
            adapters.Select(adapter => DevelopmentIdempotencyBindings.Bind(adapter.Declaration, 1)).ToArray()));
    }

    /// <summary>Refuses undeclared or ambiguous actual Handle payloads before constructing a profile.</summary>
    public static void ValidateCommands(IReadOnlyList<IDeclaredIdempotencyIntentAdapter> adapters, IReadOnlyList<Type> aggregates)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        ArgumentNullException.ThrowIfNull(aggregates);
        string[] actual = aggregates.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.Name == "Handle").Select(method => method.GetParameters()[0].ParameterType.FullName!).Order(StringComparer.Ordinal).ToArray();
        string[] declared = adapters.Select(adapter => adapter.Declaration.Route.Execution!.HandlerType).Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(declared, StringComparer.Ordinal)) Fail("actual-command-handler-inventory-drift");
        foreach (IDeclaredIdempotencyIntentAdapter adapter in adapters)
        {
            if (adapter.CommandType != adapter.Declaration.Route.MessageType || adapter.AdapterId != adapter.Declaration.AdapterId
                || adapter.OperationId != adapter.Declaration.OperationId || adapter.DescriptorVersion != adapter.Declaration.DescriptorSchema.Version
                || adapter.RetentionTier != adapter.Declaration.RetentionTier) Fail("adapter-declaration-drift");
        }
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
        // The actual Sample SDK assembly has exactly the declared legacy Counter handler and no Greeting projection.
        Type[] legacyHandlers = typeof(CounterAggregate).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IDomainProjectionHandler).IsAssignableFrom(type)).ToArray();
        if (legacyHandlers.Length != 1 || legacyHandlers[0].FullName != SampleCatalog.Projection.Execution!.HandlerType)
            Fail("actual-projection-handler-inventory-drift");
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string code) => throw new RouteCatalogValidationException([new("$.developmentProfile", code)]);
}

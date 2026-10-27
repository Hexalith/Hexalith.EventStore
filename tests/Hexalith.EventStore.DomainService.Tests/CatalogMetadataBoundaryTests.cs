using System.Reflection;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public class CatalogMetadataBoundaryTests
{
    [Fact]
    public void LegacyBindingKeepsOnlyTheRequestedMetadataDomains()
    {
        RouteContractDeclaration first = Projection("counter");
        RouteContractDeclaration second = Projection("other");
        var registrations = new[] { new LegacyProjectionCatalogRegistration([first, second]) };
        var response = new AdminOperationalIndexMetadata.Response([
            new("counter", [], [], [], [], [], []), new("other", [], [], [], [], [], [])]);
        AdminOperationalIndexMetadata.Response bound = LegacyProjectionCatalogRegistration.Bind(response, registrations);
        foreach (RouteContractDeclaration route in new[] { first, second })
            bound.Domains.Single(domain => domain.Domain == route.Domain).LegacyProjectionOutputs![route.ProjectionType!]
                .ShouldBe(route.Execution!.ProjectionOutputs);
        AdminOperationalIndexMetadata.Response subset = LegacyProjectionCatalogRegistration.Bind(response with { Domains = response.Domains.Take(1).ToArray() }, registrations);
        subset.Domains.ShouldHaveSingleItem().Domain.ShouldBe(first.Domain);
        subset.Domains[0].LegacyProjectionOutputs![first.ProjectionType!].ShouldBe(first.Execution!.ProjectionOutputs);
        LegacyProjectionCatalogRegistration.Bind(response with { Domains = [] }, registrations).Domains.ShouldBeEmpty();
        response.Domains.ShouldAllBe(domain => domain.LegacyProjectionOutputs == null);
        LegacyProjectionCatalogRegistration.Bind(response, []).Domains.ShouldBe(response.Domains);
    }

    [Fact]
    public void StaticQueryMetadataGetterFailureIsValueFreeBesideAValidControl()
    {
        Assembly assembly = Substitute.For<Assembly>();
        Type valid = ConcreteProbe(typeof(CatalogQueryMetadataProbe));
        Type throwing = ConcreteProbe(typeof(ThrowingCatalogQueryMetadataProbe));
        assembly.GetTypes().Returns([valid]);
        DomainCatalogCapabilities.Queries(assembly).ShouldHaveSingleItem().ShouldBe(CatalogQueryMetadataProbe.CatalogDeclaration);
        assembly.GetTypes().Returns([valid, throwing]);
        RouteCatalogValidationException failure = Should.Throw<RouteCatalogValidationException>(() => DomainCatalogCapabilities.Queries(assembly));
        failure.Diagnostics.ShouldHaveSingleItem().ShouldBe(new RouteCatalogDiagnostic("$.capabilities.queries", "unavailable-compiled-handler-metadata"));
        failure.Message.ShouldBe("Route catalog rejected: $.capabilities.queries: unavailable-compiled-handler-metadata");
        failure.InnerException.ShouldBeNull();
        failure.ToString().ShouldNotContain("never-print-owner-secret");
        failure.ToString().ShouldNotContain(nameof(ThrowingCatalogQueryMetadataProbe));
    }

    private static RouteContractDeclaration Projection(string domain) => new(domain + "-projection", "projection", domain, null, domain,
        "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceProject, null, [],
        new("Fixture.Projection", "exact", [domain], "domain-service", null, [domain]));

    private static Type ConcreteProbe(Type baseType)
    {
        // Keep fixture classes abstract in this assembly so other tests' SDK host discovery
        // cannot accidentally enroll them as real query handlers.
        var assembly = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new("CatalogMetadataProbe" + Guid.NewGuid().ToString("N")), System.Reflection.Emit.AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("probe").DefineType("ConcreteProbe", TypeAttributes.Public, baseType);
        var property = type.DefineProperty("CatalogDeclaration", PropertyAttributes.None, typeof(RouteContractDeclaration), Type.EmptyTypes);
        var getter = type.DefineMethod("get_CatalogDeclaration", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(RouteContractDeclaration), Type.EmptyTypes);
        var code = getter.GetILGenerator();
        code.Emit(System.Reflection.Emit.OpCodes.Call, baseType.GetProperty("CatalogDeclaration")!.GetMethod!);
        code.Emit(System.Reflection.Emit.OpCodes.Ret);
        property.SetGetMethod(getter);
        return type.CreateType()!;
    }
}

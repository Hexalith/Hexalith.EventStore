using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Queries;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class SampleCompiledCatalogTests
{
    [Fact]
    public void ActualSeparateSampleAssembliesExposeCompleteMatchingOperationsAndUnchangedAdapters()
    {
        var sample = typeof(SampleCatalog).Assembly;
        var contracts = typeof(CounterCatalogDeclarations).Assembly;
        sample.ShouldNotBe(contracts);
        var all = CompiledRouteCatalogDeclarations.Read([sample]);
        var operations = CompiledRouteCatalogDeclarations.ReadOperations([sample]);
        all.Count.ShouldBe(7);
        SampleCatalog.ReceiverMethods.Count.ShouldBe(7);
        SampleCatalog.ReceiverMethods[CounterCatalogDeclarations.Query.RouteId].ShouldBe("QueryAsync");
        SampleCatalog.ReceiverMethods[SampleCatalog.Projection.RouteId].ShouldBe("project");
        SampleCatalog.Commands.ShouldAllBe(command => SampleCatalog.ReceiverMethods[command.Route.RouteId] == "process");
        CounterCatalogDeclarations.ReceiverMethods.Count.ShouldBe(5);
        operations.Count.ShouldBe(7);
        var counter = CompiledRouteCatalogDeclarations.Read([contracts]);
        var counterOperations = CompiledRouteCatalogDeclarations.ReadOperations([contracts]);
        counter.Count.ShouldBe(5);
        foreach (var operation in counterOperations)
            operations.Single(candidate => candidate.RouteId == operation.RouteId).Digest.ShouldBe(operation.Digest);
        operations.Where(operation => operation.RouteId != SampleCatalog.Projection.RouteId)
            .ShouldAllBe(operation => operation.Ingress == "public" && operation.Eligibility == "UI-only");
        operations.Single(operation => operation.RouteId == SampleCatalog.Projection.RouteId).Ingress.ShouldBe("internal");
        operations.ShouldAllBe(operation => operation.Digest == RouteCatalogCodec.ComputeOperationContractDigest(operation));
        sample.GetCustomAttributes<RouteCatalogAdapterSourceAttribute>().Single().SourceType.ShouldBe(typeof(SampleCatalogAdapters));
        var adapters = SampleCatalogAdapters.Create();
        adapters.Count.ShouldBe(5);
        adapters.Select(adapter => adapter.Declaration.Route.RouteId).ShouldBe([
            "counter-increment-counter", "counter-decrement-counter", "counter-reset-counter", "counter-close-counter", "greeting-send-greeting"]);
        adapters.Select(adapter => RouteCatalogCodec.ComputeDeclarationDigest(adapter.Declaration.Route))
            .ShouldBe(SampleCatalog.Commands.Select(command => RouteCatalogCodec.ComputeDeclarationDigest(command.Route)));
        foreach (var adapter in adapters)
        {
            adapter.Declaration.DescriptorSchema.Fields.Select(field => field.Name).ShouldBe(["target"]);
        }
        JsonSerializer.Serialize(new CounterStatusPayload(5), JsonSerializerOptions.Web).ShouldBe("{\"count\":5}");
    }
}

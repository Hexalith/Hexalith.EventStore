using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Projections;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class ProjectionWireJsonSerializerTests
{
    [Fact]
    public async Task GuardedTransport_UsesTheAdmittedWireProfileDespiteConfigurableDaprStateSerialization()
    {
        var configured = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        configured.Converters.Add(new CustomBytesConverter());
        using DaprClient dapr = new DaprClientBuilder().UseJsonSerializationOptions(configured).Build();
        string response = JsonSerializer.Serialize(new ProjectionDispatchResponse(2,
            [new("detail", ProjectionDispatchStatus.Completed, null, null)]), JsonSerializerOptions.Web);
        using var handler = new ProjectionDispatchHttpMessageHandler(response);
        using var http = new HttpClient(handler);
        IHttpClientFactory clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient(Arg.Any<string>()).Returns(http);
        var transport = new DaprProjectionSourceGuardTransport(dapr, clients);
        var identity = new AggregateIdentity("tenant-a", "counter", "counter-1");
        var catalog = new NamedProjectionRouteCatalogEntry("sample", "v1", "counter", 2, ProjectionDispatchProtocol.Capability,
            "catalog", ["detail"], mappedOnlyStreamedProjectionTypes: ["detail"]);
        ProjectionEventDto item = Event([1, 2, 3, 4]);
        byte[] admittedArray = JsonSerializer.SerializeToUtf8Bytes(new[] { item }, JsonSerializerOptions.Web);
        JsonSerializer.SerializeToUtf8Bytes(new[] { item }, configured).ShouldNotBe(admittedArray);
        (await transport.DispatchEventAsync(identity, catalog, "detail", item, item.MessageId!, CancellationToken.None))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var request = new ProjectionDispatchRequest(new(identity.TenantId, identity.Domain, identity.AggregateId, [item]),
            ["detail"], item.MessageId!, "catalog");
        handler.RequestJson.ShouldBe(JsonSerializer.Serialize(request, JsonSerializerOptions.Web));
        using (JsonDocument sent = JsonDocument.Parse(handler.RequestJson!))
            Encoding.UTF8.GetBytes(sent.RootElement.GetProperty("request").GetProperty("events").GetRawText()).ShouldBe(admittedArray);
        ProjectionDispatchRequest prepared = (await transport.PrepareReplayRequestAsync(catalog, request, CancellationToken.None)).ShouldNotBeNull();
        prepared.RebuildTransport.ShouldNotBeNull().PartCount.ShouldBe(1);
        using JsonDocument part = JsonDocument.Parse(handler.RequestJson!);
        part.RootElement.GetProperty("data").GetBytesFromBase64().ShouldBe(admittedArray);
    }
}

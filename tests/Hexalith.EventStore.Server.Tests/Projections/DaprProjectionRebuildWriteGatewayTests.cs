using System.Net;
using System.Reflection;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Testing.Http;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class DaprProjectionRebuildWriteGatewayTests
{
    [Fact]
    public async Task StrictCompletionUsesAdditiveCapabilityAndPreservesReleasedPublicationTransport()
    {
        var request = new ProjectionReplayPublicationRequest(new("tenant-a", "counter", "counter-1", "counter", "operation-1", 1,
            "message-1", new string('a', 64), new string('b', 64)), new(ProjectionLifecyclePhase.Delivering, 1), "lease-1", null);
        var methods = new List<string>();
        using var handler = new MockHttpMessageHandler(async (message, token) =>
        {
            methods.Add(message.RequestUri!.AbsolutePath);
            using JsonDocument body = JsonDocument.Parse(await message.Content!.ReadAsStringAsync(token));
            ProjectionReplayPublicationRequest transported = body.RootElement.GetProperty("value").GetProperty("request")
                .Deserialize<ProjectionReplayPublicationRequest>(JsonSerializerOptions.Web).ShouldNotBeNull();
            transported.ShouldBe(request);
            Type contract = methods.Count == 1 ? typeof(IProjectionReplayPublicationActor) : typeof(IProjectionBoundReplayCompletionActor);
            string method = methods.Count == 1 ? nameof(IProjectionReplayPublicationActor.ReadReplayCompletionAsync)
                : nameof(IProjectionBoundReplayCompletionActor.ReconcileBoundReplayCompletionAsync);
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new ByteArrayContent(SerializeResponse(contract, method)) };
        });
        var factory = new ActorProxyFactory(new ActorProxyOptions
        { HttpEndpoint = "http://localhost:3500", UseJsonSerialization = true, JsonSerializerOptions = JsonSerializerOptions.Web }, handler);
        var gateway = new DaprProjectionRebuildWriteGateway(factory);
        (await gateway.ReadReplayCompletionAsync("counter:tenant-a:counter-1", request, CancellationToken.None)).ShouldBeTrue();
        (await gateway.ReconcileBoundReplayCompletionAsync("counter:tenant-a:counter-1", request, CancellationToken.None)).ShouldBeTrue();

        methods.Count.ShouldBe(2);
        methods[0].ShouldEndWith("/method/ReadReplayCompletionAsync");
        methods[1].ShouldEndWith("/method/ReconcileBoundReplayCompletionAsync");
        methods.ShouldAllBe(path => path.Contains("/actors/ProjectionActor/counter:tenant-a:counter-1/", StringComparison.Ordinal));
    }

    private static byte[] SerializeResponse(Type contract, string method)
    {
        // Use the pinned SDK's generated remoting response, including its return-value wrapper.
        Type providerType = typeof(ActorId).Assembly.GetType("Dapr.Actors.Communication.ActorMessageBodyJsonSerializationProvider", true)!;
        Type responseType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes())
            .Single(type => type.FullName == contract.FullName + "_.actorV1.mt." + method + "RespBody");
        object response = Activator.CreateInstance(responseType)!;
        FieldInfo result = responseType.GetFields(BindingFlags.Instance | BindingFlags.Public).Single(field => field.FieldType == typeof(bool));
        result.SetValue(response, true);
        object provider = Activator.CreateInstance(providerType, [JsonSerializerOptions.Web])!;
        object factory = providerType.GetMethod("CreateMessageBodyFactory")!.Invoke(provider, null)!;
        object wrapped = factory.GetType().GetMethod("CreateResponseMessageBody")!.Invoke(factory, [contract.Name, method, response])!;
        object serializer = providerType.GetMethod("CreateResponseMessageBodySerializer")!
            .Invoke(provider, [contract, new[] { responseType }, new[] { responseType }])!;
        Type serializerContract = typeof(ActorId).Assembly.GetType("Dapr.Actors.Communication.IActorResponseMessageBodySerializer", true)!;
        return (byte[])serializerContract.GetMethod("Serialize")!.Invoke(serializer, [wrapped])!;
    }
}

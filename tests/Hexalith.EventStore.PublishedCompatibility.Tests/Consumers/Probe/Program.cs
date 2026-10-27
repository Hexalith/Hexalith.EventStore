using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Events;

using PublishedConsumer;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
using JsonDocument input = JsonDocument.Parse(await Console.In.ReadToEndAsync());
JsonElement request = input.RootElement;
string operation = request.GetProperty("operation").GetString()!;
object result;
switch (operation)
{
#if SELECTED
    case "signed-query": result = SignedQueryFixture.Create(); break;
#endif
    case "identity": result = new { identities = ConsumerIdentity.Read() }; break;
    case "cursor":
#if FLOOR
        result = CursorFixture.Execute();
#else
        result = new { supported = false, denial = "incapable-published-endpoint", protectedAccess = false, effect = false, write = false, identities = ConsumerIdentity.Read() };
#endif
        break;
    case "metadata":
    {
        string raw = request.GetProperty("value").GetString()!;
        AggregateMetadata metadata = JsonSerializer.Deserialize<AggregateMetadata>(raw, json)!;
        result = new { value = JsonSerializer.SerializeToElement(metadata, json), identities = ConsumerIdentity.Read() };
        break;
    }
    case "wire":
    {
        Type type = request.GetProperty("kind").GetString() == "query" ? typeof(QueryEnvelope) : typeof(ProjectionEventDto);
        bool xml = request.GetProperty("serializer").GetString() == "xml";
        string value = request.GetProperty("value").GetString()!;
        object instance;
        if (xml)
        {
            using var stream = new MemoryStream(Convert.FromBase64String(value));
            instance = new DataContractSerializer(type).ReadObject(stream)!;
        }
        else instance = JsonSerializer.Deserialize(value, type, json)!;
        string wire;
        if (xml)
        {
            using var stream = new MemoryStream();
            new DataContractSerializer(type).WriteObject(stream, instance);
            wire = Convert.ToBase64String(stream.ToArray());
        }
        else wire = JsonSerializer.Serialize(instance, type, json);
        result = new { wire, value = JsonSerializer.SerializeToElement(instance, type, json), identities = ConsumerIdentity.Read() };
        break;
    }
    case "make-wire":
    {
        Type type = request.GetProperty("kind").GetString() == "query" ? typeof(QueryEnvelope) : typeof(ProjectionEventDto);
        object instance = JsonSerializer.Deserialize(request.GetProperty("value").GetString()!, type, json)!;
        using var stream = new MemoryStream();
        new DataContractSerializer(type).WriteObject(stream, instance);
        result = new { wire = Convert.ToBase64String(stream.ToArray()), value = JsonSerializer.SerializeToElement(instance, type, json), identities = ConsumerIdentity.Read() };
        break;
    }
    case "capabilities":
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        _ = ConsumerIdentity.Read();
        Type actor = Type.GetType("Hexalith.EventStore.Server.Actors.IAggregateActor, Hexalith.EventStore.Server")!;
        result = new { methods = actor.GetMethods().Select(method => method.Name).Order().ToArray(),
            queryProperties = typeof(QueryEnvelope).GetProperties().Select(property => property.Name).Order().ToArray(),
            projectionProperties = typeof(ProjectionEventDto).GetProperties().Select(property => property.Name).Order().ToArray(),
            identities = ConsumerIdentity.Read() };
        break;
    }
    default: throw new InvalidOperationException("Unknown operation.");
}
Console.WriteLine("P1R_RESULT " + JsonSerializer.Serialize(result, json));

using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Server.Events;
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;

JsonSerializerOptions options = args.Contains("pascal", StringComparer.Ordinal) ? new() : JsonSerializerOptions.Web;
Type Find(string name) => typeof(AggregateMetadata).Assembly.GetType(name)
    ?? Assembly.Load("Hexalith.EventStore.Contracts").GetType(name)
    ?? throw new NotSupportedException(name);
if (args[0] == "cursor-scope")
{
    var scope = Hexalith.EventStore.Client.Queries.QueryCursorScope.Create().Add("tenant", "tenant-a");
    MethodInfo? method = scope.GetType().GetMethod("AddProjectionWatermark");
    if (method is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { method = "AddProjectionWatermark", handling = "unsupported-client-method" }));
    }
    else
    {
        method.Invoke(scope, new object?[] { (long?)987 });
        bool rejected = false;
        try { method.Invoke(scope, new object?[] { (long?)0 }); }
        catch (TargetInvocationException error) when (error.InnerException is ArgumentOutOfRangeException) { rejected = true; }
        Console.WriteLine(JsonSerializer.Serialize(new { method = "AddProjectionWatermark", handling = "executed", scope = scope.Build(), invalid_watermark_rejected = rejected }));
    }
}
else if (args[0] == "capability")
{
    var factory = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = args[1] });
    IAggregateActor proxy = factory.CreateActorProxy<IAggregateActor>(new ActorId("tenant-a:counter:fixture"), "AggregateActor");
    MethodInfo? method = typeof(IAggregateActor).GetMethod(args[2]);
    if (method is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { outcome = "unsupported-client-method", method = args[2] }));
    }
    else
    {
        try
        {
            Task task = (Task)(method.Invoke(proxy, new object?[method.GetParameters().Length]) ?? throw new InvalidOperationException("Null task."));
            await task;
            Console.WriteLine(JsonSerializer.Serialize(new { outcome = "returned", method = args[2], result = task.GetType().GetProperty("Result")?.GetValue(task) }));
        }
        catch (Exception error)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { outcome = "rejected", method = args[2], exception = error.GetType().Name, detail = error.ToString() }));
        }
    }
}
else if (args[0] == "seed")
{
    var factory = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = args[1] });
    IAggregateActor proxy = factory.CreateActorProxy<IAggregateActor>(new ActorId($"{args[2]}:counter:{args[3]}"), "AggregateActor");
    int count = int.Parse(args[4]);
    for (int index = 0; index <= count; index++)
    {
        string message = Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId();
        var command = new CommandEnvelope(message, args[2], "counter", args[3],
            "P1R.Counter." + (index == count ? "AssertCounter" : "IncrementCounter"),
            JsonSerializer.SerializeToUtf8Bytes(new { Expected = count }), message, null, "fixture-user", null);
        CommandProcessingResult result = await proxy.ProcessCommandAsync(command);
        if (!result.Accepted || result.EventCount != (index == count ? 0 : 1))
        {
            throw new InvalidOperationException("Actor seed or hydration assertion failed.");
        }
    }
    Console.WriteLine(JsonSerializer.Serialize(new { assertions = 2 * (count + 1), committedEvents = count, hydratedCount = count }));
}
else if (args[0] == "actor")
{
    var factory = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = args[1] });
    IAggregateActor proxy = factory.CreateActorProxy<IAggregateActor>(new ActorId($"{args[2]}:counter:{args[3]}"), "AggregateActor");
    var command = new CommandEnvelope(args[4], args[2], "counter", args[3], "P1R.Counter." + args[5],
        JsonSerializer.SerializeToUtf8Bytes(new { Expected = int.Parse(args[6]) }), args[4], null, "fixture-user", null);
    CommandProcessingResult result = await proxy.ProcessCommandAsync(command);
    Console.WriteLine(JsonSerializer.Serialize(new { assertions = 1, accepted = result.Accepted, eventCount = result.EventCount,
        failureReason = result.GetType().GetProperty("FailureReason")?.GetValue(result),
        failureCategory = result.ErrorMessage?.Contains("MissingEventException", StringComparison.Ordinal) == true ? "missing-event" : null,
        errorSha256 = result.ErrorMessage is null ? null : Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(result.ErrorMessage))) }));
}
else if (args[0] == "identity")
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        assertions = 1,
        assemblies = new[] { typeof(AggregateMetadata).Assembly, Assembly.Load("Hexalith.EventStore.Contracts") }
            .Select(a => new { name = a.GetName().Name, version = a.GetName().Version?.ToString(), sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(a.Location))) }),
        actorMethods = typeof(Hexalith.EventStore.Server.Actors.IAggregateActor).GetMethods().Select(m => m.Name).Order(),
        statusFields = typeof(CommandStatusRecord).GetProperties().Select(p => p.Name).Order(),
        cursorScopeMethods = typeof(Hexalith.EventStore.Client.Queries.QueryCursorScope).GetMethods().Select(m => m.Name).Distinct().Order(),
    }));
}
else if (args[0] == "metadata")
{
    byte[] input = File.ReadAllBytes(args[1]);
    AggregateMetadata value = JsonSerializer.Deserialize<AggregateMetadata>(input, options) ?? throw new InvalidOperationException("Null metadata.");
    if (value.CurrentSequence != 12 || value.ETag != "fixture-etag" || value.LastModified != DateTimeOffset.Parse("2026-01-01T00:00:00Z"))
    {
        throw new InvalidOperationException("Legacy metadata fields changed.");
    }
    File.WriteAllBytes(args[2], JsonSerializer.SerializeToUtf8Bytes(value, options));
    Console.WriteLine(JsonSerializer.Serialize(new { assertions = 3, currentSequence = value.CurrentSequence, floor = value.GetType().GetProperty("RetainedFloor")?.GetValue(value), inputSha256 = Convert.ToHexStringLower(SHA256.HashData(input)), outputSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(args[2]))) }));
}
else if (args[0] == "wire")
{
    string typeName = args[1] == "query" ? "Hexalith.EventStore.Contracts.Queries.QueryEnvelope" : "Hexalith.EventStore.Contracts.Projections.ProjectionEventDto";
    Type? type;
    try
    {
        type = Find(typeName);
    }
    catch (NotSupportedException)
    {
        type = null;
    }

    if (type is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { assertions = 1, handling = "unsupported-contract-type", type = typeName }));
    }
    else
    {
        object value;
        if (args[2] == "json")
        {
            value = JsonSerializer.Deserialize(File.ReadAllBytes(args[3]), type, options) ?? throw new InvalidOperationException("Null wire value.");
            File.WriteAllBytes(args[4], JsonSerializer.SerializeToUtf8Bytes(value, type, options));
        }
        else
        {
            var serializer = new DataContractSerializer(type);
            if (args[2] == "to-xml")
            {
                value = JsonSerializer.Deserialize(File.ReadAllBytes(args[3]), type, options) ?? throw new InvalidOperationException("Null wire value.");
            }
            else
            {
                using var input = File.OpenRead(args[3]);
                value = serializer.ReadObject(input) ?? throw new InvalidOperationException("Null wire value.");
            }

            using var output = File.Create(args[4]);
            serializer.WriteObject(output, value);
        }

        Console.WriteLine(JsonSerializer.Serialize(new { assertions = 1, type = type.FullName, inputSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(args[3]))), outputSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(args[4]))), fields = type.GetProperties().Where(p => p.Name is "OriginalActorId" or "AuthenticatedWorkloadId" or "IsDelegated" or "DelegationId" or "Scopes" or "Audience" or "GlobalPosition" or "SequenceNumber" or "UserId" or "TenantId" or "Domain" or "AggregateId" or "QueryType" or "Payload" or "CorrelationId" or "EntityId" or "IsGlobalAdmin" or "Paging" or "EventTypeName" or "SerializationFormat" or "Timestamp" or "MessageId").ToDictionary(p => p.Name, p => p.GetValue(value)) }));
    }
}
else
{
    throw new ArgumentException("Unknown probe operation.");
}

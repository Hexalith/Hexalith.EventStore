using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;

ExecutedChecks checks = new();
object? observation = null;
int exitCode = 0;
JsonSerializerOptions options = args.Contains("pascal", StringComparer.Ordinal) ? new() : JsonSerializerOptions.Web;
try
{
    if (args[0] == "identity")
    {
        observation = PublishedIdentity.Read();
        checks.Record("physical-identity-observed", observation is not null);
    }
    else if (args[0] == "metadata")
    {
        byte[] input = File.ReadAllBytes(args[1]);
        AggregateMetadata value = JsonSerializer.Deserialize<AggregateMetadata>(input, options) ?? throw new InvalidOperationException("Null metadata.");
        checks.Record("sequence-twelve", value.CurrentSequence == 12);
        checks.Record("etag-preserved", value.ETag == "fixture-etag");
        checks.Record("last-modified-preserved", value.LastModified == DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        File.WriteAllBytes(args[2], JsonSerializer.SerializeToUtf8Bytes(value, options));
        observation = new { sequence = value.CurrentSequence, etag = value.ETag, last_modified = value.LastModified, floor = value.GetType().GetProperty("RetainedFloor")?.GetValue(value),
            output_sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(args[2]))) };
    }
    else if (args[0] == "wire")
    {
        string name = args[1] == "query" ? "Hexalith.EventStore.Contracts.Queries.QueryEnvelope" : "Hexalith.EventStore.Contracts.Projections.ProjectionEventDto";
        Type? type = Assembly.Load("Hexalith.EventStore.Contracts").GetType(name);
        checks.Record("contract-type-present", type is not null);
        if (type is null)
        {
            observation = new { handling = "unsupported-contract-type", type = name };
        }
        else
        {
            object value;
            if (args[2] is "json" or "to-xml")
            {
                value = JsonSerializer.Deserialize(File.ReadAllBytes(args[3]), type, options) ?? throw new InvalidOperationException("Null wire.");
            }
            else
            {
                using FileStream input = File.OpenRead(args[3]);
                value = new DataContractSerializer(type).ReadObject(input) ?? throw new InvalidOperationException("Null wire.");
            }

            if (args[2] == "json")
            {
                File.WriteAllBytes(args[4], JsonSerializer.SerializeToUtf8Bytes(value, type, options));
            }
            else
            {
                using FileStream output = File.Create(args[4]);
                new DataContractSerializer(type).WriteObject(output, value);
            }

            checks.Record("wire-output-nonempty", new FileInfo(args[4]).Length > 0);
            observation = new { handling = "executed", type = name, fields = type.GetProperties()
                .ToDictionary(property => property.Name, property => property.GetValue(value)),
                output_sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(args[4]))) };
        }
    }
    else if (args[0] == "sequence")
    {
        ActorProxyFactory factory = new(new ActorProxyOptions { HttpEndpoint = args[1] });
        IAggregateActor proxy = factory.CreateActorProxy<IAggregateActor>(new ActorId($"{args[2]}:counter:{args[3]}"), "AggregateActor");
        long sequence = await proxy.GetCurrentSequenceAsync();
        checks.Record("actor-sequence-observed", sequence >= 0);
        observation = new { sequence };
    }
    else if (args[0] is "actor" or "seed")
    {
        ActorProxyFactory factory = new(new ActorProxyOptions { HttpEndpoint = args[1] });
        IAggregateActor proxy = factory.CreateActorProxy<IAggregateActor>(new ActorId($"{args[2]}:counter:{args[3]}"), "AggregateActor");
        int count = int.Parse(args[4]);
        if (args[0] == "seed")
        {
            for (int index = 0; index < count; index++)
            {
                string message = Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId();
                CommandEnvelope command = new(message, args[2], "counter", args[3], "P1R.Counter.IncrementCounter",
                    "{}"u8.ToArray(), message, null, "fixture-user", null);
                CommandProcessingResult result = await proxy.ProcessCommandAsync(command);
                checks.Record($"append-{index + 1}-accepted", result.Accepted);
                checks.Record($"append-{index + 1}-one-event", result.EventCount == 1);
                if (!result.Accepted || result.EventCount != 1)
                {
                    observation = new { accepted = result.Accepted, event_count = result.EventCount, committed = index,
                        error = result.ErrorMessage };
                    break;
                }
            }
        }
        else
        {
            string message = Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId();
            string kind = args[5];
            CommandEnvelope command = new(message, args[2], "counter", args[3], "P1R.Counter." + kind,
                JsonSerializer.SerializeToUtf8Bytes(new { Expected = count }), message, null, "fixture-user", null);
            CommandProcessingResult result = await proxy.ProcessCommandAsync(command);
            checks.Record("actor-response-observed", result is not null);
            observation = new { accepted = result!.Accepted, event_count = result.EventCount, error = result.ErrorMessage,
                sequence = await proxy.GetCurrentSequenceAsync() };
        }

        observation ??= new { committed = count, sequence = await proxy.GetCurrentSequenceAsync() };
    }
    else if (args[0] == "capabilities")
    {
        observation = new { actor_methods = typeof(IAggregateActor).GetMethods().Select(method => method.Name).Order().ToArray(),
            cursor_methods = typeof(Hexalith.EventStore.Client.Queries.QueryCursorScope).GetMethods().Select(method => method.Name).Distinct().Order().ToArray(),
            status_fields = typeof(CommandStatusRecord).GetProperties().Select(property => property.Name).Order().ToArray() };
        checks.Record("capability-inventory-observed", observation is not null);
    }
    else if (args[0] == "cursor-scope")
    {
        var scope = Hexalith.EventStore.Client.Queries.QueryCursorScope.Create().Add("tenant", "tenant-a");
        MethodInfo? method = scope.GetType().GetMethod("AddProjectionWatermark");
        if (method is null)
        {
            checks.Record("unsupported-method-observed", true);
            observation = new { handling = "unsupported", method = "AddProjectionWatermark" };
        }
        else
        {
            method.Invoke(scope, new object?[] { (long?)987 });
            bool refused = false;
            try
            {
                method.Invoke(scope, new object?[] { (long?)0 });
            }
            catch (TargetInvocationException error) when (error.InnerException is ArgumentOutOfRangeException)
            {
                refused = true;
            }

            checks.Record("zero-watermark-refused", refused);
            observation = new { handling = "executed", scope = scope.Build() };
        }
    }
    else if (args[0] == "retained-floor")
    {
        MethodInfo? method = typeof(IAggregateActor).GetMethod("GetRetainedFloorAsync");
        if (method is null)
        {
            checks.Record("unsupported-method-observed", true);
            observation = new { handling = "unsupported", method = "GetRetainedFloorAsync" };
        }
        else
        {
            ActorProxyFactory factory = new(new ActorProxyOptions { HttpEndpoint = args[1] });
            IAggregateActor proxy = factory.CreateActorProxy<IAggregateActor>(new ActorId("tenant-a:counter:fixture"), "AggregateActor");
            Task task = (Task)(method.Invoke(proxy, Array.Empty<object>()) ?? throw new InvalidOperationException("Null actor task."));
            await task;
            observation = new { handling = "executed", floor = task.GetType().GetProperty("Result")?.GetValue(task) };
            checks.Record("actor-retained-floor-returned", observation is not null);
        }
    }
    else
    {
        throw new ArgumentException("Unknown probe operation.");
    }
}
catch (Exception error)
{
    exitCode = 1;
    checks.Record("operation-completed", false);
    observation = new { error = error.GetType().Name, detail = error.Message };
}

Console.WriteLine(JsonSerializer.Serialize(new { instrumentation = "p1r-executed-checks-v1", measurement = checks.Read(), observation }));
return exitCode;

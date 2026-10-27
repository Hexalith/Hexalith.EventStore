using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
#if FLOOR
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Commands;
#endif

namespace PublishedConsumer;

/// <summary>Invokes actual actor remoting through this process's independently loaded versioned SDK.</summary>
public static class CrossClientFixture
{
    /// <summary>Executes one common or protected actor operation, retaining typed failures without declaring opaque faults successful.</summary>
    public static async Task<object> ExecuteAsync(JsonElement input)
    {
        string actorId = input.GetProperty("actor").GetString()!;
        string action = input.GetProperty("action").GetString()!;
        IAggregateActor actor = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = input.GetProperty("sidecar").GetString()!, RequestTimeout = TimeSpan.FromSeconds(20),
        }).CreateActorProxy<IAggregateActor>(new ActorId(actorId), "AggregateActor");
        try
        {
            object outcome;
            if (action == "common")
            {
                string[] address = actorId.Split(':');
                long before = await actor.GetCurrentSequenceAsync();
                CommandProcessingResult result = await actor.ProcessCommandAsync(new CommandEnvelope("cross-common-" + Guid.NewGuid().ToString("N"),
                    address[0], address[1], address[2], "Increment", "{}"u8.ToArray(), "cross-common", "cross-cause", "fixture-user", null));
                outcome = new { accepted = result.Accepted, before, sequence = await actor.GetCurrentSequenceAsync() };
            }
            else
            {
#if FLOOR
                var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                JsonElement prepared = input.GetProperty("prepared");
                bool supported = prepared.GetProperty("supported").GetBoolean();
                string[] address = actorId.Split(':');
                var command = new CommandEnvelope("cross-missing", address[0], address[1], address[2], "Increment", "{}"u8.ToArray(), "cross-correlation", "cause", "user", null);
                FencedCommandEnvelope fenced = supported ? prepared.GetProperty("fenced").Deserialize<FencedCommandEnvelope>(json)!
                    : new(command, new IdempotencyExecutionContext(1, "missing", 1, "fixture-v1", command.MessageId, command.CorrelationId,
                        address[0], address[1], address[2], command.CommandType, "forged"));
                if (action.StartsWith("fenced", StringComparison.Ordinal))
                {
                    if (action == "fenced-forged") fenced = fenced with { ExecutionContext = fenced.ExecutionContext with { Proof = "forged" } };
                    CommandProcessingResult result = await actor.ProcessFencedCommandAsync(fenced);
                    outcome = new { accepted = result.Accepted, sequence = await actor.GetCurrentSequenceAsync() };
                }
                else
                {
                    var identity = new EffectIdentity(address[0], address[1], address[2], 1, EffectKindCatalog.DateResume, address[1], address[2], 0);
                    string message = EffectIdentityCodec.ComputeMessageId(identity);
                    TrustedEffectSubmission submission = supported ? prepared.GetProperty("submission").Deserialize<TrustedEffectSubmission>(json)!
                        : new(identity, "Increment", "{}"u8.ToArray(), message, message);
                    TrustedEffectContext context = supported ? prepared.GetProperty("context").Deserialize<TrustedEffectContext>(json)!
                        : new("p1r-fixture", "qualification", "cross-cause", "fixture-only");
                    string proof = action == "trusted-forged" || !supported ? "forged-proof" : prepared.GetProperty("proof").GetString()!;
                    TrustedEffectResult result = await actor.ProcessTrustedEffectAsync(submission, context, proof);
                    outcome = new { accepted = result.Disposition.ToString() == "Success", result.Replayed, sequence = await actor.GetCurrentSequenceAsync() };
                }
#else
                return new { ok = false, supported = false, code = "client-contract-lacks-protected-methods", action,
                    missingMethods = new[] { "ProcessFencedCommandAsync", "ProcessTrustedEffectAsync" }
                        .Where(name => !typeof(IAggregateActor).GetMethods().Any(method => method.Name == name)).ToArray(),
                    identities = ConsumerIdentity.Read() };
#endif
            }
            return new { ok = true, supported = true, action, outcome, identities = ConsumerIdentity.Read() };
        }
        catch (Exception error)
        {
            Exception cause = error is ActorMethodInvocationException { InnerException: ActorInvokeException remote } ? remote : error;
            string type = cause is ActorInvokeException actorError ? actorError.ActualExceptionType : cause.GetType().FullName!;
            return new { ok = false, supported = true, action, error = error.GetType().FullName, actualType = type,
                message = cause.Message, cause = error.ToString(), identities = ConsumerIdentity.Read() };
        }
    }
}

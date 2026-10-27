using System.Text.Json;
using System.Security.Cryptography;
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Server.Actors;
#if FLOOR
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
#endif

namespace PublishedConsumer;

/// <summary>Executes real actor fence/effect operations or exposes an old process's typed unsupported boundary.</summary>
public static class CapabilityEndpoints
{
    /// <summary>Maps the fenced command and trusted-effect fixture endpoints.</summary>
    public static void Map(WebApplication app)
    {
        app.MapPost("/capabilities/{aggregate}", async (string aggregate, IServiceProvider services) =>
        {
#if FLOOR
            try
            {
                var command = new SubmitCommand("fenced-message-" + aggregate, "tenant-a", "counter", aggregate, "Increment", "{}"u8.ToArray(),
                    "correlation", "fixture-user", IdempotencyKey: "p1r-key-" + aggregate);
                var admission = services.GetRequiredService<IIdempotencyAdmissionCoordinator>();
                var router = services.GetRequiredService<ICommandRouter>();
                IdempotencyAdmissionSession original = (await admission.AdmitAsync(command))!;
                await admission.BeginAsync(original);
                await admission.MarkRecoveryAsync(original, IdempotencyAdmissionState.Recoverable);
                IdempotencyAdmissionSession current = (await admission.AdmitAsync(command))!;
                var execution = command with { MessageId = current.ExecutionMessageId!, CorrelationId = current.ExecutionCorrelationId!, IdempotencyKey = null };
                string actorId = "tenant-a:counter:" + aggregate;
                IAggregateActor actor = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = "http://127.0.0.1:" + Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") })
                    .CreateActorProxy<IAggregateActor>(new ActorId(actorId), "AggregateActor");
                long baseline = await actor.GetCurrentSequenceAsync();
                int inventoryHead = checked((int)baseline + 3);
                var staleExecution = command with { MessageId = original.ExecutionMessageId!, CorrelationId = original.ExecutionCorrelationId!, IdempotencyKey = null };
                JsonElement stale = await DeniedAsync(actorId, inventoryHead, "stale-execution-authority", "execution-authority",
                    "The idempotency execution authority is no longer current.", () => router.RouteFencedCommandAsync(staleExecution, original.ExecutionContext!), services, original.ExecutionContext!.AdmissionActorId);
                JsonElement forged = await DeniedAsync(actorId, inventoryHead, "invalid-execution-fence", "execution-fence-proof",
                    "The idempotency execution fence is missing, stale, or invalid.", () => router.RouteFencedCommandAsync(execution, current.ExecutionContext! with { Proof = "forged" }), services);
                await admission.BeginAsync(current);
                CommandProcessingResult result = await router.RouteFencedCommandAsync(execution, current.ExecutionContext!);
                await admission.CompleteAsync(current, result);
                JsonElement completedStale = await DeniedAsync(actorId, inventoryHead, "completed-execution-authority", "execution-authority",
                    "The idempotency execution authority is no longer current.", () => router.RouteFencedCommandAsync(execution, current.ExecutionContext!), services, current.ExecutionContext!.AdmissionActorId);
                var identity = new EffectIdentity("tenant-a", "counter", aggregate, 1, EffectKindCatalog.DateResume, "counter", aggregate, 0);
                string message = EffectIdentityCodec.ComputeMessageId(identity);
                var submission = new TrustedEffectSubmission(identity, "Increment", "{}"u8.ToArray(), message, message);
                var context = new TrustedEffectContext("p1r-fixture", "qualification", "wrk-" + EffectIdentityCodec.ComputeEffectId(identity), "fixture-only");
                var policy = services.GetRequiredService<ITrustedEffectAdmissionPolicy>();
                var proof = services.GetRequiredService<ITrustedEffectGatewayProof>();
                TrustedEffectAdmission prepared = await policy.PrepareAsync(submission, context);
                string signed = await proof.SignAsync(prepared);
                JsonElement unauthorized = await DeniedAsync(actorId, inventoryHead, "unauthorized-effect-delegation", "trusted-effect-delegation",
                    "Synthetic trusted-effect delegation refused.", () => actor.ProcessTrustedEffectAsync(submission, context with { DelegationToken = "forged" }, signed), services);
                JsonElement forgedEffect = await DeniedAsync(actorId, inventoryHead, "invalid-effect-gateway-proof", "trusted-effect-gateway-proof",
                    "Trusted effect gateway proof is invalid.", () => actor.ProcessTrustedEffectAsync(submission, context, "forged-proof"), services);
                TrustedEffectResult effect = await actor.ProcessTrustedEffectAsync(submission, context, signed);
                TrustedEffectResult replay = await actor.ProcessTrustedEffectAsync(submission, context, signed);
                return Results.Json(new { supported = true, stale, forged, completedStale, accepted = result.Accepted,
                    unauthorized, forgedEffect, baseline, sequence = await actor.GetCurrentSequenceAsync(),
                    effect = effect.Disposition.ToString(), replayed = replay.Replayed, recoveryFenceReused = original.FencingToken == current.FencingToken });
            }
            catch (Exception error) { return Results.Json(new { supported = true, error = error.GetType().FullName, message = error.Message }); }
#else
            await Task.CompletedTask;
            return Results.Json(new { supported = false, denial = "incapable-published-endpoint", stage = "before-protected-route",
                protectedAccess = false, effect = false, write = false,
                methods = typeof(IAggregateActor).GetMethods().Select(method => method.Name).ToArray() });
#endif
        });
    }

#if FLOOR
    private static async Task<JsonElement> DeniedAsync(string actorId, int head, string expectedCode, string expectedStage,
        string expectedMessage, Func<Task> operation, IServiceProvider services, string? authorityId = null)
    {
        Dictionary<string, string?> before = await ReadCommittedAsync(actorId, head);
        int deadLettersBefore = services.GetRequiredService<BoundedDeadLetterPublisher>().Count;
        var observer = services.GetRequiredService<ExecutionAuthorityRefusalObserver>();
        long checkpoint = observer.Checkpoint;
        Exception? failure = null;
        try { await operation(); }
        catch (Exception error) { failure = error; }
        Dictionary<string, string?> after = await ReadCommittedAsync(actorId, head);
        Exception? cause = failure is ActorMethodInvocationException { InnerException: ActorInvokeException remote } ? remote : failure;
        string? actualType = cause is ActorInvokeException invocation ? invocation.ActualExceptionType : cause?.GetType().FullName;
        string? actualMessage = cause?.Message;
        JsonElement? guard = observer.Read(checkpoint, actorId, authorityId);
        if (failure is ActorMethodInvocationException && cause is ActorInvokeException nested
            && nested.ActualExceptionType == typeof(ActorMethodInvocationException).FullName
            && expectedMessage == "The idempotency execution authority is no longer current." && guard is not null)
        {
            actualType = guard.Value.GetProperty("exceptionType").GetString();
            actualMessage = guard.Value.GetProperty("message").GetString();
        }
        bool expected = actualType == typeof(InvalidOperationException).FullName && actualMessage == expectedMessage;
        return JsonSerializer.SerializeToElement(new
        {
            expected, code = expected ? expectedCode : "unexpected-fault", stage = expected ? expectedStage : "unverified",
            exceptionType = failure?.GetType().FullName, actualType, message = actualMessage, guard,
            cause = failure?.ToString(), before, after,
            bookkeeping = new { deadLettersBefore, deadLettersAfter = services.GetRequiredService<BoundedDeadLetterPublisher>().Count },
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static async Task<Dictionary<string, string?>> ReadCommittedAsync(string actorId, int head)
    {
        // Dapr owns application state access. This inventory deliberately contains only committed stream state;
        // admission/command-status/pipeline/dead-letter bookkeeping has separate identities and observations.
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:" + Environment.GetEnvironmentVariable("DAPR_HTTP_PORT")), Timeout = TimeSpan.FromSeconds(30) };
        string[] keys = [actorId + ":metadata", actorId + ":snapshot", .. Enumerable.Range(1, head).Select(sequence => actorId + ":events:" + sequence)];
        var inventory = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (string key in keys)
        {
            using HttpResponseMessage response = await http.GetAsync("/v1.0/actors/AggregateActor/" + Uri.EscapeDataString(actorId) + "/state/" + Uri.EscapeDataString(key));
            if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.NoContent) inventory.Add(key, null);
            else
            {
                response.EnsureSuccessStatusCode();
                inventory.Add(key, Convert.ToHexStringLower(SHA256.HashData(await response.Content.ReadAsByteArrayAsync())));
            }
        }
        return inventory;
    }
#endif
}

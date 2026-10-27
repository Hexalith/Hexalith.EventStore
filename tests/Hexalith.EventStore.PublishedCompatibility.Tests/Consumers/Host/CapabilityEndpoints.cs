using System.Text.Json;
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
                bool stale = await DeniedAsync(() => router.RouteFencedCommandAsync(execution, original.ExecutionContext!));
                bool forged = await DeniedAsync(() => router.RouteFencedCommandAsync(execution, current.ExecutionContext! with { Proof = "forged" }));
                await admission.BeginAsync(current);
                CommandProcessingResult result = await router.RouteFencedCommandAsync(execution, current.ExecutionContext!);
                await admission.CompleteAsync(current, result);
                bool completedStale = await DeniedAsync(() => router.RouteFencedCommandAsync(execution, original.ExecutionContext!));
                var identity = new EffectIdentity("tenant-a", "counter", aggregate, 1, EffectKindCatalog.DateResume, "counter", aggregate, 0);
                string message = EffectIdentityCodec.ComputeMessageId(identity);
                var submission = new TrustedEffectSubmission(identity, "Increment", "{}"u8.ToArray(), message, message);
                var context = new TrustedEffectContext("p1r-fixture", "qualification", "wrk-" + EffectIdentityCodec.ComputeEffectId(identity), "fixture-only");
                var policy = services.GetRequiredService<ITrustedEffectAdmissionPolicy>();
                var proof = services.GetRequiredService<ITrustedEffectGatewayProof>();
                TrustedEffectAdmission prepared = await policy.PrepareAsync(submission, context);
                string signed = await proof.SignAsync(prepared);
                IAggregateActor actor = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = "http://127.0.0.1:" + Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") })
                    .CreateActorProxy<IAggregateActor>(new ActorId("tenant-a:counter:" + aggregate), "AggregateActor");
                long before = await actor.GetCurrentSequenceAsync();
                bool unauthorized = await DeniedAsync(() => actor.ProcessTrustedEffectAsync(submission, context with { DelegationToken = "forged" }, signed));
                bool forgedEffect = await DeniedAsync(() => actor.ProcessTrustedEffectAsync(submission, context, "forged-proof"));
                long afterDenial = await actor.GetCurrentSequenceAsync();
                TrustedEffectResult effect = await actor.ProcessTrustedEffectAsync(submission, context, signed);
                TrustedEffectResult replay = await actor.ProcessTrustedEffectAsync(submission, context, signed);
                return Results.Json(new { supported = true, stale, forged, completedStale, accepted = result.Accepted,
                    unauthorized, forgedEffect, before, afterDenial, sequence = await actor.GetCurrentSequenceAsync(),
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
    private static async Task<bool> DeniedAsync(Func<Task> operation)
    {
        try { await operation(); return false; }
        catch (Exception) { return true; }
    }
#endif
}

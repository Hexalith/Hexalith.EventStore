using Hexalith.EventStore.Server.Actors;
#if FLOOR
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
#endif

namespace PublishedConsumer;

/// <summary>Prepares host-owned admission capabilities for an independently versioned remote actor client.</summary>
public static class CrossClientEndpoints
{
    /// <summary>Maps preparation only; the versioned Probe invokes every actor operation itself.</summary>
    public static void Map(WebApplication app)
    {
        app.MapPost("/prepare-cross-client/{aggregate}", async (string aggregate, IServiceProvider services) =>
        {
#if FLOOR
            var command = new SubmitCommand("cross-message-" + aggregate, "tenant-a", "counter", aggregate, "Increment", "{}"u8.ToArray(),
                "cross-correlation", "fixture-user", IdempotencyKey: "cross-key-" + aggregate);
            var admission = services.GetRequiredService<IIdempotencyAdmissionCoordinator>();
            IdempotencyAdmissionSession session = (await admission.AdmitAsync(command))!;
            await admission.BeginAsync(session);
            var envelope = new CommandEnvelope(session.ExecutionMessageId!, "tenant-a", "counter", aggregate, "Increment", "{}"u8.ToArray(),
                session.ExecutionCorrelationId!, "cross-cause", "fixture-user", null);
            var identity = new EffectIdentity("tenant-a", "counter", aggregate, 1, EffectKindCatalog.DateResume, "counter", aggregate, 0);
            string message = EffectIdentityCodec.ComputeMessageId(identity);
            var submission = new TrustedEffectSubmission(identity, "Increment", "{}"u8.ToArray(), message, message);
            var context = new TrustedEffectContext("p1r-fixture", "qualification", "wrk-" + EffectIdentityCodec.ComputeEffectId(identity), "fixture-only");
            TrustedEffectAdmission prepared = await services.GetRequiredService<ITrustedEffectAdmissionPolicy>().PrepareAsync(submission, context);
            string proof = await services.GetRequiredService<ITrustedEffectGatewayProof>().SignAsync(prepared);
            return Results.Json(new { supported = true, fenced = new FencedCommandEnvelope(envelope, session.ExecutionContext!), submission, context, proof });
#else
            await Task.CompletedTask;
            return Results.Json(new { supported = false, code = "host-contract-lacks-protected-methods" });
#endif
        });
    }
}

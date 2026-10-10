#if P1R_CAPABILITIES
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.DomainService;
#if P1R_CANDIDATE
using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Reminders;
#endif
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

/// <summary>Executes non-null signed capabilities through the actual published actor methods.</summary>
internal static class QualificationCapabilities
{
    /// <summary>Configures a bounded semantic adapter and explicitly disclosed fixture authority.</summary>
    internal static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IIdempotencyIntentAdapter, FixtureIntentAdapter>();
        builder.Services.AddSingleton<ITrustedEffectAdmissionPolicy, FixtureEffectAdmission>();
        builder.Services.AddSingleton<ITrustedEffectAuditSink, FixtureTrustedEffectAuditSink>();
#if P1R_CANDIDATE
        builder.Services.AddSingleton<FixtureReminderIntentSource>();
        builder.Services.AddSingleton<IReminderIntentSource>(services => services.GetRequiredService<FixtureReminderIntentSource>());
        builder.Services.AddEventStoreReminders<FixtureReminderIntentSource>();
        builder.Services.AddSingleton<IReminderDelegationTokenProvider, FixtureReminderDelegation>();
        builder.Services.AddSingleton<Hexalith.EventStore.Client.Effects.ITrustedEffectSubmitter, FixtureEffectSubmitter>();
        builder.Configuration["EventStore:Reminders:ActorTypeName"] = "P1RReminderActor";
        builder.Configuration["EventStore:Reminders:Workload"] = "p1r-fixture";
        builder.Configuration["EventStore:Reminders:Purposes:" + EffectKindCatalog.DateResume] = "published-qualification";
        builder.Configuration["EventStore:Reminders:ReconciliationEnabled"] = "false";
#endif
        if (builder.Configuration["P1R_DIGEST_KEY"] is string key)
        {
            builder.Configuration["EventStore:IdempotencyAdmission:Enabled"] = "true";
            builder.Configuration["EventStore:IdempotencyAdmission:ActiveDigestKeyVersion"] = "p1r-v1";
            builder.Configuration["EventStore:IdempotencyAdmission:DigestKeySource"] = "Configuration";
            builder.Configuration["EventStore:IdempotencyAdmission:DigestKeys:p1r-v1"] = key;
        }
    }

    /// <summary>Maps isolated instrumentation operations; credentials never appear in response bodies.</summary>
    internal static void Map(WebApplication app)
    {
#if P1R_CANDIDATE
        app.MapEventStoreReminders();
        app.MapPost("/reminder/register/{revision:int}", async (int revision, FixtureReminderIntentSource source, IReminderRegistrar registrar) =>
        {
            source.Current = FixtureReminderIntentSource.Intent(revision);
            return Results.Ok(await registrar.ConvergeAsync(new ReminderTarget("tenant-a", "counter", "fixture")));
        });
        app.MapPost("/reminder/callback/{revision:int}", async (int revision, IConfiguration configuration) =>
        {
            string actorId = ReminderIdentityCodec.ComputeActorId("tenant-a", "fixture");
            string name = ReminderIdentityCodec.ComputeReminderName(FixtureReminderIntentSource.Intent(revision));
            using HttpClient client = new();
            string address = configuration["ASPNETCORE_URLS"] ?? throw new InvalidOperationException("Owned app address unavailable.");
            using HttpRequestMessage request = new(HttpMethod.Put, address + $"/actors/P1RReminderActor/{actorId}/method/remind/{name}");
            request.Content = new StringContent("{\"data\":null,\"dueTime\":\"0h0m0s0ms\"}", System.Text.Encoding.UTF8, "application/json");
            request.Headers.Add("dapr-api-token", configuration["APP_API_TOKEN"]);
            using HttpResponseMessage response = await client.SendAsync(request);
            IAggregateActor actor = ActorProxy.Create<IAggregateActor>(new ActorId("tenant-a:counter:fixture"), "AggregateActor");
            return Results.Ok(new { callback_status = (int)response.StatusCode, sequence = await actor.GetCurrentSequenceAsync() });
        });
#endif
        app.MapPost("/qualification/{operation}", async (string operation, IServiceProvider services, IConfiguration configuration) =>
        {
            if (operation is not ("fenced-effect" or "stale-fence" or "trusted-effect" or "unauthorized-effect"))
            {
                return Results.BadRequest(new { error = "unknown-qualification-operation" });
            }

            string aggregate = "fixture";
            var identity = new AggregateIdentity("tenant-a", "counter", aggregate);
            IAggregateActor actor = ActorProxy.Create<IAggregateActor>(new ActorId(identity.ActorId), "AggregateActor");
            if (operation is "fenced-effect" or "stale-fence")
            {
                var request = new SubmitCommand("p1r-message", identity.TenantId, identity.Domain, identity.AggregateId,
                    "P1R.Counter.IncrementCounter", "{}"u8.ToArray(), "p1r-correlation", "fixture-user", IdempotencyKey: "p1r-key");
                IIdempotencyAdmissionCoordinator admission = services.GetRequiredService<IIdempotencyAdmissionCoordinator>();
                IdempotencyAdmissionSession original = (await admission.AdmitAsync(request))!;
                await admission.BeginAsync(original);
                await admission.MarkRecoveryAsync(original, IdempotencyAdmissionState.Recoverable);
                IdempotencyAdmissionSession current = (await admission.AdmitAsync(request))!;
                var execution = request with { MessageId = current.ExecutionMessageId!, CorrelationId = current.ExecutionCorrelationId!, IdempotencyKey = null };
                ICommandRouter router = services.GetRequiredService<ICommandRouter>();
                if (operation == "stale-fence")
                {
                    bool staleRefused = false, forgedRefused = false, staleAccepted = false, forgedAccepted = false, unexpected = false;
                    object? staleDiagnostic = null, forgedDiagnostic = null;
                    try
                    {
                        staleAccepted = (await router.RouteFencedCommandAsync(execution, original.ExecutionContext!)).Accepted;
                    }
                    catch (Exception error) when (ExpectedDenial.Matches(error, ExpectedDenial.StaleAuthority))
                    {
                        staleRefused = true;
                        staleDiagnostic = ExpectedDenial.Describe(error);
                    }
                    catch (Exception error)
                    {
                        unexpected = true;
                        staleDiagnostic = ExpectedDenial.Describe(error);
                    }

                    try
                    {
                        forgedAccepted = (await router.RouteFencedCommandAsync(execution, current.ExecutionContext! with { Proof = "forged-proof" })).Accepted;
                    }
                    catch (Exception error) when (ExpectedDenial.Matches(error, ExpectedDenial.Fence))
                    {
                        forgedRefused = true;
                        forgedDiagnostic = ExpectedDenial.Describe(error);
                    }
                    catch (Exception error)
                    {
                        unexpected = true;
                        forgedDiagnostic = ExpectedDenial.Describe(error);
                    }
                    return Results.Ok(new { accepted = staleAccepted || forgedAccepted, unexpected,
                        stale_diagnostic = staleDiagnostic, forged_diagnostic = forgedDiagnostic, stale_refused = staleRefused, forged_refused = forgedRefused,
                        stale_denial = staleRefused ? "stale-fencing-token" : null, forged_denial = forgedRefused ? "stale-or-invalid-fence" : null,
                        stale_actor_method = "ProcessFencedCommandAsync", forged_actor_method = "ProcessFencedCommandAsync",
                        sequence = await actor.GetCurrentSequenceAsync() });
                }

                await admission.BeginAsync(current);
                CommandProcessingResult result = await router.RouteFencedCommandAsync(execution, current.ExecutionContext!);
                await admission.CompleteAsync(current, result);
                return Results.Ok(new { accepted = result.Accepted, event_count = result.EventCount,
                    sequence = await actor.GetCurrentSequenceAsync() });
            }

            var effect = new EffectIdentity("tenant-a", "counter", "source-fixture", 1, EffectKindCatalog.DateResume, "counter", aggregate, 0);
            string message = EffectIdentityCodec.ComputeMessageId(effect);
            var submission = new TrustedEffectSubmission(effect, "P1R.Counter.IncrementCounter", "{}"u8.ToArray(), message, message);
            var context = new TrustedEffectContext("p1r-fixture", "published-qualification", "fixture-source-event",
                configuration["P1R_DELEGATION"] ?? throw new InvalidOperationException("Owned fixture delegation unavailable."));
            ITrustedEffectAdmissionPolicy policy = services.GetRequiredService<ITrustedEffectAdmissionPolicy>();
            ITrustedEffectGatewayProof proof = services.GetRequiredService<ITrustedEffectGatewayProof>();
            TrustedEffectAdmission prepared = await policy.PrepareAsync(submission, context);
            string signed = await proof.SignAsync(prepared);
            if (operation == "unauthorized-effect")
            {
                bool refused = false, acceptedUnexpectedly = false, unexpected = false;
                object? diagnostic = null;
                try
                {
                    acceptedUnexpectedly = (await actor.ProcessTrustedEffectAsync(submission, context, "forged-proof")).Disposition == TrustedEffectDisposition.Success;
                }
                catch (Exception error) when (ExpectedDenial.Matches(error, ExpectedDenial.GatewayProof))
                {
                    refused = true;
                    diagnostic = ExpectedDenial.Describe(error);
                }
                catch (Exception error)
                {
                    unexpected = true;
                    diagnostic = ExpectedDenial.Describe(error);
                }
                return Results.Ok(new { accepted = acceptedUnexpectedly, unexpected, diagnostic, unauthorized_refused = refused, denial = refused ? "invalid-gateway-proof" : null, sequence = await actor.GetCurrentSequenceAsync() });
            }

            TrustedEffectResult accepted = await actor.ProcessTrustedEffectAsync(submission, context, signed);
            await policy.CompleteAsync(prepared);
            TrustedEffectResult replayed = await actor.ProcessTrustedEffectAsync(submission, context, signed);
            return Results.Ok(new { accepted = accepted.Disposition == TrustedEffectDisposition.Success,
                replayed = replayed.Replayed, sequence = await actor.GetCurrentSequenceAsync() });
        });
    }
}
#endif

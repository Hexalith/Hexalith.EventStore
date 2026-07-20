using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;

/// <summary>Production-equivalent OQ8 proof against the deployed PostgreSQL actor-state profile.</summary>
[Collection("Oq8Postgresql")]
[Trait("Category", "LiveSidecar")]
[Trait("Profile", "oq8-postgresql-v1")]
public sealed class IdempotencyAdmissionOq8PostgresqlTests(Oq8PostgresqlFixture fixture)
{
    [Fact]
    public async Task ConcurrentFirstWriters_HostFailover_PreserveOneFencedExecutionAndExactReplay()
    {
        const string RawKey = "oq8-postgresql-raw-idempotency-sentinel";
        fixture.DomainServiceInvoker.ClearAll();
        fixture.EventPublisher.Reset();
        fixture.DomainServiceInvoker.SetupResponse(
            "IncrementCounter",
            Hexalith.EventStore.Contracts.Results.DomainResult.Success(
            [
                new Hexalith.EventStore.Sample.Counter.Events.CounterIncremented(),
            ]));
        IIdempotencyAdmissionCoordinator primary = fixture.PrimaryServices
            .GetRequiredService<IIdempotencyAdmissionCoordinator>();
        IIdempotencyAdmissionCoordinator replica = fixture.ReplicaServices
            .GetRequiredService<IIdempotencyAdmissionCoordinator>();
        string aggregateId = $"oq8-{Guid.NewGuid():N}";
        var request = new SubmitCommand(
            "01JOQ800000000000000000000",
            "tenant-oq8",
            "counter",
            aggregateId,
            "IncrementCounter",
            "{\"amount\":1}"u8.ToArray(),
            "oq8-correlation-0",
            "oq8-user",
            IdempotencyKey: RawKey);

        IdempotencyAdmissionSession[] sessions = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(async index =>
            {
                IIdempotencyAdmissionCoordinator coordinator = index % 2 == 0 ? primary : replica;
                return await coordinator.AdmitAsync(request with
                {
                    MessageId = $"01JOQ8000000000000000000{index:D2}",
                    CorrelationId = $"oq8-correlation-{index}",
                }) ?? throw new InvalidOperationException("OQ8 admission returned no session.");
            }));

        IdempotencyAdmissionSession executable = sessions.Single(session =>
            session.Decision == IdempotencyAdmissionDecision.Execute);
        sessions.Count(session => session.Decision == IdempotencyAdmissionDecision.Pending).ShouldBe(11);
        sessions.Select(session => session.FencingToken).Distinct().ShouldBe([executable.FencingToken]);
        sessions.Select(session => session.ExecutionMessageId).Distinct().ShouldBe([executable.ExecutionMessageId]);

        IdempotencyAdmissionSession conflict = (await replica.AdmitAsync(request with
        {
            MessageId = "01JOQ8CONFLICT000000000000",
            CorrelationId = "oq8-conflict",
            Payload = "{\"amount\":2}"u8.ToArray(),
        }))!;
        conflict.Decision.ShouldBe(IdempotencyAdmissionDecision.Conflict);
        fixture.DomainServiceInvoker.Invocations.ShouldBeEmpty();
        fixture.EventPublisher.PublishCalls.ShouldBeEmpty();

        IIdempotencyAdmissionActor admissionActor = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = fixture.ReplicaDaprHttpEndpoint,
            RequestTimeout = TimeSpan.FromSeconds(15),
        }).CreateActorProxy<IIdempotencyAdmissionActor>(
            new ActorId(executable.ActorId),
            IdempotencyAdmissionActor.ActorTypeName);
        IdempotencyAdmissionInspection before = await admissionActor.InspectAsync();
        before.Record.ShouldNotBeNull().State.ShouldBe(IdempotencyAdmissionState.Reserved);
        before.Record.FencingToken.ShouldBe(executable.FencingToken);

        await fixture.StopPrimaryNodeAsync();
        await Task.Delay(2000);

        var execution = request with
        {
            MessageId = executable.ExecutionMessageId!,
            CorrelationId = executable.ExecutionCorrelationId!,
            IdempotencyKey = null,
        };
        await replica.BeginAsync(executable);
        await replica.ValidateExecutionAsync(executable, execution);
        ICommandRouter router = fixture.ReplicaServices.GetRequiredService<ICommandRouter>();
        CommandProcessingResult terminal = await router.RouteFencedCommandAsync(
            execution,
            executable.ExecutionContext!);
        await replica.CompleteAsync(executable, terminal);

        IdempotencyAdmissionSession replay = (await replica.AdmitAsync(request with
        {
            MessageId = "01JOQ8REPLAY0000000000000",
            CorrelationId = "oq8-replay",
        }))!;
        IdempotencyAdmissionInspection after = await admissionActor.InspectAsync();

        terminal.Accepted.ShouldBeTrue();
        terminal.EventCount.ShouldBe(1);
        replay.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        replay.ReplayResult.ShouldBe(terminal);
        fixture.DomainServiceInvoker.Invocations.Count.ShouldBe(1);
        fixture.EventPublisher.PublishCalls.Count.ShouldBe(1);
        fixture.EventPublisher.TotalEventsPublished.ShouldBe(1);
        after.Record.ShouldNotBeNull().State.ShouldBe(IdempotencyAdmissionState.Terminal);
        after.Record.ReplayResult.ShouldBe(terminal);
        JsonSerializer.Serialize(new { Before = before, After = after }).ShouldNotContain(RawKey);
    }
}

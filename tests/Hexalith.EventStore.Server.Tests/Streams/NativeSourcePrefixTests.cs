using System.Security.Claims;
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Streams;

public sealed class NativeSourcePrefixTests
{
    [Theory]
    [InlineData(101, 2)]
    [InlineData(3, 1_100_000)]
    public async Task AuthenticatedNativePagingPreservesExactFinalProofAcrossCountAndActorByteBounds(int count, int payloadSize)
    {
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { data = new string('a', payloadSize) });
        EventEnvelope[] events = [.. Enumerable.Range(1, count).Select(sequence => new EventEnvelope("message-" + sequence,
            identity.AggregateId, "Aggregate", identity.TenantId, identity.Domain, sequence, sequence, now,
            "corr", "cause", "actor", "v1", "Event", 1, "json", payload, null))];
        SourceProof native = SourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        foreach (var item in events) native = SourceProofCodec.Extend(native, item);
        var committed = events.ToDictionary(item => identity.EventStreamKeyPrefix + item.SequenceNumber, item => (object)item, StringComparer.Ordinal);
        committed[identity.MetadataKey] = new AggregateMetadata(count, now, null) { SourceProof = native };
        var state = new Hexalith.EventStore.Server.Tests.Actors.FaultInjectingActorStateManager();
        await state.SeedCommittedStateAsync(committed);
        var actor = Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper.CreateActor(stateManager: state).Actor;
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor", Arg.Any<ActorProxyOptions?>()).Returns(actor);
        var controller = new StreamsController(actors, Substitute.For<Hexalith.EventStore.Authorization.ITenantValidator>(), Substitute.For<IRbacValidator>(),
            NullLogger<StreamsController>.Instance)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "tenants"),
                new Claim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, identity.TenantId),
                new Claim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType, identity.Domain),
            ], "workload")) } },
        };
        var dataPages = new List<StreamReadPage>();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var request = call.Arg<StreamReadRequest>();
            var response = await controller.ReadWorkloadStreamAsync(request, call.Arg<CancellationToken>());
            var page = response.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<StreamReadPage>();
            if (request.ToSequence > 0) dataPages.Add(page);
            return page;
        });
        var result = await new AuthoritativeEventStreamReader(gateway, TimeProvider.System, requireNativeProof: true)
            .ReadAsync(identity, TestContext.Current.CancellationToken);
        result.IsAuthoritative.ShouldBeTrue();
        result.Stream!.NativeProof.ShouldBe(SourceProofCodec.ToNative(native));
        result.Stream.Events.Count.ShouldBe(count);
        result.Stream.Events.Select(item => item.NativeEventDigest).ShouldBe(events.Select(SourceProofCodec.ComputeEventHash));
        dataPages.Select(page => page.Events.Count).ShouldBe(count == 101 ? [100, 1] : [1, 1, 1]);
        dataPages.Take(dataPages.Count - 1).ShouldAllBe(page => page.Metadata.IsTruncated);
        dataPages[^1].Metadata.IsTruncated.ShouldBeFalse();
        dataPages.ShouldAllBe(page => page.Metadata.NativeProof == SourceProofCodec.ToNative(native));
        state.CommittedSnapshots.ShouldBeEmpty();
        state.Trace.ShouldNotContain(entry => entry.StartsWith("SaveState", StringComparison.Ordinal)
            || entry.StartsWith("SetState", StringComparison.Ordinal) || entry.StartsWith("AddState", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NativeActorRangeRefusesRecoveryAndPreservesActualPersistedState()
    {
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var stored = new EventEnvelope("actual-message", identity.AggregateId, "Aggregate", identity.TenantId, identity.Domain,
            1, 5, DateTimeOffset.UnixEpoch, "corr", "cause", "actor", "v1", "Event", 1, "json", "{}"u8.ToArray(), null);
        var native = SourceProofCodec.Extend(SourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV"), stored);
        var state = new Hexalith.EventStore.Server.Tests.Actors.FaultInjectingActorStateManager();
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [identity.MetadataKey] = new AggregateMetadata(1, DateTimeOffset.UnixEpoch, null) { SourceProof = native },
            [identity.EventStreamKeyPrefix + "1"] = stored,
        });
        var context = Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper.CreateActor(stateManager: state);
        string before = JsonSerializer.Serialize(state.CommittedState);
        var range = await context.Actor.ReadNativeEventsBoundedRangeAsync(0, 1, 100, 2_097_152);
        range.Events.ShouldBe([stored]); range.ReasonCode.ShouldBeNull();
        JsonSerializer.Serialize(state.CommittedState).ShouldBe(before);
        state.CommittedSnapshots.ShouldBeEmpty();
        state.Trace.ShouldNotContain(entry => entry.StartsWith("SaveState", StringComparison.Ordinal)
            || entry.StartsWith("SetState", StringComparison.Ordinal) || entry.StartsWith("AddState", StringComparison.Ordinal));
        typeof(AggregateActor).GetField("_pendingFinalizerRecoveryRequired", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(context.Actor, true);
        await Should.ThrowAsync<InvalidOperationException>(() => context.Actor.ReadNativeEventsBoundedRangeAsync(0, 1, 100, 2_097_152));
        JsonSerializer.Serialize(state.CommittedState).ShouldBe(before);
        state.CommittedSnapshots.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("stable")]
    [InlineData("same-head-replacement")]
    [InlineData("same-incarnation-payload-replacement")]
    [InlineData("legacy")]
    public async Task AuthenticatedGatewayAndNativeReaderBindTheExactPersistedRawPrefix(string scenario)
    {
        var identity = new AggregateIdentity("system", "tenants", "tenant-a");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        EventEnvelope[] events = [.. Enumerable.Range(1, 2).Select(index => new EventEnvelope("source-" + index,
            identity.AggregateId, "Tenant", identity.TenantId, identity.Domain, index, index, now,
            "corr", "cause", "actor", "v1", "TenantEvent", 1, "json", "{}"u8.ToArray(), null))];
        SourceProof original = SourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        foreach (var item in events) original = SourceProofCodec.Extend(original, item);
        SourceProof replacement = SourceProofCodec.CreateSeed(identity, scenario == "same-head-replacement"
            ? "01ARZ3NDEKTSV4RRFFQ69G5FAW" : original.SourceIncarnation);
        foreach (var item in events) replacement = SourceProofCodec.Extend(replacement,
            item.SequenceNumber == 1 && scenario == "same-incarnation-payload-replacement" ? item with { Payload = "{\"replaced\":true}"u8.ToArray() } : item);
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        bool changed = false;
        actor.InspectSourceProofAsync().Returns(_ => new SourceProofInspection(
            scenario == "legacy" ? SourceProofInspectionStatus.Unsupported : SourceProofInspectionStatus.Available,
            true, 2, false, scenario == "legacy" ? null : changed ? replacement : original));
        actor.ReadNativeEventsBoundedRangeAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(call =>
            {
                long after = call.ArgAt<long>(0), head = call.ArgAt<long>(1);
                if (head > 0 && scenario is "same-head-replacement" or "same-incarnation-payload-replacement") changed = true;
                EventEnvelope[] page = [.. events.Where(item => item.SequenceNumber > after && item.SequenceNumber <= head)];
                return new SourceEventRange(page, JsonSerializer.SerializeToUtf8Bytes(page, JsonSerializerOptions.Web).Length);
            });
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        actors.CreateActorProxy<IAggregateActor>(Arg.Is<ActorId>(id => id.GetId() == identity.ActorId), "AggregateActor", Arg.Any<ActorProxyOptions?>()).Returns(actor);
        var controller = new StreamsController(actors, Substitute.For<Hexalith.EventStore.Authorization.ITenantValidator>(), Substitute.For<IRbacValidator>(),
            NullLogger<StreamsController>.Instance)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "tenants"),
                new Claim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, identity.TenantId),
                new Claim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType, identity.Domain),
            ], "workload")) } },
        };
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var result = await controller.ReadWorkloadStreamAsync(call.Arg<StreamReadRequest>(), call.Arg<CancellationToken>());
            if (result is OkObjectResult { Value: StreamReadPage page }) return page;
            throw new InvalidOperationException("native source unavailable");
        });
        var result = await new AuthoritativeEventStreamReader(gateway, TimeProvider.System, requireNativeProof: true)
            .ReadAsync(identity, TestContext.Current.CancellationToken);
        result.IsAuthoritative.ShouldBe(scenario == "stable");
        if (scenario == "stable")
        {
            result.Stream!.NativeProof.ShouldBe(SourceProofCodec.ToNative(original));
            result.Stream.Events.Select(item => item.NativeEventDigest).ShouldBe(events.Select(SourceProofCodec.ComputeEventHash));
            result.Stream.Events.Select(item => item.MessageId).ShouldBe(events.Select(item => item.MessageId));
        }
        await actor.DidNotReceive().GetStreamMetadataAsync();
        await actor.DidNotReceiveWithAnyArgs().ReadEventsRangeAsync(default, default, default);
    }
}

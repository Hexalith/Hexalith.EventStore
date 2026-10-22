using System.Runtime.InteropServices;
using System.Text.Json;

using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class ProjectionEventWorkingMemoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedCanonicalPrefixBoundsAttributableSdkActorAndStateDecodeBuffers(bool mixedClear)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(48, 1, 2,
            "full-replay-v1").ConfigureAwait(true);
        Hexalith.EventStore.Server.Events.EventEnvelope clear = fixture.Events[^1];
        fixture.SetProtectedTailPayloadSizes(1, Enumerable.Repeat(1_048_576, 47).ToArray());
        if (mixedClear)
        {
            fixture.Events[^1] = clear;
            fixture.SetTailPayloadSizes(47, 1_048_576);
        }
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        ProjectionSourceGuardReplayAdmission retained = await fixture.AdmitCanonicalRouteAsync(
            fixture.Catalog.OrdinaryProjectionTypes[0]).ConfigureAwait(true);
        object? sdkRange = null, httpBody = null;
        long actorDecode = 0, stateDecode = 0, provider = 0;
        int stateCharacters = 0, statePoolBytes = 0, actorBodyBytes = 0;
        fixture.EventWorkingObserver = (phase, roots) =>
        {
            if (phase == "source_page")
            {
                var page = (Hexalith.EventStore.Server.Actors.SourceEventRange)roots[0]!;
                var actor = ProjectionSourceDecodeWorkingMemory.ActorResponse(page, retained.Events);
                sdkRange = actor.Decoded;
                httpBody = actor.Body;
                actorBodyBytes = Math.Max(actorBodyBytes, actor.Body.Length);
                actorDecode = Math.Max(actorDecode, actor.Bytes);
                var state = ProjectionSourceDecodeWorkingMemory.StateJson(page, retained.Events);
                stateCharacters = Math.Max(stateCharacters, state.JsonCharacters);
                statePoolBytes = Math.Max(statePoolBytes, state.PoolBytes);
                stateDecode = Math.Max(stateDecode, state.Bytes);
            }
            else provider = Math.Max(provider, ProjectionRetainedGraphSize.Measure(retained.Events, roots, sdkRange, httpBody) + fixture.ProviderNativeBytes);
        };
        ProjectionSourceGuardReplayAdmission shared = await fixture.AdmitCanonicalRouteAsync(
            fixture.Catalog.OrdinaryProjectionTypes[1], retained.Events).ConfigureAwait(true);
        shared.Events.ShouldBeSameAs(retained.Events);
        const long ceiling = 69_206_016;
        actorDecode.ShouldBeGreaterThan(0);
        stateDecode.ShouldBeGreaterThan(0);
        provider.ShouldBeGreaterThan(0);
        actorDecode.ShouldBeLessThanOrEqualTo(ceiling);
        stateDecode.ShouldBeLessThanOrEqualTo(ceiling);
        provider.ShouldBeLessThanOrEqualTo(ceiling);
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionSourceDecodeWorkingRoots " + JsonSerializer.Serialize(new
        { head = 48, checkpoint = 1, routes = 2, mixedClear, actorBodyBytes, stateCharacters, statePoolBytes,
            actorDecodeRootBytes = actorDecode, stateDecodeRootBytes = stateDecode, providerRootBytes = provider,
            nativeDecoderBufferBytes = fixture.ProviderNativeBytes, ceiling,
            coverage = "installed default DataContract serializer actual HTTP/copy arrays plus conservative full decoded range; concrete json.Length*3 pool capacity with UTF16 source and decoded envelope; retained shared prefix in every phase" }));
    }

    [Fact]
    public async Task SharedProtectedCanonicalAdmissionCountsRetainedPrefixProviderCopiesAndActualWireSendBuffer()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(48, 1, 2,
            "full-replay-v1").ConfigureAwait(true);
        fixture.SetProtectedTailPayloadSizes(1, Enumerable.Repeat(1_048_576, 47).ToArray());
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        ProjectionSourceGuardReplayAdmission? retained = null;
        long providerBytes = 0, sourceBytes = 0, writerBytes = 0;
        fixture.EventWorkingObserver = (phase, roots) =>
        {
            long bytes = ProjectionRetainedGraphSize.Measure(retained?.Events, roots);
            if (phase == "provider_return") providerBytes = Math.Max(providerBytes, bytes + fixture.ProviderNativeBytes);
            else sourceBytes = Math.Max(sourceBytes, bytes);
        };
        string[] routes = [.. fixture.Catalog.OrdinaryProjectionTypes];
        retained = await fixture.AdmitCanonicalRouteAsync(routes[0]).ConfigureAwait(true);
        ProjectionSourceGuardReplayAdmission shared = await fixture.AdmitCanonicalRouteAsync(routes[1], retained.Events).ConfigureAwait(true);
        shared.Events.ShouldBeSameAs(retained.Events);
        long prefixBytes = ProjectionRetainedGraphSize.Measure(retained.Events);
        int sendArrayBytes = 0;
        using var send = new ObservedGrpcWriteStream(memory =>
        {
            MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> actual).ShouldBeTrue();
            sendArrayBytes = Math.Max(sendArrayBytes, actual.Array!.Length);
            writerBytes = Math.Max(writerBytes, ProjectionRetainedGraphSize.Measure(retained.Events, actual.Array));
        });
        await ProjectionWireJsonSerializer.SerializeAsync(send, retained.Events, JsonSerializerOptions.Web,
            CancellationToken.None).ConfigureAwait(true);
        const long ceiling = 69_206_016;
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionEventWorkingRoots " + JsonSerializer.Serialize(new
        { head = 48, checkpoint = 1, routes = 2, pageSize = 256, protectedPayloadBytes = 1_048_576,
            sourcePages = fixture.Ranges.Count, wireBytes = retained.Tail.WireBytes, prefixBytes,
            providerRootBytes = providerBytes, sourceRootBytes = sourceBytes, writerRootBytes = writerBytes,
            sendArrayBytes, ceiling,
            coverage = "unique managed retained prefix plus actual owning range, provider decrypt/plaintext/MemoryStream arrays and bounded wire writer send array by phase" }));
        retained.Events.Sum(static item => (long)item.Payload.Length).ShouldBe(47L * 1_048_576 + 2);
        retained.Tail.SourceBytes.ShouldBeLessThan(2_097_152);
        fixture.Ranges.Count.ShouldBe(2);
        fixture.ProtectedReads.ShouldBe(94);
        providerBytes.ShouldBeGreaterThan(prefixBytes);
        sourceBytes.ShouldBeGreaterThan(prefixBytes);
        writerBytes.ShouldBeGreaterThan(prefixBytes);
        providerBytes.ShouldBeLessThanOrEqualTo(ceiling);
        sourceBytes.ShouldBeLessThanOrEqualTo(ceiling);
        writerBytes.ShouldBeLessThanOrEqualTo(ceiling);
        fixture.Handlers.ShouldAllBe(static handler => handler.Calls == 0 && handler.RebuildCalls == 0);
    }
}

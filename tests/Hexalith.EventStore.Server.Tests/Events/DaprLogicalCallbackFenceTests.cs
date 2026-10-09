using System.Reflection;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Server.Events;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Exercises individual callable fences through the real addressed source and canonical replay owner.</summary>
public sealed class DaprLogicalCallbackFenceTests
{
    /// <summary>Gets all individually executed application boundaries and independently lost authorities.</summary>
    public static IEnumerable<object[]> LossCases()
    {
        foreach (string scope in new[] { "source", "output", "current" })
        {
            string[] stages = scope == "source" ? ["schema-options", "schema-1", "identity-options", "identity-1", "upcast"]
                : scope == "output" ? ["schema-options", "schema-2", "identity-options", "identity-2"]
                : ["schema-options", "schema-2", "identity-options", "identity-2", "deserialize-options", "deserialize"];
            foreach (string stage in stages)
            {
                foreach (string authority in new[] { "head", "key", "state-binding", "registry", "cancellation", "cancellation-throw" })
                {
                    yield return [stage, authority, scope];
                }
            }
        }
    }

    /// <summary>Checks a lost individual callback admits no later application or durable work.</summary>
    [Theory]
    [MemberData(nameof(LossCases))]
    public async Task CallableAuthorityLossStopsLaterCallbacksApplySaveAndProof(string stage, string authority, string scope)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(authority);
        await using var fixture = new DaprLogicalReconstructionFixture(events: 1, mixedHistory: true);
        using var cancellation = new CancellationTokenSource();
        fixture.Source.CallbackToken = cancellation.Token;
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", null, cancellation.Token);
        using var begin = await fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust, "owner", null, cancellation.Token);
        begin.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        int saves = fixture.Store.Saves;
        int initialReads = fixture.Reads;
        EventBufferBudget budget = (EventBufferBudget)typeof(DaprReplayOperationOwner).GetField("_bufferBudget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Owner)!;
        int initialBytes = budget.LiveBytes;
        var durable = fixture.Store.Durable.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        int stoppedCalls = 0;
        var privateArrays = new List<byte[]>();
        fixture.Source.CallbackHook = (point, token) =>
        {
            token.ShouldBe(cancellation.Token);
            foreach (IReadOnlyPayload payload in fixture.Source.Borrowed) { CapturePayloadArray(payload, privateArrays); }
            bool current = fixture.Reads > initialReads;
            bool output = fixture.Source.Callbacks.Contains("upcast") && point != "upcast";
            if (point != stage || (scope == "current" ? !current : current || (scope == "output" ? !output : output))) { return; }
            stoppedCalls = fixture.Source.Callbacks.Count;
            switch (authority)
            {
                case "head": fixture.Source.Metadata = fixture.Source.Metadata! with { CurrentSequence = 2 }; break;
                case "key": fixture.Source.Trust.Dispose(); break;
                case "state-binding":
                    byte[] pin = (byte[])typeof(RegisteredLogicalReplayBinding).GetField("_stateAssemblyHash", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Binding)!;
                    pin[0] ^= 1;
                    break;
                case "registry": fixture.Source.Registry.Dispose(); break;
                case "cancellation":
                case "cancellation-throw":
                    cancellation.Cancel(); fixture.Source.Trust.Dispose(); fixture.Source.Registry.Dispose();
                    if (authority == "cancellation-throw") { throw new IOException("callback-failure-after-original-cancellation"); }
                    break;
            }
        };
        fixture.Source.OptionsHook = _ => stoppedCalls > 0 ? new byte[] { 0xff } : "{}\n"u8.ToArray();

        Exception failure = await Should.ThrowAsync<Exception>(async () =>
        {
            using DaprReplayOperationResult result = await fixture.Owner.ExecutePageAsync(fixture.Source.Source, binding, fixture.Source.Trust,
                fixture.Source.Key, "owner", begin.Generation, 1, "request", 1, cancellation.Token);
        });

        if (authority.StartsWith("cancellation", StringComparison.Ordinal)) { failure.ShouldBeAssignableTo<OperationCanceledException>().CancellationToken.ShouldBe(cancellation.Token); }
        else if (authority == "key") { (failure is ObjectDisposedException || failure is InvalidOperationException && failure.Message.Contains("CapabilityMismatch", StringComparison.Ordinal)).ShouldBeTrue(); }
        else if (authority == "registry") { failure.ShouldBeOfType<ObjectDisposedException>(); }
        else { failure.ShouldBeOfType<InvalidOperationException>().Message.ShouldContain(authority == "head" ? "SourceHeadChanged" : "CapabilityMismatch"); }
        stoppedCalls.ShouldBeGreaterThan(0);
        fixture.Source.Callbacks.Count.ShouldBe(stoppedCalls);
        fixture.Applies.ShouldBe(0);
        fixture.Store.Saves.ShouldBe(saves);
        budget.LiveBytes.ShouldBe(initialBytes);
        fixture.Store.Durable.Keys.Order().ShouldBe(durable.Keys.Order());
        foreach ((string key, byte[] bytes) in durable) { fixture.Store.Durable[key].ShouldBe(bytes); }
        fixture.Store.Durable.ContainsKey("logical-replay:final:v1").ShouldBeFalse();
        fixture.Source.Borrowed.ForEach(payload => Should.Throw<ObjectDisposedException>(() => payload.CopyTo(0, new byte[1])));
        privateArrays.ForEach(bytes => bytes.ShouldAllBe(value => value == 0));
        if (!stage.EndsWith("options", StringComparison.Ordinal) || scope != "source") { privateArrays.ShouldNotBeEmpty(); }
    }

    /// <summary>Provides finite representative source, output and current-intake refusal controls for isolated mutations.</summary>
    [Fact]
    public async Task RepresentativeCallbackAuthorityLossControls()
    {
        await CallableAuthorityLossStopsLaterCallbacksApplySaveAndProof("schema-options", "head", "source");
        await CallableAuthorityLossStopsLaterCallbacksApplySaveAndProof("schema-2", "key", "output");
        await CallableAuthorityLossStopsLaterCallbacksApplySaveAndProof("identity-options", "head", "current");
        await CallableAuthorityLossStopsLaterCallbacksApplySaveAndProof("deserialize-options", "head", "current");
        await CallableAuthorityLossStopsLaterCallbacksApplySaveAndProof("schema-1", "state-binding", "source");
    }

    /// <summary>Checks every asynchronous source observation sees expired facades and canonical final truth.</summary>
    [Fact]
    public async Task YieldingAddressedFenceSeesExpiredAllCallbackFacadesAndExactFinalState()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(events: 1, mixedHistory: true);
        int observed = 0;
        var privateArrays = new List<byte[]>();
        fixture.Source.CallbackHook = (_, _) =>
        {
            foreach (IReadOnlyPayload payload in fixture.Source.Borrowed) { CapturePayloadArray(payload, privateArrays); }
        };
        _ = fixture.Source.SourceState.TryGetStateAsync<AggregateMetadata>(DaprLogicalReplayFixture.Identity.MetadataKey,
            Arg.Any<CancellationToken>()).Returns(async call =>
            {
                await Task.Yield();
                call.Arg<CancellationToken>().ThrowIfCancellationRequested();
                fixture.Source.Borrowed.ForEach(payload => Should.Throw<ObjectDisposedException>(() => payload.CopyTo(0, new byte[1])));
                fixture.Source.BorrowedWriters.ForEach(writer => Should.Throw<ObjectDisposedException>(() => writer.Complete()));
                // A late scratch attempt is sticky misuse; inspect expiry without invoking it here.
                fixture.Source.BorrowedScratch.ForEach(scratch => ((bool)typeof(BoundedScratchAllocator)
                    .GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scratch)!).ShouldBeTrue());
                if (fixture.Source.Borrowed.Count > 0) { observed++; }
                return new ConditionalValue<AggregateMetadata>(true, fixture.Source.Metadata!);
            });
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust, "owner", null, CancellationToken.None);
        using var result = await fixture.Owner.ExecutePageAsync(fixture.Source.Source, binding, fixture.Source.Trust,
            fixture.Source.Key, "owner", begin.Generation, 1, "request", 1, CancellationToken.None);

        observed.ShouldBeGreaterThan(0);
        result.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        result.IsComplete.ShouldBeTrue();
        fixture.Applies.ShouldBe(1);
        fixture.Source.Callbacks.ShouldBe(["schema-options", "schema-1", "identity-options", "identity-1", "upcast",
            "schema-options", "schema-2", "identity-options", "identity-2",
            "schema-options", "schema-2", "identity-options", "identity-2", "deserialize-options", "deserialize"]);
        fixture.Store.Get<byte[]>("logical-replay:final-state:v1").ShouldBe("{\"value\":13}"u8.ToArray());
        privateArrays.ShouldNotBeEmpty();
        privateArrays.ForEach(bytes => bytes.ShouldAllBe(value => value == 0));
    }

    private static void CapturePayloadArray(IReadOnlyPayload payload, List<byte[]> arrays)
    {
        if (payload is ImmutablePayload)
        {
            byte[]? bytes = (byte[]?)typeof(ImmutablePayload).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(payload);
            if (bytes is not null && !arrays.Contains(bytes)) { arrays.Add(bytes); }
            return;
        }
        foreach (FieldInfo field in payload.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (field.GetValue(payload) is IReadOnlyPayload inner) { CapturePayloadArray(inner, arrays); }
        }
    }
}

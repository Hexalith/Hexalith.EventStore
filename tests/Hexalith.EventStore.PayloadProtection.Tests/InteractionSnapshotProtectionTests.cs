using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Exercises actual snapshot ciphertext and original recovery with synthetic custody, never installed qualification.</summary>
public sealed class InteractionSnapshotProtectionTests
{
    private const string Target = """{"TenantId":"tenant-a","AgentInteractionId":"interaction-a","TargetProtectionKeyAlias":"alias-a"}""";
    private const string Envelope = """{"Target":TARGET,"Value":{"Secret":"snapshot-canary"}}""";
    private static JsonTypeInfo StateType => JsonSerializerOptions.Default.GetTypeInfo(typeof(JsonElement));

    /// <summary>Nested content uses its explicit interaction root, retaining the same complete encrypted original on retry.</summary>
    [Theory]
    [InlineData("{\"ProtectedContent\":ENVELOPE}")]
    [InlineData("{\"Nested\":[{\"Request\":ENVELOPE},{\"Output\":ENVELOPE}]}")]
    [InlineData("[{\"Requests\":[ENVELOPE]}]")]
    [InlineData("{\"Owner\":TARGET,\"Items\":[ENVELOPE]}")]
    public async Task SameTargetSnapshotRoundtripRetainsOriginalCiphertextAndCompletion(string template)
    {
        var fixture = Create();
        JsonElement state = State(template);
        PayloadProtectionOccurrenceContext occurrence = Occurrence(fixture.Binding);
        SnapshotProtectionWriteResult write = await fixture.Service.ProtectSnapshotAsync(fixture.Binding.Identity.Owner,
            state, StateType, occurrence, TestContext.Current.CancellationToken);
        var snapshot = write.ProtectionResult.State.ShouldBeOfType<ProtectedSnapshotPayloadV2>();
        JsonSerializer.Serialize(snapshot).ShouldNotContain("snapshot-canary");
        Encoding.UTF8.GetString(fixture.Occurrences.Record!.Sealed!.PayloadBytes).ShouldNotContain("snapshot-canary");
        fixture.Occurrences.Record.Request.Identity.Target.ShouldBe(fixture.Binding.Identity.Target);
        fixture.Occurrences.Record.Request.Identity.Owner.AggregateId.ShouldBe("directory");
        fixture.Occurrences.Record.WriterState.ShouldBe(InteractionOccurrenceWriterState.SealedPending);
        (await fixture.Service.TryUnprotectSnapshotAsync(fixture.Binding.Identity.Owner, snapshot,
            write.ProtectionResult.Metadata, StateType, occurrence, TestContext.Current.CancellationToken)).IsUnreadable.ShouldBeTrue();

        SnapshotProtectionWriteResult retry = await fixture.Service.ProtectSnapshotAsync(fixture.Binding.Identity.Owner,
            state, StateType, occurrence, TestContext.Current.CancellationToken);
        retry.ProtectionResult.State.ShouldBe(snapshot);
        retry.CompletionContext.ShouldBe(write.CompletionContext);
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
        fixture.Occurrences.Keys.WriteRoots.ShouldBe(1);
        await fixture.Bindings.Received(2).ResolveAsync(fixture.Binding.Identity.Owner, occurrence, fixture.Binding.Identity.Target,
            true, Arg.Any<CancellationToken>());
        await fixture.Service.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, TestContext.Current.CancellationToken);
        await fixture.Service.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted,
            TestContext.Current.CancellationToken);
        SnapshotUnprotectionOutcome readable = await fixture.Service.TryUnprotectSnapshotAsync(fixture.Binding.Identity.Owner,
            snapshot, write.ProtectionResult.Metadata, StateType, occurrence, TestContext.Current.CancellationToken);
        readable.IsReadable.ShouldBeTrue();
        readable.State.ShouldBeOfType<JsonElement>().GetRawText().ShouldBe(state.GetRawText());
        fixture.Occurrences.Record.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        fixture.Occurrences.OwnedBuffers.ShouldNotBeEmpty();
        fixture.Occurrences.OwnedBuffers.All(static bytes => bytes.All(static value => value == 0)).ShouldBeTrue();
    }

    /// <summary>Another interaction, alias or tenant cannot be placed under the first root, even in nested content values.</summary>
    [Theory]
    [InlineData("interaction-a", "interaction-b", false)]
    [InlineData("alias-a", "alias-b", false)]
    [InlineData("tenant-a", "tenant-b", false)]
    [InlineData("interaction-a", "interaction-b", true)]
    [InlineData("alias-a", "alias-b", true)]
    [InlineData("tenant-a", "tenant-b", true)]
    public async Task MixedTargetsDenyBeforeAnyAuthorityOrKeyCall(string before, string after, bool insideValue)
    {
        var fixture = Create();
        string second = Expand(insideValue ? "TARGET" : "ENVELOPE").Replace(before, after, StringComparison.Ordinal);
        string template = insideValue
            ? "{\"Request\":{\"Target\":TARGET,\"Value\":{\"Nested\":[" + second + "]}}}"
            : "{\"Requests\":[ENVELOPE,{\"Nested\":[" + second + "]}]}";
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Service.ProtectSnapshotAsync(fixture.Binding.Identity.Owner,
            State(template), StateType, Occurrence(fixture.Binding), TestContext.Current.CancellationToken));
        fixture.Bindings.ReceivedCalls().ShouldBeEmpty();
        fixture.Occurrences.Registry.ReceivedCalls().ShouldBeEmpty();
        fixture.Occurrences.OwnedBuffers.ShouldBeEmpty();
        fixture.Occurrences.Keys.Leases.ShouldBe(0);
        fixture.Occurrences.Record.ShouldBeNull();
    }

    /// <summary>Even a single foreign-tenant target is rejected before consulting a binding authority.</summary>
    [Fact]
    public async Task ForeignTenantSingleTargetDeniesBeforeBinding()
    {
        var fixture = Create();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Service.ProtectSnapshotAsync(fixture.Binding.Identity.Owner,
            State(Expand("{\"Request\":ENVELOPE}").Replace("tenant-a", "tenant-b", StringComparison.Ordinal)), StateType,
            Occurrence(fixture.Binding), TestContext.Current.CancellationToken));
        fixture.Bindings.ReceivedCalls().ShouldBeEmpty();
        fixture.Occurrences.Registry.ReceivedCalls().ShouldBeEmpty();
        fixture.Occurrences.OwnedBuffers.ShouldBeEmpty();
    }

    /// <summary>Reserved envelopes and targets must retain their exact closed schema and unambiguous decoded members.</summary>
    [Theory]
    [InlineData("{\"ProtectedContent\":null}")]
    [InlineData("{\"nested\":[{\"protectedcontent\":ENVELOPE}]}")]
    [InlineData("{\"ProtectedContent\":{\"Target\":null,\"Value\":\"snapshot-canary\"}}")]
    [InlineData("{\"ProtectedContent\":{\"Target\":[],\"Value\":\"snapshot-canary\"}}")]
    [InlineData("{\"ProtectedContent\":{\"Target\":{\"TenantId\":\"tenant-a\"},\"Value\":\"snapshot-canary\"}}")]
    [InlineData("{\"Request\":{\"Target\":TARGET,\"value\":\"snapshot-canary\"}}")]
    [InlineData("{\"ProtectedContent\":{\"Target\":TARGET}}")]
    [InlineData("{\"Request\":{\"Target\":TARGET,\"Value\":null}}")]
    [InlineData("{\"Request\":{\"Target\":TARGET,\"Value\":1,\"Other\":2}}")]
    [InlineData("{\"Request\":{\"Target\":TARGET,\"Value\":1,\"Value\":2}}")]
    [InlineData("{\"Request\":{\"Target\":TARGET,\"Value\":1,\"Val\\u0075e\":2}}")]
    [InlineData("{\"TargetProtectionKeyAlias\":\"alias-a\",\"TenantId\":\"tenant-a\",\"AgentInteractionId\":true}")]
    [InlineData("{\"TargetProtectionKeyAlias\":\"alias-a\",\"TenantId\":\"tenant-a\",\"AgentInteractionId\":\"interaction-a\",\"TenantId\":\"tenant-b\"}")]
    [InlineData("{\"TargetProtectionKeyAlias\":\" \",\"TenantId\":\"tenant-a\",\"AgentInteractionId\":\"interaction-a\"}")]
    [InlineData("{\"nested\":[{\"$pdenc\":\"opaque\"}]}")]
    [InlineData("{\"nested\":[{\"$enc\":\"opaque\"}]}")]
    public async Task MalformedReservedShapesDenyBeforeAnyAuthorityOrKeyCall(string json)
    {
        var fixture = Create();
        await Should.ThrowAsync<Exception>(() => fixture.Service.ProtectSnapshotAsync(fixture.Binding.Identity.Owner,
            State(json), StateType, Occurrence(fixture.Binding), TestContext.Current.CancellationToken));
        fixture.Bindings.ReceivedCalls().ShouldBeEmpty();
        fixture.Occurrences.Registry.ReceivedCalls().ShouldBeEmpty();
        fixture.Occurrences.OwnedBuffers.ShouldBeEmpty();
        fixture.Occurrences.Keys.Leases.ShouldBe(0);
        fixture.Occurrences.Record.ShouldBeNull();
    }

    /// <summary>Explicit content cannot enroll itself or replace an independently retained different interaction root.</summary>
    [Fact]
    public async Task SingleTargetCannotOverrideRetainedEnrollment()
    {
        var fixture = Create();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Service.ProtectSnapshotAsync(fixture.Binding.Identity.Owner,
            State(Expand("{\"Request\":ENVELOPE}").Replace("alias-a", "alias-b", StringComparison.Ordinal)), StateType,
            Occurrence(fixture.Binding), TestContext.Current.CancellationToken));
        await fixture.Bindings.Received(1).ResolveAsync(fixture.Binding.Identity.Owner, Occurrence(fixture.Binding),
            new ProtectionTarget("tenant-a", "interaction-a", "alias-b"), true, Arg.Any<CancellationToken>());
        fixture.Occurrences.Registry.ReceivedCalls().ShouldBeEmpty();
        fixture.Occurrences.OwnedBuffers.ShouldBeEmpty();
    }

    /// <summary>Ordinary snapshot fields, including unrelated Target/Value members, retain their enrolled single root.</summary>
    [Theory]
    [InlineData("{\"Target\":\"queue-a\",\"Value\":5}")]
    [InlineData("{\"Target\":null,\"Value\":null}")]
    [InlineData("{\"Target\":{\"Name\":\"queue-a\"},\"Value\":{\"Payload\":2}}")]
    [InlineData("{\"Secret\":\"snapshot-canary\"}")]
    [InlineData("{}")]
    public async Task StateWithoutProtectionTargetsKeepsSingleRootCompatibility(string json)
    {
        var fixture = Create();
        JsonElement state = State(json);
        SnapshotProtectionWriteResult write = await fixture.Service.ProtectSnapshotAsync(fixture.Binding.Identity.Owner,
            state, StateType, Occurrence(fixture.Binding), TestContext.Current.CancellationToken);
        await fixture.Bindings.Received(1).ResolveAsync(fixture.Binding.Identity.Owner, Occurrence(fixture.Binding), null,
            true, Arg.Any<CancellationToken>());
        await fixture.Service.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, TestContext.Current.CancellationToken);
        await fixture.Service.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted,
            TestContext.Current.CancellationToken);
        SnapshotUnprotectionOutcome result = await fixture.Service.TryUnprotectSnapshotAsync(fixture.Binding.Identity.Owner,
            write.ProtectionResult.State, write.ProtectionResult.Metadata, StateType, Occurrence(fixture.Binding), TestContext.Current.CancellationToken);
        result.IsReadable.ShouldBeTrue();
        result.State.ShouldBeOfType<JsonElement>().GetRawText().ShouldBe(state.GetRawText());
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
    }

    /// <summary>Historical authenticated ciphertext under the wrong retained root must never return readable state.</summary>
    [Theory]
    [InlineData("interaction-a", "interaction-b")]
    [InlineData("alias-a", "alias-b")]
    [InlineData("tenant-a", "tenant-b")]
    public async Task HistoricalCiphertextWithForeignExplicitTargetIsUnreadable(string before, string after)
    {
        var fixture = Create();
        byte[] plaintext = Encoding.UTF8.GetBytes(Expand("{\"Nested\":[{\"Request\":ENVELOPE}]}" )
            .Replace(before, after, StringComparison.Ordinal));
        InteractionOccurrenceSealedResult original = (await fixture.Occurrences.Adapter.ProtectSnapshotAsync(plaintext,
            fixture.Binding.Identity, fixture.Binding.DigestKeyVersion, 1, fixture.Binding.KeyReference, TestContext.Current.CancellationToken))!;
        await fixture.Occurrences.Adapter.CompleteWriterAsync(fixture.Binding.Identity, fixture.Binding.KeyReference,
            fixture.Binding.OperationId, true, TestContext.Current.CancellationToken);
        using JsonDocument carrier = JsonDocument.Parse(original.PayloadBytes);
        var metadata = new EventStorePayloadProtectionMetadata(PayloadProtectionState.Protected, 1, "hexalith-pdenc-v2",
            fixture.Binding.KeyReference, "application/json", null);
        SnapshotUnprotectionOutcome result = await fixture.Service.TryUnprotectSnapshotAsync(fixture.Binding.Identity.Owner,
            carrier.RootElement, metadata, StateType, Occurrence(fixture.Binding), TestContext.Current.CancellationToken);
        result.IsUnreadable.ShouldBeTrue();
        result.State.ShouldBeNull();
        fixture.Occurrences.Record!.Sealed!.PayloadBytes.ShouldBe(original.PayloadBytes);
        fixture.Occurrences.Record.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        fixture.Occurrences.OwnedBuffers.All(static bytes => bytes.All(static value => value == 0)).ShouldBeTrue();
    }

    /// <summary>Cancellation before serialization starts neither authority nor registry work.</summary>
    [Fact]
    public async Task CancelledSnapshotDoesNotConsultAuthority()
    {
        var fixture = Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => fixture.Service.ProtectSnapshotAsync(
            fixture.Binding.Identity.Owner, State("{\"Request\":ENVELOPE}"), StateType, Occurrence(fixture.Binding), cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
        fixture.Bindings.ReceivedCalls().ShouldBeEmpty();
        fixture.Occurrences.Registry.ReceivedCalls().ShouldBeEmpty();
        fixture.Occurrences.OwnedBuffers.ShouldBeEmpty();
    }

    /// <summary>A binding dependency fault must not turn an explicit caller cancellation into an ordinary unreadable response.</summary>
    [Fact]
    public async Task CancellationDuringSnapshotReadKeepsTheCallerToken()
    {
        var fixture = Create();
        SnapshotProtectionWriteResult write = await fixture.Service.ProtectSnapshotAsync(fixture.Binding.Identity.Owner,
            State("{\"Request\":ENVELOPE}"), StateType, Occurrence(fixture.Binding), TestContext.Current.CancellationToken);
        await fixture.Service.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted,
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        fixture.Bindings.ResolveAsync(Arg.Any<AggregateIdentity>(), Arg.Any<PayloadProtectionOccurrenceContext>(), null,
            false, Arg.Any<CancellationToken>()).Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromException<InteractionProtectionBinding?>(new InvalidOperationException("dependency-unavailable"));
            });
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => fixture.Service.TryUnprotectSnapshotAsync(
            fixture.Binding.Identity.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata, StateType,
            Occurrence(fixture.Binding), cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
    }

    private static string Expand(string template) => template.Replace("ENVELOPE", Envelope, StringComparison.Ordinal).Replace("TARGET", Target, StringComparison.Ordinal);
    private static JsonElement State(string template) => JsonSerializer.Deserialize<JsonElement>(Expand(template));
    private static PayloadProtectionOccurrenceContext Occurrence(InteractionProtectionBinding binding)
        => new(binding.Identity.Sequence, binding.Identity.Kind, binding.Identity.PayloadTypeId);

    private static (InteractionOccurrenceProtectorFixture Occurrences, InteractionProtectionBinding Binding,
        IInteractionProtectionBindingProvider Bindings, InteractionPayloadProtectionService Service) Create()
    {
        var occurrences = new InteractionOccurrenceProtectorFixture();
        InteractionOccurrenceIdentity identity = occurrences.Identity with
        {
            Owner = new AggregateIdentity("tenant-a", "agents", "directory"),
            Kind = PayloadProtectionPayloadKind.Snapshot,
            PayloadTypeId = "hx-snapshot-v1:test",
        };
        var binding = new InteractionProtectionBinding(identity, "digest-v1", 1,
            InteractionOccurrenceProtectorFixture.Reference, InteractionOccurrenceProtectorFixture.Reference, 1);
        var bindings = Substitute.For<IInteractionProtectionBindingProvider>();
        bindings.ResolveAsync(Arg.Any<AggregateIdentity>(), Arg.Any<PayloadProtectionOccurrenceContext>(), Arg.Any<ProtectionTarget?>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(binding);
        bindings.AcquireCompletionLeaseAsync(binding, Arg.Any<CancellationToken>()).Returns(true);
        return (occurrences, binding, bindings, new(occurrences.Registry, occurrences.Keys, bindings, TimeProvider.System));
    }
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Security;

using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Authenticates retained mixed-root ciphertext and checks historical ownership before returning readable state.</summary>
public sealed class InteractionMixedSnapshotProtectionTests
{
    /// <summary>Materialization cancellation cannot expose the reconstructed state or change retained ciphertext.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringMixedSnapshotMaterializationKeepsTheCallerToken(bool replay)
    {
        var fixture = new InteractionMixedSnapshotFixture();
        var write = await fixture.Service.ProtectSnapshotAsync(fixture.Owner, fixture.Source, fixture.StateType, fixture.Occurrence,
            TestContext.Current.CancellationToken);
        await fixture.Service.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted,
            TestContext.Current.CancellationToken);
        byte[][] originals = fixture.Records.Select(record => record.Sealed!.PayloadBytes.ToArray()).ToArray();
        using var cancellation = new CancellationTokenSource();
        var options = new JsonSerializerOptions(JsonSerializerOptions.Default);
        options.Converters.Add(new CancellingMixedSnapshotConverter(cancellation));
        var stateType = options.GetTypeInfo(typeof(JsonElement));

        var error = await Should.ThrowAsync<OperationCanceledException>(() => replay
            ? fixture.Service.TryReplaySnapshotAsync(fixture.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata,
                stateType, fixture.Occurrence, cancellation.Token)
            : fixture.Service.TryUnprotectSnapshotAsync(fixture.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata,
                stateType, fixture.Occurrence, cancellation.Token));

        error.CancellationToken.ShouldBe(cancellation.Token);
        for (int index = 0; index < originals.Length; index++)
        {
            fixture.Records[index].Sealed!.PayloadBytes.ShouldBe(originals[index]);
            fixture.Records[index].WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        }
        fixture.OwnedBuffers.ShouldNotBeEmpty();
        fixture.OwnedBuffers.All(bytes => bytes.All(value => value == 0)).ShouldBeTrue();
    }

    /// <summary>The actual engine preserves original per-root ciphertext, completion and nested array state across retry.</summary>
    [Fact]
    public async Task MixedSnapshotRoundtripAndRetryKeepBothOriginalCiphertexts()
    {
        var fixture = new InteractionMixedSnapshotFixture();
        var write = await fixture.Service.ProtectSnapshotAsync(fixture.Owner, fixture.Source, fixture.StateType, fixture.Occurrence, TestContext.Current.CancellationToken);
        byte[][] originals = fixture.Records.Select(record => record.Sealed!.PayloadBytes.ToArray()).ToArray();
        Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(write.ProtectionResult.State)).ShouldNotContain("secret-");
        var retry = await fixture.Service.ProtectSnapshotAsync(fixture.Owner, fixture.Source, fixture.StateType, fixture.Occurrence, TestContext.Current.CancellationToken);
        JsonSerializer.SerializeToUtf8Bytes(retry.ProtectionResult.State).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(write.ProtectionResult.State));
        JsonSerializer.SerializeToUtf8Bytes(retry.CompletionContext).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(write.CompletionContext));
        await fixture.Service.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, TestContext.Current.CancellationToken);
        await fixture.Service.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, TestContext.Current.CancellationToken);
        var opened = await fixture.Service.TryUnprotectSnapshotAsync(fixture.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata,
            fixture.StateType, fixture.Occurrence, TestContext.Current.CancellationToken);
        opened.IsReadable.ShouldBeTrue();
        JsonNode.DeepEquals(JsonSerializer.SerializeToNode(opened.State), JsonSerializer.SerializeToNode(fixture.Source)).ShouldBeTrue();
        for (int index = 0; index < originals.Length; index++)
        {
            fixture.Records[index].Sealed!.PayloadBytes.ShouldBe(originals[index]);
            fixture.Records[index].WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        }
        fixture.OwnedBuffers.ShouldNotBeEmpty();
        fixture.OwnedBuffers.All(bytes => bytes.All(value => value == 0)).ShouldBeTrue();
    }

    /// <summary>Trusted partial replay preserves the surviving root while external reads stay unreadable and original ciphertext stays unchanged.</summary>
    [Fact]
    public async Task MixedSnapshotWithOneErasedRootReplaysOnlyThatValueAsErased()
    {
        var fixture = new InteractionMixedSnapshotFixture();
        var write = await fixture.Service.ProtectSnapshotAsync(fixture.Owner, fixture.Source, fixture.StateType, fixture.Occurrence, TestContext.Current.CancellationToken);
        await fixture.Service.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, TestContext.Current.CancellationToken);
        var carrier = write.ProtectionResult.State.ShouldBeOfType<ProtectedSnapshotEntriesV2>();
        byte[] stored = JsonSerializer.SerializeToUtf8Bytes(carrier);
        byte[][] originals = fixture.Records.Select(record => record.Sealed!.PayloadBytes.ToArray()).ToArray();
        var erased = fixture.Bindings[0];
        fixture.Authority.ResolveErasedStoredAsync(fixture.Owner, InteractionSnapshotEntries.EntryOccurrence(fixture.Occurrence, carrier.Entries[0].Path),
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(originals[0])), erased.KeyReference, Arg.Any<CancellationToken>()).Returns(erased);
        fixture.Authority.IsIrreversiblyErasedAsync(erased, Arg.Any<CancellationToken>()).Returns(true);
        using var cached = JsonDocument.Parse(stored);
        (await fixture.Service.TryUnprotectSnapshotAsync(fixture.Owner, cached.RootElement, write.ProtectionResult.Metadata,
            fixture.StateType, fixture.Occurrence, TestContext.Current.CancellationToken)).IsUnreadable.ShouldBeTrue();
        var replay = await fixture.Service.TryReplaySnapshotAsync(fixture.Owner, cached.RootElement, write.ProtectionResult.Metadata,
            fixture.StateType, fixture.Occurrence, TestContext.Current.CancellationToken);
        replay.IsReadable.ShouldBeTrue();
        var restored = replay.State.ShouldBeOfType<JsonElement>();
        restored.GetProperty("Safe").GetInt32().ShouldBe(7);
        restored.GetProperty("Entries")[0].GetProperty("ProtectedContent").GetProperty("Value").GetProperty("State").GetString().ShouldBe("Erased");
        restored.GetProperty("Entries")[1].GetProperty("ProtectedContent").GetProperty("Value").GetProperty("Secret").GetString().ShouldBe("secret-b");
        JsonSerializer.SerializeToUtf8Bytes(carrier).ShouldBe(stored);
        for (int index = 0; index < originals.Length; index++)
        {
            fixture.Records[index].Sealed!.PayloadBytes.ShouldBe(originals[index]);
            fixture.Records[index].WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        }
        fixture.OwnedBuffers.All(bytes => bytes.All(value => value == 0)).ShouldBeTrue();
    }

    /// <summary>Unrelated Target/Value domain members inside an authenticated protected value remain compatible.</summary>
    [Theory]
    [InlineData("{\"Target\":\"queue-a\",\"Value\":5}")]
    [InlineData("{\"Nested\":[{\"Target\":{\"Name\":\"queue-a\"},\"Value\":null}]}")]
    public async Task HistoricalMixedSnapshotWithOrdinaryDomainValueRemainsReadable(string value)
    {
        var fixture = new InteractionMixedSnapshotFixture();
        var expected = JsonSerializer.Deserialize<JsonElement>(value);
        var write = await fixture.HistoricalAsync(expected, TestContext.Current.CancellationToken);
        var opened = await fixture.Service.TryUnprotectSnapshotAsync(fixture.Owner, write.State, write.Metadata, fixture.StateType,
            fixture.Occurrence, TestContext.Current.CancellationToken);
        opened.IsReadable.ShouldBeTrue();
        var actual = opened.State.ShouldBeOfType<JsonElement>().GetProperty("Entries")[0].GetProperty("ProtectedContent").GetProperty("Value");
        JsonNode.DeepEquals(JsonSerializer.SerializeToNode(actual), JsonSerializer.SerializeToNode(expected)).ShouldBeTrue();
    }

    /// <summary>Historical authentic fragments cannot hide foreign ownership, reserved ciphertext or nested protected envelopes under another interaction root.</summary>
    [Theory]
    [InlineData("{\"Nested\":[{\"TenantId\":\"tenant-a\",\"AgentInteractionId\":\"interaction-b\",\"TargetProtectionKeyAlias\":\"alias-a\"}]}")]
    [InlineData("{\"Nested\":[{\"TenantId\":\"tenant-a\",\"AgentInteractionId\":\"interaction-a\",\"TargetProtectionKeyAlias\":\"alias-b\"}]}")]
    [InlineData("{\"Nested\":[{\"TenantId\":\"tenant-b\",\"AgentInteractionId\":\"interaction-a\",\"TargetProtectionKeyAlias\":\"alias-a\"}]}")]
    [InlineData("{\"ProtectedContent\":{\"Target\":{\"TenantId\":\"tenant-a\",\"AgentInteractionId\":\"interaction-b\",\"TargetProtectionKeyAlias\":\"alias-b\"},\"Value\":\"foreign-secret\"}}")]
    [InlineData("{\"protectedcontent\":null}")]
    [InlineData("{\"$enc\":\"opaque\"}")]
    public async Task HistoricalMixedSnapshotWithInvalidDecryptedOwnershipIsUnreadable(string value)
    {
        var fixture = new InteractionMixedSnapshotFixture();
        var write = await fixture.HistoricalAsync(JsonSerializer.Deserialize<JsonElement>(value), TestContext.Current.CancellationToken);
        byte[][] originals = fixture.Records.Select(record => record.Sealed!.PayloadBytes.ToArray()).ToArray();
        foreach (bool replay in new[] { false, true })
        {
            var opened = replay
                ? await fixture.Service.TryReplaySnapshotAsync(fixture.Owner, write.State, write.Metadata,
                    fixture.StateType, fixture.Occurrence, TestContext.Current.CancellationToken)
                : await fixture.Service.TryUnprotectSnapshotAsync(fixture.Owner, write.State, write.Metadata,
                    fixture.StateType, fixture.Occurrence, TestContext.Current.CancellationToken);
            opened.IsUnreadable.ShouldBeTrue();
            opened.State.ShouldBeNull();
        }
        for (int index = 0; index < originals.Length; index++)
        {
            fixture.Records[index].Sealed!.PayloadBytes.ShouldBe(originals[index]);
            fixture.Records[index].WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        }
        fixture.OwnedBuffers.ShouldNotBeEmpty();
        fixture.OwnedBuffers.All(bytes => bytes.All(value => value == 0)).ShouldBeTrue();
    }
}

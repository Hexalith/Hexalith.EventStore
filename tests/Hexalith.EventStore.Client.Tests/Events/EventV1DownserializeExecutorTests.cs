using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

public sealed class EventV1DownserializeExecutorTests
{
    [Fact]
    public async Task ConvertsExactRegisteredAliasAndValidatesThatOutputBeforeTransfer()
    {
        using EventDomainRegistry registry = CreateRegistry();
        var downserializer = new LeaseRecordingV1Downserializer();
        var budget = new EventBufferBudget();
        bool aliasValidated = false;
        bool semanticsValidated = false;
        var executor = new EventV1DownserializeExecutor(registry,
            static (_, _, version, _, _, _) => version.ShouldBe(2),
            (_, type, alias, version, format, payload, _) =>
            {
                type.ShouldBe("evt"); alias.ShouldBe("Legacy.Event"); version.ShouldBe(1); format.ShouldBe("json");
                byte[] copy = new byte[payload.Length];
                payload.CopyTo(0, copy);
                copy.ShouldBe([1, 2]);
                aliasValidated = true;
            },
            (_, _, _) => semanticsValidated = true);
        using var sourceWriter = new BoundedPayloadWriter(2, CancellationToken.None);
        sourceWriter.Write([1, 2]); sourceWriter.Complete();
        using ImmutablePayload source = sourceWriter.TakeCompletedPayload();
        var binding = new RegisteredV1Downserializer("test-downserializer", downserializer, new byte[32]);

        using ImmutablePayload result = await executor.DownserializeAsync("evt", "Legacy.Event", source, binding,
            2, budget, CancellationToken.None);

        aliasValidated.ShouldBeTrue(); semanticsValidated.ShouldBeTrue();
        byte[] bytes = new byte[2]; result.CopyTo(0, bytes); bytes.ShouldBe([1, 2]);
        source.CopyTo(0, bytes); bytes.ShouldBe([1, 2]);
        Should.Throw<ObjectDisposedException>(() => downserializer.Input!.CopyTo(0, new byte[2]));
        Should.Throw<ObjectDisposedException>(() => downserializer.Scratch!.WithScratch(0, _ => { }, CancellationToken.None));
        result.Dispose(); budget.LiveBytes.ShouldBe(0);
    }

    [Theory]
    [InlineData("wrong-alias")]
    [InlineData("missing-complete")]
    [InlineData("semantic-rejection")]
    [InlineData("alias-schema-rejection")]
    public async Task RefusesFailedOrLossyConversionAndReleasesEveryOwner(string behavior)
    {
        using EventDomainRegistry registry = CreateRegistry();
        var downserializer = new LeaseRecordingV1Downserializer(behavior);
        var budget = new EventBufferBudget();
        var executor = new EventV1DownserializeExecutor(registry, static (_, _, _, _, _, _) => { },
            (_, _, _, _, _, _, _) =>
            {
                if (behavior == "alias-schema-rejection") { throw new InvalidOperationException("DownserializeRejected"); }
            },
            (_, _, _) =>
            {
                if (behavior == "semantic-rejection") { throw new InvalidOperationException("DownserializeRejected"); }
            });
        using var sourceWriter = new BoundedPayloadWriter(2, CancellationToken.None);
        sourceWriter.Write([1, 2]); sourceWriter.Complete();
        using ImmutablePayload source = sourceWriter.TakeCompletedPayload();
        var binding = new RegisteredV1Downserializer("test-downserializer", downserializer, new byte[32]);

        await Should.ThrowAsync<InvalidOperationException>(async () => await executor.DownserializeAsync(
            "evt", "Legacy.Event", source, binding, 2, budget, CancellationToken.None));

        budget.LiveBytes.ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => downserializer.Input!.CopyTo(0, new byte[2]));
    }

    [Fact]
    public void LegacyWriter_KeepsIndependentMeasuredCeilingAboveOneMiB()
    {
        var budget = new EventBufferBudget(2 * 1024 * 1024);
        using var writer = BoundedPayloadWriter.CreateLegacy(1024 * 1024 + 1, CancellationToken.None, budget);
        writer.Write(new byte[1024 * 1024 + 1]); writer.Complete();
        using ImmutablePayload payload = writer.TakeCompletedPayload();
        payload.Length.ShouldBe(1024 * 1024 + 1);
        budget.LiveBytes.ShouldBe(1024 * 1024 + 1);
        payload.Dispose(); budget.LiveBytes.ShouldBe(0);
    }

    private static EventDomainRegistry CreateRegistry()
    {
        using EventDomainRegistry baseRegistry = EventUpcastChainExecutorTests.CreateRegistry();
        List<ReadOnlyMemory<byte>> rows = baseRegistry.Rows.Select(static row => (ReadOnlyMemory<byte>)row.Encoded.ToArray()).ToList();
        using var writer = new EventEvolutionBinaryWriter(1024);
        writer.WriteByte(0x46); writer.WriteString("d"); writer.WriteString("evt"); writer.WriteString("Legacy.Event");
        writer.WriteUInt16(15);
        writer.WriteByte(1); writer.WriteInt32(2);
        writer.WriteByte(2); writer.WriteInt32(1);
        writer.WriteByte(3); writer.WriteBytes("{}"u8);
        writer.WriteByte(4); writer.WriteBytes("{}"u8);
        writer.WriteByte(5); writer.WriteString("json");
        writer.WriteByte(6); writer.WriteString("json");
        writer.WriteByte(7); writer.WriteString("test-downserializer");
        writer.WriteByte(8);
        using (FileStream assembly = File.OpenRead(typeof(LeaseRecordingV1Downserializer).Assembly.Location))
        {
            writer.WriteHash(SHA256.HashData(assembly));
        }

        writer.WriteByte(9); writer.WriteHash(new byte[32]);
        writer.WriteByte(10); writer.WriteString("identity");
        writer.WriteByte(11); writer.WriteHash(new byte[32]);
        writer.WriteByte(12); writer.WriteHash(new byte[32]);
        writer.WriteByte(13); writer.WriteString("semantics");
        writer.WriteByte(14); writer.WriteHash(new byte[32]);
        writer.WriteByte(15); writer.WriteHash(new byte[32]);
        rows.Add(writer.CopyEncodedBytes());
        return new EventDomainRegistry("d", rows);
    }
}

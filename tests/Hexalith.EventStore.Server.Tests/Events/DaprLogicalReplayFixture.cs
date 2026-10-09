using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Events;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Composes actual shared evolution/source/claim/protocol objects with explicit test-local pins.</summary>
internal sealed class DaprLogicalReplayFixture : IDisposable
{
    /// <summary>Creates a local immutable catalog and addressed actor state source.</summary>
    internal DaprLogicalReplayFixture(int events = 1, int payloadBytes = 2, bool mixedHistory = false)
    {
        Registry = CreateRegistry(mixedHistory); Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        EventVersionValidator schema = (_, _, version, _, payload, token) =>
        {
            ValidationCalls++; Borrowed.Add(payload); Callbacks.Add($"schema-{version}");
            CallbackHook?.Invoke($"schema-{version}", token); OnValidation?.Invoke(token);
        };
        EventVersionValidator identity = (_, _, version, _, payload, token) =>
        {
            ValidationCalls++; Borrowed.Add(payload); Callbacks.Add($"identity-{version}");
            CallbackHook?.Invoke($"identity-{version}", token); OnValidation?.Invoke(token);
        };
        ReadOnlyMemory<byte> Options(string name)
        {
            Callbacks.Add(name); CallbackHook?.Invoke(name, CallbackToken);
            return OptionsHook?.Invoke(name) ?? "{}\n"u8.ToArray();
        }
        var validation = new RegisteredEventVersionValidation(Registry, "test-schema", schema, "{}\n"u8.ToArray(), [],
            "test-identity", identity, "{}\n"u8.ToArray(), [], () => Options("schema-options"), () => Options("identity-options"));
        var validations = new Dictionary<(string, int), RegisteredEventVersionValidation> { [("evt", 1)] = validation };
        if (mixedHistory) { validations[("evt", 2)] = validation; }
        var upcasters = new Dictionary<(string, int), RegisteredEventUpcaster>();
        if (mixedHistory) { upcasters[("evt", 1)] = new RegisteredEventUpcaster("test-upcaster", new DaprLogicalReconstructionUpcaster((payload, writer, scratch, token) =>
            {
                Borrowed.Add(payload); BorrowedWriters.Add(writer); BorrowedScratch.Add(scratch);
                Callbacks.Add("upcast"); CallbackHook?.Invoke("upcast", token);
            }), new byte[32]); }
        Service = new EventEvolutionService(Registry, validations, upcasters, new Dictionary<string, RegisteredCurrentEventDeserializer> { ["evt"] = new(typeof(DaprLogicalReplayTestValue), "test-serializer", (payload, token) =>
            {
                DeserializationCalls++; Borrowed.Add(payload); Callbacks.Add("deserialize");
                CallbackHook?.Invoke("deserialize", token); OnDeserialization?.Invoke(token);
                if (!mixedHistory) { return new DaprLogicalReplayTestValue(); }
                Span<byte> image = stackalloc byte[64]; payload.CopyTo(0, image[..payload.Length]);
                var reader = new Utf8JsonReader(image[..payload.Length]); reader.Read(); reader.Read();
                if (!reader.ValueTextEquals("delta")) { throw new JsonException("current event requires delta"); }
                reader.Read(); return new DaprLogicalReplayTestValue { Delta = reader.GetInt32() };
            }, "{}\n"u8.ToArray(), [], () => Options("deserialize-options")) });
        SourceState = Substitute.For<IActorStateManager>();
        Metadata = new AggregateMetadata(events, Timestamp, "etag");
        _ = SourceState.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>()).Returns(call => { call.Arg<CancellationToken>().ThrowIfCancellationRequested(); return new ConditionalValue<AggregateMetadata>(Metadata is not null, Metadata!); });
        for (int sequence = 1; sequence <= events; sequence++)
        {
            byte[] payload = payloadBytes == 2 ? "{}"u8.ToArray() : new byte[payloadBytes];
            var value = new Hexalith.EventStore.Server.Events.EventEnvelope("message", "aggregate", "r", "tenant", "d", sequence, 7, Timestamp,
                "correlation", "cause", "", "1.0", "Legacy.Event", 1, "json", payload,
                new Dictionary<string, string> { ["z"] = "2", ["a"] = "1" });
            if (mixedHistory)
            {
                payload = System.Text.Encoding.UTF8.GetBytes(sequence % 2 == 1 ? "{\"units\":" + sequence + "}" : "{\"delta\":" + sequence * 7 + "}");
                value = value with { Payload = payload };
                if (sequence % 2 == 0)
                {
                    value = value with { EventTypeName = "evt", MetadataVersion = 2, EventContractType = "evt", PayloadVersion = 2 };
                    value = value with { ApplicationPayloadDigest = EventLogicalDigest.Compute(value, "json", SHA256.HashData(payload)) };
                }
            }
            Events[sequence] = value;
            int addressed = sequence;
            _ = SourceState.TryGetStateAsync<Hexalith.EventStore.Server.Events.EventEnvelope>(
                Identity.EventStreamKeyPrefix + sequence, Arg.Any<CancellationToken>()).Returns(call => { EventReads++; call.Arg<CancellationToken>().ThrowIfCancellationRequested(); return new ConditionalValue<Hexalith.EventStore.Server.Events.EventEnvelope>(true, Events[addressed]); });
        }
        Source = new DaprLogicalReplaySource(SourceState, new NoOpEventPayloadProtectionService(), Service, Identity,
            "local-app", "local-ns", "AggregateActor", "local source"u8);
        Trust = NewTrust();
    }

    /// <summary>Gets the exact addressed fixture identity.</summary>
    internal static AggregateIdentity Identity { get; } = new("tenant", "d", "aggregate");
    /// <summary>Gets the exact nonzero original timestamp offset in the independent vectors.</summary>
    internal static DateTimeOffset Timestamp { get; } = DateTimeOffset.UnixEpoch.ToOffset(TimeSpan.FromHours(2));
    /// <summary>Gets the local catalog.</summary>
    internal EventDomainRegistry Registry { get; }
    /// <summary>Gets the real shared evolution service.</summary>
    internal EventEvolutionService Service { get; }
    /// <summary>Gets the actual owning actor API seam.</summary>
    internal IActorStateManager SourceState { get; }
    /// <summary>Gets or sets the returned actor metadata presence/value.</summary>
    internal AggregateMetadata? Metadata { get; set; }
    /// <summary>Gets the addressed source event values.</summary>
    internal Dictionary<int, Hexalith.EventStore.Server.Events.EventEnvelope> Events { get; } = [];
    /// <summary>Gets the composed reader/signer path.</summary>
    internal DaprLogicalReplaySource Source { get; }
    /// <summary>Gets the separate local trust object.</summary>
    internal DaprLogicalClaimTrust Trust { get; }
    /// <summary>Gets the generated test-only key.</summary>
    internal ECDsa Key { get; }
    /// <summary>Gets actual catalog callback count.</summary>
    internal int ValidationCalls { get; private set; }
    /// <summary>Gets actual shared current deserialization count.</summary>
    internal int DeserializationCalls { get; private set; }
    /// <summary>Gets or sets the current event deserializer boundary hook.</summary>
    internal Action<CancellationToken>? OnDeserialization { get; set; }
    /// <summary>Gets actual addressed event read count.</summary>
    internal int EventReads { get; private set; }
    /// <summary>Gets or sets a boundary hook with the original callback token.</summary>
    internal Action<CancellationToken>? OnValidation { get; set; }

    /// <summary>Gets the observed individually invoked application callbacks.</summary>
    internal List<string> Callbacks { get; } = [];
    /// <summary>Gets or sets a boundary hook for composed authority-loss controls.</summary>
    internal Action<string, CancellationToken>? CallbackHook { get; set; }
    /// <summary>Gets or sets options output used to prove refusal precedes parsing.</summary>
    internal Func<string, ReadOnlyMemory<byte>>? OptionsHook { get; set; }
    /// <summary>Gets or sets the original token associated with parameterless options getters.</summary>
    internal CancellationToken CallbackToken { get; set; }
    /// <summary>Gets borrowed application payload facades.</summary>
    internal List<IReadOnlyPayload> Borrowed { get; } = [];
    /// <summary>Gets borrowed upcaster writer facades.</summary>
    internal List<IBoundedPayloadWriter> BorrowedWriters { get; } = [];
    /// <summary>Gets borrowed upcaster scratch facades.</summary>
    internal List<IBoundedScratchAllocator> BorrowedScratch { get; } = [];

    /// <summary>Creates a deliberately explicit model/domain/key/registry/loss-scope test trust.</summary>
    internal DaprLogicalClaimTrust NewTrust(string domain = "d", byte[]? registry = null, EventEvolutionCapabilityLoss? loss = null, string keyId = "local-key", TimeProvider? time = null)
        => new(domain, DaprLogicalSourceBinding.ModelId, keyId, Key.ExportSubjectPublicKeyInfo(), registry ?? Convert.FromHexString(Registry.Fingerprint),
            DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue, loss ?? Registry.CapabilityLoss, time);

    /// <summary>Reads independently encoded expected vectors from the retained model companion.</summary>
    internal static byte[] Vector(string name, string field)
    {
        using JsonDocument values = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root(),
            "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-model-2026-10-08/vectors.json")));
        return Convert.FromHexString(values.RootElement.GetProperty(name).GetProperty(field).GetString()!);
    }
    private static string Root()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx"))) { root = root.Parent; }
        return root!.FullName;
    }
    private static EventDomainRegistry CreateRegistry(bool mixedHistory)
    {
        Dictionary<string, string> fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(Root(),
            "tests/Hexalith.EventStore.Client.Tests/Events/Fixtures/EventRegistryV17.json")))!;
        var rows = new List<ReadOnlyMemory<byte>> { Convert.FromHexString(fixture["AliasRow"]), ReplaceRow(fixture["DescriptorRow"], "UIUHUHIBUHHH", 0x44),
            ReplaceRow(fixture["VersionRow"], "UHHUHHUUHH", 0x56), Convert.FromHexString(fixture["SharedRow"]) };
        if (mixedHistory)
        {
            byte[] descriptor = rows[1].ToArray(); System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(descriptor.AsSpan(22, 4), 2); rows[1] = descriptor;
            byte[] version = rows[2].ToArray(); System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(version.AsSpan(13, 4), 2); rows.Add(version);
            using var edge = new EventEvolutionBinaryWriter(1024);
            edge.WriteByte(0x45); edge.WriteString("d"); edge.WriteString("evt"); edge.WriteInt32(1); edge.WriteUInt16(12);
            edge.WriteByte(1); edge.WriteInt32(2); edge.WriteByte(2); edge.WriteString("json"); edge.WriteByte(3); edge.WriteString("json");
            edge.WriteByte(4); edge.WriteString("serializer"); edge.WriteByte(5); edge.WriteHash(new byte[32]); edge.WriteByte(6); edge.WriteHash(new byte[32]);
            edge.WriteByte(7); edge.WriteString("test-upcaster"); edge.WriteByte(8);
            using FileStream assembly = File.OpenRead(typeof(DaprLogicalReconstructionUpcaster).Assembly.Location); edge.WriteHash(SHA256.HashData(assembly));
            edge.WriteByte(9); edge.WriteHash(new byte[32]); edge.WriteByte(10); edge.WriteString("test-identity");
            edge.WriteByte(11); edge.WriteHash(new byte[32]); edge.WriteByte(12); edge.WriteHash(new byte[32]); rows.Add(edge.CopyEncodedBytes());
        }
        return new EventDomainRegistry("d", rows, capabilityLoss: new EventEvolutionCapabilityLoss());
    }

    private static byte[] ReplaceRow(string hex, string fields, byte tag)
    {
        var reader = new EventEvolutionBinaryReader(Convert.FromHexString(hex));
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(reader.ReadByte()); writer.WriteString(reader.ReadString(64)); writer.WriteString(reader.ReadString(64));
        if (tag == 0x56) { writer.WriteInt32(reader.ReadInt32()); }
        writer.WriteUInt16(reader.ReadUInt16());
        for (int field = 1; field <= fields.Length; field++)
        {
            writer.WriteByte(reader.ReadByte());
            if (fields[field - 1] == 'U')
            {
                string value = reader.ReadString(4096);
                writer.WriteString(tag == 0x44 && field == 3 ? typeof(DaprLogicalReplayTestValue).AssemblyQualifiedName!
                    : tag == 0x56 && field == 4 ? "test-serializer"
                    : tag == 0x56 && field == 1 ? "test-schema"
                    : tag == 0x56 && field == 8 ? "test-identity" : value);
            }
            else if (fields[field - 1] == 'H')
            {
                byte[] value = reader.ReadHash().ToArray();
                if (tag == 0x44 && field == 4 || tag == 0x56 && field is 2 or 5 or 9)
                {
                    using FileStream stream = File.OpenRead(typeof(DaprLogicalReplayTestValue).Assembly.Location);
                    value = SHA256.HashData(stream);
                }
                if (tag == 0x56 && field is 3 or 6 or 10) { value = EventOptionsManifestCodec.ComputeHash("{}\n"u8.ToArray(), []); }
                writer.WriteHash(value);
            }
            else if (fields[field - 1] == 'I') { writer.WriteInt32(reader.ReadInt32()); }
            else { writer.WriteBytes(reader.ReadBytes(4096)); }
        }
        reader.RequireEnd();
        return writer.CopyEncodedBytes();
    }

    /// <inheritdoc/>
    public void Dispose() { Trust.Dispose(); Key.Dispose(); Registry.Dispose(); }
}

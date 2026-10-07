using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Checks dormant wrapper integration with actual private-image registry callbacks.</summary>
public sealed class EventPrivateImageRegistryCallbackTests
{
    /// <summary>Checks exact private-image validators, converters and current deserialization share one registry scope.</summary>
    [Fact]
    public async Task PrivateImageCallbacksExecuteThroughExistingRegistryWrappers()
    {
        var loss = new EventEvolutionCapabilityLoss();
        using EventRegistryRow dependency = CreateDependencyRow();
        using var artifact = new EventManagedArtifact(dependency, typeof(PrivateImageEventCallbacks).Assembly.Location,
            new EventBufferBudget(), loss, CancellationToken.None);
        using EventManagedArtifactExecutionBinding execution = artifact.Load(CancellationToken.None);
        using EventDomainRegistry registry = CreateRegistry(execution, loss);
        RegisteredEventVersionValidation validation = CreateValidation(registry, execution);
        var upcaster = new RegisteredEventUpcaster("test-upcaster", CreateUpcaster(execution), new byte[32], execution);
        var executor = new EventUpcastChainExecutor(registry, new Dictionary<(string, int), RegisteredEventUpcaster>
        {
            [("evt", 1)] = upcaster,
        }, validation.Validate);
        using ImmutablePayload source = CreatePayload();
        using ImmutablePayload effective = await executor.UpcastAsync("evt", 1, source, new EventBufferBudget(), CancellationToken.None);
        var downserializer = new RegisteredV1Downserializer("test-downserializer", CreateDownserializer(execution), new byte[32], execution);
        var downExecutor = new EventV1DownserializeExecutor(registry, validation.Validate,
            static (_, _, _, _, _, _, _) => { }, static (_, _, _) => { });
        using ImmutablePayload legacy = await downExecutor.DownserializeAsync("evt", "Legacy.Event", effective,
            downserializer, 2, new EventBufferBudget(), CancellationToken.None);
        object value = CreateDeserializer(execution).Deserialize(registry, "evt", effective, CancellationToken.None);

        value.GetType().ShouldBeSameAs(CurrentType(execution));
        value.GetType().Assembly.ShouldBeSameAs(execution.Assembly);
        ReadCount(execution, "SchemaCalls").ShouldBe(3);
        ReadCount(execution, "IdentityCalls").ShouldBe(3);
        ReadCount(execution, "DeserializerCalls").ShouldBe(1);
        foreach (ImmutablePayload payload in new[] { source, effective, legacy })
        {
            byte[] bytes = new byte[2]; payload.CopyTo(0, bytes); bytes.ShouldBe([1, 2]);
        }

        loss.RequireNoObservedLoss();
    }

    /// <summary>Checks every artifact-backed wrapper refuses a different registry loss scope before callbacks.</summary>
    /// <param name="route">The exact local wrapper under test.</param>
    [Theory]
    [InlineData("upcaster")]
    [InlineData("downserializer")]
    [InlineData("validator")]
    [InlineData("deserializer")]
    public async Task SeparateCapabilityScopeRefusesBeforeAnyPrivateImageCallback(string route)
    {
        using EventRegistryRow dependency = CreateDependencyRow();
        using var artifact = new EventManagedArtifact(dependency, typeof(PrivateImageEventCallbacks).Assembly.Location,
            new EventBufferBudget(), new EventEvolutionCapabilityLoss(), CancellationToken.None);
        using EventManagedArtifactExecutionBinding execution = artifact.Load(CancellationToken.None);
        using EventDomainRegistry registry = CreateRegistry(execution, new EventEvolutionCapabilityLoss());
        using ImmutablePayload source = CreatePayload();
        switch (route)
        {
            case "upcaster":
                Should.Throw<InvalidOperationException>(() => new EventUpcastChainExecutor(registry,
                    new Dictionary<(string, int), RegisteredEventUpcaster>
                    {
                        [("evt", 1)] = new("test-upcaster", CreateUpcaster(execution), new byte[32], execution),
                    }, static (_, _, _, _, _, _) => { })).Message.ShouldStartWith("CapabilityMismatch:");
                break;
            case "downserializer":
                var downExecutor = new EventV1DownserializeExecutor(registry, static (_, _, _, _, _, _) => { },
                    static (_, _, _, _, _, _, _) => { }, static (_, _, _) => { });
                var downserializer = new RegisteredV1Downserializer("test-downserializer", CreateDownserializer(execution), new byte[32], execution);
                (await Should.ThrowAsync<InvalidOperationException>(async () => await downExecutor.DownserializeAsync(
                    "evt", "Legacy.Event", source, downserializer, 2, new EventBufferBudget(), CancellationToken.None)))
                    .Message.ShouldStartWith("CapabilityMismatch:");
                break;
            case "validator":
                Should.Throw<InvalidOperationException>(() => CreateValidation(registry, execution))
                    .Message.ShouldStartWith("CapabilityMismatch:");
                break;
            case "deserializer":
                Should.Throw<InvalidOperationException>(() => CreateDeserializer(execution).Deserialize(
                    registry, "evt", source, CancellationToken.None)).Message.ShouldStartWith("CapabilityMismatch:");
                break;
        }

        ReadCount(execution, "SchemaCalls").ShouldBe(0);
        ReadCount(execution, "IdentityCalls").ShouldBe(0);
        ReadCount(execution, "DeserializerCalls").ShouldBe(0);
    }

    /// <summary>Checks losing retained artifact evidence prevents a registered callback from executing again.</summary>
    [Fact]
    public void ArtifactDisposalRefusesExistingRegisteredDeserializerBeforeCallback()
    {
        var loss = new EventEvolutionCapabilityLoss();
        using EventRegistryRow dependency = CreateDependencyRow();
        using var artifact = new EventManagedArtifact(dependency, typeof(PrivateImageEventCallbacks).Assembly.Location,
            new EventBufferBudget(), loss, CancellationToken.None);
        using EventManagedArtifactExecutionBinding execution = artifact.Load(CancellationToken.None);
        using EventDomainRegistry registry = CreateRegistry(execution, loss);
        RegisteredCurrentEventDeserializer deserializer = CreateDeserializer(execution);
        using ImmutablePayload payload = CreatePayload();
        artifact.Dispose();

        Should.Throw<InvalidOperationException>(() => deserializer.Deserialize(registry, "evt", payload, CancellationToken.None));
        ReadCount(execution, "DeserializerCalls").ShouldBe(0);
    }

    /// <summary>Checks pinned graph admission accepts exact private objects and observed late loss fences actual callbacks.</summary>
    [Fact]
    public void ComposedPrivateImageObservationFencesActualRegistryCallbacksAfterUndeclaredLoad()
    {
        var loss = new EventEvolutionCapabilityLoss();
        using EventRegistryRow dependency = CreateDependencyRow();
        using var artifact = new EventManagedArtifact(dependency, typeof(PrivateImageEventCallbacks).Assembly.Location,
            new EventBufferBudget(), loss, CancellationToken.None);
        using EventManagedArtifactExecutionBinding execution = artifact.Load(CancellationToken.None);
        using EventDomainRegistry registry = CreateRegistry(execution, loss, dependency);
        AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(execution.Assembly)!;
        using var observer = CreateComposedObserver(registry, dependency, execution, context, includeImage: true);
        RegisteredEventVersionValidation validation = CreateValidation(registry, execution);
        RegisteredCurrentEventDeserializer deserializer = CreateDeserializer(execution);
        using ImmutablePayload payload = CreatePayload();
        observer.ReconcileCurrentLoads(CancellationToken.None);
        validation.Validate("d", "evt", 2, "json", payload, CancellationToken.None);
        deserializer.Deserialize(registry, "evt", payload, CancellationToken.None).GetType().ShouldBeSameAs(CurrentType(execution));

        // This is still a supplied local direct-root graph, not a complete catalog or process
        // attestation. The undeclared load completes; subsequent registry callbacks must refuse.
        _ = context.LoadFromAssemblyPath(typeof(EventDomainRegistry).Assembly.Location);

        Should.Throw<InvalidOperationException>(() => validation.Validate("d", "evt", 2, "json", payload, CancellationToken.None))
            .Message.ShouldStartWith("CapabilityMismatch:");
        Should.Throw<InvalidOperationException>(() => deserializer.Deserialize(registry, "evt", payload, CancellationToken.None))
            .Message.ShouldStartWith("CapabilityMismatch:");
        ReadCount(execution, "SchemaCalls").ShouldBe(1);
        ReadCount(execution, "IdentityCalls").ShouldBe(1);
        ReadCount(execution, "DeserializerCalls").ShouldBe(1);
        byte[] original = new byte[2]; payload.CopyTo(0, original); original.ShouldBe([1, 2]);
    }

    /// <summary>Checks stream images receive no observer admission without exact same-scope object evidence.</summary>
    /// <param name="caseName">The missing or foreign-scope evidence input.</param>
    [Theory]
    [InlineData("missing")]
    [InlineData("foreign-scope")]
    public void ComposedObserverRefusesMissingOrForeignScopePrivateImageEvidence(string caseName)
    {
        var artifactLoss = new EventEvolutionCapabilityLoss();
        using EventRegistryRow dependency = CreateDependencyRow();
        using var artifact = new EventManagedArtifact(dependency, typeof(PrivateImageEventCallbacks).Assembly.Location,
            new EventBufferBudget(), artifactLoss, CancellationToken.None);
        using EventManagedArtifactExecutionBinding execution = artifact.Load(CancellationToken.None);
        using EventDomainRegistry registry = CreateRegistry(execution,
            caseName == "missing" ? artifactLoss : new EventEvolutionCapabilityLoss(), dependency);
        AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(execution.Assembly)!;

        Should.Throw<InvalidOperationException>(() => CreateComposedObserver(registry, dependency, execution, context,
            includeImage: caseName != "missing")).Message.ShouldStartWith("CapabilityMismatch:");

        ReadCount(execution, "SchemaCalls").ShouldBe(0);
        ReadCount(execution, "IdentityCalls").ShouldBe(0);
        ReadCount(execution, "DeserializerCalls").ShouldBe(0);
    }

    /// <summary>Checks equal bytes cannot relabel the original private-image dependency or loader context.</summary>
    /// <param name="changeContext">Whether to replace the context identity instead of the logical dependency identity.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComposedObserverRefusesSameHashUnderDifferentOriginalDependencyDeclaration(bool changeContext)
    {
        var loss = new EventEvolutionCapabilityLoss();
        using EventRegistryRow original = CreateDependencyRow();
        using EventRegistryRow relabelled = CreateDependencyRow(
            logicalIdentity: changeContext ? "private-callbacks" : "relabelled-dependency",
            contextId: changeContext ? "relabelled-context" : "private-callback-context");
        original.GetEncodedField(2).SequenceEqual(relabelled.GetEncodedField(2)).ShouldBeTrue();
        using var artifact = new EventManagedArtifact(original, typeof(PrivateImageEventCallbacks).Assembly.Location,
            new EventBufferBudget(), loss, CancellationToken.None);
        using EventManagedArtifactExecutionBinding execution = artifact.Load(CancellationToken.None);
        using EventDomainRegistry registry = CreateRegistry(execution, loss, relabelled);
        AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(execution.Assembly)!;

        Should.Throw<InvalidOperationException>(() => CreateComposedObserver(registry, relabelled, execution, context,
            includeImage: true)).Message.ShouldContain("different dependency declaration");

        ReadCount(execution, "SchemaCalls").ShouldBe(0);
        ReadCount(execution, "IdentityCalls").ShouldBe(0);
        ReadCount(execution, "DeserializerCalls").ShouldBe(0);
    }

    private static EventEvolutionManagedLoadObserver CreateComposedObserver(EventDomainRegistry registry,
        EventRegistryRow dependency, EventManagedArtifactExecutionBinding execution, AssemblyLoadContext context, bool includeImage)
    {
        var identity = new EventDependencyIdentity(dependency.GetTextKey(1), "managed");
        var node = new EventResolvedDependency(identity, dependency.GetTextField(1), dependency.GetTextField(3),
            typeof(PrivateImageEventCallbacks).Assembly.Location, []);
        return new EventEvolutionManagedLoadObserver(registry, [node], [identity],
            [new(dependency.GetTextField(3), context)], CancellationToken.None, includeImage ? [execution] : []);
    }

    private static RegisteredEventVersionValidation CreateValidation(EventDomainRegistry registry, EventManagedArtifactExecutionBinding execution)
        => new(registry, "private-schema", Callback<EventVersionValidator>(execution, "ValidateSchema"), "{}\n"u8.ToArray(), [],
            "private-identity", Callback<EventVersionValidator>(execution, "ValidateIdentity"), "{}\n"u8.ToArray(), [],
            schemaExecutionBinding: execution, identityExecutionBinding: execution);

    private static RegisteredCurrentEventDeserializer CreateDeserializer(EventManagedArtifactExecutionBinding execution)
        => new(CurrentType(execution), "private-serializer", Callback<Func<IReadOnlyPayload, CancellationToken, object>>(execution, "Deserialize"),
            "{}\n"u8.ToArray(), [], serializerExecutionBinding: execution, currentTypeExecutionBinding: execution);

    private static TDelegate Callback<TDelegate>(EventManagedArtifactExecutionBinding execution, string name) where TDelegate : Delegate
        => Callbacks(execution).GetMethod(name)!.CreateDelegate<TDelegate>();

    private static Type Callbacks(EventManagedArtifactExecutionBinding execution)
        => execution.Assembly.GetType(typeof(PrivateImageEventCallbacks).FullName!, throwOnError: true)!;

    private static Type CurrentType(EventManagedArtifactExecutionBinding execution)
        => execution.Assembly.GetType(typeof(AllowlistedCurrentEventTestValue).FullName!, throwOnError: true)!;

    private static int ReadCount(EventManagedArtifactExecutionBinding execution, string name)
        => (int)Callbacks(execution).GetProperty(name)!.GetValue(null)!;

    private static IEventUpcaster CreateUpcaster(EventManagedArtifactExecutionBinding execution)
        => (IEventUpcaster)Activator.CreateInstance(execution.Assembly.GetType(typeof(LeaseRecordingEventUpcaster).FullName!, true)!,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, binder: null, args: ["success", null], culture: null)!;

    private static IV1Downserializer CreateDownserializer(EventManagedArtifactExecutionBinding execution)
        => (IV1Downserializer)Activator.CreateInstance(execution.Assembly.GetType(typeof(LeaseRecordingV1Downserializer).FullName!, true)!,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, binder: null, args: ["success", null], culture: null)!;

    private static ImmutablePayload CreatePayload()
    {
        using var writer = new BoundedPayloadWriter(2, CancellationToken.None);
        writer.Write([1, 2]); writer.Complete();
        return writer.TakeCompletedPayload();
    }

    private static EventRegistryRow CreateDependencyRow(string logicalIdentity = "private-callbacks",
        string contextId = "private-callback-context")
    {
        Assembly assembly = typeof(PrivateImageEventCallbacks).Assembly;
        using FileStream source = File.OpenRead(assembly.Location);
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(0x47); writer.WriteString("d"); writer.WriteString(logicalIdentity); writer.WriteString("managed");
        writer.WriteUInt16(3);
        writer.WriteByte(1); writer.WriteString(assembly.GetName().Version!.ToString());
        writer.WriteByte(2); writer.WriteHash(SHA256.HashData(source));
        writer.WriteByte(3); writer.WriteString(contextId);
        return new EventRegistryRow(writer.CopyEncodedBytes());
    }

    private static EventDomainRegistry CreateRegistry(EventManagedArtifactExecutionBinding execution, EventEvolutionCapabilityLoss loss,
        EventRegistryRow? dependency = null)
    {
        using EventDomainRegistry fixture = EventUpcastChainExecutorTests.CreateRegistry(loss);
        byte[] hash = execution.CopyHashForAssembly(execution.Assembly);
        byte[] optionsHash = EventOptionsManifestCodec.ComputeHash("{}\n"u8.ToArray(), []);
        ReadOnlyMemory<byte>[] rows = [.. fixture.Rows.Select(row => (ReadOnlyMemory<byte>)(row.Tag is 0x44 or 0x56
            ? RewriteBindingRow(row, hash, optionsHash, CurrentType(execution)) : row.Encoded.ToArray())), CreateDownserializerRow(hash)];
        return new EventDomainRegistry("d", dependency is null ? rows : [.. rows, dependency.Encoded.ToArray()], loss);
    }

    private static byte[] RewriteBindingRow(EventRegistryRow row, byte[] hash, byte[] optionsHash, Type currentType)
    {
        var reader = new EventEvolutionBinaryReader(row.Encoded);
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(reader.ReadByte()); writer.WriteString(reader.ReadString(64)); writer.WriteString(reader.ReadString(64));
        if (row.Tag == 0x56) { writer.WriteInt32(reader.ReadInt32()); }
        writer.WriteUInt16(reader.ReadUInt16());
        string fields = row.Tag == 0x44 ? "UIUHUHIBUHHH" : "UHHUHHUUHH";
        for (int field = 1; field <= fields.Length; field++)
        {
            writer.WriteByte(reader.ReadByte());
            switch (fields[field - 1])
            {
                case 'U':
                    string value = reader.ReadString(4096);
                    writer.WriteString(row.Tag == 0x44 && field == 3 ? currentType.AssemblyQualifiedName!
                        : row.Tag == 0x56 && field == 1 ? "private-schema"
                        : row.Tag == 0x56 && field == 4 ? "private-serializer"
                        : row.Tag == 0x56 && field == 8 ? "private-identity" : value);
                    break;
                case 'H':
                    byte[] original = reader.ReadHash().ToArray();
                    writer.WriteHash(row.Tag == 0x44 && field == 4 || row.Tag == 0x56 && field is 2 or 5 or 9 ? hash
                        : row.Tag == 0x56 && field is 3 or 6 or 10 ? optionsHash : original);
                    break;
                case 'I': writer.WriteInt32(reader.ReadInt32()); break;
                case 'B': writer.WriteBytes(reader.ReadBytes(4096)); break;
            }
        }

        reader.RequireEnd();
        return writer.CopyEncodedBytes();
    }

    private static byte[] CreateDownserializerRow(byte[] hash)
    {
        using var writer = new EventEvolutionBinaryWriter(1024);
        writer.WriteByte(0x46); writer.WriteString("d"); writer.WriteString("evt"); writer.WriteString("Legacy.Event");
        writer.WriteUInt16(15);
        writer.WriteByte(1); writer.WriteInt32(2); writer.WriteByte(2); writer.WriteInt32(1);
        writer.WriteByte(3); writer.WriteBytes("{}"u8); writer.WriteByte(4); writer.WriteBytes("{}"u8);
        writer.WriteByte(5); writer.WriteString("json"); writer.WriteByte(6); writer.WriteString("json");
        writer.WriteByte(7); writer.WriteString("test-downserializer"); writer.WriteByte(8); writer.WriteHash(hash);
        writer.WriteByte(9); writer.WriteHash(new byte[32]); writer.WriteByte(10); writer.WriteString("identity");
        writer.WriteByte(11); writer.WriteHash(new byte[32]); writer.WriteByte(12); writer.WriteHash(new byte[32]);
        writer.WriteByte(13); writer.WriteString("semantics"); writer.WriteByte(14); writer.WriteHash(new byte[32]);
        writer.WriteByte(15); writer.WriteHash(new byte[32]);
        return writer.CopyEncodedBytes();
    }
}

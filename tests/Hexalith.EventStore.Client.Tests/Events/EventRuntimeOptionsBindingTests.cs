using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Exercises runtime options from real retained managed images without granting catalog readiness.</summary>
public sealed class EventRuntimeOptionsBindingTests
{
    /// <summary>Checks admitted same-image options survive source replacement and execute exactly once.</summary>
    [Fact]
    public void SameImageOptionsUseRetainedObjectAfterSourceReplacement()
    {
        string directory = Directory.CreateTempSubdirectory("runtime-options-").FullName;
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        try
        {
            string file = Emit(directory);
            using EventRegistryRow row = Row(file, "root");
            using var artifact = new EventManagedArtifact(row, file, budget, loss, CancellationToken.None);
            using EventManagedArtifactExecutionBinding execution = artifact.Load(CancellationToken.None);
            var binding = new EventImplementationBinding("validator", Primary(execution.Assembly), "{}\n"u8.ToArray(), [],
                Options(execution.Assembly), execution);
            binding.RequireCapabilityScope(loss);
            using EventRegistryRow descriptor = VersionRow(row.GetEncodedField(2));
            File.WriteAllText(file, "replaced source");

            binding.RequireFields(descriptor, 1);

            Calls(execution.Assembly).ShouldBe(1);
            PrimaryCalls(execution.Assembly).ShouldBe(0);
            loss.RequireNoObservedLoss();
        }
        finally { Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks separately admitted options execute only from their explicitly bound image object.</summary>
    [Fact]
    public void SeparatelyAdmittedOptionsAssemblyUsesExplicitBinding()
    {
        string directory = Directory.CreateTempSubdirectory("runtime-options-").FullName;
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        try
        {
            string primaryFile = Emit(directory);
            string optionsFile = Emit(directory);
            using EventRegistryRow primary = Row(primaryFile, "root");
            using EventRegistryRow options = Row(optionsFile, "options");
            using EventRegistryRow runtime = Row(typeof(object).Assembly.Location, "runtime", "Default");
            using EventRegistryRow contracts = Row(typeof(IReadOnlyPayload).Assembly.Location, "contracts", "Default");
            using var set = new EventManagedArtifactSet([new(primary, primaryFile), new(options, optionsFile)],
                [new(runtime, typeof(object).Assembly), new(contracts, typeof(IReadOnlyPayload).Assembly)], budget, loss, CancellationToken.None);
            EventManagedArtifactExecutionBinding primaryExecution = set.Load(new("root", "managed"), CancellationToken.None);
            EventManagedArtifactExecutionBinding optionsExecution = set.Load(new("options", "managed"), CancellationToken.None);
            Func<ReadOnlyMemory<byte>> source = Options(optionsExecution.Assembly);
            Should.Throw<InvalidOperationException>(() => new EventImplementationBinding("validator", Primary(primaryExecution.Assembly),
                "{}\n"u8.ToArray(), [], source, primaryExecution)).Message.ShouldContain("CapabilityMismatch");
            var binding = new EventImplementationBinding("validator", Primary(primaryExecution.Assembly), "{}\n"u8.ToArray(), [],
                source, primaryExecution, optionsExecution);
            binding.RequireCapabilityScope(loss);
            using EventRegistryRow descriptor = VersionRow(primary.GetEncodedField(2));
            File.Delete(primaryFile); File.Delete(optionsFile);

            binding.RequireFields(descriptor, 1);

            Calls(optionsExecution.Assembly).ShouldBe(1);
            Calls(primaryExecution.Assembly).ShouldBe(0);
            PrimaryCalls(primaryExecution.Assembly).ShouldBe(0);
            set.RequireActive();
        }
        finally { Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks equal assembly identities cannot substitute a Default-context options callback.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameIdentityDefaultOptionsRefuseBeforeAnyCallback(bool explicitBinding)
    {
        string directory = Directory.CreateTempSubdirectory("runtime-options-").FullName;
        var budget = new EventBufferBudget();
        try
        {
            string file = Emit(directory);
            Assembly shared = AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
            using EventRegistryRow row = Row(file, "root");
            using var artifact = new EventManagedArtifact(row, file, budget, new EventEvolutionCapabilityLoss(), CancellationToken.None);
            using EventManagedArtifactExecutionBinding execution = artifact.Load(CancellationToken.None);
            shared.FullName.ShouldBe(execution.Assembly.FullName);
            shared.ShouldNotBeSameAs(execution.Assembly);

            Should.Throw<InvalidOperationException>(() => new EventImplementationBinding("validator", Primary(execution.Assembly),
                "{}\n"u8.ToArray(), [], Options(shared), execution, explicitBinding ? execution : null))
                .Message.ShouldContain("CapabilityMismatch");

            Calls(shared).ShouldBe(0);
            Calls(execution.Assembly).ShouldBe(0);
            PrimaryCalls(execution.Assembly).ShouldBe(0);
        }
        finally { Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks a multicast getter cannot execute an unadmitted earlier callback.</summary>
    [Fact]
    public void MulticastOptionsRefuseBeforeEitherCallback()
    {
        int calls = 0;
        Func<ReadOnlyMemory<byte>> source = () => { calls++; return "{}\n"u8.ToArray(); };
        source += () => { calls++; return "{}\n"u8.ToArray(); };

        Should.Throw<ArgumentException>(() => new EventImplementationBinding("validator", (Action)(static () => { }),
            "{}\n"u8.ToArray(), [], source)).ParamName.ShouldBe("runtimeOptions");

        calls.ShouldBe(0);
    }

    /// <summary>Checks both retained callbacks require one capability-loss scope before options execute.</summary>
    [Fact]
    public void ForeignOptionsCapabilityScopeRefusesBeforeCallback()
    {
        string directory = Directory.CreateTempSubdirectory("runtime-options-").FullName;
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        try
        {
            string primaryFile = Emit(directory);
            string optionsFile = Emit(directory);
            using EventRegistryRow primary = Row(primaryFile, "root");
            using EventRegistryRow options = Row(optionsFile, "options");
            using var primaryArtifact = new EventManagedArtifact(primary, primaryFile, budget, loss, CancellationToken.None);
            using var optionsArtifact = new EventManagedArtifact(options, optionsFile, budget, new EventEvolutionCapabilityLoss(), CancellationToken.None);
            using EventManagedArtifactExecutionBinding primaryExecution = primaryArtifact.Load(CancellationToken.None);
            using EventManagedArtifactExecutionBinding optionsExecution = optionsArtifact.Load(CancellationToken.None);
            var binding = new EventImplementationBinding("validator", Primary(primaryExecution.Assembly), "{}\n"u8.ToArray(), [],
                Options(optionsExecution.Assembly), primaryExecution, optionsExecution);

            Should.Throw<InvalidOperationException>(() => binding.RequireCapabilityScope(loss)).Message.ShouldContain("CapabilityMismatch");

            Calls(optionsExecution.Assembly).ShouldBe(0);
        }
        finally { Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks separately retained options cannot be used after ending their binding evidence.</summary>
    [Fact]
    public void DisposedOptionsBindingFencesDescriptorAdmissionBeforeCallback()
    {
        string directory = Directory.CreateTempSubdirectory("runtime-options-").FullName;
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        try
        {
            string primaryFile = Emit(directory);
            string optionsFile = Emit(directory);
            using EventRegistryRow primary = Row(primaryFile, "root");
            using EventRegistryRow options = Row(optionsFile, "options");
            using var primaryArtifact = new EventManagedArtifact(primary, primaryFile, budget, loss, CancellationToken.None);
            using var optionsArtifact = new EventManagedArtifact(options, optionsFile, budget, loss, CancellationToken.None);
            using EventManagedArtifactExecutionBinding primaryExecution = primaryArtifact.Load(CancellationToken.None);
            using EventManagedArtifactExecutionBinding optionsExecution = optionsArtifact.Load(CancellationToken.None);
            var binding = new EventImplementationBinding("validator", Primary(primaryExecution.Assembly), "{}\n"u8.ToArray(), [],
                Options(optionsExecution.Assembly), primaryExecution, optionsExecution);
            binding.RequireCapabilityScope(loss);
            using EventRegistryRow descriptor = VersionRow(primary.GetEncodedField(2));
            optionsExecution.Dispose();

            Should.Throw<InvalidOperationException>(() => binding.RequireFields(descriptor, 1)).Message.ShouldContain("CapabilityMismatch");

            Calls(optionsExecution.Assembly).ShouldBe(0);
        }
        finally { Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks both actual wrappers forward separate retained options and original pre-cancellation boundaries.</summary>
    [Theory]
    [InlineData("schema", false)]
    [InlineData("identity", false)]
    [InlineData("deserialize", false)]
    [InlineData("schema", true)]
    [InlineData("identity", true)]
    [InlineData("deserialize", true)]
    public void RegisteredWrappersUseSeparatelyAdmittedOptionsAndRefuseDefaultSubstitution(string stage, bool mismatch)
    {
        string directory = Directory.CreateTempSubdirectory("runtime-options-").FullName;
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        try
        {
            string primaryFile = Emit(directory);
            string optionsFile = Emit(directory);
            Assembly sharedOptions = AssemblyLoadContext.Default.LoadFromAssemblyPath(optionsFile);
            using EventRegistryRow primary = Row(primaryFile, "root");
            using EventRegistryRow options = Row(optionsFile, "options");
            using EventRegistryRow runtime = Row(typeof(object).Assembly.Location, "runtime", "Default");
            using EventRegistryRow contracts = Row(typeof(IReadOnlyPayload).Assembly.Location, "contracts", "Default");
            using var set = new EventManagedArtifactSet([new(primary, primaryFile), new(options, optionsFile)],
                [new(runtime, typeof(object).Assembly), new(contracts, typeof(IReadOnlyPayload).Assembly)], budget, loss, CancellationToken.None);
            EventManagedArtifactExecutionBinding primaryExecution = set.Load(new("root", "managed"), CancellationToken.None);
            EventManagedArtifactExecutionBinding optionsExecution = set.Load(new("options", "managed"), CancellationToken.None);
            Assembly image = primaryExecution.Assembly;
            Type valueType = image.GetType("EventValue")!;
            EventVersionValidator validate = image.GetType("Boundary")!.GetMethod("Validate")!.CreateDelegate<EventVersionValidator>();
            Func<IReadOnlyPayload, CancellationToken, object> deserialize = image.GetType("Boundary")!.GetMethod("Deserialize")!
                .CreateDelegate<Func<IReadOnlyPayload, CancellationToken, object>>();
            using EventDomainRegistry registry = Registry(valueType, primary.GetEncodedField(2), loss);
            Func<ReadOnlyMemory<byte>> valid = Options(optionsExecution.Assembly);
            Func<ReadOnlyMemory<byte>> source = mismatch ? Options(sharedOptions) : valid;
            RegisteredEventVersionValidation? validation = null;
            RegisteredCurrentEventDeserializer? deserializer = null;
            void Bind()
            {
                if (stage == "deserialize")
                {
                    deserializer = new(valueType, "serializer", deserialize, "{}\n"u8.ToArray(), [], source,
                        primaryExecution, primaryExecution, optionsExecution);
                }
                else
                {
                    validation = new(registry, "validator", validate, "{}\n"u8.ToArray(), [], "identity", validate,
                        "{}\n"u8.ToArray(), [], stage == "schema" ? source : valid, stage == "identity" ? source : valid,
                        primaryExecution, primaryExecution, optionsExecution, optionsExecution);
                }
            }
            if (mismatch)
            {
                Should.Throw<InvalidOperationException>(Bind).Message.ShouldContain("CapabilityMismatch");
                Calls(sharedOptions).ShouldBe(0);
                Calls(optionsExecution.Assembly).ShouldBe(0);
                PrimaryCalls(image).ShouldBe(0);
                return;
            }
            Bind();
            using var writer = new BoundedPayloadWriter(2, CancellationToken.None, budget);
            writer.Write([1, 2]); writer.Complete();
            using ImmutablePayload payload = writer.TakeCompletedPayload();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            void Invoke(CancellationToken token)
            {
                if (deserializer is not null) { deserializer.Deserialize(registry, "evt", payload, token).GetType().ShouldBe(valueType); }
                else { validation!.Validate("d", "evt", 1, "json", payload, token); }
            }

            Should.Throw<OperationCanceledException>(() => Invoke(cancellation.Token)).CancellationToken.ShouldBe(cancellation.Token);
            Calls(optionsExecution.Assembly).ShouldBe(0);
            PrimaryCalls(image).ShouldBe(0);
            File.Delete(primaryFile); File.Delete(optionsFile);
            Invoke(CancellationToken.None);

            Calls(optionsExecution.Assembly).ShouldBe(stage == "deserialize" ? 1 : 2);
            PrimaryCalls(image).ShouldBe(stage == "deserialize" ? 1 : 2);
            Calls(sharedOptions).ShouldBe(0);
            set.RequireActive();
        }
        finally { Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks immediate options boundaries preserve cancellation or observed loss before parsing invalid output.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptionsCancellationOrObservedLossPrecedesReturnedOptionsParsing(bool observeLoss)
    {
        using var cancellation = new CancellationTokenSource();
        var loss = new EventEvolutionCapabilityLoss();
        int calls = 0;
        var binding = new EventImplementationBinding("validator", (Action)(static () => { }), "{}\n"u8.ToArray(), [],
            () => { calls++; if (observeLoss) { loss.ObserveViolation(); } else { cancellation.Cancel(); } return new byte[] { 0xff }; });
        binding.RequireCapabilityScope(loss);

        if (observeLoss)
        {
            Should.Throw<InvalidOperationException>(() => binding.RequireRuntimeOptions(cancellation.Token)).Message.ShouldContain("CapabilityMismatch");
        }
        else
        {
            Should.Throw<OperationCanceledException>(() => binding.RequireRuntimeOptions(cancellation.Token)).CancellationToken.ShouldBe(cancellation.Token);
        }
        calls.ShouldBe(1);
    }

    private static Func<int> Primary(Assembly assembly) => assembly.GetType("Boundary")!.GetMethod("Read")!.CreateDelegate<Func<int>>();
    private static Func<ReadOnlyMemory<byte>> Options(Assembly assembly) => assembly.GetType("Boundary")!.GetMethod("Options")!.CreateDelegate<Func<ReadOnlyMemory<byte>>>();
    private static int Calls(Assembly assembly) => (int)assembly.GetType("Boundary")!.GetField("OptionsCalls")!.GetValue(null)!;
    private static int PrimaryCalls(Assembly assembly) => (int)assembly.GetType("Boundary")!.GetField("PrimaryCalls")!.GetValue(null)!;

    private static string Emit(string directory)
    {
        string name = "Options" + Guid.NewGuid().ToString("N");
        var assembly = new PersistedAssemblyBuilder(new AssemblyName(name) { Version = new Version(1, 0, 0, 0) }, typeof(object).Assembly);
        TypeBuilder type = assembly.DefineDynamicModule(name).DefineType("Boundary", TypeAttributes.Public | TypeAttributes.Class);
        FieldBuilder primaryCalls = type.DefineField("PrimaryCalls", typeof(int), FieldAttributes.Public | FieldAttributes.Static);
        FieldBuilder optionsCalls = type.DefineField("OptionsCalls", typeof(int), FieldAttributes.Public | FieldAttributes.Static);
        TypeBuilder value = ((ModuleBuilder)type.Module).DefineType("EventValue", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        ConstructorBuilder valueConstructor = value.DefineDefaultConstructor(MethodAttributes.Public);
        ILGenerator validator = type.DefineMethod("Validate", MethodAttributes.Public | MethodAttributes.Static, typeof(void),
            [typeof(string), typeof(string), typeof(int), typeof(string), typeof(IReadOnlyPayload), typeof(CancellationToken)]).GetILGenerator();
        Increment(validator, primaryCalls); validator.Emit(OpCodes.Ret);
        ILGenerator deserialize = type.DefineMethod("Deserialize", MethodAttributes.Public | MethodAttributes.Static, typeof(object),
            [typeof(IReadOnlyPayload), typeof(CancellationToken)]).GetILGenerator();
        Increment(deserialize, primaryCalls); deserialize.Emit(OpCodes.Newobj, valueConstructor); deserialize.Emit(OpCodes.Ret);
        ILGenerator primary = type.DefineMethod("Read", MethodAttributes.Public | MethodAttributes.Static, typeof(int), Type.EmptyTypes).GetILGenerator();
        Increment(primary, primaryCalls);
        primary.Emit(OpCodes.Ldc_I4_7); primary.Emit(OpCodes.Ret);
        ILGenerator options = type.DefineMethod("Options", MethodAttributes.Public | MethodAttributes.Static, typeof(ReadOnlyMemory<byte>), Type.EmptyTypes).GetILGenerator();
        Increment(options, optionsCalls);
        options.Emit(OpCodes.Ldc_I4_3); options.Emit(OpCodes.Newarr, typeof(byte));
        for (int index = 0; index < 3; index++)
        {
            options.Emit(OpCodes.Dup); options.Emit(OpCodes.Ldc_I4, index);
            options.Emit(OpCodes.Ldc_I4, new[] { 123, 125, 10 }[index]); options.Emit(OpCodes.Stelem_I1);
        }
        options.Emit(OpCodes.Newobj, typeof(ReadOnlyMemory<byte>).GetConstructor([typeof(byte[])])!);
        options.Emit(OpCodes.Ret);
        _ = value.CreateType(); _ = type.CreateType();
        string file = Path.Combine(directory, name + ".dll");
        assembly.Save(file);
        return file;
    }

    private static void Increment(ILGenerator il, FieldBuilder field)
    {
        il.Emit(OpCodes.Ldsfld, field); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Add); il.Emit(OpCodes.Stsfld, field);
    }

    private static EventRegistryRow Row(string file, string identity, string context = "private-options")
    {
        using FileStream source = File.OpenRead(file);
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(0x47); writer.WriteString("d"); writer.WriteString(identity); writer.WriteString("managed"); writer.WriteUInt16(3);
        writer.WriteByte(1); writer.WriteString(AssemblyName.GetAssemblyName(file).Version!.ToString());
        writer.WriteByte(2); writer.WriteHash(SHA256.HashData(source));
        writer.WriteByte(3); writer.WriteString(context);
        return new EventRegistryRow(writer.CopyEncodedBytes());
    }

    private static EventRegistryRow VersionRow(ReadOnlySpan<byte> assemblyHash)
    {
        byte[] optionsHash = EventOptionsManifestCodec.ComputeHash("{}\n"u8.ToArray(), []);
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(0x56); writer.WriteString("d"); writer.WriteString("evt"); writer.WriteInt32(1); writer.WriteUInt16(10);
        writer.WriteByte(1); writer.WriteString("validator"); writer.WriteByte(2); writer.WriteHash(assemblyHash); writer.WriteByte(3); writer.WriteHash(optionsHash);
        writer.WriteByte(4); writer.WriteString("serializer"); writer.WriteByte(5); writer.WriteHash(assemblyHash); writer.WriteByte(6); writer.WriteHash(optionsHash);
        writer.WriteByte(7); writer.WriteString("json");
        writer.WriteByte(8); writer.WriteString("identity"); writer.WriteByte(9); writer.WriteHash(assemblyHash); writer.WriteByte(10); writer.WriteHash(optionsHash);
        return new EventRegistryRow(writer.CopyEncodedBytes());
    }

    private static EventDomainRegistry Registry(Type currentType, ReadOnlySpan<byte> assemblyHash, EventEvolutionCapabilityLoss loss)
    {
        Dictionary<string, string> fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Events", "Fixtures", "EventRegistryV17.json")))!;
        byte[] descriptor = Convert.FromHexString(fixture["DescriptorRow"]);
        var reader = new EventEvolutionBinaryReader(descriptor);
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(reader.ReadByte()); writer.WriteString(reader.ReadString(64)); writer.WriteString(reader.ReadString(64));
        writer.WriteUInt16(reader.ReadUInt16());
        const string fields = "UIUHUHIBUHHH";
        for (int field = 1; field <= fields.Length; field++)
        {
            writer.WriteByte(reader.ReadByte());
            if (fields[field - 1] == 'U')
            {
                string text = reader.ReadString(4096); writer.WriteString(field == 3 ? currentType.AssemblyQualifiedName! : text);
            }
            else if (fields[field - 1] == 'H')
            {
                ReadOnlySpan<byte> hash = reader.ReadHash(); writer.WriteHash(field == 4 ? assemblyHash : hash);
            }
            else if (fields[field - 1] == 'I') { writer.WriteInt32(reader.ReadInt32()); }
            else { writer.WriteBytes(reader.ReadBytes(4096)); }
        }
        reader.RequireEnd();
        using EventRegistryRow version = VersionRow(assemblyHash);
        return new EventDomainRegistry("d", [Convert.FromHexString(fixture["AliasRow"]), writer.CopyEncodedBytes(),
            version.Encoded.ToArray(), Convert.FromHexString(fixture["SharedRow"])], capabilityLoss: loss);
    }
}

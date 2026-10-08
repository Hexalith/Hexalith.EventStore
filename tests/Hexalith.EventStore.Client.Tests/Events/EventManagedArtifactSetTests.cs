using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Exercises real managed dependency loading from retained images without granting runtime readiness.</summary>
public sealed class EventManagedArtifactSetTests
{
    /// <summary>Checks a same-identity Default image and subsequently replaced files cannot supply a private dependency.</summary>
    /// <param name="removeFiles">Whether the sources are removed instead of replaced.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrivateTransitiveImagesSurviveChangedSourcesAndOverrideDefaultObjects(bool removeFiles)
    {
        string directory = Directory.CreateTempSubdirectory("managed-set-").FullName;
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        string leafName = "Leaf" + Guid.NewGuid().ToString("N");
        string rootName = "Root" + Guid.NewGuid().ToString("N");
        try
        {
            string defaultFile = Emit(directory, leafName, 99, suffix: "default");
            Assembly defaultLeaf = AssemblyLoadContext.Default.LoadFromAssemblyPath(defaultFile);
            MethodInfo leafMethod = defaultLeaf.GetType("Boundary")!.GetMethod("Read")!;
            string leafFile = Emit(directory, leafName, 7);
            string rootFile = Emit(directory, rootName, 0, dependency: leafMethod);
            using EventRegistryRow root = Row(rootFile, "root", "private-set");
            using EventRegistryRow leaf = Row(leafFile, "leaf", "private-set");
            using EventRegistryRow runtime = Row(typeof(object).Assembly.Location, "runtime", "Default");
            using var set = new EventManagedArtifactSet([new(root, rootFile), new(leaf, leafFile)],
                [new(runtime, typeof(object).Assembly)], budget, loss, CancellationToken.None);
            if (removeFiles) { File.Delete(rootFile); File.Delete(leafFile); }
            else { File.WriteAllText(rootFile, "changed root"); File.WriteAllText(leafFile, "changed dependency"); }

            EventManagedArtifactExecutionBinding binding = set.Load(new("root", "managed"), CancellationToken.None);
            Func<int> callback = binding.Assembly.GetType("Boundary")!.GetMethod("Read")!.CreateDelegate<Func<int>>();
            callback().ShouldBe(7);
            EventManagedArtifactExecutionBinding dependency = set.Load(new("leaf", "managed"), CancellationToken.None);
            dependency.Assembly.ShouldNotBeSameAs(defaultLeaf);
            dependency.Assembly.FullName.ShouldBe(defaultLeaf.FullName);
            AssemblyLoadContext.GetLoadContext(dependency.Assembly).ShouldBeSameAs(AssemblyLoadContext.GetLoadContext(binding.Assembly));
            dependency.Assembly.Location.ShouldBeEmpty();
            set.Load(new("root", "managed"), CancellationToken.None).ShouldBeSameAs(binding);
            set.RequireActive();
            loss.RequireNoObservedLoss();
        }
        finally { Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks missing static dependencies refuse the complete set before any private assembly is loaded.</summary>
    [Fact]
    public void MissingStaticManagedReferenceRefusesBeforeContextCreationAndReleasesAllCharges()
    {
        string directory = Directory.CreateTempSubdirectory("managed-set-").FullName;
        var budget = new EventBufferBudget();
        string context = "missing-static-" + Guid.NewGuid().ToString("N");
        try
        {
            string leaf = Emit(directory, "Leaf" + Guid.NewGuid().ToString("N"), 7);
            Assembly defaultLeaf = AssemblyLoadContext.Default.LoadFromAssemblyPath(leaf);
            string root = Emit(directory, "Root" + Guid.NewGuid().ToString("N"), 0,
                dependency: defaultLeaf.GetType("Boundary")!.GetMethod("Read"));
            using EventRegistryRow row = Row(root, "root", context);
            using EventRegistryRow runtime = Row(typeof(object).Assembly.Location, "runtime", "Default");

            Should.Throw<InvalidOperationException>(() => new EventManagedArtifactSet([new(row, root)],
                [new(runtime, typeof(object).Assembly)], budget, new EventEvolutionCapabilityLoss(), CancellationToken.None))
                .Message.ShouldContain("undeclared managed reference");
            AssemblyLoadContext.All.ShouldNotContain(item => item.Name == context);
        }
        finally { Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks dynamic load-by-name cannot obtain a matching Default object through the runtime fallback.</summary>
    [Fact]
    public void UndeclaredDynamicResolutionThrowsBeforeDefaultFallbackAndFencesSubsequentWork()
    {
        string directory = Directory.CreateTempSubdirectory("managed-set-").FullName;
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        try
        {
            string foreignName = "Foreign" + Guid.NewGuid().ToString("N");
            string foreignFile = Emit(directory, foreignName, 99);
            Assembly foreign = AssemblyLoadContext.Default.LoadFromAssemblyPath(foreignFile);
            string rootFile = Emit(directory, "Root" + Guid.NewGuid().ToString("N"), 0, dynamicName: foreign.FullName);
            using EventRegistryRow root = Row(rootFile, "root", "private-set");
            using EventRegistryRow runtime = Row(typeof(object).Assembly.Location, "runtime", "Default");
            using var set = new EventManagedArtifactSet([new(root, rootFile)], [new(runtime, typeof(object).Assembly)],
                budget, loss, CancellationToken.None);
            EventManagedArtifactExecutionBinding binding = set.Load(new("root", "managed"), CancellationToken.None);
            Func<Assembly> callback = binding.Assembly.GetType("Boundary")!.GetMethod("Read")!.CreateDelegate<Func<Assembly>>();

            Exception failure = Should.Throw<Exception>(() => callback());
            (failure.InnerException?.Message ?? failure.Message).ShouldContain("Default fallback");
            Should.Throw<InvalidOperationException>(loss.RequireNoObservedLoss);
            Should.Throw<InvalidOperationException>(() => set.Load(new("root", "managed"), CancellationToken.None));
        }
        finally { Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks loader simple-name collisions refuse despite different versions or name casing.</summary>
    /// <param name="differentCase">Whether the conflicting name differs in casing instead of version.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameSimpleNameWithDifferentIdentityRefusesBeforePrivateLoading(bool differentCase)
    {
        string directory = Directory.CreateTempSubdirectory("managed-set-").FullName;
        var budget = new EventBufferBudget();
        string context = "ambiguous-name-" + Guid.NewGuid().ToString("N");
        try
        {
            string name = "Collision" + Guid.NewGuid().ToString("N");
            string first = Emit(directory, name, 1);
            string second = Emit(directory, differentCase ? name.ToUpperInvariant() : name, 2,
                suffix: "second", version: new Version(2, 0, 0, 0));
            using EventRegistryRow one = Row(first, "one", context);
            using EventRegistryRow two = Row(second, "two", context);
            using EventRegistryRow runtime = Row(typeof(object).Assembly.Location, "runtime", "Default");

            Should.Throw<InvalidOperationException>(() => new EventManagedArtifactSet([new(one, first), new(two, second)],
                [new(runtime, typeof(object).Assembly)], budget, new EventEvolutionCapabilityLoss(), CancellationToken.None))
                .Message.ShouldContain("ambiguous");
            AssemblyLoadContext.All.ShouldNotContain(item => item.Name == context);
        }
        finally { Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks imports cannot collide with private sources and cannot come from another runtime context.</summary>
    /// <param name="foreignContext">Whether to supply a non-Default object instead of a conflicting Default name.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedImportObjectAndIdentityRefusalsReleaseEveryCharge(bool foreignContext)
    {
        string directory = Directory.CreateTempSubdirectory("managed-set-").FullName;
        var budget = new EventBufferBudget();
        var context = new AssemblyLoadContext("foreign-import", isCollectible: true);
        try
        {
            string name = "Import" + Guid.NewGuid().ToString("N");
            string rootFile = Emit(directory, name, 7);
            string sharedFile = Emit(directory, name, 99, "shared", version: new Version(2, 0, 0, 0));
            Assembly shared = foreignContext ? context.LoadFromAssemblyPath(sharedFile)
                : AssemblyLoadContext.Default.LoadFromAssemblyPath(sharedFile);
            using EventRegistryRow root = Row(rootFile, "root", "private-set");
            using EventRegistryRow imported = Row(sharedFile, "shared", foreignContext ? context.Name! : "Default");
            using EventRegistryRow runtime = Row(typeof(object).Assembly.Location, "runtime", "Default");

            Should.Throw<InvalidOperationException>(() => new EventManagedArtifactSet([new(root, rootFile)],
                [new(runtime, typeof(object).Assembly), new(imported, shared)], budget, new EventEvolutionCapabilityLoss(), CancellationToken.None))
                .Message.ShouldContain(foreignContext ? "Default-context" : "ambiguous");
        }
        finally { context.Unload(); Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks disposal clears both retained private images and rejects retained binding evidence.</summary>
    [Fact]
    public void DisposalClearsPrivateImagesAndFencesRetainedBindings()
    {
        string directory = Directory.CreateTempSubdirectory("managed-set-").FullName;
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        try
        {
            string leafName = "Leaf" + Guid.NewGuid().ToString("N");
            string leafFile = Emit(directory, leafName, 7);
            Assembly defaultLeaf = AssemblyLoadContext.Default.LoadFromAssemblyPath(leafFile);
            string rootFile = Emit(directory, "Root" + Guid.NewGuid().ToString("N"), 0,
                dependency: defaultLeaf.GetType("Boundary")!.GetMethod("Read"));
            using EventRegistryRow root = Row(rootFile, "root", "private-set");
            using EventRegistryRow leaf = Row(leafFile, "leaf", "private-set");
            using EventRegistryRow runtime = Row(typeof(object).Assembly.Location, "runtime", "Default");
            using var set = new EventManagedArtifactSet([new(root, rootFile), new(leaf, leafFile)], [new(runtime, typeof(object).Assembly)],
                budget, loss, CancellationToken.None);
            EventManagedArtifactExecutionBinding binding = set.Load(new("root", "managed"), CancellationToken.None);
            var images = (Dictionary<string, EventManagedArtifact>)typeof(EventManagedArtifactSet)
                .GetField("_images", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(set)!;
            byte[][] storage = images.Values.Select(image => (byte[])typeof(EventManagedArtifact)
                .GetField("_image", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(image)!).ToArray();
            storage.Length.ShouldBe(2);
            storage.ShouldAllBe(image => image.Any(value => value != 0));

            set.Dispose();

            storage.ShouldAllBe(image => image.All(value => value == 0));
            budget.LiveBytes.ShouldBe(0);
            Should.Throw<ObjectDisposedException>(() => binding.RequireBoundAssembly(binding.Assembly));
            Should.Throw<InvalidOperationException>(loss.RequireNoObservedLoss);
        }
        finally { Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks a supplied context cannot turn a pre-existing foreign image into retained byte provenance.</summary>
    [Fact]
    public void ArtifactContextSeamRefusesPreExistingSameIdentityForeignBytes()
    {
        string directory = Directory.CreateTempSubdirectory("managed-set-").FullName;
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        var context = new EventManagedArtifactLoadContext("private-set", _ => typeof(object).Assembly, loss);
        try
        {
            string name = "ForeignBytes" + Guid.NewGuid().ToString("N");
            string privateFile = Emit(directory, name, 7);
            string foreignFile = Emit(directory, name, 99, "foreign");
            using EventRegistryRow row = Row(privateFile, "root", "private-set");
            using var artifact = new EventManagedArtifact(row, privateFile, budget, loss, CancellationToken.None);
            Assembly foreign = context.LoadFromAssemblyPath(foreignFile);
            foreign.GetType("Boundary")!.GetMethod("Read")!.CreateDelegate<Func<int>>()().ShouldBe(99);

            Should.Throw<InvalidOperationException>(() => artifact.LoadInContext(context, CancellationToken.None))
                .Message.ShouldContain("pre-existing context contents");
            Should.Throw<InvalidOperationException>(loss.RequireNoObservedLoss);
        }
        finally { context.Unload(); Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks explicit stream/path loads are observed on the next boundary without claiming prevention.</summary>
    /// <param name="useStream">Whether the undeclared image enters through a stream instead of a path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedContextContaminationFencesSubsequentWork(bool useStream)
    {
        string directory = Directory.CreateTempSubdirectory("managed-set-").FullName;
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        try
        {
            string rootFile = Emit(directory, "Root" + Guid.NewGuid().ToString("N"), 7);
            string foreignFile = Emit(directory, "Undeclared" + Guid.NewGuid().ToString("N"), 99);
            using EventRegistryRow root = Row(rootFile, "root", "private-set");
            using EventRegistryRow runtime = Row(typeof(object).Assembly.Location, "runtime", "Default");
            using var set = new EventManagedArtifactSet([new(root, rootFile)], [new(runtime, typeof(object).Assembly)],
                budget, loss, CancellationToken.None);
            EventManagedArtifactExecutionBinding binding = set.Load(new("root", "managed"), CancellationToken.None);
            AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(binding.Assembly)!;
            Assembly foreign;
            if (useStream)
            {
                using FileStream file = File.OpenRead(foreignFile);
                foreign = context.LoadFromStream(file);
            }
            else { foreign = context.LoadFromAssemblyPath(foreignFile); }
            foreign.ShouldNotBeNull(); // The explicit load already happened; the boundary cannot undo it.

            Should.Throw<InvalidOperationException>(loss.RequireNoObservedLoss);
            Should.Throw<InvalidOperationException>(() => binding.RequireBoundAssembly(binding.Assembly));
            Should.Throw<InvalidOperationException>(set.RequireActive);
            Should.Throw<InvalidOperationException>(() => set.Load(new("root", "managed"), CancellationToken.None));
        }
        finally { Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks P/Invoke resolution refuses the owned context's native fallback and records sticky loss.</summary>
    [Fact]
    public void NativeResolutionRefusesBeforeProbingAndFencesSubsequentWork()
    {
        string directory = Directory.CreateTempSubdirectory("managed-set-").FullName;
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        try
        {
            string rootFile = Emit(directory, "Native" + Guid.NewGuid().ToString("N"), 0, nativeImport: true);
            using EventRegistryRow root = Row(rootFile, "root", "private-set");
            using EventRegistryRow runtime = Row(typeof(object).Assembly.Location, "runtime", "Default");
            using var set = new EventManagedArtifactSet([new(root, rootFile)], [new(runtime, typeof(object).Assembly)],
                budget, loss, CancellationToken.None);
            EventManagedArtifactExecutionBinding binding = set.Load(new("root", "managed"), CancellationToken.None);
            Func<int> callback = binding.Assembly.GetType("Boundary")!.GetMethod("Read")!.CreateDelegate<Func<int>>();

            Should.Throw<InvalidOperationException>(() => callback()).Message.ShouldContain("native load route");
            Should.Throw<InvalidOperationException>(loss.RequireNoObservedLoss);
            Should.Throw<InvalidOperationException>(set.RequireActive);
        }
        finally { Directory.Delete(directory, recursive: true); budget.LiveBytes.ShouldBe(0); }
    }

    /// <summary>Checks in-load cancellation forwards its token and refuses the uncommitted set binding.</summary>
    [Fact]
    public void CancellationDuringPrivateLoadPreservesOriginalTokenAndClearsOnDisposal()
    {
        string directory = Directory.CreateTempSubdirectory("managed-set-").FullName;
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        string context = "cancel-set-" + Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource();
        void OnLoad(object? sender, AssemblyLoadEventArgs arguments)
        {
            if (AssemblyLoadContext.GetLoadContext(arguments.LoadedAssembly)?.Name == context) { cancellation.Cancel(); }
        }
        try
        {
            string rootFile = Emit(directory, "Root" + Guid.NewGuid().ToString("N"), 7);
            using EventRegistryRow root = Row(rootFile, "root", context);
            using EventRegistryRow runtime = Row(typeof(object).Assembly.Location, "runtime", "Default");
            using var set = new EventManagedArtifactSet([new(root, rootFile)], [new(runtime, typeof(object).Assembly)],
                budget, loss, CancellationToken.None);
            AppDomain.CurrentDomain.AssemblyLoad += OnLoad;

            Should.Throw<OperationCanceledException>(() => set.Load(new("root", "managed"), cancellation.Token))
                .CancellationToken.ShouldBe(cancellation.Token);
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyLoad -= OnLoad;
            Directory.Delete(directory, recursive: true);
            budget.LiveBytes.ShouldBe(0);
        }
    }

    /// <summary>Checks a later composed-memory refusal clears earlier privately captured images.</summary>
    [Fact]
    public void ComposedCapacityRefusalLeavesNoLiveReservationOrLoadedContext()
    {
        string directory = Directory.CreateTempSubdirectory("managed-set-").FullName;
        string context = "capacity-set-" + Guid.NewGuid().ToString("N");
        try
        {
            string rootFile = Emit(directory, "Root" + Guid.NewGuid().ToString("N"), 7);
            using EventRegistryRow root = Row(rootFile, "root", context);
            using EventRegistryRow runtime = Row(typeof(object).Assembly.Location, "runtime", "Default");
            // The slot/image/header admission fits, but the declared metadata workspace cannot fit.
            var budget = new EventBufferBudget(checked((int)new FileInfo(rootFile).Length * 4 + 2048));

            Should.Throw<InvalidOperationException>(() => new EventManagedArtifactSet([new(root, rootFile)],
                [new(runtime, typeof(object).Assembly)], budget, new EventEvolutionCapabilityLoss(), CancellationToken.None))
                .Message.ShouldStartWith("ScratchLimit:");
            budget.LiveBytes.ShouldBe(0);
            AssemblyLoadContext.All.ShouldNotContain(item => item.Name == context);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Checks admission forwards pre-cancellation before a missing file can be opened.</summary>
    [Fact]
    public void PreCancelledAdmissionPreservesOriginalTokenAndReservesNothing()
    {
        string directory = Directory.CreateTempSubdirectory("managed-set-").FullName;
        var budget = new EventBufferBudget();
        try
        {
            string rootFile = Emit(directory, "Root" + Guid.NewGuid().ToString("N"), 7);
            using EventRegistryRow root = Row(rootFile, "root", "private-set");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Should.Throw<OperationCanceledException>(() => new EventManagedArtifactSet([new(root, "/missing")], [],
                budget, new EventEvolutionCapabilityLoss(), cancellation.Token)).CancellationToken.ShouldBe(cancellation.Token);
            budget.LiveBytes.ShouldBe(0);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string Emit(string directory, string name, int result, string suffix = "image", MethodInfo? dependency = null,
        string? dynamicName = null, Version? version = null, bool nativeImport = false)
    {
        var assembly = new PersistedAssemblyBuilder(new AssemblyName(name) { Version = version ?? new Version(1, 0, 0, 0) }, typeof(object).Assembly);
        TypeBuilder type = assembly.DefineDynamicModule(name).DefineType("Boundary", TypeAttributes.Public | TypeAttributes.Class);
        if (nativeImport)
        {
            MethodBuilder native = type.DefinePInvokeMethod("Read", "undeclared-native-" + Guid.NewGuid().ToString("N"), "read",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.PinvokeImpl, CallingConventions.Standard,
                typeof(int), Type.EmptyTypes, System.Runtime.InteropServices.CallingConvention.Cdecl,
                System.Runtime.InteropServices.CharSet.Ansi);
            native.SetImplementationFlags(native.GetMethodImplementationFlags() | MethodImplAttributes.PreserveSig);
            _ = type.CreateType();
            string nativeFile = Path.Combine(directory, name + "-" + suffix + ".dll");
            assembly.Save(nativeFile);
            return nativeFile;
        }
        MethodBuilder method = type.DefineMethod("Read", MethodAttributes.Public | MethodAttributes.Static,
            dynamicName is null ? typeof(int) : typeof(Assembly), Type.EmptyTypes);
        ILGenerator il = method.GetILGenerator();
        if (dependency is not null) { il.Emit(OpCodes.Call, dependency); }
        else if (dynamicName is not null)
        {
            il.Emit(OpCodes.Ldstr, dynamicName);
            il.Emit(OpCodes.Call, typeof(Assembly).GetMethod(nameof(Assembly.Load), [typeof(string)])!);
        }
        else { il.Emit(OpCodes.Ldc_I4, result); }
        il.Emit(OpCodes.Ret);
        _ = type.CreateType();
        string file = Path.Combine(directory, name + "-" + suffix + ".dll");
        assembly.Save(file);
        return file;
    }

    private static EventRegistryRow Row(string file, string logicalIdentity, string context)
    {
        using FileStream source = File.OpenRead(file);
        byte[] hash = SHA256.HashData(source);
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(0x47); writer.WriteString("d"); writer.WriteString(logicalIdentity); writer.WriteString("managed");
        writer.WriteUInt16(3);
        writer.WriteByte(1); writer.WriteString(AssemblyName.GetAssemblyName(file).Version!.ToString());
        writer.WriteByte(2); writer.WriteHash(hash);
        writer.WriteByte(3); writer.WriteString(context);
        return new EventRegistryRow(writer.CopyEncodedBytes());
    }
}

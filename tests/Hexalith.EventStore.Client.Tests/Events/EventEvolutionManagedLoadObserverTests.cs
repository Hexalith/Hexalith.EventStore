using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Exercises actual managed-load observations without claiming production loader qualification.</summary>
public sealed class EventEvolutionManagedLoadObserverTests
{
    /// <summary>Checks process mode cannot admit a scoped graph that omits the existing default runtime context.</summary>
    [Fact]
    public void ProcessModeRefusesExistingAssembliesOutsideDeclaredContexts()
    {
        var context = new AssemblyLoadContext("local-managed-observation", isCollectible: true);
        using EventDomainRegistry registry = CreateRegistry(out EventResolvedDependency node);
        try
        {
            Should.Throw<InvalidOperationException>(() => new EventEvolutionManagedLoadObserver(
                registry, [node], [node.Identity], [new("declared-context", context)], CancellationToken.None,
                requireAllManagedContexts: true)).Message.ShouldStartWith("CapabilityMismatch:");
            RequireLoss(registry);
        }
        finally { context.Unload(); }
    }

    /// <summary>Checks a declared late load and an existing declared load retain local capability.</summary>
    /// <param name="loadBeforeSubscription">Whether the assembly is loaded before observer construction.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactDeclaredLoadsAreReconciledBeforeAndAfterSubscription(bool loadBeforeSubscription)
    {
        var context = new AssemblyLoadContext("local-managed-observation", isCollectible: true);
        using EventDomainRegistry registry = CreateRegistry(out EventResolvedDependency node);
        try
        {
            if (loadBeforeSubscription) { _ = context.LoadFromAssemblyPath(node.ResolvedFile); }
            using var observer = CreateObserver(registry, node, context);
            if (!loadBeforeSubscription) { _ = context.LoadFromAssemblyPath(node.ResolvedFile); }
            observer.ReconcileCurrentLoads(CancellationToken.None);
            registry.CapabilityLoss.RequireNoObservedLoss();
        }
        finally { context.Unload(); }
    }

    /// <summary>Demonstrates that same-named unlisted runtime contexts remain outside local coverage.</summary>
    [Fact]
    public void SameNamedDifferentRuntimeContextDemonstratesScopedCoverageOnly()
    {
        var observed = new AssemblyLoadContext("same-runtime-name", isCollectible: true);
        var unobserved = new AssemblyLoadContext("same-runtime-name", isCollectible: true);
        using EventDomainRegistry registry = CreateRegistry(out EventResolvedDependency node);
        try
        {
            using var observer = CreateObserver(registry, node, observed);
            Assembly outsideCoverage = unobserved.LoadFromAssemblyPath(node.ResolvedFile);
            AssemblyLoadContext.GetLoadContext(outsideCoverage).ShouldBeSameAs(unobserved);
            observer.ReconcileCurrentLoads(CancellationToken.None);
            registry.CapabilityLoss.RequireNoObservedLoss();

            // Equal display names do not extend coverage. This successful local observation
            // proves neither process coverage nor permission to execute a catalog route there.
            Assembly withinCoverage = observed.LoadFromAssemblyPath(node.ResolvedFile);
            AssemblyLoadContext.GetLoadContext(withinCoverage).ShouldBeSameAs(observed);
            registry.CapabilityLoss.RequireNoObservedLoss();
        }
        finally
        {
            observed.Unload();
            unobserved.Unload();
        }
    }

    /// <summary>Checks explicit and reflection-triggered undeclared loads fence subsequent calls.</summary>
    /// <param name="throughReflection">Whether the load is invoked through reflection.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UndeclaredLateLoadIsDetectedWithoutPreventingTheLoad(bool throughReflection)
    {
        var context = new AssemblyLoadContext("local-managed-observation", isCollectible: true);
        using EventDomainRegistry registry = CreateRegistry(out EventResolvedDependency node);
        try
        {
            using var observer = CreateObserver(registry, node, context);
            string undeclaredFile = typeof(EventDomainRegistry).Assembly.Location;
            Assembly loaded;
            if (throughReflection)
            {
                MethodInfo load = typeof(AssemblyLoadContext).GetMethod(nameof(AssemblyLoadContext.LoadFromAssemblyPath))!;
                loaded = (Assembly)load.Invoke(context, [undeclaredFile])!;
            }
            else
            {
                loaded = context.LoadFromAssemblyPath(undeclaredFile);
            }

            loaded.ShouldNotBeNull(); // Observation detects a completed load; it is not prevention.
            RequireLoss(registry);
            Should.Throw<InvalidOperationException>(() => observer.ReconcileCurrentLoads(CancellationToken.None))
                .Message.ShouldStartWith("CapabilityMismatch:");
        }
        finally { context.Unload(); }
    }

    /// <summary>Checks startup reconciles undeclared existing loads and removes a failed observer.</summary>
    [Fact]
    public void UndeclaredExistingLoadRefusesObserverAdmission()
    {
        var context = new AssemblyLoadContext("local-managed-observation", isCollectible: true);
        using EventDomainRegistry registry = CreateRegistry(out EventResolvedDependency node);
        try
        {
            _ = context.LoadFromAssemblyPath(typeof(EventDomainRegistry).Assembly.Location);
            Should.Throw<InvalidOperationException>(() => CreateObserver(registry, node, context))
                .Message.ShouldStartWith("CapabilityMismatch:");
            RequireLoss(registry);
        }
        finally { context.Unload(); }
    }

    /// <summary>Checks stream-loaded and dynamic images refuse without disclosing executable locations.</summary>
    /// <param name="dynamicImage">Whether the observed assembly is generated dynamically.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArtifactsWithoutDeclaredFileLocationsLoseCapability(bool dynamicImage)
    {
        var context = new AssemblyLoadContext("local-managed-observation", isCollectible: true);
        using EventDomainRegistry registry = CreateRegistry(out EventResolvedDependency node);
        try
        {
            using var observer = CreateObserver(registry, node, context);
            if (dynamicImage)
            {
                using AssemblyLoadContext.ContextualReflectionScope scope = context.EnterContextualReflection();
                _ = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("undeclared-dynamic"), AssemblyBuilderAccess.RunAndCollect);
            }
            else
            {
                using FileStream file = File.OpenRead(node.ResolvedFile);
                _ = context.LoadFromStream(file);
            }

            InvalidOperationException failure = RequireLoss(registry);
            failure.Message.ShouldNotContain(node.ResolvedFile);
            failure.Message.ShouldNotContain("undeclared-dynamic");
        }
        finally { context.Unload(); }
    }

    /// <summary>Checks unchanged assembly identity cannot hide changed or missing current file bytes.</summary>
    /// <param name="removeFile">Whether the admitted file disappears instead of changing.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReconciliationDetectsChangedOrMissingCurrentArtifactWithoutExposingItsPath(bool removeFile)
    {
        string directory = Path.Combine(Path.GetTempPath(), "event-managed-observation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "declared.dll");
        File.Copy(typeof(IEventPayload).Assembly.Location, file);
        var context = new AssemblyLoadContext("local-managed-observation", isCollectible: true);
        using EventDomainRegistry registry = CreateRegistry(out EventResolvedDependency node, file);
        try
        {
            using var observer = CreateObserver(registry, node, context);
            _ = context.LoadFromAssemblyPath(file);
            registry.CapabilityLoss.RequireNoObservedLoss();
            if (removeFile) { File.Delete(file); }
            else
            {
                using FileStream mutation = File.Open(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                mutation.WriteByte(0x5a);
            }

            InvalidOperationException failure = Should.Throw<InvalidOperationException>(() => observer.ReconcileCurrentLoads(CancellationToken.None));
            failure.Message.ShouldStartWith("CapabilityMismatch:");
            failure.Message.ShouldNotContain(file);
            RequireLoss(registry);
        }
        finally
        {
            context.Unload();
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Checks ending coverage through disposal or context unload causes irreversible loss.</summary>
    /// <param name="unloadContext">Whether the context ends before observer disposal.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObservationShutdownCannotLeaveCapabilityAvailable(bool unloadContext)
    {
        var context = new AssemblyLoadContext("local-managed-observation", isCollectible: true);
        using EventDomainRegistry registry = CreateRegistry(out EventResolvedDependency node);
        try
        {
            using var observer = CreateObserver(registry, node, context);
            if (unloadContext) { context.Unload(); }
            else { observer.Dispose(); }
            RequireLoss(registry);
            observer.Dispose();
            RequireLoss(registry);
        }
        finally { if (!unloadContext) { context.Unload(); } }
    }

    /// <summary>Checks context declarations cannot alias identities or omit a required runtime scope.</summary>
    /// <param name="caseName">The invalid supplied context declaration.</param>
    [Theory]
    [InlineData("duplicate-id")]
    [InlineData("duplicate-runtime")]
    [InlineData("missing-context")]
    [InlineData("unused-context")]
    public void AmbiguousOrIncompleteRuntimeContextMappingsRefuseAdmission(string caseName)
    {
        var first = new AssemblyLoadContext("same-runtime-name", isCollectible: true);
        var second = new AssemblyLoadContext("same-runtime-name", isCollectible: true);
        using EventDomainRegistry registry = CreateRegistry(out EventResolvedDependency node);
        try
        {
            EventManagedLoadContext[] contexts = caseName switch
            {
                "duplicate-id" => [new("declared-context", first), new("declared-context", second)],
                "duplicate-runtime" => [new("declared-context", first), new("other-context", first)],
                "missing-context" => [new("other-context", first)],
                "unused-context" => [new("declared-context", first), new("other-context", second)],
                _ => throw new InvalidOperationException(),
            };
            Should.Throw<InvalidOperationException>(() => new EventEvolutionManagedLoadObserver(
                registry, [node], [node.Identity], contexts, CancellationToken.None))
                .Message.ShouldStartWith("CapabilityMismatch:");
        }
        finally
        {
            first.Unload();
            second.Unload();
        }
    }

    /// <summary>Checks original cancellation precedes observation subscription and filesystem access.</summary>
    [Fact]
    public void PreCancellationPreservesTokenAndDoesNotLoseUnobservedCapability()
    {
        var context = new AssemblyLoadContext("local-managed-observation", isCollectible: true);
        using EventDomainRegistry registry = CreateRegistry(out EventResolvedDependency node);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            Should.Throw<OperationCanceledException>(() => new EventEvolutionManagedLoadObserver(
                registry, [node with { ResolvedFile = "/missing" }], [node.Identity],
                [new("declared-context", context)], cancellation.Token)).CancellationToken.ShouldBe(cancellation.Token);
            registry.CapabilityLoss.RequireNoObservedLoss();
        }
        finally { context.Unload(); }
    }

    /// <summary>Checks a load racing startup cannot escape both reconciliation and subscription.</summary>
    [Fact]
    public async Task ConcurrentStartupAndUndeclaredLoadAlwaysLoseCapability()
    {
        for (int iteration = 0; iteration < 16; iteration++)
        {
            var context = new AssemblyLoadContext("local-managed-observation", isCollectible: true);
            using EventDomainRegistry registry = CreateRegistry(out EventResolvedDependency node);
            EventEvolutionManagedLoadObserver? observer = null;
            try
            {
                Task<Assembly> load = Task.Run(() => context.LoadFromAssemblyPath(typeof(EventDomainRegistry).Assembly.Location));
                try { observer = CreateObserver(registry, node, context); }
                catch (InvalidOperationException failure) { failure.Message.ShouldStartWith("CapabilityMismatch:"); }
                (await load).ShouldNotBeNull();
                RequireLoss(registry);
            }
            finally
            {
                observer?.Dispose();
                context.Unload();
            }
        }
    }

    /// <summary>Checks a context unloaded before subscription cannot acquire local capability.</summary>
    [Fact]
    public void AlreadyUnloadedContextRefusesObserverAdmission()
    {
        var context = new AssemblyLoadContext("local-managed-observation", isCollectible: true);
        using EventDomainRegistry registry = CreateRegistry(out EventResolvedDependency node);
        context.Unload();
        Should.Throw<InvalidOperationException>(() => CreateObserver(registry, node, context))
            .Message.ShouldStartWith("CapabilityMismatch:");
        RequireLoss(registry);
    }

    /// <summary>Checks a supplied version claim must also match the actual managed artifact metadata.</summary>
    [Fact]
    public void ManagedMetadataVersionCannotBeReplacedByASuppliedGraphClaim()
    {
        var context = new AssemblyLoadContext("local-managed-observation", isCollectible: true);
        using EventDomainRegistry registry = CreateRegistry(out EventResolvedDependency node, declaredVersion: "123.0.0.0");
        try
        {
            Should.Throw<InvalidOperationException>(() => CreateObserver(registry, node, context))
                .Message.ShouldStartWith("CapabilityMismatch:");
            RequireLoss(registry);
        }
        finally { context.Unload(); }
    }

    /// <summary>Checks distinct logical aliases cannot admit the same managed identity twice in one context.</summary>
    [Fact]
    public void AmbiguousManagedAssemblyIdentityRefusesAdmission()
    {
        var context = new AssemblyLoadContext("local-managed-observation", isCollectible: true);
        using EventDomainRegistry first = CreateRegistry(out EventResolvedDependency node);
        ReadOnlyMemory<byte>[] rows = [.. first.Rows.Select(static row => (ReadOnlyMemory<byte>)row.Encoded.ToArray()),
            CreateDependencyRow(node.ResolvedFile, "another-managed", node.ResolvedVersionOrAbiIdentity)];
        using var registry = new EventDomainRegistry("d", rows, new EventEvolutionCapabilityLoss());
        EventResolvedDependency alias = node with { Identity = new("another-managed", "managed") };
        try
        {
            Should.Throw<InvalidOperationException>(() => new EventEvolutionManagedLoadObserver(
                registry, [node, alias], [node.Identity, alias.Identity], [new("declared-context", context)], CancellationToken.None))
                .Message.ShouldStartWith("CapabilityMismatch:");
            RequireLoss(registry);
        }
        finally { context.Unload(); }
    }

    private static EventEvolutionManagedLoadObserver CreateObserver(
        EventDomainRegistry registry, EventResolvedDependency node, AssemblyLoadContext context)
        => new(registry, [node], [node.Identity], [new("declared-context", context)], CancellationToken.None);

    private static InvalidOperationException RequireLoss(EventDomainRegistry registry)
    {
        InvalidOperationException failure = Should.Throw<InvalidOperationException>(registry.CapabilityLoss.RequireNoObservedLoss);
        failure.Message.ShouldStartWith("CapabilityMismatch:");
        return failure;
    }

    private static EventDomainRegistry CreateRegistry(out EventResolvedDependency node, string? file = null,
        string? declaredVersion = null)
    {
        file ??= typeof(IEventPayload).Assembly.Location;
        string version = declaredVersion ?? AssemblyName.GetAssemblyName(file).Version!.ToString();
        using EventDomainRegistry fixture = EventUpcastChainExecutorTests.CreateRegistry();
        ReadOnlyMemory<byte>[] rows = [.. fixture.Rows.Select(static row => (ReadOnlyMemory<byte>)row.Encoded.ToArray()),
            CreateDependencyRow(file, "observed-managed", version)];
        node = new EventResolvedDependency(new("observed-managed", "managed"), version, "declared-context", file, []);
        return new EventDomainRegistry("d", rows, new EventEvolutionCapabilityLoss());
    }

    private static byte[] CreateDependencyRow(string file, string logicalIdentity, string version)
    {
        using FileStream artifact = File.OpenRead(file);
        byte[] hash = SHA256.HashData(artifact);
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(0x47);
        writer.WriteString("d");
        writer.WriteString(logicalIdentity);
        writer.WriteString("managed");
        writer.WriteUInt16(3);
        writer.WriteByte(1); writer.WriteString(version);
        writer.WriteByte(2); writer.WriteHash(hash);
        writer.WriteByte(3); writer.WriteString("declared-context");
        return writer.CopyEncodedBytes();
    }
}

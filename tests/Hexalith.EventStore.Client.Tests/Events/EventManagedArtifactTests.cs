using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Qualifies dormant direct-image binding without admitting a complete execution graph.</summary>
public sealed class EventManagedArtifactTests
{
    /// <summary>Checks source replacement cannot change the private loaded image or its actual bound callback.</summary>
    /// <param name="removeFile">Whether the admitted source is removed instead of replaced.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrivateAdmissionSurvivesSourceReplacementAndBindsActualCallback(bool removeFile)
    {
        string file = Path.GetTempFileName();
        File.Copy(typeof(IEventPayload).Assembly.Location, file, overwrite: true);
        using EventRegistryRow row = CreateRow(file);
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        try
        {
            using var artifact = new EventManagedArtifact(row, file, budget, loss, CancellationToken.None);
            budget.LiveBytes.ShouldBeGreaterThan(checked((int)new FileInfo(file).Length));
            if (removeFile) { File.Delete(file); }
            else { File.WriteAllBytes(file, "replaceable path no longer contains the admitted image"u8.ToArray()); }

            using EventManagedArtifactExecutionBinding execution = artifact.Load(CancellationToken.None);
            execution.Assembly.Location.ShouldBeEmpty();
            using EventManagedArtifactExecutionBinding repeated = artifact.Load(CancellationToken.None);
            repeated.Assembly.ShouldBeSameAs(execution.Assembly);
            Func<int, string, int> callback = GetCallback(execution.Assembly);
            var implementation = new EventImplementationBinding("private-validator", callback, "{}\n"u8.ToArray(), [],
                executionBinding: execution);
            using EventRegistryRow descriptor = CreateVersionRow(row.GetEncodedField(2));
            implementation.RequireFields(descriptor, 1);
            callback(7, "version").ShouldBe(7);
            Should.Throw<ArgumentOutOfRangeException>(() => callback(0, "version"));
            loss.RequireNoObservedLoss();
        }
        finally
        {
            File.Delete(file);
            budget.LiveBytes.ShouldBe(0);
        }
    }

    /// <summary>Checks a same-identity assembly from another context cannot borrow direct image evidence.</summary>
    [Fact]
    public void ForeignAssemblyObjectAndForgedBindingRefuseBeforeCallbackUse()
    {
        string file = typeof(IEventPayload).Assembly.Location;
        using EventRegistryRow row = CreateRow(file);
        using var artifact = new EventManagedArtifact(row, file, new EventBufferBudget(), new EventEvolutionCapabilityLoss(), CancellationToken.None);
        using EventManagedArtifactExecutionBinding execution = artifact.Load(CancellationToken.None);
        var foreignContext = new AssemblyLoadContext("foreign-same-identity", isCollectible: true);
        try
        {
            Assembly foreign = foreignContext.LoadFromAssemblyPath(file);
            foreign.FullName.ShouldBe(execution.Assembly.FullName);
            foreign.ShouldNotBeSameAs(execution.Assembly);
            Should.Throw<InvalidOperationException>(() => new EventImplementationBinding("private-validator",
                GetCallback(foreign), "{}\n"u8.ToArray(), [], executionBinding: execution))
                .Message.ShouldStartWith("CapabilityMismatch:");
            Should.Throw<InvalidOperationException>(() => new EventManagedArtifactExecutionBinding(artifact, foreign))
                .Message.ShouldStartWith("CapabilityMismatch:");
        }
        finally { foreignContext.Unload(); }
    }

    /// <summary>Checks artifact disposal clears full private capacity and ends retained implementation evidence.</summary>
    [Fact]
    public void ArtifactDisposalClearsImageBeforeReleaseAndFencesRetainedImplementation()
    {
        string file = typeof(IEventPayload).Assembly.Location;
        using EventRegistryRow row = CreateRow(file);
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        using var artifact = new EventManagedArtifact(row, file, budget, loss, CancellationToken.None);
        byte[] privateStorage = (byte[])typeof(EventManagedArtifact).GetField("_image", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(artifact)!;
        privateStorage.Any(static value => value != 0).ShouldBeTrue();
        using EventManagedArtifactExecutionBinding execution = artifact.Load(CancellationToken.None);
        var implementation = new EventImplementationBinding("private-validator", GetCallback(execution.Assembly), "{}\n"u8.ToArray(), [],
            executionBinding: execution);
        using EventRegistryRow descriptor = CreateVersionRow(row.GetEncodedField(2));
        implementation.RequireFields(descriptor, 1);

        artifact.Dispose();

        privateStorage.All(static value => value == 0).ShouldBeTrue();
        budget.LiveBytes.ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => artifact.Load(CancellationToken.None));
        Should.Throw<ObjectDisposedException>(() => implementation.RequireFields(descriptor, 1));
        Should.Throw<InvalidOperationException>(loss.RequireNoObservedLoss);
    }

    /// <summary>Checks binding disposal or explicit context unload ends use before descriptor acceptance.</summary>
    /// <param name="unloadContext">Whether the context is unloaded instead of ending the binding.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindingDisposalOrContextUnloadFencesRetainedImplementation(bool unloadContext)
    {
        string file = typeof(IEventPayload).Assembly.Location;
        using EventRegistryRow row = CreateRow(file);
        var loss = new EventEvolutionCapabilityLoss();
        using var artifact = new EventManagedArtifact(row, file, new EventBufferBudget(), loss, CancellationToken.None);
        using EventManagedArtifactExecutionBinding execution = artifact.Load(CancellationToken.None);
        var implementation = new EventImplementationBinding("private-validator", GetCallback(execution.Assembly), "{}\n"u8.ToArray(), [],
            executionBinding: execution);
        using EventRegistryRow descriptor = CreateVersionRow(row.GetEncodedField(2));
        if (unloadContext)
        {
            AssemblyLoadContext.GetLoadContext(execution.Assembly)!.Unload();
            Should.Throw<InvalidOperationException>(() => implementation.RequireFields(descriptor, 1));
        }
        else
        {
            execution.Dispose();
            Should.Throw<ObjectDisposedException>(() => implementation.RequireFields(descriptor, 1));
        }

        Should.Throw<InvalidOperationException>(loss.RequireNoObservedLoss);
    }

    /// <summary>Checks admission and metadata work release every capacity on wrong hashes or versions.</summary>
    /// <param name="wrongVersion">Whether metadata differs instead of the pinned byte hash.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidPinsAndMetadataRefuseBeforeLoadingAndReleasePrivateImage(bool wrongVersion)
    {
        string file = typeof(IEventPayload).Assembly.Location;
        using EventRegistryRow row = CreateRow(file, wrongVersion ? "99.0.0.0" : null, wrongHash: !wrongVersion);
        var budget = new EventBufferBudget();
        Should.Throw<InvalidOperationException>(() => new EventManagedArtifact(
            row, file, budget, new EventEvolutionCapabilityLoss(), CancellationToken.None))
            .Message.ShouldStartWith("CapabilityMismatch:");
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks configured artifact and composed workspace refusals precede retained allocation.</summary>
    /// <param name="workspaceLimit">Whether the shared workspace refuses instead of the artifact limit.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapacityRefusalsReleaseEveryCharge(bool workspaceLimit)
    {
        string file = typeof(IEventPayload).Assembly.Location;
        using EventRegistryRow row = CreateRow(file);
        int length = checked((int)new FileInfo(file).Length);
        var budget = new EventBufferBudget(workspaceLimit ? length : 128 * 1024 * 1024);
        Should.Throw<InvalidOperationException>(() => new EventManagedArtifact(
            row, file, budget, new EventEvolutionCapabilityLoss(), CancellationToken.None,
            maximumArtifactBytes: workspaceLimit ? 64 * 1024 * 1024 : 1))
            .Message.ShouldStartWith(workspaceLimit ? "ScratchLimit:" : "RegistryLimit:");
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks pre-cancellation forwards its token before opening an unavailable file.</summary>
    [Fact]
    public void PreCancelledAdmissionDoesNotOpenFileOrReserveImage()
    {
        using EventRegistryRow row = CreateRow(typeof(IEventPayload).Assembly.Location);
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Should.Throw<OperationCanceledException>(() => new EventManagedArtifact(
            row, "/missing-private-artifact", budget, loss, cancellation.Token)).CancellationToken.ShouldBe(cancellation.Token);
        budget.LiveBytes.ShouldBe(0);
        loss.RequireNoObservedLoss();
    }

    /// <summary>Checks cancellation or observed loss during loading refuses the uncommitted binding.</summary>
    /// <param name="cancel">Whether to cancel instead of observing policy loss.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LossOrCancellationDuringLoadRefusesReturnedBinding(bool cancel)
    {
        string file = typeof(IEventPayload).Assembly.Location;
        string contextId = "private-image-" + Guid.NewGuid().ToString("N");
        using EventRegistryRow row = CreateRow(file, contextId: contextId);
        var loss = new EventEvolutionCapabilityLoss();
        using var artifact = new EventManagedArtifact(row, file, new EventBufferBudget(), loss, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        void OnLoad(object? sender, AssemblyLoadEventArgs arguments)
        {
            if (AssemblyLoadContext.GetLoadContext(arguments.LoadedAssembly)?.Name != contextId) { return; }
            if (cancel) { cancellation.Cancel(); }
            else { loss.ObserveViolation(); }
        }

        AppDomain.CurrentDomain.AssemblyLoad += OnLoad;
        try
        {
            if (cancel)
            {
                Should.Throw<OperationCanceledException>(() => artifact.Load(cancellation.Token))
                    .CancellationToken.ShouldBe(cancellation.Token);
            }
            else
            {
                Should.Throw<InvalidOperationException>(() => artifact.Load(cancellation.Token))
                    .Message.ShouldStartWith("CapabilityMismatch:");
            }
        }
        finally { AppDomain.CurrentDomain.AssemblyLoad -= OnLoad; }
    }

    /// <summary>Checks unreadable artifact failures reveal no executable path.</summary>
    [Fact]
    public void MissingArtifactRefusalIsSupportSafeAndRetainsNoCapacity()
    {
        using EventRegistryRow row = CreateRow(typeof(IEventPayload).Assembly.Location);
        var budget = new EventBufferBudget();
        string missing = Path.Combine(Path.GetTempPath(), "missing-private-artifact-" + Guid.NewGuid().ToString("N"));
        InvalidOperationException failure = Should.Throw<InvalidOperationException>(() => new EventManagedArtifact(
            row, missing, budget, new EventEvolutionCapabilityLoss(), CancellationToken.None));
        failure.Message.ShouldStartWith("CapabilityMismatch:");
        failure.Message.ShouldNotContain(missing);
        failure.InnerException.ShouldBeNull();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks reflective corruption of private storage is detected before any assembly load.</summary>
    [Fact]
    public void ChangedRetainedPrivateImageRefusesBeforeLoadAndClearsItsCapacity()
    {
        string file = typeof(IEventPayload).Assembly.Location;
        using EventRegistryRow row = CreateRow(file);
        var budget = new EventBufferBudget();
        var loss = new EventEvolutionCapabilityLoss();
        using var artifact = new EventManagedArtifact(row, file, budget, loss, CancellationToken.None);
        byte[] privateStorage = (byte[])typeof(EventManagedArtifact).GetField("_image", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(artifact)!;
        privateStorage[^1] ^= 1;

        Should.Throw<InvalidOperationException>(() => artifact.Load(CancellationToken.None)).Message.ShouldStartWith("CapabilityMismatch:");

        typeof(EventManagedArtifact).GetField("_assembly", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(artifact).ShouldBeNull();
        privateStorage.All(static value => value == 0).ShouldBeTrue();
        budget.LiveBytes.ShouldBe(0);
        Should.Throw<InvalidOperationException>(loss.RequireNoObservedLoss);
    }

    /// <summary>Checks the owner privately retains its declaration after the caller disposes the original row.</summary>
    [Fact]
    public void CallerDeclarationDisposalCannotChangeAdmittedPrivateImage()
    {
        string file = typeof(IEventPayload).Assembly.Location;
        using EventRegistryRow declaration = CreateRow(file);
        using var artifact = new EventManagedArtifact(declaration, file, new EventBufferBudget(),
            new EventEvolutionCapabilityLoss(), CancellationToken.None);
        declaration.Dispose();

        using EventManagedArtifactExecutionBinding binding = artifact.Load(CancellationToken.None);

        GetCallback(binding.Assembly)(7, "version").ShouldBe(7);
    }

    private static Func<int, string, int> GetCallback(Assembly assembly)
        => assembly.GetType("Hexalith.EventStore.Contracts.Events.EventContractIdentityValidator", throwOnError: true)!
            .GetMethod("ValidatePayloadVersion", BindingFlags.Public | BindingFlags.Static)!
            .CreateDelegate<Func<int, string, int>>();

    private static EventRegistryRow CreateRow(string file, string? version = null, bool wrongHash = false,
        string contextId = "private-image-context")
    {
        using FileStream source = File.OpenRead(file);
        byte[] hash = SHA256.HashData(source);
        if (wrongHash) { hash[0] ^= 1; }
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(0x47); writer.WriteString("d"); writer.WriteString("private-managed"); writer.WriteString("managed");
        writer.WriteUInt16(3);
        writer.WriteByte(1); writer.WriteString(version ?? AssemblyName.GetAssemblyName(file).Version!.ToString());
        writer.WriteByte(2); writer.WriteHash(hash);
        writer.WriteByte(3); writer.WriteString(contextId);
        return new EventRegistryRow(writer.CopyEncodedBytes());
    }

    private static EventRegistryRow CreateVersionRow(ReadOnlySpan<byte> assemblyHash)
    {
        byte[] optionsHash = EventOptionsManifestCodec.ComputeHash("{}\n"u8.ToArray(), []);
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(0x56); writer.WriteString("d"); writer.WriteString("evt"); writer.WriteInt32(1);
        writer.WriteUInt16(10);
        writer.WriteByte(1); writer.WriteString("private-validator");
        writer.WriteByte(2); writer.WriteHash(assemblyHash);
        writer.WriteByte(3); writer.WriteHash(optionsHash);
        writer.WriteByte(4); writer.WriteString("test-serializer");
        writer.WriteByte(5); writer.WriteHash(assemblyHash);
        writer.WriteByte(6); writer.WriteHash(optionsHash);
        writer.WriteByte(7); writer.WriteString("json");
        writer.WriteByte(8); writer.WriteString("test-identity");
        writer.WriteByte(9); writer.WriteHash(assemblyHash);
        writer.WriteByte(10); writer.WriteHash(optionsHash);
        return new EventRegistryRow(writer.CopyEncodedBytes());
    }
}

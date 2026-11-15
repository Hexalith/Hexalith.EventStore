using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Testing.CatalogInvocation;
using Hexalith.EventStore.Testing.OriginalSource;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Hexalith.EventStore.ServiceDefaults.Routing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Testing.Tests;

/// <summary>Independent original-primary append, refusal, bounded source pages and process-replacement persistence.</summary>
public sealed class SyntheticOriginalSourceTests : IDisposable
{
    private const string Run = "0123456789abcdef";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "original-source-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates only the disposable owner-private source root.</summary>
    public SyntheticOriginalSourceTests()
    {
        Directory.CreateDirectory(_directory);
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>Normal composition is inert and wrong environment, run, scope, mode, catalog or IPC permissions refuse.</summary>
    [Fact]
    public void ExplicitPrivateSelectionIsRequired()
    {
        var services = new ServiceCollection();
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Production");
        services.AddPrivateSyntheticOriginalSource(new ConfigurationBuilder().Build(), environment); services.ShouldBeEmpty();
        var values = new Dictionary<string, string?>
        {
            ["EventStore:Testing:OriginalSource:Enabled"] = "true",
            ["EventStore:Testing:OriginalSource:Mode"] = SyntheticOriginalSourceOptions.SelectedMode,
            ["EventStore:Testing:OriginalSource:RunId"] = Run,
            ["EventStore:Testing:OriginalSource:Tenant"] = "durability-" + Run,
            ["EventStore:Testing:OriginalSource:Domain"] = "folders",
            ["EventStore:Testing:OriginalSource:AggregatePrefix"] = "capacity-",
            ["EventStore:Testing:OriginalSource:Directory"] = _directory,
            ["Authentication:WorkloadIssuer:Workload"] = "eventstore-" + Run,
        };
        foreach (string fault in new[] { "environment", "Mode", "RunId", "Tenant", "Domain", "AggregatePrefix", "owner" })
        {
            environment.EnvironmentName.Returns(fault == "environment" ? "Production" : "Development");
            var current = new Dictionary<string, string?>(values);
            if (fault != "environment") current[fault == "owner" ? "Authentication:WorkloadIssuer:Workload" : "EventStore:Testing:OriginalSource:" + fault] = "other";
            Should.Throw<InvalidOperationException>(() => new ServiceCollection().AddPrivateSyntheticOriginalSource(
                new ConfigurationBuilder().AddInMemoryCollection(current).Build(), environment));
        }
        environment.EnvironmentName.Returns("Development");
#if DEBUG
        services.AddPrivateSyntheticOriginalSource(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), environment);
        services.AddSingleton(Options.Create(new RouteCatalogHostOptions()));
        using var provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<Hexalith.EventStore.Server.Events.INativeSourceScanSource>());
#else
        Should.Throw<InvalidOperationException>(() => services.AddPrivateSyntheticOriginalSource(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(), environment));
        services.ShouldBeEmpty();
#endif
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead);
            Should.Throw<IOException>(() => new SyntheticOriginalSourceClient(_directory, Run));
        }
    }

    /// <summary>Original pages survive replacement, duplicates preserve bytes, and conflicting positions cannot rewrite source.</summary>
    [Theory]
    [InlineData(10_001)]
    [InlineData(32_769)]
    public async Task BoundedAppendAndRestartPreserveOriginals(int count)
    {
        var selection = Selection();
        var originals = Enumerable.Range(1, count).Select(Event).ToArray();
        using (var owner = new SyntheticOriginalSourceStore(selection))
            foreach (ProjectionEventDto[] page in originals.Chunk(255)) (await owner.AppendAsync(Request(page), TestContext.Current.CancellationToken)).ShouldBeTrue();
        // A fresh owner instance opens only independently retained original files.
        using var restarted = new SyntheticOriginalSourceStore(selection);
        foreach (ProjectionEventDto[] page in originals.Chunk(255)) (await restarted.AppendAsync(Request(page), TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await restarted.AppendAsync(Request([originals[0] with { Payload = [9] }]), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await restarted.AppendAsync(Request([originals[0] with { MessageId = "other" }]), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await restarted.AppendAsync(Request([Event(count + 2)]), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await restarted.AppendAsync(Request([Event(count + 1)]), TestContext.Current.CancellationToken)).ShouldBeTrue();
        for (long from = 0; from < count + 1; from += 255)
        {
            StreamReadPage page = await restarted.ReadAsync(Read(from, count + 1), TestContext.Current.CancellationToken);
            page.Events.Count.ShouldBeLessThanOrEqualTo(255);
            foreach (StreamReadEvent source in page.Events)
            {
                var original = Event(checked((int)source.SequenceNumber));
                source.Payload.ShouldBe(original.Payload); source.MessageId.ShouldBe(original.MessageId);
                source.Timestamp.ShouldBe(original.Timestamp); source.GlobalPosition.ShouldBe(original.GlobalPosition);
            }
        }
        foreach (string path in Directory.GetFiles(_directory, "*.page")) new FileInfo(path).Length.ShouldBeLessThanOrEqualTo(2_097_152L);
    }

    /// <summary>Selected missing, incomplete, corrupt and oversized sources refuse without reconstructing from derived state.</summary>
    [Fact]
    public async Task MissingCorruptIncompleteAndOversizeRefuse()
    {
        using var owner = new SyntheticOriginalSourceStore(Selection());
        owner.IsSelected(new("durability-" + Run, "folders", "capacity-missing")).ShouldBeTrue();
        owner.IsSelected(new("durability-" + Run, "folders", "ordinary")).ShouldBeFalse();
        await Should.ThrowAsync<IOException>(() => owner.ReadAsync(Read(0, 0), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<InvalidOperationException>(() => owner.AppendAsync(Request([Event(1)]) with { Tenant = "other" }, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<InvalidOperationException>(() => owner.AppendAsync(Request(Enumerable.Range(1, 256).Select(Event).ToArray()), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<InvalidOperationException>(() => owner.AppendAsync(Request([Event(1) with { Payload = new byte[2_097_152] }]), TestContext.Current.CancellationToken));
        (await owner.AppendAsync(Request([Event(1)]), TestContext.Current.CancellationToken)).ShouldBeTrue();
        string pagePath = Directory.GetFiles(_directory, "*.page").Single();
        byte[] bytes = await File.ReadAllBytesAsync(pagePath, TestContext.Current.CancellationToken);
        var page = JsonSerializer.Deserialize<SyntheticOriginalSourcePage>(bytes, CatalogInvocationFiles.Json)!;
        await File.WriteAllBytesAsync(pagePath, JsonSerializer.SerializeToUtf8Bytes(page with { Events = [page.Events[0] with { Payload = [9] }] }, CatalogInvocationFiles.Json), TestContext.Current.CancellationToken);
        await Should.ThrowAsync<IOException>(() => owner.ReadAsync(Read(0, 0), TestContext.Current.CancellationToken));
        await File.WriteAllBytesAsync(pagePath, bytes, TestContext.Current.CancellationToken);
        File.Delete(pagePath);
        await Should.ThrowAsync<IOException>(() => owner.ReadAsync(Read(0, 0), TestContext.Current.CancellationToken));
    }

    /// <summary>A request expiring while queued never commits after acquiring the append gate.</summary>
    [Fact]
    public async Task QueuedExpiryCannotCommitOriginals()
    {
        using var owner = new SyntheticOriginalSourceStore(Selection());
        var gate = (SemaphoreSlim)typeof(SyntheticOriginalSourceStore).GetField("_appendGate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(owner)!;
        await gate.WaitAsync(TestContext.Current.CancellationToken);
        var request = Request([Event(1)]) with { ExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds(100) };
        Task<bool> pending = owner.AppendAsync(request, TestContext.Current.CancellationToken);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        gate.Release();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        Directory.GetFiles(_directory, "*.head").ShouldBeEmpty(); Directory.GetFiles(_directory, "*.page").ShouldBeEmpty();
    }

    /// <summary>Canceled head publication retires only its verified uncommitted page after actual I/O ends.</summary>
    [Fact]
    public async Task InterruptedCommitRetiresItsUncommittedPage()
    {
        using var cancellation = new CancellationTokenSource();
        using var owner = new SyntheticOriginalSourceStore(Selection(), () => cancellation.Cancel());
        await Should.ThrowAsync<OperationCanceledException>(() => owner.AppendAsync(Request([Event(1)]), cancellation.Token));
        Directory.GetFiles(_directory, "*.head").ShouldBeEmpty(); Directory.GetFiles(_directory, "*.page").ShouldBeEmpty();
        Directory.GetFiles(_directory, "*.tmp").ShouldBeEmpty();
    }

    /// <summary>Restart retires valid crash-left pages outside every committed index, preserving all committed bytes.</summary>
    [Fact]
    public async Task RestartRetiresOnlyVerifiedUncommittedPages()
    {
        using (var owner = new SyntheticOriginalSourceStore(Selection()))
            (await owner.AppendAsync(Request([Event(1)]), TestContext.Current.CancellationToken)).ShouldBeTrue();
        string headPath = Directory.GetFiles(_directory, "*.head").Single();
        string committedPage = Directory.GetFiles(_directory, "*.page").Single();
        byte[] headBytes = await File.ReadAllBytesAsync(headPath, TestContext.Current.CancellationToken);
        byte[] originalBytes = await File.ReadAllBytesAsync(committedPage, TestContext.Current.CancellationToken);
        var head = JsonSerializer.Deserialize<SyntheticOriginalSourceHead>(headBytes, CatalogInvocationFiles.Json)!;
        var original = JsonSerializer.Deserialize<SyntheticOriginalSourcePage>(originalBytes, CatalogInvocationFiles.Json)!;
        var next = original.Events[0] with { SequenceNumber = 2, GlobalPosition = 3, MessageId = "original-message-2", Payload = BitConverter.GetBytes(2) };
        var identity = new AggregateIdentity("durability-" + Run, "folders", "capacity-original");
        var proof = Hexalith.EventStore.Client.Streams.NativeSourceProofCodec.ExtendDigest(head.Proof, identity, 2, next.MessageId,
            Hexalith.EventStore.Server.Events.SourceProofCodec.ComputeEventHash(next));
        var orphan = new SyntheticOriginalSourcePage(Run, head.PageId, head.Proof, [next], proof);
        var files = new CatalogInvocationFiles(_directory);
        string orphanId = Guid.NewGuid().ToString("N");
        await files.WriteAsync(orphanId, ".page", orphan, TestContext.Current.CancellationToken);
        using (var restarted = new SyntheticOriginalSourceStore(Selection()))
        {
            File.Exists(files.PathFor(orphanId, ".page")).ShouldBeFalse();
            (await restarted.ReadAsync(Read(0, 1), TestContext.Current.CancellationToken)).Events.ShouldHaveSingleItem();
        }
        (await File.ReadAllBytesAsync(headPath, TestContext.Current.CancellationToken)).ShouldBe(headBytes);
        (await File.ReadAllBytesAsync(committedPage, TestContext.Current.CancellationToken)).ShouldBe(originalBytes);
        await files.WriteAsync(orphanId, ".page", orphan with { Events = [next with { Payload = [99] }] }, TestContext.Current.CancellationToken);
        Should.Throw<IOException>(() => new SyntheticOriginalSourceStore(Selection()));
        File.Exists(files.PathFor(orphanId, ".page")).ShouldBeTrue();
        (await File.ReadAllBytesAsync(committedPage, TestContext.Current.CancellationToken)).ShouldBe(originalBytes);
    }

    /// <summary>A corrupt middle address must refuse reopening before recovery can delete any committed original page.</summary>
    [Fact]
    public async Task CorruptMiddleIndexPreservesEveryCommittedOriginalPage()
    {
        using (var owner = new SyntheticOriginalSourceStore(Selection()))
            for (int sequence = 1; sequence <= 3; sequence++)
                (await owner.AppendAsync(Request([Event(sequence)]), TestContext.Current.CancellationToken)).ShouldBeTrue();
        var originals = Directory.GetFiles(_directory, "*.page").ToDictionary(path => path, File.ReadAllBytes);
        originals.Count.ShouldBe(3);
        string path = Directory.GetFiles(_directory, "*.head").Single();
        var head = JsonSerializer.Deserialize<SyntheticOriginalSourceHead>(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken), CatalogInvocationFiles.Json)!;
        head.Pages.Length.ShouldBe(3);
        var addresses = head.Pages.ToArray(); addresses[1] = addresses[1] with { PageId = Guid.NewGuid().ToString("N") };
        byte[] corrupted = JsonSerializer.SerializeToUtf8Bytes(head with { Pages = addresses }, CatalogInvocationFiles.Json);
        await File.WriteAllBytesAsync(path, corrupted, TestContext.Current.CancellationToken);
        Exception? refusal = Record.Exception(() => { using var reopened = new SyntheticOriginalSourceStore(Selection()); });
        foreach (var original in originals)
        {
            File.Exists(original.Key).ShouldBeTrue();
            (await File.ReadAllBytesAsync(original.Key, TestContext.Current.CancellationToken)).ShouldBe(original.Value);
        }
        refusal.ShouldBeOfType<IOException>();
        (await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)).ShouldBe(corrupted);
    }

    /// <summary>Directory ownership excludes simultaneous owners and is released on disposal or process exit without an extra storage format.</summary>
    [Fact]
    public async Task ExclusiveDirectoryOwnershipSurvivesUntilOwnerEnds()
    {
        using (var first = new SyntheticOriginalSourceStore(Selection()))
            Should.Throw<IOException>(() => new SyntheticOriginalSourceStore(Selection()));
        using (var reopened = new SyntheticOriginalSourceStore(Selection())) reopened.IsSelected(new("durability-" + Run, "folders", "capacity-original")).ShouldBeTrue();
        var start = new System.Diagnostics.ProcessStartInfo("flock") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { "-n", _directory, "sh", "-c", "printf 'owned\\n'; sleep 0.3" }) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)!;
        try
        {
            (await process.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken)).ShouldBe("owned");
            Should.Throw<IOException>(() => new SyntheticOriginalSourceStore(Selection()));
            await process.WaitForExitAsync(TestContext.Current.CancellationToken); process.ExitCode.ShouldBe(0);
            using var reopened = new SyntheticOriginalSourceStore(Selection());
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A replacement worker recovers live claims and retires expired claims/results under their original deadlines.</summary>
    [Fact]
    public async Task WorkerRecoversAndRetiresCrashLeftControls()
    {
        var files = new CatalogInvocationFiles(_directory);
        var live = Request([Event(1)]) with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(1) };
        var expired = Request([Event(2)]) with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) };
        await files.WriteAsync(live.Id, ".working", live, TestContext.Current.CancellationToken);
        await files.WriteAsync(expired.Id, ".working", expired, TestContext.Current.CancellationToken);
        using var owner = new SyntheticOriginalSourceStore(Selection());
        using var worker = new SyntheticOriginalSourceWorker(Selection(), owner);
        await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitUntilAsync(() => File.Exists(files.PathFor(live.Id, ".response")));
            (await files.ReadAsync<SyntheticOriginalSourceAppendResult>(live.Id, ".response", TestContext.Current.CancellationToken)).Failure.ShouldBeNull();
            await WaitUntilAsync(() => !File.Exists(files.PathFor(live.Id, ".working")) && !File.Exists(files.PathFor(expired.Id, ".working")));
            Directory.GetFiles(_directory, "*.response").ShouldBeEmpty();
            (await owner.ReadAsync(Read(0, 1), TestContext.Current.CancellationToken)).Events.ShouldHaveSingleItem();
        }
        finally { await worker.StopAsync(TestContext.Current.CancellationToken); }
    }

    /// <summary>Deadline abandonment retires a queued control only after the actual append await has ended.</summary>
    [Fact]
    public async Task WorkerRetiresTimedOutClaimAfterActualAppendEnds()
    {
        using var owner = new SyntheticOriginalSourceStore(Selection());
        var gate = (SemaphoreSlim)typeof(SyntheticOriginalSourceStore).GetField("_appendGate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(owner)!;
        await gate.WaitAsync(TestContext.Current.CancellationToken);
        var files = new CatalogInvocationFiles(_directory);
        var request = Request([Event(1)]) with { ExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds(100) };
        await files.WriteAsync(request.Id, ".working", request, TestContext.Current.CancellationToken);
        using var worker = new SyntheticOriginalSourceWorker(Selection(), owner);
        await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitUntilAsync(() => !File.Exists(files.PathFor(request.Id, ".working")));
            Directory.GetFiles(_directory, "*.response").ShouldBeEmpty(); Directory.GetFiles(_directory, "*.head").ShouldBeEmpty();
        }
        finally { gate.Release(); await worker.StopAsync(TestContext.Current.CancellationToken); }
    }

    /// <summary>Restart retires a verified response left after client consumption without replaying or changing committed originals.</summary>
    [Fact]
    public async Task WorkerRetiresConsumedOrphanResponseWithoutReplayingSource()
    {
        var files = new CatalogInvocationFiles(_directory);
        var request = Request([Event(1)]);
        await files.WriteAsync(request.Id, ".working", request, TestContext.Current.CancellationToken);
        using (var owner = new SyntheticOriginalSourceStore(Selection()))
        using (var worker = new SyntheticOriginalSourceWorker(Selection(), owner))
        {
            await worker.StartAsync(TestContext.Current.CancellationToken);
            try { await WaitUntilAsync(() => File.Exists(files.PathFor(request.Id, ".response"))); }
            finally { await worker.StopAsync(TestContext.Current.CancellationToken); }
        }
        var completed = await files.ReadAsync<SyntheticOriginalSourceAppendResult>(request.Id, ".response", TestContext.Current.CancellationToken);
        completed.Id.ShouldBe(request.Id); completed.Failure.ShouldBeNull();
        var originals = Directory.GetFiles(_directory).Where(path => path.EndsWith(".head", StringComparison.Ordinal)
            || path.EndsWith(".page", StringComparison.Ordinal)).ToDictionary(path => path, File.ReadAllBytes);
        files.Delete(request.Id, ".working"); // Client consumed the result and exited before its second unlink.
        using var restarted = new SyntheticOriginalSourceStore(Selection());
        using var recovery = new SyntheticOriginalSourceWorker(Selection(), restarted);
        await recovery.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            while (File.Exists(files.PathFor(request.Id, ".response")) && elapsed.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(10, TestContext.Current.CancellationToken);
            File.Exists(files.PathFor(request.Id, ".response")).ShouldBeFalse();
            Directory.GetFiles(_directory, "*.request").ShouldBeEmpty(); Directory.GetFiles(_directory, "*.working").ShouldBeEmpty();
            foreach (var original in originals) (await File.ReadAllBytesAsync(original.Key, TestContext.Current.CancellationToken)).ShouldBe(original.Value);
            Directory.GetFiles(_directory).Length.ShouldBe(originals.Count);
            // A different identity in a private response is not this completed control and must remain untouched.
            string mismatched = Guid.NewGuid().ToString("N");
            await files.WriteAsync(mismatched, ".response", completed, TestContext.Current.CancellationToken);
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await recovery.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            File.Exists(files.PathFor(mismatched, ".response")).ShouldBeTrue();
            foreach (var original in originals) (await File.ReadAllBytesAsync(original.Key, TestContext.Current.CancellationToken)).ShouldBe(original.Value);
        }
        finally { await recovery.StopAsync(TestContext.Current.CancellationToken); }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, deadline.Token);
    }

    private SyntheticOriginalSourceOptions Selection() => new() { Mode = SyntheticOriginalSourceOptions.SelectedMode,
        Directory = _directory, RunId = Run, Tenant = "durability-" + Run, Domain = "folders", AggregatePrefix = "capacity-" };
    private static ProjectionEventDto Event(int sequence) => new("Original.Event", BitConverter.GetBytes(sequence), "json", sequence,
        DateTimeOffset.UnixEpoch.AddSeconds(sequence), "original-correlation", "original-message-" + sequence, GlobalPosition: sequence + 1) { MetadataVersion = 1 };
    private static SyntheticOriginalSourceAppend Request(ProjectionEventDto[] events) => new(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddSeconds(30), Run,
        "durability-" + Run, "folders", "capacity-original", events);
    private static StreamReadRequest Read(long from, long to) => new("durability-" + Run, "folders", "capacity-original", from, to, PageSize: 255) { RequireNativeProof = true };

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

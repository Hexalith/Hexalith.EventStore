using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Testing.CatalogInvocation;
using Hexalith.EventStore.Testing.OwnerSourceRead;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Testing.Tests;

/// <summary>Finite owner admission, SDK request identity, bounded IPC and failure propagation.</summary>
public sealed class OwnerSourceReadTests : IDisposable
{
    /// <summary>Gets whether the actual private IPC implementation is supported on this platform.</summary>
    public static bool LinuxIpcSupported => OperatingSystem.IsLinux();

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "owner-source-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates only the test-owned private IPC directory.</summary>
    public OwnerSourceReadTests()
    {
        Directory.CreateDirectory(_directory);
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>Normal hosts have no helper and non-Development or non-owner selection refuses.</summary>
    [Fact]
    public void DefaultIsInertAndWrongHostSelectionRefuses()
    {
        var services = new ServiceCollection();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        services.AddPrivateOwnerSourceRead(new ConfigurationBuilder().Build(), environment);
        services.ShouldBeEmpty();
        var selected = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:Testing:OwnerSourceRead:Enabled"] = "true",
            ["EventStore:Testing:OwnerSourceRead:Directory"] = _directory,
            ["Authentication:WorkloadIssuer:Workload"] = "tenants",
        }).Build();
        Should.Throw<InvalidOperationException>(() => services.AddPrivateOwnerSourceRead(selected, environment));
        environment.EnvironmentName.Returns("Development");
        selected["Authentication:WorkloadIssuer:Workload"] = "eventstore";
        Should.Throw<InvalidOperationException>(() => services.AddPrivateOwnerSourceRead(selected, environment));
        services.ShouldBeEmpty();
    }

    /// <summary>Expanded source targets and modes are refused before SDK invocation.</summary>
    [Theory(Skip = "Private owner-source IPC requires Linux.", SkipUnless = nameof(LinuxIpcSupported))]
    [InlineData("mode")]
    [InlineData("scope")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    [InlineData("expired")]
    [InlineData("long-deadline")]
    [InlineData("identity")]
    public async Task FiniteSelectionRefusesBeforeSdkDispatch(string fault)
    {
        int calls = 0;
        using var worker = new OwnerSourceReadWorker(new() { Directory = _directory }, (_, _) => { calls++; return Task.FromResult(Page()); });
        var request = Request();
        request = fault switch
        {
            "mode" => request with { Mode = "generic-read" },
            "scope" => request with { Scope = "platform:tenants" },
            "domain" => request with { Domain = "tenants" },
            "aggregate" => request with { AggregateId = "other" },
            "expired" => request with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) },
            "long-deadline" => request with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2) },
            _ => request with { Id = "../escape" },
        };
        await Should.ThrowAsync<Exception>(() => worker.InvokeAsync(request, TestContext.Current.CancellationToken));
        calls.ShouldBe(0);
    }

    /// <summary>The worker invokes the SDK's workload method with the original exact finite scope.</summary>
    [Fact(Skip = "Private owner-source IPC requires Linux.", SkipUnless = nameof(LinuxIpcSupported))]
    public async Task ActualSdkMethodReceivesExactScopeAndCancellation()
    {
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(Page());
        using var worker = new OwnerSourceReadWorker(new() { Directory = _directory }, gateway.ReadWorkloadStreamAsync);
        (await worker.InvokeAsync(Request(), TestContext.Current.CancellationToken)).Tenant.ShouldBe("system");
        await gateway.Received(1).ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request =>
            request.Tenant == "platform:global-administrators" && request.Domain == "global-administrators"
            && request.AggregateId == "global-administrators" && request.FromSequence == 0 && request.ToSequence == null
            && request.ContinuationToken == null && request.Checkpoint == null && request.ProjectionName == null
            && request.PageSize == 100), Arg.Is<CancellationToken>(token => token.CanBeCanceled));
    }

    /// <summary>Wrong storage identity, partial history and oversized bytes never become source proof.</summary>
    [Theory(Skip = "Private owner-source IPC requires Linux.", SkipUnless = nameof(LinuxIpcSupported))]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    [InlineData("truncated")]
    [InlineData("continuation")]
    [InlineData("count")]
    [InlineData("oversized")]
    public async Task ResultIdentityAndBoundsRefuse(string fault)
    {
        var page = Page();
        page = fault switch
        {
            "tenant" => page with { Tenant = "platform:global-administrators" },
            "domain" => page with { Domain = "tenants" },
            "aggregate" => page with { AggregateId = "other" },
            "truncated" => page with { Metadata = page.Metadata with { IsTruncated = true } },
            "continuation" => page with { Metadata = page.Metadata with { NextContinuationToken = new("cursor") } },
            "count" => page with { Metadata = page.Metadata with { EventCount = 1 } },
            _ => page with
            {
                Events = [new(1, "test", new byte[4 * 1024 * 1024], "json", 1, "message", null, null, DateTimeOffset.UtcNow, null)],
                Metadata = page.Metadata with { EventCount = 1, LatestSequence = 1, LastSequenceReturned = 1 },
            },
        };
        using var worker = new OwnerSourceReadWorker(new() { Directory = _directory }, (_, _) => Task.FromResult(page));
        await Should.ThrowAsync<InvalidOperationException>(() => worker.InvokeAsync(Request(), TestContext.Current.CancellationToken));
    }

    /// <summary>Actual private worker responses preserve SDK success and safely propagate its failures.</summary>
    [Theory(Skip = "Private owner-source IPC requires Linux.", SkipUnless = nameof(LinuxIpcSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrivateRoundTripNeverSynthesizesSuccessfulProof(bool fail)
    {
        using var worker = new OwnerSourceReadWorker(new() { Directory = _directory }, (_, _) => fail
            ? Task.FromException<StreamReadPage>(new IOException("private-sensitive-canary")) : Task.FromResult(Page()));
        await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var client = new OwnerSourceReadClient(_directory);
            if (fail)
            {
                var refusal = await Should.ThrowAsync<InvalidOperationException>(() => client.ReadAsync(TestContext.Current.CancellationToken));
                refusal.ToString().ShouldNotContain("private-sensitive-canary");
            }
            else
            {
                var page = await client.ReadAsync(TestContext.Current.CancellationToken);
                page.Tenant.ShouldBe("system");
                page.Domain.ShouldBe("global-administrators");
                page.AggregateId.ShouldBe("global-administrators");
                page.Events.ShouldBeEmpty();
            }
        }
        finally { await worker.StopAsync(TestContext.Current.CancellationToken); }
    }

    /// <summary>Cancellation and expired private operations cannot return late pages.</summary>
    [Fact(Skip = "Private owner-source IPC requires Linux.", SkipUnless = nameof(LinuxIpcSupported))]
    public async Task CancellationPreventsLateSuccess()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => new OwnerSourceReadClient(_directory).ReadAsync(canceled.Token));
        using var worker = new OwnerSourceReadWorker(new() { Directory = _directory }, (_, _) => Task.FromResult(Page()));
        await Should.ThrowAsync<OperationCanceledException>(() => worker.InvokeAsync(Request(), canceled.Token));
        new CatalogInvocationFiles(_directory).Pending().ShouldBeEmpty();
    }

    /// <summary>A dispatched SDK callback that ignores cancellation cannot turn a late page into successful proof.</summary>
    [Theory(Skip = "Private owner-source IPC requires Linux.", SkipUnless = nameof(LinuxIpcSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedSdkSuccessIsRefusedAfterCallerCancellationOrExpiry(bool expire)
    {
        var dispatched = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sdkResult = new TaskCompletionSource<StreamReadPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        using var worker = new OwnerSourceReadWorker(new() { Directory = _directory }, (_, token) =>
        {
            calls++;
            dispatched.SetResult(token);
            return sdkResult.Task; // Deliberately ignores the callback token until after returning a successful page.
        });
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        OwnerSourceReadRequest request = expire ? Request() with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(1) } : Request();
        Task<StreamReadPage> invocation = worker.InvokeAsync(request, caller.Token);
        CancellationToken callbackToken = await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = callbackToken.Register(() => cancellationObserved.TrySetResult());
        if (!expire) caller.Cancel();
        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        callbackToken.IsCancellationRequested.ShouldBeTrue();
        invocation.IsCompleted.ShouldBeFalse();

        sdkResult.SetResult(Page());

        await Should.ThrowAsync<OperationCanceledException>(() => invocation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        invocation.IsCompletedSuccessfully.ShouldBeFalse();
        calls.ShouldBe(1);
        new CatalogInvocationFiles(_directory).Pending().ShouldBeEmpty();
    }

    /// <summary>Complete range and sequence validation is portable and does not instantiate Linux IPC.</summary>
    [Theory]
    [InlineData("upper-bound")]
    [InlineData("lower-bound")]
    [InlineData("omitted-tail")]
    [InlineData("missing-last")]
    [InlineData("wrong-last")]
    [InlineData("gap")]
    [InlineData("duplicate")]
    [InlineData("reordered")]
    [InlineData("zero-position")]
    [InlineData("negative-position")]
    [InlineData("empty-tail")]
    [InlineData("empty-last")]
    [InlineData("negative-tail")]
    public void CompleteHistoryRejectsBoundedMissingOrInconsistentPositions(string fault)
    {
        StreamReadEvent first = new(1, "test", [], "json", 1, "first", null, null, DateTimeOffset.UtcNow, null);
        StreamReadEvent second = first with { SequenceNumber = 2, MessageId = "second" };
        var page = Page() with { Events = [first, second], Metadata = new(0, null, 2, 2, 2, false, null) };
        OwnerSourceReadSelection.ValidatePage(page);
        page = fault switch
        {
            "upper-bound" => page with { Metadata = page.Metadata with { ToSequence = 2 } },
            "lower-bound" => page with { Metadata = page.Metadata with { FromSequence = 1 } },
            "omitted-tail" => page with { Metadata = page.Metadata with { LatestSequence = 3 } },
            "missing-last" => page with { Metadata = page.Metadata with { LastSequenceReturned = null } },
            "wrong-last" => page with { Metadata = page.Metadata with { LastSequenceReturned = 1 } },
            "gap" => page with { Events = [first, second with { SequenceNumber = 3 }] },
            "duplicate" => page with { Events = [first, second with { SequenceNumber = 1 }] },
            "reordered" => page with { Events = [second, first] },
            "zero-position" => page with { Events = [first with { SequenceNumber = 0 }, second] },
            "negative-position" => page with { Events = [first with { SequenceNumber = -1 }, second] },
            "empty-tail" => Page() with { Metadata = Page().Metadata with { LatestSequence = 1 } },
            "empty-last" => Page() with { Metadata = Page().Metadata with { LastSequenceReturned = 1 } },
            _ => page with { Metadata = page.Metadata with { LatestSequence = -1 } },
        };
        Should.Throw<InvalidOperationException>(() => OwnerSourceReadSelection.ValidatePage(page));
        OwnerSourceReadSelection.ValidatePage(Page());
    }

    private static StreamReadPage Page() => new("system", "global-administrators", "global-administrators", [], new(0, null, null, 0, 0, false, null));
    private static OwnerSourceReadRequest Request() => new(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddSeconds(40),
        "administrator-bootstrap", "platform:global-administrators", "global-administrators", "global-administrators");

    /// <summary>Removes only the test-owned private files.</summary>
    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

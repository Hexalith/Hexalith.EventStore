using System.Text.Json;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.CatalogInvocation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Testing.Tests;

/// <summary>Private IPC boundary, finite dispatch and genuine SDK failure propagation.</summary>
public sealed class CatalogInvocationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "catalog-invocation-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the test-owned private IPC directory.</summary>
    public CatalogInvocationTests()
    {
        Directory.CreateDirectory(_directory);
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>Verifies unselected Registration Is Inert And Production Selection Refuses.</summary>
    [Fact]
    public void UnselectedRegistrationIsInertAndProductionSelectionRefuses()
    {
        var services = new ServiceCollection();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        services.AddPrivateCatalogInvocation(new ConfigurationBuilder().Build(), environment);
        services.Count.ShouldBe(0);
        var selected = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["EventStore:Testing:CatalogInvocation:Enabled"] = "true" }).Build();
        Should.Throw<InvalidOperationException>(() => services.AddPrivateCatalogInvocation(selected, environment));
        services.Count.ShouldBe(0);
    }

    /// <summary>Verifies private Directory Rejects Links And Shared Permissions.</summary>
    [Fact]
    public void PrivateDirectoryRejectsLinksAndSharedPermissions()
    {
        if (!OperatingSystem.IsLinux()) return;
        string linked = _directory + "-link";
        Directory.CreateSymbolicLink(linked, _directory);
        try { Should.Throw<IOException>(() => new CatalogInvocationClient(linked)); }
        finally { Directory.Delete(linked); }
        File.SetUnixFileMode(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead);
        Should.Throw<IOException>(() => new CatalogInvocationClient(_directory));
    }

    /// <summary>Verifies atomic Private Request Is Bounded And Claimable Only Once.</summary>
    [Fact]
    public async Task AtomicPrivateRequestIsBoundedAndClaimableOnlyOnce()
    {
        var files = new CatalogInvocationFiles(_directory);
        string id = Guid.NewGuid().ToString("N");
        var request = Request(id);
        await files.WriteAsync(id, ".request", request, TestContext.Current.CancellationToken);
        files.Pending().Length.ShouldBe(1);
        files.Claim(id);
        (await files.ReadAsync<CatalogInvocationRequest>(id, ".working", TestContext.Current.CancellationToken)).Id.ShouldBe(id);
        Should.Throw<IOException>(() => files.Claim(id));
        await Should.ThrowAsync<IOException>(async () => await files.WriteAsync(id, ".working", request, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<IOException>(async () => await files.WriteAsync(Guid.NewGuid().ToString("N"), ".request", new string('x', CatalogInvocationFiles.MaximumBytes), TestContext.Current.CancellationToken));
        Should.Throw<IOException>(() => files.PathFor("../outside", ".request"));
    }

    /// <summary>Verifies finite Selection Rejects Caller Expansion.</summary>
    /// <param name="app">The app control.</param>
    /// <param name="method">The method control.</param>
    /// <param name="credential">The credential control.</param>
    /// <param name="includeToken">Whether a fresh nonfunctional token canary is supplied.</param>
    [Theory]
    [InlineData("other", "project/v2", CatalogInvocationCredential.Workload, false)]
    [InlineData("projects", "http://other/", CatalogInvocationCredential.Workload, false)]
    [InlineData("projects", "project/v2?query=x", CatalogInvocationCredential.Workload, false)]
    [InlineData("projects", "process", CatalogInvocationCredential.Workload, false)]
    [InlineData("projects", "project/v2", CatalogInvocationCredential.Workload, true)]
    [InlineData("projects", "project/v2", CatalogInvocationCredential.HumanBearer, false)]
    [InlineData("projects", "project/v2", (CatalogInvocationCredential)99, false)]
    public void FiniteSelectionRejectsCallerExpansion(string app, string method, CatalogInvocationCredential credential, bool includeToken)
    {
        string? token = includeToken ? Guid.NewGuid().ToString("N") : null;
        using var worker = Worker(Substitute.For<IRouteCatalogAuthority>(), Substitute.For<IWorkloadAssertionIssuer>());
        Should.Throw<InvalidOperationException>(() => worker.Validate(Request() with
        {
            AppId = app, Method = method, Credential = credential, Token = token,
        }));
        Should.Throw<InvalidOperationException>(() => worker.Validate(Request() with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) }));
        Should.Throw<InvalidOperationException>(() => worker.Validate(Request() with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) }));
    }

    /// <summary>Verifies positive Mode Uses Actual Issuer And Propagates Catalog Failure.</summary>
    [Fact]
    public async Task PositiveModeUsesActualIssuerAndPropagatesCatalogFailure()
    {
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<RouteCatalogDispatchLease>(new RouteCatalogUnavailableException("not enrolled")));
        var issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>()).Returns(new ValueTask<string?>("issuer.assertion"));
        using var worker = Worker(authority, issuer);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => worker.InvokeAsync(Request(), TestContext.Current.CancellationToken));
        await issuer.Received(1).IssueAsync(Arg.Is<WorkloadAssertionRequest>(request => request.Audience == "projects" && request.Operation == "domain-service:project"), Arg.Any<CancellationToken>());
        await authority.Received(1).AcquireAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies explicit Negative Modes Never Acquire Replacement Credentials.</summary>
    /// <param name="credential">The credential control.</param>
    /// <param name="includeToken">Whether a fresh nonfunctional token canary is supplied.</param>
    [Theory]
    [InlineData(CatalogInvocationCredential.Anonymous, false)]
    [InlineData(CatalogInvocationCredential.HumanBearer, true)]
    [InlineData(CatalogInvocationCredential.PublicIssuerWorkload, true)]
    public async Task ExplicitNegativeModesNeverAcquireReplacementCredentials(CatalogInvocationCredential credential, bool includeToken)
    {
        string? token = includeToken ? Guid.NewGuid().ToString("N") : null;
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<RouteCatalogDispatchLease>(new RouteCatalogUnavailableException("not enrolled")));
        var issuer = Substitute.For<IWorkloadAssertionIssuer>();
        using var worker = Worker(authority, issuer);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => worker.InvokeAsync(Request() with
        {
            Credential = credential, Token = token,
        }, TestContext.Current.CancellationToken));
        await issuer.DidNotReceive().IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies client Preserves Actual Status Body And Rejects Transport Failure.</summary>
    [Fact]
    public async Task ClientPreservesActualStatusBodyAndRejectsTransportFailure()
    {
        var client = new CatalogInvocationClient(_directory);
        var files = new CatalogInvocationFiles(_directory);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        async Task RespondAsync(bool failure)
        {
            string[] paths;
            while ((paths = files.Pending()).Length == 0) await Task.Delay(10, deadline.Token);
            string id = Path.GetFileNameWithoutExtension(paths.Single());
            files.Claim(id);
            await files.WriteAsync(id, ".response", new CatalogInvocationResult(id, failure ? null : 403, failure ? [] : [1, 2, 3], failure ? "RouteCatalogUnavailableException" : null, failure ? "private-sensitive-diagnostic" : null), deadline.Token);
        }
        Task respond = RespondAsync(false);
        using var response = await client.InvokeAsync("projects", "project/v2", JsonSerializer.SerializeToElement(new { }), cancellationToken: deadline.Token);
        await respond;
        ((int)response.StatusCode).ShouldBe(403);
        (await response.Content.ReadAsByteArrayAsync(deadline.Token)).ShouldBe(new byte[] { 1, 2, 3 });
        respond = RespondAsync(true);
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => client.InvokeAsync("projects", "project/v2", JsonSerializer.SerializeToElement(new { }), cancellationToken: deadline.Token));
        failure.ToString().ShouldNotContain("private-sensitive-diagnostic");
        await respond;
    }

    /// <summary>Verifies late Private Response Cannot Escape Caller Cancellation.</summary>
    [Fact]
    public async Task LatePrivateResponseCannotEscapeCallerCancellation()
    {
        var files = new CatalogInvocationFiles(_directory);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        caller.CancelAfter(TimeSpan.FromSeconds(5));
        async Task RespondAfterCancellationAsync()
        {
            string[] paths;
            while ((paths = files.Pending()).Length == 0) await Task.Delay(10, TestContext.Current.CancellationToken);
            string id = Path.GetFileNameWithoutExtension(paths.Single());
            files.Claim(id);
            caller.Cancel();
            await files.WriteAsync(id, ".response", new CatalogInvocationResult(id, 200, [1], null), TestContext.Current.CancellationToken);
        }
        Task respond = RespondAfterCancellationAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => new CatalogInvocationClient(_directory).InvokeAsync(
            "projects", "project/v2", JsonSerializer.SerializeToElement(new { }), cancellationToken: caller.Token));
        await respond;
    }

    /// <summary>Verifies host Cancellation During Issuance Prevents Catalog Dispatch.</summary>
    [Fact]
    public async Task HostCancellationDuringIssuancePreventsCatalogDispatch()
    {
        using var host = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var authority = Substitute.For<IRouteCatalogAuthority>();
        var issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            host.Cancel();
            return new ValueTask<string?>("late.assertion");
        });
        using var worker = Worker(authority, issuer);
        await Should.ThrowAsync<OperationCanceledException>(() => worker.InvokeAsync(Request(), host.Token));
        await authority.DidNotReceive().AcquireAsync(Arg.Any<CancellationToken>());
    }

    private CatalogInvocationWorker Worker(IRouteCatalogAuthority authority, IWorkloadAssertionIssuer issuer) => new(
        new CatalogInvocationOptions { Directory = _directory, MetadataAppIds = ["projects", "folders"], ProjectionAppId = "projects" },
        authority, Options.Create(new RouteCatalogHostOptions { DevelopmentFixture = true, Profile = "Development", RequireTechnicalHttpBinding = true }),
        issuer, new Uri("http://127.0.0.1:39999/"));

    private static CatalogInvocationRequest Request(string? id = null) => new(id ?? Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddSeconds(40),
        "projects", "project/v2", CatalogInvocationCredential.Workload, null, JsonSerializer.SerializeToElement(new { }));

    /// <summary>Removes the test-owned private IPC directory.</summary>
    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

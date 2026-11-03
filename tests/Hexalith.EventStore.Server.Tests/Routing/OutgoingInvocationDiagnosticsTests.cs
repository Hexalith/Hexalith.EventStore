using System.Net;

using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Distinguishes outgoing catalog work from actual egress without changing cancellation or lease ownership.</summary>
public sealed class OutgoingInvocationDiagnosticsTests
{
    /// <summary>A caller canceled during acquisition never reaches either request validation or the network.</summary>
    [Fact]
    public async Task CanceledAcquisitionRecordsNoTransportOrOwnedRelease()
    {
        var log = new DispatchDiagnosticLog();
        var acquisition = new TaskCompletionSource<RouteCatalogDispatchLease>(TaskCreationOptions.RunContinuationsAsynchronously);
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(call => acquisition.Task.WaitAsync(call.Arg<CancellationToken>()));
        using var transport = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using ServiceProvider provider = Provider(authority, log, transport);
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("outgoing-diagnostic");
        using var canceled = new CancellationTokenSource();

        Task<HttpResponseMessage> pending = client.PostAsync("/v1.0/invoke/enrolled/method/query", null, canceled.Token);
        await log.WaitForAsync("InvocationAcquire", "Started");
        canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);

        CommandDispatchDiagnosticsTests.AssertClosed(log, "InvocationAcquire", "Failed");
        log.Records.ShouldNotContain(record => Equals(record["Stage"], "InvocationTransport")
            || Equals(record["Stage"], "InvocationValidation") || Equals(record["Stage"], "InvocationRelease"));
        transport.CallCount.ShouldBe(0);
        log.Records.Last()["OperationCanceled"].ShouldBe(true);
        CommandDispatchDiagnosticsTests.AssertPrivate(log);
    }

    /// <summary>Real transport entry and uninterruptible owned release are separate observable stages.</summary>
    [Fact]
    public async Task ActualTransportAndOwnedReleaseRemainDistinctWhileCallerCancels()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        await using var captured = await fixture.Authorities[0].AcquireAsync(default);
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int releases = 0;
        var lease = new RouteCatalogDispatchLease(captured.Snapshot, () => { releases++; return release.Task; });
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(lease);
        var log = new DispatchDiagnosticLog();
        using var transport = new MockHttpMessageHandler((_, _) => response.Task);
        using ServiceProvider provider = Provider(authority, log, transport);
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("outgoing-diagnostic");
        using var cancellation = new CancellationTokenSource();
        var target = fixture.Envelope.Routes.First(route => route.Method == "query");
        Task<HttpResponseMessage> pending = client.PostAsync($"/v1.0/invoke/{target.AppId}/method/query", null, cancellation.Token);
        await log.WaitForAsync("InvocationTransport", "Started");
        transport.CallCount.ShouldBe(1);
        CommandDispatchDiagnosticsTests.AssertClosed(log, "InvocationValidation", "Completed");
        log.Records.ShouldNotContain(record => Equals(record["Stage"], "InvocationRelease"));
        response.SetResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        await log.WaitForAsync("InvocationRelease", "Started");
        pending.IsCompleted.ShouldBeFalse();
        cancellation.Cancel();
        release.SetResult();
        // HttpClient itself may translate a canceled caller after the owned handler returns;
        // the existing catalog release still runs independently and exactly once.
        try { using HttpResponseMessage result = await pending; result.StatusCode.ShouldBe(HttpStatusCode.NoContent); }
        catch (OperationCanceledException) { cancellation.IsCancellationRequested.ShouldBeTrue(); }
        releases.ShouldBe(1);
        CommandDispatchDiagnosticsTests.AssertClosed(log, "InvocationRelease", "Completed");
        log.Records.Last()["OperationCanBeCanceled"].ShouldBe(false);
        CommandDispatchDiagnosticsTests.AssertPrivate(log);
    }

    /// <summary>Original acquisition/transport/release exceptions and retained fences are unchanged.</summary>
    [Theory]
    [InlineData("acquire")]
    [InlineData("transport")]
    [InlineData("release")]
    public async Task OriginalFailureAndItsLeaseOwnershipArePreserved(string failure)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        await using var captured = await fixture.Authorities[0].AcquireAsync(default);
        var original = new InvalidOperationException("private-source-and-key");
        int releases = 0;
        var lease = new RouteCatalogDispatchLease(captured.Snapshot, () =>
        {
            releases++;
            return failure == "release" ? Task.FromException(original) : Task.CompletedTask;
        });
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => failure == "acquire"
            ? Task.FromException<RouteCatalogDispatchLease>(original) : Task.FromResult(lease));
        var log = new DispatchDiagnosticLog();
        using var transport = new MockHttpMessageHandler(_ => failure == "transport" ? throw original : new HttpResponseMessage(HttpStatusCode.OK));
        using ServiceProvider provider = Provider(authority, log, transport);
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("outgoing-diagnostic");
        var target = fixture.Envelope.Routes.First(route => route.Method == "query");

        (await Should.ThrowAsync<InvalidOperationException>(() => client.PostAsync(
            $"/v1.0/invoke/{target.AppId}/method/query", new StringContent("private-payload")))).ShouldBeSameAs(original);

        releases.ShouldBe(failure == "release" ? 1 : 0);
        transport.CallCount.ShouldBe(failure == "acquire" ? 0 : 1);
        CommandDispatchDiagnosticsTests.AssertClosed(log, failure switch
        { "acquire" => "InvocationAcquire", "transport" => "InvocationTransport", _ => "InvocationRelease" }, "Failed");
        CommandDispatchDiagnosticsTests.AssertPrivate(log);
    }

    /// <summary>Disabled or failing optional logging never replaces normal execution.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledOrThrowingDiagnosticsDoNotChangeDispatch(bool throwing)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var log = new DispatchDiagnosticLog(enabled: throwing, throwOnWrite: throwing);
        using var transport = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.NoContent));
        using ServiceProvider provider = Provider(fixture.Authorities[0], log, transport);
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("outgoing-diagnostic");
        var target = fixture.Envelope.Routes.First(route => route.Method == "query");

        using HttpResponseMessage result = await client.PostAsync($"/v1.0/invoke/{target.AppId}/method/query", null);

        result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        transport.CallCount.ShouldBe(1);
        log.Records.ShouldBeEmpty();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    private static ServiceProvider Provider(IRouteCatalogAuthority authority, DispatchDiagnosticLog log, HttpMessageHandler transport)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug)
            .AddFilter((string? category, LogLevel _) => category == typeof(RouteCatalogInvocationHandler).FullName).AddProvider(log));
        services.AddSingleton(authority);
        services.AddEventStoreRouteCatalog(new ConfigurationBuilder().Build());
        services.Configure<RouteCatalogHostOptions>(options => options.InvocationTransportFactory = _ => transport);
        services.AddHttpClient("outgoing-diagnostic", client => client.BaseAddress = new Uri("http://localhost:3500"))
            .ConfigurePrimaryHttpMessageHandler(() => new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadGateway)));
        return services.BuildServiceProvider();
    }
}

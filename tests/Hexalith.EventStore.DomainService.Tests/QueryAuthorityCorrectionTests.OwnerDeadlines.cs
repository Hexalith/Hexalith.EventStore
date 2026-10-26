using System.Net;
using System.Security.Cryptography;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed partial class QueryAuthorityCorrectionTests
{
    [Theory]
    [InlineData("acquisition")]
    [InlineData("transport")]
    [InlineData("body")]
    [InlineData("disposal")]
    [InlineData("cumulative")]
    public async Task ConfiguredWholeOwnerDeadlineExpiresBeforeTheEnclosingCaller(string phase)
    {
        using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await Should.ThrowAsync<OperationCanceledException>(() => InvokeDeadlineOwnerAsync("00:00:00.100", phase, caller.Token));
        caller.IsCancellationRequested.ShouldBeFalse();
    }

    [Theory]
    [InlineData("acquisition")]
    [InlineData("transport")]
    [InlineData("body")]
    public async Task ConfiguredOwnerDeadlinePreservesGenuineCallerCancellation(string phase)
    {
        using var caller = new CancellationTokenSource();
        await Should.ThrowAsync<OperationCanceledException>(() => InvokeDeadlineOwnerAsync("00:00:05", phase, caller.Token,
            _ => caller.CancelAfter(TimeSpan.FromMilliseconds(150))));
        caller.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task AbsentOwnerDeadlineKeepsTheOriginalCallerBudgetAndSuccessfulResult()
    {
        using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        int acquisitions = 0;
        QueryResult result = await InvokeDeadlineOwnerAsync(null, "success", caller.Token, token =>
        {
            acquisitions++;
            token.ShouldBe(caller.Token);
        });
        result.Success.ShouldBeTrue();
        acquisitions.ShouldBe(1);
        caller.IsCancellationRequested.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("00:00:00")]
    [InlineData("-00:00:01")]
    [InlineData("00:01:01")]
    [InlineData("00:00:00.0000001")]
    [InlineData("untrusted-protected-config")]
    public async Task InvalidOwnerDeadlineFailsBeforeAcquisitionWithoutEchoingTheValue(string configured)
    {
        int acquisitions = 0;
        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() =>
            InvokeDeadlineOwnerAsync(configured, "success", TestContext.Current.CancellationToken, _ => acquisitions++));
        acquisitions.ShouldBe(0);
        failure.Message.ShouldBe("EventStore:OwnerQueries:OperationTimeout must be a duration between one millisecond and one minute.");
    }

    private static async Task<QueryResult> InvokeDeadlineOwnerAsync(string? configured, string phase,
        CancellationToken caller, Action<CancellationToken>? observeAcquisition = null)
    {
        QueryEnvelope query = Query();
        DefaultHttpContext context = Context(query);
        context.Request.Headers[EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName] = "verified-original-assertion";
        using RSA key = RSA.Create(2048);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:WorkloadIssuer:Workload"] = "projects",
            ["Authentication:BoundStreamRead:PrivateKey"] = Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
            [DelegatedOwnerQueryClient.OperationTimeoutConfigurationKey] = configured,
        }).Build();
        using DaprClient dapr = new DaprClientBuilder().Build();
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        async Task<string?> AcquireAsync(CancellationToken token)
        {
            observeAcquisition?.Invoke(token);
            if (phase == "acquisition") await Task.Delay(Timeout.InfiniteTimeSpan, token);
            if (phase == "cumulative") await Task.Delay(75, token);
            return "owner-workload-assertion";
        }
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<string?>(AcquireAsync(call.ArgAt<CancellationToken>(1))));
        using var http = new HttpClient(new OwnerStatusResponseHandler(HttpStatusCode.OK,
            phase == "transport" ? Timeout.InfiniteTimeSpan : phase == "cumulative" ? TimeSpan.FromMilliseconds(75)
                : phase == "success" ? TimeSpan.FromMilliseconds(200) : TimeSpan.Zero,
            phase == "body" ? Timeout.InfiniteTimeSpan : null,
            phase == "disposal" ? TimeSpan.FromMilliseconds(250) : null)) { Timeout = Timeout.InfiniteTimeSpan };
        IHttpClientFactory clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient(Arg.Any<string>()).Returns(http);
        var accessor = new HttpContextAccessor { HttpContext = context };
        var client = new DelegatedOwnerQueryClient(dapr, clients, issuer, accessor, configuration, TimeProvider.System);
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        handler.Domain.Returns(query.Domain); handler.QueryType.Returns(query.QueryType);
        handler.ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(_ =>
            client.InvokeAsync(new("tenant", "folders", "folder", "read"), caller));
        await using ServiceProvider services = new ServiceCollection().AddSingleton<IHttpContextAccessor>(accessor).AddSingleton(handler).BuildServiceProvider();
        try { return await DomainQueryDispatcher.ExecuteAsync(services, query, caller); }
        finally { VerifiedQueryCaller.Get(context).ShouldBeNull(); }
    }
}

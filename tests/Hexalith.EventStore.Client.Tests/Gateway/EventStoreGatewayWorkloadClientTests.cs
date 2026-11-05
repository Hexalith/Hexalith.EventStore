using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Gateway;

public sealed class EventStoreGatewayWorkloadClientTests
{
    [Theory]
    [InlineData("platform:tenants", "tenants", "system", true)]
    [InlineData("platform:global-administrators", "global-administrators", "system", true)]
    [InlineData("platform:tenants", "tenants", "platform:tenants", false)]
    [InlineData("platform:global-administrators", "global-administrators", "tenant-a", false)]
    [InlineData("tenant-a", "timesheets", "tenant-a", true)]
    [InlineData("tenant-a", "timesheets", "system", false)]
    public async Task Stream_scope_mapping_keeps_original_assertion_and_proof_binding(
        string scope, string domain, string storageTenant, bool accepted)
    {
        var original = new StreamReadRequest(scope, domain, "aggregate-1");
        var assertions = Substitute.For<IEventStoreGatewayWorkloadAssertionSource>();
        assertions.IssueAsync(EventStoreGatewayWorkloadOperations.StreamRead, scope, domain, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>("original-assertion"));
        var proofs = Substitute.For<IEventStoreGatewayStreamReadProofSource>();
        proofs.IssueAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>("original-proof"));
        using var http = new HttpClient(new Handler(message =>
        {
            message.Headers.Contains("X-Hexalith-Scope").ShouldBe(scope.StartsWith("platform:", StringComparison.Ordinal));
            if (scope.StartsWith("platform:", StringComparison.Ordinal)) message.Headers.GetValues("X-Hexalith-Scope").ShouldBe([scope]);
            message.Headers.Contains("X-Hexalith-Tenant").ShouldBeFalse();
            message.Headers.GetValues("X-Hexalith-Bound-Stream-Read-Proof").ShouldBe(["original-proof"]);
            message.Headers.GetValues("X-Hexalith-Workload-Assertion").ShouldBe(["original-assertion"]);
            JsonSerializer.Deserialize<StreamReadRequest>(message.Content!.ReadAsStringAsync().GetAwaiter().GetResult(),
                EventStoreGatewayClient.JsonOptions).ShouldBe(original);
            return StreamResponse(storageTenant, domain, "aggregate-1");
        })) { BaseAddress = new Uri("https://eventstore.test/") };
        var gateway = new EventStoreGatewayClient(http, Options.Create(new EventStoreGatewayClientOptions()), assertions, proofs);
        if (accepted) (await gateway.ReadWorkloadStreamAsync(original)).Tenant.ShouldBe(storageTenant);
        else await Should.ThrowAsync<EventStoreGatewayException>(() => gateway.ReadWorkloadStreamAsync(original));
        await proofs.Received(1).IssueAsync(Arg.Is<StreamReadRequest>(request => ReferenceEquals(original, request)), Arg.Any<CancellationToken>());
        await assertions.Received(1).IssueAsync(EventStoreGatewayWorkloadOperations.StreamRead, scope, domain, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("platform:tenants", "global-administrators", null)]
    [InlineData("platform:global-administrators", "tenants", null)]
    [InlineData("platform:other", "other", null)]
    [InlineData("platform:tenants", "tenants", "X-Hexalith-Scope")]
    [InlineData("platform:tenants", "tenants", "X-Hexalith-Tenant")]
    [InlineData("tenant-a", "timesheets", "X-Hexalith-Scope")]
    [InlineData("tenant-a", "timesheets", "X-Hexalith-Tenant")]
    public async Task Unsupported_or_conflicting_stream_scope_refuses_before_credentials(
        string scope, string domain, string? header)
    {
        using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("Unexpected transport")))
            { BaseAddress = new Uri("https://eventstore.test/") };
        if (header is not null) http.DefaultRequestHeaders.Add(header, "conflicting-scope");
        var assertions = Substitute.For<IEventStoreGatewayWorkloadAssertionSource>();
        var proofs = Substitute.For<IEventStoreGatewayStreamReadProofSource>();
        var gateway = new EventStoreGatewayClient(http, Options.Create(new EventStoreGatewayClientOptions()), assertions, proofs);
        await Should.ThrowAsync<InvalidOperationException>(() => gateway.ReadWorkloadStreamAsync(new(scope, domain, "aggregate-1")));
        await proofs.DidNotReceive().IssueAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("other", "aggregate-1")]
    [InlineData("tenants", "other")]
    public async Task Platform_mapping_still_requires_exact_domain_and_aggregate(string domain, string aggregate)
    {
        using var http = new HttpClient(new Handler(_ => StreamResponse("system", domain, aggregate)))
            { BaseAddress = new Uri("https://eventstore.test/") };
        var gateway = new EventStoreGatewayClient(http, Options.Create(new EventStoreGatewayClientOptions()), new StubAssertions());
        await Should.ThrowAsync<EventStoreGatewayException>(() => gateway.ReadWorkloadStreamAsync(new("platform:tenants", "tenants", "aggregate-1")));
    }

    private static HttpResponseMessage StreamResponse(string tenant, string domain, string aggregate)
        => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new StreamReadPage(
            tenant, domain, aggregate, [], new(0, null, null, 0, 0, false, null)), EventStoreGatewayClient.JsonOptions),
            Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Workload_calls_attach_assertion_and_use_only_workload_routes()
    {
        var requests = new List<(string Path, string? Assertion, string? Body)>();
        using var http = new HttpClient(new Handler(request =>
        {
            requests.Add((request.RequestUri!.AbsolutePath,
                request.Headers.GetValues("X-Hexalith-Workload-Assertion").Single(),
                request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()));
            string json = request.RequestUri.AbsolutePath switch
            {
                "/api/v1/commands/workload" => """{"correlationId":"message-1","messageId":"message-1"}""",
                "/api/v1/commands/status/workload/tenant-a/message-1" =>
                    """{"correlationId":"message-1","tenantId":"tenant-a","status":"Completed","statusCode":4,"messageId":"message-1","domain":"timesheets","aggregateId":"entry-1","eventCount":2,"committedEventSequence":7}""",
                "/api/v1/streams/read/workload" => JsonSerializer.Serialize(new StreamReadPage(
                    "tenant-a", "timesheets", "entry-1", [],
                    new StreamReadMetadata(0, null, null, 0, 0, false, null)),
                    EventStoreGatewayClient.JsonOptions),
                _ => throw new InvalidOperationException("Unexpected route")
            };
            return new HttpResponseMessage(request.RequestUri.AbsolutePath == "/api/v1/commands/workload"
                ? HttpStatusCode.Accepted : HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        })) { BaseAddress = new Uri("https://eventstore.test/") };
        var source = new StubAssertions();
        var gateway = new EventStoreGatewayClient(http, Options.Create(new EventStoreGatewayClientOptions()), source);

        await gateway.SubmitWorkloadCommandAsync(new SubmitCommandRequest(
            "message-1", "tenant-a", "timesheets", "entry-1", "CommitMagicLinkUse",
            JsonSerializer.SerializeToElement(new { capabilityId = "capability-1" })));
        CommandStatusQueryResponse status = (await gateway.GetWorkloadCommandStatusAsync(
            "tenant-a", "message-1")).ShouldNotBeNull();
        status.TenantId.ShouldBe("tenant-a");
        status.Domain.ShouldBe("timesheets");
        status.AggregateId.ShouldBe("entry-1");
        status.EventCount.ShouldBe(2);
        status.CommittedEventSequence.ShouldBe(7);
        var streamRequest = new StreamReadRequest(
            "tenant-a", "timesheets", "entry-1", FromSequence: 5,
            ContinuationToken: new ReplayContinuationToken("cursor-1"), PageSize: 17);
        (await gateway.ReadWorkloadStreamAsync(streamRequest)).Domain.ShouldBe("timesheets");

        requests.Select(static request => request.Path).ShouldBe([
            "/api/v1/commands/workload",
            "/api/v1/commands/status/workload/tenant-a/message-1",
            "/api/v1/streams/read/workload"
        ]);
        requests.Select(static request => request.Assertion).ShouldAllBe(static assertion => assertion == "signed-assertion");
        requests[1].Body.ShouldBeNull();
        SubmitCommandRequest submitted = JsonSerializer.Deserialize<SubmitCommandRequest>(
            requests[0].Body!, EventStoreGatewayClient.JsonOptions).ShouldNotBeNull();
        submitted.MessageId.ShouldBe("message-1");
        submitted.Tenant.ShouldBe("tenant-a");
        submitted.Domain.ShouldBe("timesheets");
        submitted.AggregateId.ShouldBe("entry-1");
        submitted.CommandType.ShouldBe("CommitMagicLinkUse");
        submitted.Payload.GetProperty("capabilityId").GetString().ShouldBe("capability-1");
        StreamReadRequest transmitted = JsonSerializer.Deserialize<StreamReadRequest>(
            requests[2].Body!, EventStoreGatewayClient.JsonOptions).ShouldNotBeNull();
        transmitted.Tenant.ShouldBe("tenant-a");
        transmitted.Domain.ShouldBe("timesheets");
        transmitted.AggregateId.ShouldBe("entry-1");
        transmitted.FromSequence.ShouldBe(5);
        transmitted.ContinuationToken.ShouldBe(new ReplayContinuationToken("cursor-1"));
        transmitted.PageSize.ShouldBe(17);
        source.Issued.ShouldBe([
            (EventStoreGatewayWorkloadOperations.CommandSubmit, "tenant-a"),
            (EventStoreGatewayWorkloadOperations.CommandStatus, "tenant-a"),
            (EventStoreGatewayWorkloadOperations.StreamRead, "tenant-a")
        ]);
    }

    [Fact]
    public async Task Missing_assertion_fails_before_transport()
    {
        int calls = 0;
        using var http = new HttpClient(new Handler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        })) { BaseAddress = new Uri("https://eventstore.test/") };
        var gateway = new EventStoreGatewayClient(http, Options.Create(new EventStoreGatewayClientOptions()));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            gateway.GetWorkloadCommandStatusAsync("tenant-a", "message-1"));
        calls.ShouldBe(0);
    }

    private sealed class StubAssertions : IEventStoreGatewayWorkloadAssertionSource
    {
        public List<(string Operation, string Tenant)> Issued { get; } = [];

        public ValueTask<string?> IssueAsync(string operation, string tenant, CancellationToken cancellationToken)
        {
            Issued.Add((operation, tenant));
            return ValueTask.FromResult<string?>("signed-assertion");
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handle(request));
    }
}

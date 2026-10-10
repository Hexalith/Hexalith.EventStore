using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Gateway;

public sealed class EventStoreGatewayWorkloadClientTests
{
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

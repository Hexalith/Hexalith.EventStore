using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Operations.Models;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

public sealed partial class HttpHostSecurityTests
{
    /// <summary>Matches Admin's any-explicit-role behavior and never falls back after invalid explicit roles.</summary>
    [Theory]
    [InlineData("invalid-only", false)]
    [InlineData("invalid-first", true)]
    [InlineData("read-only-first", true)]
    [InlineData("read-only-only", false)]
    public async Task ExplicitRolesMatchAdminReceiverSemantics(string scenario, bool allowed)
    {
        string[] roles = scenario switch
        {
            "invalid-first" => ["Invalid", "Operator"],
            "read-only-first" => ["ReadOnly", "Operator"],
            "read-only-only" => ["ReadOnly"],
            _ => ["Invalid"],
        };
        var actor = Substitute.For<Actors.IDeadLetterDrainActor>();
        actor.RetryAsync(Arg.Any<DeadLetterActionRequest>()).Returns(new DeadLetterActorActionResult(true, "accepted"));
        await using var factory = Factory(actor);
        using var client = factory.CreateClient();
        using var request = Authorized(HttpMethod.Post, "/internal/dead-letters/retry");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(additional: new Dictionary<string, object>
        { ["eventstore:admin-role"] = roles, ["global_admin"] = true }));
        request.Content = JsonContent.Create(new { tenantId = "tenant-a", messageIds = new[] { "message-a" } });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        actor.ReceivedCalls().Count().ShouldBe(allowed ? 1 : 0);
    }

    /// <summary>Restores only the independently validated Admin-wide backlog read used by the existing facade.</summary>
    [Theory]
    [InlineData("ReadOnly", false)]
    [InlineData("Operator", false)]
    [InlineData("Admin", true)]
    public async Task GlobalBacklogReadRequiresAdmin(string role, bool allowed)
    {
        var actor = Substitute.For<Actors.IDeadLetterDrainActor>();
        actor.ListAsync(Arg.Any<DeadLetterListRequest>()).Returns(new DeadLetterListResult([], 37, null));
        await using var factory = Factory(actor);
        using var client = factory.CreateClient();
        using var request = RoleRequest(HttpMethod.Get, "/internal/dead-letters", role);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        if (allowed)
        {
            var value = actor.ReceivedCalls().ShouldHaveSingleItem().GetArguments()[0].ShouldBeOfType<DeadLetterListRequest>();
            value.TenantId.ShouldBeNull();
        }
        else actor.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Allows only Admin to query or act on the actor's unidentified bucket, even with an ordinary matching grant.</summary>
    [Theory]
    [InlineData("Operator", false, false)]
    [InlineData("Admin", false, true)]
    [InlineData("Operator", true, false)]
    [InlineData("Admin", true, true)]
    public async Task UnidentifiedBucketRequiresAdmin(string role, bool action, bool allowed)
    {
        var actor = Substitute.For<Actors.IDeadLetterDrainActor>();
        actor.ListAsync(Arg.Any<DeadLetterListRequest>()).Returns(new DeadLetterListResult([], 0, null));
        actor.RetryAsync(Arg.Any<DeadLetterActionRequest>()).Returns(new DeadLetterActorActionResult(true, "accepted"));
        await using var factory = Factory(actor);
        using var client = factory.CreateClient();
        using var request = RoleRequest(action ? HttpMethod.Post : HttpMethod.Get,
            action ? "/internal/dead-letters/retry" : "/internal/dead-letters?tenantId=unidentified", role, "unidentified");
        if (action) request.Content = JsonContent.Create(new { tenantId = "unidentified", messageIds = new[] { "message-a" } });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        actor.ReceivedCalls().Count().ShouldBe(allowed ? 1 : 0);
    }

    /// <summary>Requires the Admin role for the actual global count receiver, with a real channel token.</summary>
    [Theory]
    [InlineData("ReadOnly", false)]
    [InlineData("Operator", false)]
    [InlineData("Admin", true)]
    public async Task GlobalCountRequiresAdminAndReturnsActorCount(string role, bool allowed)
    {
        var actor = Substitute.For<Actors.IDeadLetterDrainActor>();
        actor.ListAsync(Arg.Any<DeadLetterListRequest>()).Returns(new DeadLetterListResult([], 37, null));
        await using var factory = Factory(actor);
        using var client = factory.CreateClient();
        using var request = RoleRequest(HttpMethod.Get, "/internal/dead-letters/count", role);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        if (allowed)
        {
            (await response.Content.ReadFromJsonAsync<int>(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(37);
            var value = actor.ReceivedCalls().ShouldHaveSingleItem().GetArguments()[0].ShouldBeOfType<DeadLetterListRequest>();
            value.TenantId.ShouldBeNull();
            value.Count.ShouldBe(1);
        }
        else actor.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Bounds declared and streaming action bodies before parsing or actor effects.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedActionBodyRejectsBeforeActorWork(bool streaming)
    {
        var actor = Substitute.For<Actors.IDeadLetterDrainActor>();
        await using var factory = Factory(actor, maxActionItems: 1);
        using var client = factory.CreateClient();
        using var request = Authorized(HttpMethod.Post, "/internal/dead-letters/retry");
        byte[] body = Encoding.UTF8.GetBytes("{\"tenantId\":\"tenant-a\",\"messageIds\":[\"message-a\"],\"padding\":\"" + new string('x', 5000) + "\"}");
        request.Content = streaming ? new HttpHostSecurityStreamingContent(body) : new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (streaming) request.Headers.TransferEncodingChunked = true;
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        actor.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Retains the maximum configured item count and identifier fields, including JSON escaping expansion.</summary>
    [Fact]
    public async Task MaximumActionFieldsRemainAdmitted()
    {
        var actor = Substitute.For<Actors.IDeadLetterDrainActor>();
        actor.RetryAsync(Arg.Any<DeadLetterActionRequest>()).Returns(new DeadLetterActorActionResult(true, "accepted"));
        await using var factory = Factory(actor, maxActionItems: 1000);
        using var client = factory.CreateClient();
        string tenant = new('t', 64);
        using var request = RoleRequest(HttpMethod.Post, "/internal/dead-letters/retry", "Operator", tenant);
        string[] ids = Enumerable.Repeat(new string('\u1234', DeadLetterSafeIdentity.MaxValueLength), 1000).ToArray();
        request.Content = JsonContent.Create(new { tenantId = tenant, messageIds = ids, requestedBy = new string('x', 256) });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var value = actor.ReceivedCalls().ShouldHaveSingleItem().GetArguments()[0].ShouldBeOfType<DeadLetterActionRequest>();
        value.MessageIds.Count.ShouldBe(1000);
        value.RequestedBy.ShouldBe("validated-human");
    }

    /// <summary>Preserves a downstream JSON failure after an admitted actor call instead of reporting a client 400.</summary>
    [Fact]
    public async Task DownstreamJsonFailureIsNotReclassifiedAsBadRequest()
    {
        var actor = Substitute.For<Actors.IDeadLetterDrainActor>();
        actor.RetryAsync(Arg.Any<DeadLetterActionRequest>()).Returns(Task.FromException<DeadLetterActorActionResult>(new JsonException("downstream")));
        await using var factory = Factory(actor);
        using var client = factory.CreateClient();
        using var request = Authorized(HttpMethod.Post, "/internal/dead-letters/retry");
        request.Content = JsonContent.Create(new { tenantId = "tenant-a", messageIds = new[] { "message-a" } });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        actor.ReceivedCalls().ShouldHaveSingleItem();
    }

    /// <summary>Malformed request JSON remains a client 400 before actor effects.</summary>
    [Fact]
    public async Task MalformedActionJsonRejectsBeforeActorWork()
    {
        var actor = Substitute.For<Actors.IDeadLetterDrainActor>();
        await using var factory = Factory(actor);
        using var client = factory.CreateClient();
        using var request = Authorized(HttpMethod.Post, "/internal/dead-letters/retry");
        request.Content = new StringContent("{", Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        actor.ReceivedCalls().ShouldBeEmpty();
    }

    private static HttpRequestMessage RoleRequest(HttpMethod method, string path, string role, string tenant = "tenant-a")
    {
        var request = Authorized(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(additional: new Dictionary<string, object>
        { ["eventstore:admin-role"] = role, ["eventstore:tenant"] = tenant }));
        return request;
    }
}

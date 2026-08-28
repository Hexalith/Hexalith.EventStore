using System.Net;
using System.Net.Http.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Operations.Actors;
using Hexalith.EventStore.Operations.Configuration;
using Hexalith.EventStore.Operations.Models;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>
/// Verifies the operator list endpoint's projection onto the admin contract.
/// </summary>
/// <remarks>
/// This projection is the only operator-visible view of the backlog, and it is the seam where a redacted
/// <see cref="DeadLetterListItem"/> with nullable identity slots meets a contract that rejects a null or blank
/// value in every one of them. Asserting it through the mapped route rather than by calling the private
/// projection keeps the field order, the placeholder substitution, and the paging envelope pinned together: a
/// transposed slot compiles, and a dropped placeholder makes the whole page fail for an unidentified item --
/// exactly the item the runbook tells an operator to go looking for.
/// </remarks>
public sealed class DeadLetterListProjectionTests
{
    /// <summary>Verifies identified and unidentified items both project onto the admin contract.</summary>
    [Fact]
    public async Task ListProjectsIdentifiedAndUnidentifiedItemsOntoTheAdminContract()
    {
        var capturedAt = new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero);
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        _ = actor.ListAsync(Arg.Any<DeadLetterListRequest>()).Returns(new DeadLetterListResult(
            [
                new DeadLetterListItem(
                    new DeadLetterSafeIdentity(
                        "message-a",
                        "tenant-a",
                        "work",
                        "work-a",
                        "correlation-a",
                        "WorkItemCreated"),
                    capturedAt,
                    2,
                    DeadLetterReplayState.ReplayRequested,
                    "timeout"),
                new DeadLetterListItem(
                    new DeadLetterSafeIdentity("unidentified-abc", null, null, null, null, null),
                    capturedAt,
                    0,
                    DeadLetterReplayState.Pending,
                    null),
            ],
            7,
            5));

        using WebApplicationFactory<Program> factory = CreateFactory(actor);
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/dead-letters?count=2");
        request.Headers.TryAddWithoutValidation("dapr-caller-app-id", "eventstore-admin").ShouldBeTrue();
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer operator-token").ShouldBeTrue();

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<DeadLetterEntry> page = (await response.Content.ReadFromJsonAsync<PagedResult<DeadLetterEntry>>())
            .ShouldNotBeNull();
        page.TotalCount.ShouldBe(7);
        page.ContinuationToken.ShouldBe("5");
        page.Items.Count().ShouldBe(2);

        DeadLetterEntry identified = page.Items.First();
        identified.MessageId.ShouldBe("message-a");
        identified.TenantId.ShouldBe("tenant-a");
        identified.Domain.ShouldBe("work");
        identified.AggregateId.ShouldBe("work-a");
        identified.CorrelationId.ShouldBe("correlation-a");
        identified.OriginalCommandType.ShouldBe("WorkItemCreated");
        identified.FailureReason.ShouldBe("timeout");
        identified.FailedAtUtc.ShouldBe(capturedAt);
        identified.RetryCount.ShouldBe(2);

        DeadLetterEntry unidentified = page.Items.Last();
        unidentified.MessageId.ShouldBe("unidentified-abc");
        unidentified.TenantId.ShouldBe(DeadLetterSafeIdentity.UnidentifiedTenantId);
        unidentified.Domain.ShouldBe(DeadLetterSafeIdentity.UnknownValue);
        unidentified.AggregateId.ShouldBe(DeadLetterSafeIdentity.UnknownValue);
        unidentified.CorrelationId.ShouldBe(DeadLetterSafeIdentity.UnknownValue);
        unidentified.OriginalCommandType.ShouldBe(DeadLetterSafeIdentity.UnknownValue);
        unidentified.FailureReason.ShouldBe("retained");
    }

    /// <summary>Verifies the endpoint clamps a caller page size to the configured list bound.</summary>
    /// <remarks>
    /// The clamp and the actor's guard read the same option. Were they to drift, the endpoint would forward a
    /// page size the actor refuses and every operator list call would fail with a server error.
    /// </remarks>
    [Fact]
    public async Task ListClampsCallerPageSizeToTheConfiguredBound()
    {
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        _ = actor.ListAsync(Arg.Any<DeadLetterListRequest>())
            .Returns(new DeadLetterListResult([], 0, null));

        using WebApplicationFactory<Program> factory = CreateFactory(
            actor,
            builder => builder.UseSetting($"{EventStoreOperationsOptions.SectionName}:MaxListItems", "5"));
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/dead-letters?count=999");
        request.Headers.TryAddWithoutValidation("dapr-caller-app-id", "eventstore-admin").ShouldBeTrue();
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer operator-token").ShouldBeTrue();

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await actor.Received(1).ListAsync(Arg.Is<DeadLetterListRequest>(value => value.Count == 5));
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IDeadLetterDrainActor actor,
        Action<IWebHostBuilder>? configure = null)
    {
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        _ = proxyFactory.CreateActorProxy<IDeadLetterDrainActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            _ = builder.UseEnvironment("Development");
            configure?.Invoke(builder);
            builder.ConfigureServices(services => services.AddSingleton(proxyFactory));
        });
    }
}

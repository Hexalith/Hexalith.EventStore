using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminDeadLetterApiClientTests
{
    private static AdminDeadLetterApiClient CreateClient(HttpClient httpClient)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminDeadLetterApiClient(factory, NullLogger<AdminDeadLetterApiClient>.Instance);
    }

    // === GetDeadLetterCountAsync ===

    [Fact]
    public async Task GetDeadLetterCountAsync_ReturnsCount_WhenApiResponds()
    {
        string json = "3";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminDeadLetterApiClient client = CreateClient(httpClient);

        int? result = await client.GetDeadLetterCountAsync();

        result.ShouldNotBeNull();
        result.Value.ShouldBe(3);
    }

    [Fact]
    public async Task GetDeadLetterCountAsync_ReturnsNull_WhenApiReturnsError()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminDeadLetterApiClient client = CreateClient(httpClient);

        int? result = await client.GetDeadLetterCountAsync();

        result.ShouldBeNull();
    }

    // === GetDeadLettersAsync ===

    [Fact]
    public async Task GetDeadLettersAsync_ReturnsPage_WhenApiResponds()
    {
        string json = """{"items":[{"messageId":"m1","tenantId":"t1","domain":"Counter","aggregateId":"a1","correlationId":"c1","failureReason":"timeout","failedAtUtc":"2026-01-01T00:00:00Z","retryCount":2,"originalCommandType":"IncrementCounter"}],"totalCount":1,"continuationToken":null}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminDeadLetterApiClient client = CreateClient(httpClient);

        PagedResult<DeadLetterEntry> result = await client.GetDeadLettersAsync();

        result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(1);
        result.Items.Count.ShouldBe(1);
        result.Items[0].MessageId.ShouldBe("m1");
    }

    [Fact]
    public async Task GetDeadLettersAsync_ThrowsServiceUnavailable_WhenApiReturnsError()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminDeadLetterApiClient client = CreateClient(httpClient);

        await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetDeadLettersAsync());
    }
}

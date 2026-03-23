using Microsoft.AspNetCore.Mvc.Testing;

namespace Hexalith.EventStore.Admin.UI.Tests;

/// <summary>
/// Test 9.11: Blazor Server host bootstraps without errors (AC: 1).
/// Merge-blocking test.
/// </summary>
public class HostBootstrapTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HostBootstrapTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BlazorServerHost_BootstrapsWithoutErrors()
    {
        // Arrange
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/");

        // Assert — 200 OK means the host bootstrapped and the page rendered
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }
}

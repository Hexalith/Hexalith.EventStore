using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

public sealed partial class RestApiGeneratedControllerErrorSemanticsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualEmittedControllerRequiresExplicitHostingValidationBeforeGatewayEffects(bool installed)
    {
        var controller = CreateController();
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IEventStoreGatewayClient>(controller.Gateway);
        builder.Services.AddSingleton<ICommandStatusLocationBuilder>(new FakeCommandStatusLocationBuilder(null));
        builder.Services.AddControllers().AddApplicationPart(controller.Assembly);
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", "test-user")], "fixture"));
            if (installed) context.Items[RouteCatalogGeneratedAction.ValidatorKey] = (Func<Type, string, string, bool>)((_, _, tenant) => tenant == "tenant-a");
            await next();
        });
        app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var response = await client.PostAsJsonAsync("api/tenant-a/counter/counter-1/increment", new { counterId = "counter-1", amount = 2 });
        response.StatusCode.ShouldBe(installed ? HttpStatusCode.Accepted : HttpStatusCode.ServiceUnavailable, await response.Content.ReadAsStringAsync());
        controller.Gateway.CommandCallCount.ShouldBe(installed ? 1 : 0);
    }
}

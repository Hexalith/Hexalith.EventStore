using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Admin.Server.Tests.IntegrationTests;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.OpenApi;

/// <summary>
/// Test host variant with OpenAPI disabled via configuration.
/// </summary>
public sealed class AdminOpenApiDisabledFactory : IAsyncLifetime
{
    private WebApplication? _app;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:Admin:OpenApi:Enabled"] = "false",
        });

        builder.Services.AddAdminApi(builder.Configuration);
        builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(AdminStreamsController).Assembly);

        // Override DAPR-backed services with NSubstitute mocks
        builder.Services.AddScoped(_ => Substitute.For<IStreamQueryService>());
        builder.Services.AddScoped(_ => Substitute.For<IProjectionQueryService>());
        builder.Services.AddScoped(_ => Substitute.For<IProjectionCommandService>());
        builder.Services.AddScoped(_ => Substitute.For<ITypeCatalogService>());
        builder.Services.AddScoped(_ => Substitute.For<IHealthQueryService>());
        builder.Services.AddScoped(_ => Substitute.For<IStorageQueryService>());
        builder.Services.AddScoped(_ => Substitute.For<IStorageCommandService>());
        builder.Services.AddScoped(_ => Substitute.For<IDeadLetterQueryService>());
        builder.Services.AddScoped(_ => Substitute.For<IDeadLetterCommandService>());
        builder.Services.AddScoped(_ => Substitute.For<ITenantQueryService>());

        builder.WebHost.UseTestServer();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();

        // Gated: only map OpenAPI/Swagger if enabled (same logic as real host)
        if (_app.Configuration.GetValue("EventStore:Admin:OpenApi:Enabled", true))
        {
            _ = _app.MapOpenApi();
            _ = _app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "Hexalith EventStore Admin API v1");
                options.RoutePrefix = "swagger";
            });
        }

        _app.MapControllers();
        await _app.StartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an HTTP client for the test server.
    /// </summary>
    public HttpClient CreateClient() => _app!.GetTestClient();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }
}

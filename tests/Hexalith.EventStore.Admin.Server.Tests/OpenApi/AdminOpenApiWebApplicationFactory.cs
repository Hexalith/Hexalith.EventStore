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
/// Creates a test server with admin controllers, authorization pipeline, OpenAPI, and Swagger UI.
/// Mocks all DAPR-backed services since OpenAPI generation only needs controller metadata.
/// </summary>
public sealed class AdminOpenApiWebApplicationFactory : IAsyncLifetime {
    private WebApplication? _app;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["EventStore:Admin:OpenApi:Enabled"] = "true",
        });

        _ = builder.Services.AddAdminApi(builder.Configuration);
        _ = builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
        _ = builder.Services.AddControllers()
            .AddApplicationPart(typeof(AdminStreamsController).Assembly);

        // Override DAPR-backed services with NSubstitute mocks
        _ = builder.Services.AddScoped(_ => Substitute.For<IStreamQueryService>());
        _ = builder.Services.AddScoped(_ => Substitute.For<IProjectionQueryService>());
        _ = builder.Services.AddScoped(_ => Substitute.For<IProjectionCommandService>());
        _ = builder.Services.AddScoped(_ => Substitute.For<ITypeCatalogService>());
        _ = builder.Services.AddScoped(_ => Substitute.For<IHealthQueryService>());
        _ = builder.Services.AddScoped(_ => Substitute.For<IStorageQueryService>());
        _ = builder.Services.AddScoped(_ => Substitute.For<IStorageCommandService>());
        _ = builder.Services.AddScoped(_ => Substitute.For<IDeadLetterQueryService>());
        _ = builder.Services.AddScoped(_ => Substitute.For<IDeadLetterCommandService>());
        _ = builder.Services.AddScoped(_ => Substitute.For<ITenantQueryService>());

        _ = builder.WebHost.UseTestServer();

        _app = builder.Build();
        _ = _app.UseAuthentication();
        _ = _app.UseAuthorization();

        // OpenAPI/Swagger UI (mirrors real host middleware order)
        _ = _app.MapOpenApi();
        _ = _app.UseSwaggerUI(options => {
            options.SwaggerEndpoint("/openapi/v1.json", "Hexalith EventStore Admin API v1");
            options.RoutePrefix = "swagger";
        });

        _ = _app.MapControllers();
        await _app.StartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an HTTP client for the test server.
    /// </summary>
    public HttpClient CreateClient() => _app!.GetTestClient();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() {
        if (_app is not null) {
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }
}

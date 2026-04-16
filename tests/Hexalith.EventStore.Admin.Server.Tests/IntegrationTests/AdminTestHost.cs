using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.IntegrationTests;

/// <summary>
/// Creates a test server with admin controllers, authorization pipeline, and mock services.
/// </summary>
public sealed class AdminTestHost : IDisposable {
    private readonly WebApplication _app;

    public AdminTestHost() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

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
        _ = builder.Services.AddScoped(_ => Substitute.For<ITenantCommandService>());

        _ = builder.WebHost.UseTestServer();

        _app = builder.Build();
        _ = _app.UseAuthentication();
        _ = _app.UseAuthorization();
        _ = _app.MapControllers();
        _app.StartAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Creates an HTTP client for the test server.
    /// </summary>
    public HttpClient CreateClient() => _app.GetTestClient();

    /// <inheritdoc/>
    public void Dispose() => _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

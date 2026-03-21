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
public sealed class AdminTestHost : IDisposable
{
    private readonly WebApplication _app;

    public AdminTestHost()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

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
        _app.MapControllers();
        _app.StartAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Creates an HTTP client for the test server.
    /// </summary>
    public HttpClient CreateClient() => _app.GetTestClient();

    /// <inheritdoc/>
    public void Dispose() => _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

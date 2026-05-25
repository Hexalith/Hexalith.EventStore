using System.Net;

using Hexalith.EventStore.Admin.UI;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;

namespace Hexalith.EventStore.Admin.UI.E2E;

/// <summary>
/// Shared Playwright browser fixture for E2E tests.
/// Builds the Admin.UI application with Kestrel on a real TCP port
/// (skipping Aspire service defaults that require infrastructure),
/// then launches a Chromium browser for Playwright automation.
/// </summary>
/// <remarks>
/// Prerequisite: run <c>pwsh bin/Debug/net10.0/playwright.ps1 install chromium</c> once after restore.
/// </remarks>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private WebApplication? _app;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public string BaseUrl { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        // Build the Admin.UI using the same service configuration as Program.cs,
        // but with Kestrel instead of TestServer and without Aspire service defaults.
        // The content root points to the Admin.UI output directory so
        // MapStaticAssets() finds the correct manifest.
        string adminUiDir = Path.GetDirectoryName(typeof(Hexalith.EventStore.Admin.UI.AdminUIServiceExtensions).Assembly.Location)!;

        // Bind Kestrel to port 0 and read the OS-assigned port back after start (below).
        // This avoids the TOCTOU race of pre-allocating a socket, closing it, then racing
        // another process to re-bind the same port number.
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = adminUiDir,
            ApplicationName = typeof(AdminUIServiceExtensions).Assembly.GetName().Name,
            EnvironmentName = "Development",
            Args = ["--urls=http://127.0.0.1:0"],
        });

        // Lightweight replacement for AddServiceDefaults — just health checks, no
        // service discovery or resilience handlers that require Aspire infrastructure.
        builder.Services.AddHealthChecks();

        // Reuse the same service registrations as production Program.cs
        builder.AddAdminUI();

        _app = builder.Build();

        _app.UseAdminUI();

        // Map health endpoints (lightweight replacement for MapDefaultEndpoints)
        _app.MapHealthChecks("/health");
        _app.MapHealthChecks("/alive");

        await _app.StartAsync().ConfigureAwait(false);

        // Read the actual bound address — port 0 was resolved to a real port on start.
        IServerAddressesFeature addresses = _app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Kestrel did not expose a server address feature.");
        BaseUrl = addresses.Addresses.First();

        // Verify the server is listening
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        HttpResponseMessage response = await httpClient.GetAsync(BaseUrl).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"Admin.UI did not respond successfully. Status: {response.StatusCode}");
        }

        // Headed / slow-mo debugging via env vars (default: headless, no slow-mo):
        //   HEXALITH_E2E_HEADED=1    launch a visible browser
        //   HEXALITH_E2E_SLOWMO=250  add 250 ms between actions
        bool headed = Environment.GetEnvironmentVariable("HEXALITH_E2E_HEADED") == "1";
        float? slowMo = float.TryParse(
            Environment.GetEnvironmentVariable("HEXALITH_E2E_SLOWMO"),
            out float slowMoMs) ? slowMoMs : null;

        _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !headed,
            SlowMo = slowMo,
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new browser context and page targeting the test host.
    /// Caller is responsible for disposing the returned context.
    /// </summary>
    public async Task<(IBrowserContext Context, IPage Page)> CreatePageAsync()
    {
        if (_browser is null)
        {
            throw new InvalidOperationException("Fixture not initialized. Call InitializeAsync first.");
        }

        IBrowserContext context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            IgnoreHTTPSErrors = true,
        }).ConfigureAwait(false);

        IPage page = await context.NewPageAsync().ConfigureAwait(false);
        return (context, page);
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync().ConfigureAwait(false);
        }

        _playwright?.Dispose();

        if (_app is not null)
        {
            await _app.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }
}

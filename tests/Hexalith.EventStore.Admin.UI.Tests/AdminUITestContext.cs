using Bunit;

using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.SignalR;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests;

/// <summary>
/// Base test context for Admin.UI bUnit tests.
/// Registers FluentUI components and mock services.
/// </summary>
public class AdminUITestContext : BunitContext {
    public AdminUITestContext() {
        // Register FluentUI components
        Services.AddFluentUIComponents();

        // Mock JSInterop for FluentUI and custom interop
        JSInterop.Setup<string>("hexalithAdmin.registerShortcuts", _ => true).SetResult("shortcut-test");
        JSInterop.SetupVoid("hexalithAdmin.unregisterShortcuts", _ => true).SetVoidResult();
        JSInterop.Setup<string?>("hexalithAdmin.getLocalStorage", _ => true).SetResult(null);
        JSInterop.SetupVoid("hexalithAdmin.setLocalStorage", _ => true).SetVoidResult();
        JSInterop.Setup<int>("hexalithAdmin.getViewportWidth", _ => true).SetResult(1920);
        JSInterop.Setup<double>("hexalithAdmin.getScrollTop", _ => true).SetResult(0d);
        JSInterop.SetupVoid("hexalithAdmin.setScrollTop", _ => true).SetVoidResult();
        JSInterop.Setup<string>("hexalithAdmin.registerViewportListener", _ => true).SetResult("vp-test-1");
        JSInterop.SetupVoid("hexalithAdmin.unregisterViewportListener", _ => true).SetVoidResult();
        JSInterop.SetupVoid("hexalithAdmin.focusCommandPaletteSearch", _ => true).SetVoidResult();
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Mock authentication state provider
        AuthenticationStateProvider authStateProvider = Substitute.For<AuthenticationStateProvider>();
        System.Security.Claims.ClaimsPrincipal user = new(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim(AdminClaimTypes.Role, "Admin"),
        ], "TestAuth"));
        _ = authStateProvider.GetAuthenticationStateAsync()
            .Returns(Task.FromResult(new AuthenticationState(user)));

        Services.AddSingleton(authStateProvider);
        Services.AddScoped<AdminUserContext>();
        Services.AddScoped<ThemeState>();

        // Mock AdminStreamApiClient for pages that inject it (tests can override)
        Services.AddScoped(_ => Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance));
        Services.AddScoped<DashboardRefreshService>();
        Services.AddScoped<TopologyCacheService>();
        Services.AddScoped<ViewportService>();

        // SignalR client with test-safe disposal
        TestSignalRClient testSignalRClient = new();
        Services.AddSingleton(testSignalRClient);
        Services.AddSingleton(testSignalRClient.Inner);
        Services.AddSingleton<IConfiguration>(_ => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:AdminServer:SwaggerUrl"] = "https://localhost:8091/swagger/index.html",
                ["EventStore:AdminServer:BaseUrl"] = "https://eventstore-admin",
            })
            .Build());
        Services.AddCascadingValue(sp => {
            AuthenticationStateProvider asp = sp.GetRequiredService<AuthenticationStateProvider>();
            return asp.GetAuthenticationStateAsync();
        });
    }
}

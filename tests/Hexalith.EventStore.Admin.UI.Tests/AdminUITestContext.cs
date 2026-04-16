using Bunit;

using Hexalith.EventStore.Admin.UI.Tests.Services;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        _ = Services.AddFluentUIComponents();

        // Replace the real IToastService with a test fake to avoid requiring a FluentToastProvider
        // in the render tree for unit tests. Tests that need to inspect toasts can resolve
        // TestToastService from DI instead.
        _ = Services.RemoveAll<IToastService>();
        _ = Services.AddSingleton<TestToastService>();
        _ = Services.AddSingleton<IToastService>(sp => sp.GetRequiredService<TestToastService>());

        // Mock JSInterop for FluentUI and custom interop
        _ = JSInterop.Setup<string>("hexalithAdmin.registerShortcuts", _ => true).SetResult("shortcut-test");
        _ = JSInterop.SetupVoid("hexalithAdmin.unregisterShortcuts", _ => true).SetVoidResult();
        _ = JSInterop.Setup<string?>("hexalithAdmin.getLocalStorage", _ => true).SetResult(null);
        _ = JSInterop.SetupVoid("hexalithAdmin.setLocalStorage", _ => true).SetVoidResult();
        _ = JSInterop.Setup<int>("hexalithAdmin.getViewportWidth", _ => true).SetResult(1920);
        _ = JSInterop.Setup<double>("hexalithAdmin.getScrollTop", _ => true).SetResult(0d);
        _ = JSInterop.SetupVoid("hexalithAdmin.setScrollTop", _ => true).SetVoidResult();
        _ = JSInterop.Setup<string>("hexalithAdmin.registerViewportListener", _ => true).SetResult("vp-test-1");
        _ = JSInterop.SetupVoid("hexalithAdmin.unregisterViewportListener", _ => true).SetVoidResult();
        _ = JSInterop.SetupVoid("hexalithAdmin.focusCommandPaletteSearch", _ => true).SetVoidResult();
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Mock authentication state provider
        AuthenticationStateProvider authStateProvider = Substitute.For<AuthenticationStateProvider>();
        System.Security.Claims.ClaimsPrincipal user = new(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim(AdminClaimTypes.Role, "Admin"),
        ], "TestAuth"));
        _ = authStateProvider.GetAuthenticationStateAsync()
            .Returns(Task.FromResult(new AuthenticationState(user)));

        _ = Services.AddSingleton(authStateProvider);
        _ = Services.AddScoped<AdminUserContext>();
        _ = Services.AddScoped<ThemeState>();

        // Mock AdminStreamApiClient for pages that inject it (tests can override)
        _ = Services.AddScoped(_ => Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance));
        _ = Services.AddScoped<DashboardRefreshService>();
        _ = Services.AddScoped<TopologyCacheService>();
        _ = Services.AddScoped<ViewportService>();

        // SignalR client with test-safe disposal
        TestSignalRClient testSignalRClient = new();
        _ = Services.AddSingleton(testSignalRClient);
        _ = Services.AddSingleton(testSignalRClient.Inner);
        _ = Services.AddSingleton<IConfiguration>(_ => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:AdminServer:SwaggerUrl"] = "https://localhost:8091/swagger/index.html",
                ["EventStore:AdminServer:BaseUrl"] = "https://eventstore-admin",
            })
            .Build());
        _ = Services.AddCascadingValue(sp => {
            AuthenticationStateProvider asp = sp.GetRequiredService<AuthenticationStateProvider>();
            return asp.GetAuthenticationStateAsync();
        });
    }
}

using Bunit;
using Bunit.Rendering;

using Hexalith.EventStore.Admin.UI.Tests.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests;

/// <summary>
/// Base test context for Admin.UI bUnit tests. Registers FluentUI components and mock services,
/// and intercepts every render entry point so the renderer info FluentUI v5 requires is applied.
/// Any new render helper added here MUST route through <see cref="EnsureRendererInfo"/>.
/// </summary>
public class AdminUITestContext : BunitContext {
    private bool _rendererInfoSet;

    public AdminUITestContext() {
        // Register FluentUI components
        _ = Services.AddFluentUIComponents();

        // Replace the real INotificationService with a test fake to avoid requiring a FluentToastProvider
        // in the render tree for unit tests. Tests that need to inspect toasts can resolve
        // TestToastService from DI instead.
        _ = Services.RemoveAll<INotificationService>();
        _ = Services.AddSingleton<TestToastService>();
        _ = Services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<TestToastService>());

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

        // Mock AdminTenantOptionsProvider — pages /commands /events /streams /projections inject it.
        // Default substitute returns Empty so unrelated tests don't have to stub a tenant list.
        _ = Services.AddScoped(sp => {
            AdminTenantOptionsProvider provider = Substitute.For<AdminTenantOptionsProvider>(
                Substitute.For<AdminTenantApiClient>(
                    Substitute.For<IHttpClientFactory>(),
                    NullLogger<AdminTenantApiClient>.Instance),
                Substitute.For<AdminStreamApiClient>(
                    Substitute.For<IHttpClientFactory>(),
                    NullLogger<AdminStreamApiClient>.Instance),
                NullLogger<AdminTenantOptionsProvider>.Instance);
            _ = provider.GetTenantOptionsAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new TenantOptionsResult(
                    [],
                    TenantOptionsLoadStatus.Empty,
                    AdminTenantOptionsProvider.EmptyMessage)));
            return provider;
        });
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
        _ = Services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        _ = Services.AddScoped<DevelopmentAdminRoleState>();
        _ = Services.AddCascadingValue(sp => {
            AuthenticationStateProvider asp = sp.GetRequiredService<AuthenticationStateProvider>();
            return asp.GetAuthenticationStateAsync();
        });
    }

    /// <summary>
    /// Gets the renderer info applied to every render. Production Admin.UI runs
    /// <c>InteractiveServer</c>; override to exercise the static prerender pass instead.
    /// </summary>
    protected virtual RendererInfo TestRendererInfo => new("Server", true);

    /// <inheritdoc/>
    /// <remarks>Applies <see cref="TestRendererInfo"/> before the first render.</remarks>
    public override IRenderedComponent<ContainerFragment> Render(RenderFragment renderFragment) {
        EnsureRendererInfo();
        return base.Render(renderFragment);
    }

    /// <inheritdoc/>
    /// <remarks>Applies <see cref="TestRendererInfo"/> before the first render.</remarks>
    public override IRenderedComponent<TComponent> Render<TComponent>(RenderFragment renderFragment) {
        EnsureRendererInfo();
        return base.Render<TComponent>(renderFragment);
    }

    /// <inheritdoc/>
    /// <remarks>Applies <see cref="TestRendererInfo"/> before the first render.</remarks>
    public override IRenderedComponent<TComponent> Render<TComponent>(Action<ComponentParameterCollectionBuilder<TComponent>>? parameterBuilder = null) {
        // Defense in depth, and deliberately not covered by a test: bUnit's implementation of this
        // overload dispatches virtually into Render<TComponent>(RenderFragment) above, so removing
        // this line changes no behaviour today and no test would go red. Keep it so the contract
        // survives a bUnit release that stops routing through the fragment overload.
        EnsureRendererInfo();
        return base.Render(parameterBuilder);
    }

    /// <summary>
    /// Sets the renderer info and records that a test has chosen one, so
    /// <see cref="EnsureRendererInfo"/> will not overwrite it at render time.
    /// </summary>
    /// <param name="rendererInfo">Renderer info to apply.</param>
    public new void SetRendererInfo(RendererInfo? rendererInfo) {
        base.SetRendererInfo(rendererInfo);
        _rendererInfoSet = true;
    }

    /// <summary>
    /// Declares the renderer info once, immediately before the first render, unless a test already
    /// set one. FluentUI v5 components (FluentLayoutHamburger and friends) read
    /// <see cref="ComponentBase.RendererInfo"/> through <c>IsInteractive</c>, and bUnit throws
    /// <see cref="MissingRendererInfoException"/> when it is unset. This cannot run in the
    /// constructor: touching <see cref="BunitContext.Renderer"/> builds the service provider and
    /// seals <see cref="BunitContext.Services"/>, which derived contexts and tests still register
    /// into. The unsynchronized check-then-set is safe because a bUnit context instance is scoped
    /// to a single test and every render is dispatched from that test's thread.
    /// </summary>
    protected void EnsureRendererInfo() {
        if (_rendererInfoSet) {
            return;
        }

        // Latches _rendererInfoSet only after the call succeeds.
        SetRendererInfo(TestRendererInfo);
    }

    private sealed class TestHostEnvironment : IHostEnvironment {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Hexalith.EventStore.Admin.UI.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

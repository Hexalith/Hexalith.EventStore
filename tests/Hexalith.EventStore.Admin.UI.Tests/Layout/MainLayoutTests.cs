using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.UI.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Admin.UI.Tests.Layout;

/// <summary>
/// Test 9.3: MainLayout renders FluentLayout + FluentHeader + FluentNavMenu (AC: 2).
/// Merge-blocking test.
/// </summary>
public class MainLayoutTests : AdminUITestContext {
    [Fact]
    public void MainLayout_RendersFluentLayout() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Test Content")));

        string markup = cut.Markup;
        markup.ShouldContain("Hexalith EventStore Admin");
        markup.ShouldContain("Test Content");
    }

    [Fact]
    public void MainLayout_RendersSkipToMainContentLink() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Content")));

        string markup = cut.Markup;
        markup.ShouldContain("Skip to main content");
        markup.ShouldContain("skip-to-main");
    }

    [Fact]
    public void MainLayout_RendersNavMenu() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Content")));

        string markup = cut.Markup;
        markup.ShouldContain("Home");
        markup.ShouldContain("Commands");
        markup.ShouldContain("Events");
        markup.ShouldContain("Health");
        markup.ShouldContain("Services");
        markup.ShouldContain("Tenants");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Settings"));
    }

    [Fact]
    public void MainLayout_RendersMainContentArea() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Content")));

        _ = cut.Find("#main-content").ShouldNotBeNull();
    }

    [Fact]
    public void MainLayout_DoesNotRenderBreadcrumbOnHomePage() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Content")));

        cut.Markup.ShouldNotContain("admin-breadcrumb");
    }

    [Fact]
    public void MainLayout_RendersDevelopmentRoleSelector_WhenDevelopmentWithoutAuthority() {
        ConfigureRoleSwitcher("Development", authority: null);

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Content")));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Development role"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("ReadOnly");
        cut.Markup.ShouldContain("Operator");
        cut.Markup.ShouldContain("Admin");
    }

    [Fact]
    public async Task MainLayout_DevelopmentRoleSelector_DoesNotRenderRedundantVisibleRoleCopy() {
        ConfigureRoleSwitcher("Development", authority: null);

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Content")));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Development role"), TimeSpan.FromSeconds(5));

        DevelopmentAdminRoleState roleState = Services.GetRequiredService<DevelopmentAdminRoleState>();
        await cut.InvokeAsync(() => roleState.SetRole(AdminRole.Operator));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Operator"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldNotContain("Role: Operator");
        cut.Markup.ShouldNotContain("Development role Operator selected.");
        cut.Markup.ShouldContain("aria-label=\"Development role\"");
    }

    [Theory]
    [InlineData("Production", null)]
    [InlineData("Development", "https://keycloak/realms/test")]
    public void MainLayout_HidesDevelopmentRoleSelector_WhenGateFails(string environmentName, string? authority) {
        ConfigureRoleSwitcher(environmentName, authority);

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Content")));

        cut.Markup.ShouldNotContain("Development role");
    }

    private void ConfigureRoleSwitcher(string environmentName, string? authority) {
        _ = Services.RemoveAll<IConfiguration>();
        _ = Services.RemoveAll<IHostEnvironment>();
        _ = Services.RemoveAll<DevelopmentAdminRoleState>();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:Authentication:Authority"] = authority,
                ["EventStore:Authentication:SigningKey"] = "DevOnlySigningKey-AtLeast32Chars!",
            })
            .Build();
        _ = Services.AddSingleton(config);
        _ = Services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName));
        _ = Services.AddScoped<DevelopmentAdminRoleState>();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Hexalith.EventStore.Admin.UI.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

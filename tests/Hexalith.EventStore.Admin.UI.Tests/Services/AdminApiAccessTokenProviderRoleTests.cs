using System.Text.Json;

using Hexalith.EventStore.Admin.UI.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminApiAccessTokenProviderRoleTests {
    [Theory]
    [InlineData(AdminRole.ReadOnly)]
    [InlineData(AdminRole.Operator)]
    [InlineData(AdminRole.Admin)]
    public async Task GetAccessTokenAsync_UsesSelectedDevelopmentRoleAndPreservesClaims(AdminRole selectedRole) {
        IConfiguration config = CreateDevelopmentConfig();
        var roleState = new DevelopmentAdminRoleState(config, new TestHostEnvironment("Development"));
        var provider = new AdminApiAccessTokenProvider(config, roleState);

        roleState.SetRole(selectedRole);

        string token = await provider.GetAccessTokenAsync();
        JsonElement payload = DecodePayload(token);

        payload.GetProperty(AdminClaimTypes.Role).GetString().ShouldBe(selectedRole.ToString());
        payload.GetProperty("sub").GetString().ShouldBe("test-user");
        payload.GetProperty("iss").GetString().ShouldBe("hexalith-dev");
        payload.GetProperty("aud").GetString().ShouldBe("hexalith-eventstore");
        payload.GetProperty("tenants").GetString().ShouldNotBeNull().ShouldContain("tenant-a");
        payload.GetProperty("domains").GetString().ShouldNotBeNull().ShouldContain("counter");
        payload.GetProperty("permissions").GetString().ShouldNotBeNull().ShouldContain("admin:read");
        payload.EnumerateObject().Count(p => p.Name == AdminClaimTypes.Role).ShouldBe(1);

        if (selectedRole is AdminRole.Admin) {
            payload.GetProperty("global_admin").GetBoolean().ShouldBeTrue();
        }
        else {
            payload.TryGetProperty("global_admin", out _).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetAccessTokenAsync_InvalidatesCachedTokenAfterRoleChange() {
        IConfiguration config = CreateDevelopmentConfig();
        var roleState = new DevelopmentAdminRoleState(config, new TestHostEnvironment("Development"));
        var provider = new AdminApiAccessTokenProvider(config, roleState);

        string adminToken = await provider.GetAccessTokenAsync();
        roleState.SetRole(AdminRole.ReadOnly);
        string readOnlyToken = await provider.GetAccessTokenAsync();

        readOnlyToken.ShouldNotBe(adminToken);
        DecodePayload(readOnlyToken).GetProperty(AdminClaimTypes.Role).GetString().ShouldBe("ReadOnly");
    }

    [Theory]
    [InlineData("Production", null, false)]
    [InlineData("Development", "https://keycloak/realms/test", false)]
    [InlineData("Development", null, true)]
    public void IsRoleSwitcherAvailable_RequiresDevelopmentWithoutAuthority(string environmentName, string? authority, bool expected) {
        Dictionary<string, string?> values = CreateConfigValues();
        values["EventStore:Authentication:Authority"] = authority;
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var roleState = new DevelopmentAdminRoleState(config, new TestHostEnvironment(environmentName));

        roleState.IsRoleSwitcherAvailable.ShouldBe(expected);
    }

    private static IConfiguration CreateDevelopmentConfig()
        => new ConfigurationBuilder().AddInMemoryCollection(CreateConfigValues()).Build();

    private static Dictionary<string, string?> CreateConfigValues()
        => new() {
            ["EventStore:Authentication:Issuer"] = "hexalith-dev",
            ["EventStore:Authentication:Audience"] = "hexalith-eventstore",
            ["EventStore:Authentication:SigningKey"] = "DevOnlySigningKey-AtLeast32Chars!",
            ["EventStore:Authentication:Subject"] = "test-user",
            ["EventStore:Authentication:GlobalAdmin"] = "true",
            ["EventStore:Authentication:Tenants:0"] = "tenant-a",
            ["EventStore:Authentication:Domains:0"] = "counter",
            ["EventStore:Authentication:Permissions:0"] = "admin:read",
            ["EventStore:Authentication:Permissions:1"] = "admin:write",
        };

    private static JsonElement DecodePayload(string token) {
        string payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        return JsonDocument.Parse(Convert.FromBase64String(payload)).RootElement.Clone();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Hexalith.EventStore.Admin.UI.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Configuration;

/// <summary>
/// Validates the Keycloak realm JSON structure to prevent configuration regressions.
/// These tests parse the static realm file — no Keycloak/Aspire infrastructure needed.
/// </summary>
[Trait("Category", "Configuration")]
public class KeycloakRealmConfigurationTests {
    private static readonly Lazy<JsonElement> _realmRoot = new(LoadRealmJson);

    [Fact]
    public void AllUsers_HaveDeterministicIds() {
        JsonElement users = _realmRoot.Value.GetProperty("users");
        foreach (JsonElement user in users.EnumerateArray()) {
            string username = user.GetProperty("username").GetString()!;
            user.TryGetProperty("id", out JsonElement idElement)
                .ShouldBeTrue($"User '{username}' is missing a fixed 'id' field. " +
                    "Without it, Keycloak generates a random UUID for the 'sub' claim on each container rebuild, " +
                    "breaking bootstrap userId matching.");

            string id = idElement.GetString()!;
            id.ShouldNotBeNullOrWhiteSpace($"User '{username}' has an empty 'id' field.");
        }
    }

    [Fact]
    public void AdminUser_HasGlobalAdminAttribute() {
        JsonElement adminUser = FindUser("admin-user");
        JsonElement attributes = adminUser.GetProperty("attributes");
        attributes.TryGetProperty("global_admin", out JsonElement globalAdmin).ShouldBeTrue(
            "admin-user must have 'global_admin' attribute for RBAC bypass.");

        globalAdmin.EnumerateArray().ShouldContain(
            v => v.GetString() == "true",
            "admin-user global_admin attribute must contain 'true'.");
    }

    [Fact]
    public void AdminUser_HasSystemTenantClaim() {
        JsonElement adminUser = FindUser("admin-user");
        JsonElement tenants = adminUser.GetProperty("attributes").GetProperty("tenants");
        tenants.EnumerateArray().ShouldContain(
            v => v.GetString() == "system",
            "admin-user must have 'system' tenant for tenant management operations.");
    }

    private static JsonElement FindUser(string username) {
        JsonElement users = _realmRoot.Value.GetProperty("users");
        foreach (JsonElement user in users.EnumerateArray()) {
            if (user.GetProperty("username").GetString() == username) {
                return user;
            }
        }

        throw new InvalidOperationException($"User '{username}' not found in realm JSON.");
    }

    private static JsonElement LoadRealmJson() {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null) {
            string candidate = Path.Combine(directory, "src", "Hexalith.EventStore.AppHost", "KeycloakRealms", "hexalith-realm.json");
            if (File.Exists(candidate)) {
                string json = File.ReadAllText(candidate);
                return JsonDocument.Parse(json).RootElement;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new FileNotFoundException(
            "Could not find hexalith-realm.json. Searched from " + AppContext.BaseDirectory + " upwards.");
    }
}

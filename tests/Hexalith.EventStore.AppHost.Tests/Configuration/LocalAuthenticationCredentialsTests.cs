namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.Text;

using Hexalith.EventStore.AppHost;
using Hexalith.EventStore.Aspire;

using Microsoft.Extensions.Configuration;

public sealed class LocalAuthenticationCredentialsTests
{
    [Fact]
    public void Create_WithoutOverrides_GeneratesIndependentStrongValuesPerRun()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        LocalAuthenticationCredentials first = LocalAuthenticationCredentials.Create(configuration);
        LocalAuthenticationCredentials second = LocalAuthenticationCredentials.Create(configuration);

        Encoding.UTF8.GetByteCount(first.SigningKey).ShouldBeGreaterThanOrEqualTo(32);
        first.SigningKey.ShouldNotBe(second.SigningKey);
        GetAllValues(first).Distinct(StringComparer.Ordinal).Count().ShouldBe(GetAllValues(first).Length);
        GetAllValues(first).Intersect(GetAllValues(second), StringComparer.Ordinal).ShouldBeEmpty();
        GetRealmUserIds(first).ShouldAllBe(static identifier => identifier.Length <= 36);
        GetRealmUserIds(first)
            .Select(static identifier => Guid.TryParseExact(identifier, "D", out _))
            .ShouldAllBe(static isGuid => isGuid);
    }

    [Fact]
    public void Create_WithOrdinaryCredentialConfiguration_StillGeneratesFreshValues()
    {
        string configuredKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        string configuredUserId = Guid.NewGuid().ToString("D");
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalAuthentication:SigningKey"] = configuredKey,
                ["LocalAuthentication:AdminUserId"] = configuredUserId,
            })
            .Build();

        LocalAuthenticationCredentials credentials = LocalAuthenticationCredentials.Create(configuration);

        credentials.SigningKey.ShouldNotBe(configuredKey);
        credentials.AdminUserId.ShouldNotBe(configuredUserId);
    }

    [Fact]
    public void Create_WithUnregisteredInvocationId_FailsWithoutUsingConfiguredCredentialValues()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalAuthentication:TestInjection:InvocationId"] = Guid.NewGuid().ToString("D"),
                ["LocalAuthentication:TestInjection:SigningKey"] = Guid.NewGuid().ToString("N"),
            })
            .Build();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => LocalAuthenticationCredentials.Create(configuration));

        exception.Message.ShouldContain("InvocationId");
        exception.Message.ShouldNotContain(configuration["LocalAuthentication:TestInjection:SigningKey"]!);
    }

    [Fact]
    public void Create_WithCallerHeldTestInvocation_ConsumesRegisteredValuesExactlyOnce()
    {
        string signingKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        string adminUserId = Guid.NewGuid().ToString("D");
        using LocalAuthenticationTestInvocation invocation =
            LocalAuthenticationCredentials.RegisterTestInvocation(signingKey, adminUserId);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalAuthentication:TestInjection:InvocationId"] = invocation.InvocationId.ToString("D"),
            })
            .Build();

        LocalAuthenticationCredentials credentials = LocalAuthenticationCredentials.Create(configuration);

        credentials.ShouldBeSameAs(invocation.Credentials);
        credentials.SigningKey.ShouldBe(signingKey);
        credentials.AdminUserId.ShouldBe(adminUserId);
        _ = Should.Throw<InvalidOperationException>(() => LocalAuthenticationCredentials.Create(configuration));
    }

    [Fact]
    public void Render_ReplacesEveryInertRealmPlaceholder_AndDeletesTemporaryContent()
    {
        LocalAuthenticationCredentials credentials = LocalAuthenticationCredentials.Create(
            new ConfigurationBuilder().Build());
        string sourceDirectory = Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.AppHost",
            "KeycloakRealms");
        string importDirectory;

        using (KeycloakRealmTemplate renderedRealm = KeycloakRealmTemplate.Render(sourceDirectory, credentials))
        {
            importDirectory = renderedRealm.ImportDirectory;
            string rendered = File.ReadAllText(Path.Combine(importDirectory, "hexalith-realm.json"));

            rendered.Contains("__HEXALITH_", StringComparison.Ordinal).ShouldBeFalse(
                "No inert realm placeholder may reach Keycloak.");
            rendered.Contains(credentials.AdminUsername, StringComparison.Ordinal).ShouldBeTrue(
                "The per-run administrator identity must reach the rendered realm without being printed.");
            rendered.Contains(credentials.AdminPassword, StringComparison.Ordinal).ShouldBeTrue(
                "The per-run administrator credential must reach the rendered realm without being printed.");
        }

        Directory.Exists(importDirectory).ShouldBeFalse();
    }

    [Fact]
    public void Render_OnUnix_CreatesSecretContentWithOwnerOnlyPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        LocalAuthenticationCredentials credentials = LocalAuthenticationCredentials.Create(
            new ConfigurationBuilder().Build());
        string sourceDirectory = GetRealmSourceDirectory();

        using KeycloakRealmTemplate renderedRealm = KeycloakRealmTemplate.Render(sourceDirectory, credentials);

        File.GetUnixFileMode(renderedRealm.ImportDirectory).ShouldBe(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.GetUnixFileMode(Path.Combine(renderedRealm.ImportDirectory, "hexalith-realm.json")).ShouldBe(
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public void Render_WhenSetupFails_RemovesPartialOwnedDirectory()
    {
        LocalAuthenticationCredentials credentials = LocalAuthenticationCredentials.Create(
            new ConfigurationBuilder().Build());
        string? partialDirectory = null;

        _ = Should.Throw<InvalidOperationException>(() => KeycloakRealmTemplate.Render(
            GetRealmSourceDirectory(),
            credentials,
            temporaryPath =>
            {
                partialDirectory = Path.GetDirectoryName(temporaryPath);
                throw new InvalidOperationException("Injected setup failure.");
            }));

        partialDirectory.ShouldNotBeNull();
        Directory.Exists(partialDirectory).ShouldBeFalse();
    }

    [Fact]
    public void CleanupStaleOwnedRunDirectories_RemovesOnlyDirectOwnedStaleDirectories()
    {
        string root = Path.Combine(Path.GetTempPath(), $"hexalith-realm-cleanup-{Guid.NewGuid():N}");
        string owned = Path.Combine(root, "run-0123456789abcdef01234567");
        string unrelated = Path.Combine(root, "unrelated");
        Directory.CreateDirectory(owned);
        Directory.CreateDirectory(unrelated);
        File.WriteAllText(Path.Combine(owned, ".hexalith-owned"), "hexalith-eventstore-keycloak-v1");
        File.WriteAllText(Path.Combine(owned, "hexalith-realm.json"), "{}");
        Directory.SetLastWriteTimeUtc(owned, DateTime.UtcNow.AddDays(-2));

        try
        {
            KeycloakRealmTemplate.CleanupStaleOwnedRunDirectories(root, DateTimeOffset.UtcNow.AddDays(-1));

            Directory.Exists(owned).ShouldBeFalse();
            Directory.Exists(unrelated).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string GetRealmSourceDirectory()
        => Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.AppHost",
            "KeycloakRealms");

    private static string[] GetAllValues(LocalAuthenticationCredentials credentials)
        =>
        [
            credentials.SigningKey,
            credentials.AdminUserId,
            credentials.AdminUsername,
            credentials.AdminPassword,
            credentials.TenantAUserId,
            credentials.TenantAPassword,
            credentials.TenantBUserId,
            credentials.TenantBPassword,
            credentials.ReadOnlyUserId,
            credentials.ReadOnlyPassword,
            credentials.NoTenantUserId,
            credentials.NoTenantPassword,
        ];

    private static string[] GetRealmUserIds(LocalAuthenticationCredentials credentials)
        =>
        [
            credentials.AdminUserId,
            credentials.TenantAUserId,
            credentials.TenantBUserId,
            credentials.ReadOnlyUserId,
            credentials.NoTenantUserId,
        ];
}

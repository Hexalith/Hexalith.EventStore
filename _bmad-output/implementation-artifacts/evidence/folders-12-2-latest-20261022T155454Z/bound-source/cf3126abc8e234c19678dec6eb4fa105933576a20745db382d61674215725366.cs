namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.Text;

using Hexalith.EventStore.AppHost;
using Hexalith.EventStore.Aspire;

public sealed class LocalAuthenticationCredentialsTests
{
    [Fact]
    public void Create_WithoutOverrides_GeneratesIndependentStrongValuesPerRun()
    {
        LocalAuthenticationCredentials first = LocalAuthenticationCredentials.Create();
        LocalAuthenticationCredentials second = LocalAuthenticationCredentials.Create();

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
    public void Create_HasNoConfigurationAddressableOverload()
    {
        System.Reflection.MethodInfo[] createMethods = typeof(LocalAuthenticationCredentials)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(static method => method.Name == nameof(LocalAuthenticationCredentials.Create))
            .ToArray();

        createMethods.ShouldHaveSingleItem().GetParameters().ShouldBeEmpty();
    }

    [Fact]
    public void Create_WithoutActivatedCapability_CannotSelectRegisteredValues()
    {
        string signingKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        using LocalAuthenticationTestInvocation invocation =
            LocalAuthenticationCredentials.RegisterTestInvocation(signingKey);

        LocalAuthenticationCredentials credentials = LocalAuthenticationCredentials.Create();

        credentials.SigningKey.ShouldNotBe(signingKey);
        credentials.ShouldNotBeSameAs(invocation.Credentials);
    }

    [Fact]
    public void Create_WithCallerHeldTestInvocation_ConsumesRegisteredValuesExactlyOnce()
    {
        string signingKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        string adminUserId = Guid.NewGuid().ToString("D");
        using LocalAuthenticationTestInvocation invocation =
            LocalAuthenticationCredentials.RegisterTestInvocation(signingKey, adminUserId);
        invocation.Activate();

        LocalAuthenticationCredentials credentials = LocalAuthenticationCredentials.Create();

        credentials.ShouldBeSameAs(invocation.Credentials);
        credentials.SigningKey.ShouldBe(signingKey);
        credentials.AdminUserId.ShouldBe(adminUserId);
        LocalAuthenticationCredentials second = LocalAuthenticationCredentials.Create();
        second.ShouldNotBeSameAs(credentials);
        second.SigningKey.ShouldNotBe(signingKey);
    }

    [Fact]
    public void Render_ReplacesEveryInertRealmPlaceholder_AndDeletesTemporaryContent()
    {
        LocalAuthenticationCredentials credentials = LocalAuthenticationCredentials.Create();
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

        LocalAuthenticationCredentials credentials = LocalAuthenticationCredentials.Create();
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
        LocalAuthenticationCredentials credentials = LocalAuthenticationCredentials.Create();
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

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.Text.Json;

using Hexalith.EventStore.AppHost;
using Hexalith.EventStore.Aspire;

using Microsoft.Extensions.Configuration;

public sealed class KeycloakRealmTemplateTests
{
    [Fact]
    public void Render_ProducesValidRealmJson()
    {
        string testDirectory = CreateTestDirectory();
        string temporaryRoot = Path.Combine(testDirectory, "temporary-root");

        try
        {
            using KeycloakRealmTemplate renderedRealm = KeycloakRealmTemplate.Render(
                GetRealmSourceDirectory(),
                CreateCredentials(),
                temporaryRoot: temporaryRoot);

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(renderedRealm.ImportDirectory, "hexalith-realm.json")));

            document.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Render_WhenReplacementContainsAnotherPlaceholder_DoesNotReplaceItInASecondPass()
    {
        string testDirectory = CreateTestDirectory();
        string temporaryRoot = Path.Combine(testDirectory, "temporary-root");
        string placeholder = string.Concat("__", "HEXALITH", "_TENANT_A_PASSWORD__");
        LocalAuthenticationCredentials credentials = CreateCredentials() with
        {
            AdminPassword = placeholder,
        };

        try
        {
            InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
                KeycloakRealmTemplate.Render(
                    GetRealmSourceDirectory(),
                    credentials,
                    temporaryRoot: temporaryRoot));

            exception.Message.ShouldContain("unknown inert placeholder", Case.Insensitive);
            Directory.Exists(temporaryRoot).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Render_WhenApprovedPlaceholderIsDuplicated_RejectsBeforeCreatingTemporaryContent()
    {
        string testDirectory = CreateTestDirectory();
        string sourceDirectory = Path.Combine(testDirectory, "source");
        string temporaryRoot = Path.Combine(testDirectory, "temporary-root");
        Directory.CreateDirectory(sourceDirectory);
        string template = File.ReadAllText(Path.Combine(GetRealmSourceDirectory(), "hexalith-realm.json"));
        string placeholder = string.Concat("__", "HEXALITH", "_ADMIN_USER_ID__");
        File.WriteAllText(
            Path.Combine(sourceDirectory, "hexalith-realm.json"),
            template.Replace(
                $"\"id\": \"{placeholder}\"",
                $"\"id\": \"{placeholder}\", \"duplicate\": \"{placeholder}\"",
                StringComparison.Ordinal));

        try
        {
            InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
                KeycloakRealmTemplate.Render(
                    sourceDirectory,
                    CreateCredentials(),
                    temporaryRoot: temporaryRoot));

            exception.Message.ShouldContain("exactly once", Case.Insensitive);
            Directory.Exists(temporaryRoot).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Render_WhenApprovedPlaceholderIsRelocated_RejectsBeforeCreatingTemporaryContent()
    {
        string testDirectory = CreateTestDirectory();
        string sourceDirectory = Path.Combine(testDirectory, "source");
        string temporaryRoot = Path.Combine(testDirectory, "temporary-root");
        Directory.CreateDirectory(sourceDirectory);
        string template = File.ReadAllText(Path.Combine(GetRealmSourceDirectory(), "hexalith-realm.json"));
        string placeholder = string.Concat("__", "HEXALITH", "_ADMIN_USERNAME__");
        File.WriteAllText(
            Path.Combine(sourceDirectory, "hexalith-realm.json"),
            template.Replace(
                $"\"username\": \"{placeholder}\"",
                $"\"username\": \"admin\", \"relocated\": \"{placeholder}\"",
                StringComparison.Ordinal));

        try
        {
            InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
                KeycloakRealmTemplate.Render(
                    sourceDirectory,
                    CreateCredentials(),
                    temporaryRoot: temporaryRoot));

            exception.Message.ShouldContain("approved JSON path", Case.Insensitive);
            Directory.Exists(temporaryRoot).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Render_WhenTemporaryRootIsAReparsePoint_RejectsItWithoutMutatingTheTarget()
    {
        string testDirectory = CreateTestDirectory();
        string targetDirectory = Path.Combine(testDirectory, "target");
        string temporaryRoot = Path.Combine(testDirectory, "root-link");
        Directory.CreateDirectory(targetDirectory);
        UnixFileMode originalMode = default;
        if (!OperatingSystem.IsWindows())
        {
            originalMode = File.GetUnixFileMode(targetDirectory);
        }

        _ = Directory.CreateSymbolicLink(temporaryRoot, targetDirectory);

        try
        {
            InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
                KeycloakRealmTemplate.Render(
                    GetRealmSourceDirectory(),
                    CreateCredentials(),
                    temporaryRoot: temporaryRoot));

            exception.Message.ShouldContain("reparse point", Case.Insensitive);
            Directory.EnumerateFileSystemEntries(targetDirectory).ShouldBeEmpty();
            if (!OperatingSystem.IsWindows())
            {
                File.GetUnixFileMode(targetDirectory).ShouldBe(originalMode);
            }
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot);
            }

            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Render_WhenMalformedPlaceholderRemains_RejectsItBeforeCreatingTemporaryContent()
    {
        string testDirectory = CreateTestDirectory();
        string sourceDirectory = Path.Combine(testDirectory, "source");
        string temporaryRoot = Path.Combine(testDirectory, "temporary-root");
        Directory.CreateDirectory(sourceDirectory);
        string unresolvedMarker = string.Concat("__", "HEXALITH", "_UNRESOLVED");
        File.WriteAllText(
            Path.Combine(sourceDirectory, "hexalith-realm.json"),
            File.ReadAllText(Path.Combine(GetRealmSourceDirectory(), "hexalith-realm.json")) + unresolvedMarker);

        try
        {
            InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
                KeycloakRealmTemplate.Render(
                    sourceDirectory,
                    CreateCredentials(),
                    temporaryRoot: temporaryRoot));

            exception.Message.ShouldContain("unknown inert placeholder", Case.Insensitive);
            Directory.Exists(temporaryRoot).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Render_WhenCallbackAddsUnrelatedContentAndFails_RemovesOnlyRendererCreatedFiles()
    {
        string testDirectory = CreateTestDirectory();
        string temporaryRoot = Path.Combine(testDirectory, "temporary-root");
        string? importDirectory = null;
        string? unrelatedPath = null;

        try
        {
            _ = Should.Throw<InvalidOperationException>(() => KeycloakRealmTemplate.Render(
                GetRealmSourceDirectory(),
                CreateCredentials(),
                temporaryPath =>
                {
                    importDirectory = Path.GetDirectoryName(temporaryPath);
                    unrelatedPath = Path.Combine(importDirectory!, "unrelated.txt");
                    File.WriteAllText(unrelatedPath, "unrelated");
                    throw new InvalidOperationException("Injected render failure.");
                },
                temporaryRoot));

            importDirectory.ShouldNotBeNull();
            unrelatedPath.ShouldNotBeNull();
            Directory.Exists(importDirectory).ShouldBeTrue();
            File.Exists(unrelatedPath).ShouldBeTrue();
            File.Exists(Path.Combine(importDirectory, ".hexalith-owned")).ShouldBeTrue();
            File.Exists(Path.Combine(importDirectory, ".hexalith-owner-lease")).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Dispose_WhenDirectoryContainsUnownedEntry_RefusesCleanupAndCanBeRetried()
    {
        string testDirectory = CreateTestDirectory();
        string temporaryRoot = Path.Combine(testDirectory, "temporary-root");
        KeycloakRealmTemplate renderedRealm = KeycloakRealmTemplate.Render(
            GetRealmSourceDirectory(),
            CreateCredentials(),
            temporaryRoot: temporaryRoot);
        string importDirectory = renderedRealm.ImportDirectory;
        string unrelatedPath = Path.Combine(importDirectory, "unrelated.txt");
        File.WriteAllText(unrelatedPath, "unrelated");

        try
        {
            IOException exception = Should.Throw<IOException>(renderedRealm.Dispose);

            exception.Message.ShouldContain("ownership boundary", Case.Insensitive);
            File.Exists(unrelatedPath).ShouldBeTrue();
            File.Exists(Path.Combine(importDirectory, ".hexalith-owned")).ShouldBeTrue();
            File.Exists(Path.Combine(importDirectory, ".hexalith-owner-lease")).ShouldBeTrue();

            File.Delete(unrelatedPath);
            renderedRealm.Dispose();

            Directory.Exists(importDirectory).ShouldBeFalse();
        }
        finally
        {
            renderedRealm.Dispose();
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Dispose_WhenImportPathBecomesReparsePoint_DoesNotRestoreLeaseThroughIt()
    {
        string testDirectory = CreateTestDirectory();
        string temporaryRoot = Path.Combine(testDirectory, "temporary-root");
        string targetDirectory = Path.Combine(testDirectory, "target");
        string parkedDirectory = Path.Combine(testDirectory, "parked");
        Directory.CreateDirectory(targetDirectory);
        KeycloakRealmTemplate renderedRealm = KeycloakRealmTemplate.Render(
            GetRealmSourceDirectory(),
            CreateCredentials(),
            temporaryRoot: temporaryRoot);
        string importDirectory = renderedRealm.ImportDirectory;
        Directory.Move(importDirectory, parkedDirectory);
        _ = Directory.CreateSymbolicLink(importDirectory, targetDirectory);

        try
        {
            IOException exception = Should.Throw<IOException>(renderedRealm.Dispose);

            exception.Message.ShouldContain("ownership boundary", Case.Insensitive);
            Directory.EnumerateFileSystemEntries(targetDirectory).ShouldBeEmpty();

            Directory.Delete(importDirectory);
            Directory.Move(parkedDirectory, importDirectory);
            renderedRealm.Dispose();

            Directory.Exists(importDirectory).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(importDirectory) &&
                (File.GetAttributes(importDirectory) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(importDirectory);
            }

            if (Directory.Exists(parkedDirectory) && !Directory.Exists(importDirectory))
            {
                Directory.Move(parkedDirectory, importDirectory);
            }

            renderedRealm.Dispose();
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void CleanupStaleOwnedRunDirectories_WhenOwnerLeaseIsActive_PreservesTheLiveRun()
    {
        string testDirectory = CreateTestDirectory();
        string temporaryRoot = Path.Combine(testDirectory, "temporary-root");
        KeycloakRealmTemplate? renderedRealm = null;

        try
        {
            renderedRealm = KeycloakRealmTemplate.Render(
                GetRealmSourceDirectory(),
                CreateCredentials(),
                temporaryRoot: temporaryRoot);
            Directory.SetLastWriteTimeUtc(renderedRealm.ImportDirectory, DateTime.UtcNow.AddDays(-2));

            KeycloakRealmTemplate.CleanupStaleOwnedRunDirectories(
                temporaryRoot,
                DateTimeOffset.UtcNow.AddDays(-1));

            Directory.Exists(renderedRealm.ImportDirectory).ShouldBeTrue();
        }
        finally
        {
            renderedRealm?.Dispose();
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Dispose_WhenOwnedFileDeletionFails_PreservesTheMarkerAndCanBeRetried()
    {
        string testDirectory = CreateTestDirectory();
        string temporaryRoot = Path.Combine(testDirectory, "temporary-root");
        bool failDeletion = true;
        KeycloakRealmTemplate renderedRealm = KeycloakRealmTemplate.Render(
            GetRealmSourceDirectory(),
            CreateCredentials(),
            temporaryRoot: temporaryRoot,
            beforeDelete: path =>
            {
                if (failDeletion && string.Equals(Path.GetFileName(path), "hexalith-realm.json", StringComparison.Ordinal))
                {
                    failDeletion = false;
                    throw new IOException("Injected file deletion failure.");
                }
            });
        string importDirectory = renderedRealm.ImportDirectory;

        try
        {
            _ = Should.Throw<IOException>(renderedRealm.Dispose);

            File.Exists(Path.Combine(importDirectory, ".hexalith-owned")).ShouldBeTrue(
                "The ownership marker must remain until every renderer-created secret file is removed.");
            File.Exists(Path.Combine(importDirectory, "hexalith-realm.json")).ShouldBeTrue();

            renderedRealm.Dispose();

            Directory.Exists(importDirectory).ShouldBeFalse();
        }
        finally
        {
            renderedRealm.Dispose();
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Dispose_WhenDirectoryDeletionFailsAfterMarkerRemoval_CanBeRetried()
    {
        string testDirectory = CreateTestDirectory();
        string temporaryRoot = Path.Combine(testDirectory, "temporary-root");
        string? importDirectory = null;
        bool failDeletion = true;
        KeycloakRealmTemplate renderedRealm = KeycloakRealmTemplate.Render(
            GetRealmSourceDirectory(),
            CreateCredentials(),
            temporaryRoot: temporaryRoot,
            beforeDelete: path =>
            {
                if (failDeletion && string.Equals(path, importDirectory, StringComparison.Ordinal))
                {
                    failDeletion = false;
                    throw new UnauthorizedAccessException("Injected directory deletion failure.");
                }
            });
        importDirectory = renderedRealm.ImportDirectory;

        try
        {
            _ = Should.Throw<UnauthorizedAccessException>(renderedRealm.Dispose);

            Directory.Exists(importDirectory).ShouldBeTrue();
            File.Exists(Path.Combine(importDirectory, ".hexalith-owned")).ShouldBeFalse(
                "The directory failure occurs only after the owner marker is deleted last.");

            renderedRealm.Dispose();

            Directory.Exists(importDirectory).ShouldBeFalse();
        }
        finally
        {
            renderedRealm.Dispose();
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private static LocalAuthenticationCredentials CreateCredentials()
        => LocalAuthenticationCredentials.Create();

    private static string CreateTestDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"hexalith-realm-lifecycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string GetRealmSourceDirectory()
        => Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.AppHost",
            "KeycloakRealms");
}

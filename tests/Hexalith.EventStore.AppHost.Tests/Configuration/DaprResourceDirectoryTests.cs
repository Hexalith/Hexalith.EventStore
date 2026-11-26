namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using CommunityToolkit.Aspire.Hosting.Dapr;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using global::Aspire.Hosting.Testing;

using Hexalith.EventStore.Aspire;

[Collection(AspireEnvironmentMutationCollection.Name)]
public sealed class DaprResourceDirectoryTests
{
    [Fact]
    public async Task AppHostModelsOwnSeparateDaprResourceDirectories()
    {
        const string skipPrerequisites = "SKIP_PREREQUISITE_CHECK";
        string? original = Environment.GetEnvironmentVariable(skipPrerequisites);
        string? firstPath = null;
        string? firstEmptyPath = null;
        string? secondPath = null;
        string? secondEmptyPath = null;
        try
        {
            Environment.SetEnvironmentVariable(skipPrerequisites, "true");
            string sourcePath = Path.Combine(RepositoryProjectPaths.GetRepositoryRoot(),
                "src", "Hexalith.EventStore.AppHost", "DaprComponents", "statestore.yaml");
            byte[] expectedStateStore = File.ReadAllBytes(sourcePath);
            await using (IDistributedApplicationTestingBuilder second = await CreateModelAsync().ConfigureAwait(true))
            {
                secondPath = GetStateStorePath(second);
                secondEmptyPath = GetSampleResourcesPath(second);
                await using DistributedApplication secondApplication = await second.BuildAsync().ConfigureAwait(true);
                await using (IDistributedApplicationTestingBuilder first = await CreateModelAsync().ConfigureAwait(true))
                {
                    firstPath = GetStateStorePath(first);
                    firstEmptyPath = GetSampleResourcesPath(first);
                    await using DistributedApplication firstApplication = await first.BuildAsync().ConfigureAwait(true);
                    firstPath.ShouldNotBe(secondPath);
                    firstEmptyPath.ShouldNotBe(secondEmptyPath);
                    Directory.GetFileSystemEntries(firstEmptyPath).ShouldBeEmpty();
                    Directory.GetFileSystemEntries(secondEmptyPath).ShouldBeEmpty();
                    File.ReadAllBytes(firstPath).ShouldBe(expectedStateStore);
                    File.ReadAllBytes(secondPath).ShouldBe(expectedStateStore);
                    Directory.GetFiles(Path.GetDirectoryName(firstPath)!).ShouldBe(new[] { firstPath });
                    Directory.GetFiles(Path.GetDirectoryName(secondPath)!).ShouldBe(new[] { secondPath });
                }

                Directory.GetFileSystemEntries(secondEmptyPath).ShouldBeEmpty();
                File.ReadAllBytes(secondPath).ShouldBe(expectedStateStore);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(skipPrerequisites, original);
            TryDeleteResourceRoot(firstPath);
            TryDeleteResourceRoot(firstEmptyPath);
            TryDeleteResourceRoot(secondPath);
            TryDeleteResourceRoot(secondEmptyPath);
        }
    }

    [Fact]
    public void InstancesOwnSeparateDirectoriesAndDisposeIndependently()
    {
        string sourcePath = Path.Combine(RepositoryProjectPaths.GetRepositoryRoot(),
            "src", "Hexalith.EventStore.AppHost", "DaprComponents", "statestore.yaml");
        byte[] expectedStateStore = File.ReadAllBytes(sourcePath);
        string firstPath;
        string firstEmptyPath;
        string secondPath;
        string secondEmptyPath;
        using (DaprResourceDirectory second = DaprResourceDirectory.Create(sourcePath))
        {
            secondPath = second.StateStorePath;
            secondEmptyPath = second.EmptyResourcesPath;
            using (DaprResourceDirectory first = DaprResourceDirectory.Create(sourcePath))
            {
                firstPath = first.StateStorePath;
                firstEmptyPath = first.EmptyResourcesPath;
                firstPath.ShouldNotBe(secondPath);
                firstEmptyPath.ShouldNotBe(secondEmptyPath);
                Directory.GetFileSystemEntries(firstEmptyPath).ShouldBeEmpty();
                Directory.GetFileSystemEntries(secondEmptyPath).ShouldBeEmpty();
                File.ReadAllBytes(firstPath).ShouldBe(expectedStateStore);
                File.ReadAllBytes(secondPath).ShouldBe(expectedStateStore);
            }

            Directory.Exists(Path.GetDirectoryName(firstPath)).ShouldBeFalse();
            Directory.Exists(firstEmptyPath).ShouldBeFalse();
            Directory.GetFileSystemEntries(secondEmptyPath).ShouldBeEmpty();
            File.ReadAllBytes(secondPath).ShouldBe(expectedStateStore);
        }

        Directory.Exists(Path.GetDirectoryName(secondPath)).ShouldBeFalse();
        Directory.Exists(secondEmptyPath).ShouldBeFalse();
    }

    [Fact]
    public void ManifestAssetsRemainAvailableAfterAppHostDisposal()
    {
        DirectoryInfo output = Directory.CreateTempSubdirectory();
        try
        {
            string sourcePath = Path.Combine(RepositoryProjectPaths.GetRepositoryRoot(),
                "src", "Hexalith.EventStore.AppHost", "DaprComponents", "statestore.yaml");
            string manifestPath = Path.Combine(output.FullName, "aspire-manifest.json");
            string stateStorePath;
            string emptyPath;
            using (DaprResourceDirectory resources = DaprResourceDirectory.Create(sourcePath, manifestPath))
            {
                stateStorePath = resources.StateStorePath;
                emptyPath = resources.EmptyResourcesPath;
            }

            Path.GetRelativePath(output.FullName, stateStorePath).ShouldNotStartWith("..");
            File.ReadAllBytes(stateStorePath).ShouldBe(File.ReadAllBytes(sourcePath));
            Directory.GetFileSystemEntries(emptyPath).ShouldBeEmpty();
        }
        finally
        {
            output.Delete(recursive: true);
        }
    }

    private static Task<IDistributedApplicationTestingBuilder> CreateModelAsync()
        => DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_EventStore_AppHost>(
            ["--EnableKeycloak=false"]);

    private static string GetStateStorePath(IDistributedApplicationTestingBuilder builder)
        => builder.Resources.OfType<DaprComponentResource>().Single(resource => resource.Name == "statestore")
            .Options.ShouldNotBeNull().LocalPath.ShouldNotBeNull();

    private static string GetSampleResourcesPath(IDistributedApplicationTestingBuilder builder)
    {
        ProjectResource sample = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "sample");
        sample.TryGetLastAnnotation<DaprSidecarAnnotation>(out DaprSidecarAnnotation? sidecar).ShouldBeTrue();
        sidecar!.Sidecar.TryGetLastAnnotation<DaprSidecarOptionsAnnotation>(out DaprSidecarOptionsAnnotation? options).ShouldBeTrue();
        return options!.Options.ResourcesPaths.ShouldNotBeNull().ShouldHaveSingleItem();
    }

    private static void TryDeleteResourceRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        DirectoryInfo? directory = File.Exists(path)
            ? new FileInfo(path).Directory
            : new DirectoryInfo(Directory.Exists(path) ? path : Path.GetDirectoryName(path)!);
        while (directory is not null)
        {
            if (directory.Name.StartsWith("hexalith-eventstore-dapr-", StringComparison.Ordinal))
            {
                try
                {
                    directory.Delete(recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                return;
            }

            directory = directory.Parent;
        }
    }
}

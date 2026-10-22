using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;

using Hexalith.EventStore.Aspire;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Verifies direct project execution without changing the resource graph.</summary>
public sealed class CompiledProjectExecutableTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "compiled project " + Guid.NewGuid().ToString("N"));
    private readonly string _executable;

    /// <summary>Creates an executable fixture in a path containing spaces.</summary>
    public CompiledProjectExecutableTests()
    {
        Directory.CreateDirectory(_directory);
        _executable = Path.Combine(_directory, "compiled gateway");
        File.Copy(Environment.ProcessPath!, _executable);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>Retains the project graph and application arguments when selecting the native command.</summary>
    [Fact]
    public async Task DirectCommandPreservesProjectGraphApplicationArgumentsAndAnnotationsAsync()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ProjectResource> dependency = builder.AddProject<EventStoreProjectMetadata>("dependency")
            .WithHttpEndpoint(name: "service");
        IResourceBuilder<ProjectResource> gateway = builder.AddHexalithEventStoreGatewayProject()
            .WithHttpEndpoint(name: "probe", port: 18082)
            .WithReference(dependency)
            .WaitFor(dependency)
            .WithEnvironment("COMPILED_EXECUTABLE_PROBE", "retained")
            .WithArgs("--application-setting", "value with spaces");
        IResource[] graph = builder.Resources.ToArray();
        IResourceAnnotation[] annotations = gateway.Resource.Annotations.ToArray();

        gateway.WithCompiledProjectExecutable(_executable, _directory).ShouldBeSameAs(gateway);

        builder.Resources.ShouldBe(graph);
        gateway.Resource.ShouldBeSameAs(graph.Single(resource => resource.Name == "eventstore"));
        gateway.Resource.Annotations.Where(annotation => annotation is not ExecutableAnnotation).ShouldBe(annotations);
        ExecutableAnnotation command = gateway.Resource.Annotations.OfType<ExecutableAnnotation>().ShouldHaveSingleItem();
        command.Command.ShouldBe(_executable);
        command.WorkingDirectory.ShouldBe(_directory);
        List<object> arguments = [];
        foreach (CommandLineArgsCallbackAnnotation callback in gateway.Resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
        {
            await callback.Callback(new CommandLineArgsCallbackContext(arguments, gateway.Resource, TestContext.Current.CancellationToken));
        }
        arguments.ShouldBe(["--application-setting", "value with spaces"]);
    }

    /// <summary>Rejects blank and relative executable paths without changing the project.</summary>
    /// <param name="path">The invalid executable path.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("relative-command")]
    public void InvalidExecutableIsRejectedWithoutChangingTheProject(string? path)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ProjectResource> gateway = builder.AddHexalithEventStoreGatewayProject();
        int count = gateway.Resource.Annotations.Count;
        Should.Throw<ArgumentException>(() => gateway.WithCompiledProjectExecutable(path!, _directory));
        gateway.Resource.Annotations.Count.ShouldBe(count);
    }

    /// <summary>Rejects blank and relative working directories without adding a command.</summary>
    /// <param name="path">The invalid working directory.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("relative-directory")]
    public void InvalidWorkingDirectoryIsRejected(string? path)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ProjectResource> gateway = builder.AddHexalithEventStoreGatewayProject();
        Should.Throw<ArgumentException>(() => gateway.WithCompiledProjectExecutable(_executable, path!));
        gateway.Resource.Annotations.OfType<ExecutableAnnotation>().ShouldBeEmpty();
    }

    /// <summary>Rejects missing paths and paths whose filesystem type does not match the input.</summary>
    [Fact]
    public void MissingPathsAndDirectoryAsExecutableAreRejected()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ProjectResource> gateway = builder.AddHexalithEventStoreGatewayProject();
        Should.Throw<ArgumentException>(() => gateway.WithCompiledProjectExecutable(Path.Combine(_directory, "missing"), _directory));
        Should.Throw<ArgumentException>(() => gateway.WithCompiledProjectExecutable(_directory, _directory));
        Should.Throw<ArgumentException>(() => gateway.WithCompiledProjectExecutable(_executable, Path.Combine(_directory, "missing")));
        Should.Throw<ArgumentException>(() => gateway.WithCompiledProjectExecutable(_executable, _executable));
    }

    /// <summary>Rejects Unix files without an executable permission bit.</summary>
    [Fact]
    public void NonExecutableFileIsRejectedOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        File.SetUnixFileMode(_executable, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ProjectResource> gateway = builder.AddHexalithEventStoreGatewayProject();
        Should.Throw<ArgumentException>(() => gateway.WithCompiledProjectExecutable(_executable, _directory));
    }

    /// <summary>Refuses direct project execution in publish mode.</summary>
    [Fact]
    public void PublishModeIsRejectedWithoutAddingAnExecutableAnnotation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(["--AppHost:Operation=publish"]);
        IResourceBuilder<ProjectResource> gateway = builder.AddHexalithEventStoreGatewayProject();
        Should.Throw<InvalidOperationException>(() => gateway.WithCompiledProjectExecutable(_executable, _directory));
        gateway.Resource.Annotations.OfType<ExecutableAnnotation>().ShouldBeEmpty();
    }

    /// <summary>Rejects a missing project resource builder.</summary>
    [Fact]
    public void NullBuilderIsRejected()
        => Should.Throw<ArgumentNullException>(() => HexalithEventStoreExecutableExtensions.WithCompiledProjectExecutable(null!, _executable, _directory));

    /// <summary>Removes only this test's private files.</summary>
    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

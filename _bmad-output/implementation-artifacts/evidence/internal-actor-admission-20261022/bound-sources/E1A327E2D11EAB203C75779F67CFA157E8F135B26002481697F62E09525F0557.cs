using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>Controls direct execution of an already compiled project during local orchestration.</summary>
public static class HexalithEventStoreExecutableExtensions
{
    /// <summary>Runs the existing project resource directly with its compiled executable.</summary>
    /// <remarks>
    /// Resolve both paths from the project's SDK run metadata after building it. This run-mode
    /// override suppresses Aspire's synthesized dotnet-run wrapper while retaining the project,
    /// application arguments, environment, endpoints, references, and restart commands.
    /// </remarks>
    /// <param name="builder">The existing project resource builder.</param>
    /// <param name="executablePath">The absolute path to the existing compiled executable.</param>
    /// <param name="workingDirectory">The absolute path to its existing working directory.</param>
    /// <returns>The same project resource builder.</returns>
    /// <exception cref="ArgumentNullException">The builder is null.</exception>
    /// <exception cref="ArgumentException">A path is blank, relative, missing, or not executable.</exception>
    /// <exception cref="InvalidOperationException">The application is not in run mode.</exception>
    public static IResourceBuilder<ProjectResource> WithCompiledProjectExecutable(
        this IResourceBuilder<ProjectResource> builder,
        string executablePath,
        string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (!builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            throw new InvalidOperationException("Compiled project executables require run mode.");
        }

        if (!Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath)
            || (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(executablePath)
                & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0))
        {
            throw new ArgumentException("An absolute existing executable file is required.", nameof(executablePath));
        }

        if (!Path.IsPathFullyQualified(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            throw new ArgumentException("An absolute existing working directory is required.", nameof(workingDirectory));
        }

        return builder.WithAnnotation(new ExecutableAnnotation
        {
            Command = executablePath,
            WorkingDirectory = workingDirectory,
        });
    }
}

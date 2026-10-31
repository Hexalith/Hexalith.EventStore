using global::Aspire.Hosting;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Actual selected child metadata with an explicit build environment and working directory.</summary>
internal sealed class SelectedOwnerProjectMetadata(string projectPath, string workingDirectory, string outputDirectory) : IProjectMetadata
{
    public string ProjectPath => projectPath;
    public string BuildWorkingDirectory => workingDirectory;
    public IReadOnlyDictionary<string, string> BuildEnvironment => new Dictionary<string, string>
    {
        ["SelectedOwnerArtifact"] = "owned", ["SelectedOwnerOutput"] = outputDirectory,
    };
}

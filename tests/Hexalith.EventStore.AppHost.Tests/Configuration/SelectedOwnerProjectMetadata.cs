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
        // Synthetic projects live outside the producer source graph. Mask its injected
        // MSBuild imports so the selected child is evaluated from its own project only.
        ["AfterMicrosoftNETSdkTargets"] = string.Empty,
        ["CustomAfterDirectoryBuildTargets"] = string.Empty,
        ["CustomAfterMicrosoftCommonProps"] = string.Empty,
        ["CustomBeforeDirectoryBuildProps"] = string.Empty,
        ["CustomBeforeDirectoryBuildTargets"] = string.Empty,
        ["HEXALITH_SOURCE_MAPPING_HASH"] = string.Empty,
        ["HEXALITH_SOURCE_MAPPING_PATH"] = string.Empty,
    };
}

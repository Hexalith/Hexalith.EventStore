using System.Diagnostics;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Keeps isolated MSBuild fixtures independent of the caller's producer mapping.</summary>
internal static class SourceMappingFixtureEnvironment
{
    /// <summary>Removes inherited imports before the fixture selects its own mapping inputs.</summary>
    /// <param name="start">The isolated fixture process; the parent environment is unchanged.</param>
    internal static void RemoveInheritedMapping(ProcessStartInfo start)
    {
        foreach (string key in start.Environment.Keys.ToArray())
        {
            if (key is "AfterMicrosoftNETSdkTargets" or "CustomAfterDirectoryBuildTargets"
                or "CustomAfterMicrosoftCommonProps" or "CustomBeforeDirectoryBuildProps"
                or "CustomBeforeDirectoryBuildTargets"
                || key.StartsWith("HEXALITH_SOURCE_MAPPING_", StringComparison.Ordinal))
            {
                start.Environment.Remove(key);
            }
        }
    }
}

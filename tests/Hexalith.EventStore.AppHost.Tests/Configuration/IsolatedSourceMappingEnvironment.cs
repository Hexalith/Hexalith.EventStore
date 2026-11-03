namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Scopes synthetic Aspire child fixtures outside the producer source mapping.</summary>
internal sealed class IsolatedSourceMappingEnvironment : IDisposable
{
    private readonly Dictionary<string, string?> previous;

    private IsolatedSourceMappingEnvironment()
    {
        previous = Environment.GetEnvironmentVariables().Keys.Cast<string>()
            .Where(key => key is "AfterMicrosoftNETSdkTargets" or "CustomAfterDirectoryBuildTargets"
                or "CustomAfterMicrosoftCommonProps" or "CustomBeforeDirectoryBuildProps"
                or "CustomBeforeDirectoryBuildTargets"
                || key.StartsWith("HEXALITH_SOURCE_MAPPING_", StringComparison.Ordinal))
            .ToDictionary(key => key, Environment.GetEnvironmentVariable, StringComparer.Ordinal);
        foreach (string key in previous.Keys) Environment.SetEnvironmentVariable(key, null);
    }

    public static IsolatedSourceMappingEnvironment Enter() => new();

    public void Dispose()
    {
        foreach (var pair in previous) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
    }
}

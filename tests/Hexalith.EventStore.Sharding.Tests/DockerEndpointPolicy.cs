namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Resolves Docker CLI precedence and permits only local transport endpoints.</summary>
internal static class DockerEndpointPolicy
{
    /// <summary>Resolves explicit flags, environment overrides, then the configured context.</summary>
    /// <param name="cliHost">An explicit host flag, if any.</param>
    /// <param name="cliContext">An explicit context flag, if any.</param>
    /// <param name="environment">The environment reader.</param>
    /// <param name="inspectContext">Reads a context endpoint; null selects the current context.</param>
    /// <returns>The validated local endpoint to pin on every Docker invocation.</returns>
    internal static async Task<string> ResolveAsync(string? cliHost, string? cliContext,
        Func<string, string?> environment, Func<string?, Task<string>> inspectContext)
    {
        if (!string.IsNullOrEmpty(cliHost) && !string.IsNullOrEmpty(cliContext))
        {
            throw new InvalidOperationException("Docker host and context flags are mutually exclusive.");
        }

        string endpoint;
        if (!string.IsNullOrEmpty(cliContext)) endpoint = await inspectContext(cliContext);
        else if (!string.IsNullOrEmpty(cliHost)) endpoint = cliHost;
        else if (environment("DOCKER_CONTEXT") is { Length: > 0 } context) endpoint = await inspectContext(context);
        else if (environment("DOCKER_HOST") is { Length: > 0 } host) endpoint = host;
        else endpoint = await inspectContext(null);

        endpoint = endpoint.Trim();
        if ((endpoint.StartsWith("unix:///", StringComparison.Ordinal) && Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
                && uri.Scheme == "unix" && uri.Host.Length == 0 && uri.AbsolutePath.Length > 1 && uri.Query.Length == 0 && uri.Fragment.Length == 0)
            || endpoint.StartsWith("npipe:////./pipe/", StringComparison.OrdinalIgnoreCase))
        {
            return endpoint;
        }

        throw new InvalidOperationException("The sharding lane requires a local Docker unix or npipe endpoint; remote transports are rejected before resource creation.");
    }
}

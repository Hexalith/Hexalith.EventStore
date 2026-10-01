namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Retains only an exact Docker-owned identity, including partial launch failures.</summary>
internal static class Oq8OwnedContainerLaunch
{
    /// <summary>Runs a launch and records its validated CID in every completion path.</summary>
    /// <param name="identityFile">The fixture-private Docker CID file.</param>
    /// <param name="launch">The bounded Docker launch.</param>
    /// <param name="retainOwnership">Records the exact owned identity for cleanup.</param>
    /// <returns>The exact validated identity.</returns>
    internal static async Task<string> RunAsync(string identityFile, Func<Task<string>> launch, Action<string> retainOwnership)
    {
        string identity = string.Empty;
        try
        {
            _ = await launch().ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(identityFile))
            {
                string candidate = File.ReadAllText(identityFile).Trim();
                if (candidate.Length == 64 && candidate.All(static value => value is >= '0' and <= '9' or >= 'a' and <= 'f'))
                {
                    identity = candidate;
                    retainOwnership(identity);
                }
            }
        }

        return identity.Length == 64
            ? identity
            : throw new InvalidOperationException("The OQ8 PostgreSQL launch did not produce a validated owned CID.");
    }
}

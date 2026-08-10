namespace Hexalith.EventStore.ProviderVerification;

internal sealed record ProviderVerificationOptions(
    string PactDirectory,
    string ManifestPath,
    string StateCatalogPath,
    string IdentityRecordPath,
    string IdentityEvidenceDirectory,
    string ReportOutputPath,
    TimeSpan StartupTimeout,
    TimeSpan RequestTimeout,
    TimeSpan CleanupTimeout)
{
    private static readonly HashSet<string> _pathOptions = new(StringComparer.Ordinal)
    {
        "--pact-directory",
        "--manifest",
        "--provider-state-catalog",
        "--identity-record",
        "--identity-evidence-directory",
        "--report-output",
    };

    private static readonly HashSet<string> _timeoutOptions = new(StringComparer.Ordinal)
    {
        "--startup-timeout-seconds",
        "--request-timeout-seconds",
        "--cleanup-timeout-seconds",
    };

    public static bool TryParse(string[] args, out ProviderVerificationOptions? options, out string failureCode)
    {
        options = null;
        failureCode = "input.cli.invalid";
        if (args.Length == 0 || args.Length % 2 != 0)
        {
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            string key = args[index];
            string value = args[index + 1];
            if ((!_pathOptions.Contains(key) && !_timeoutOptions.Contains(key))
                || !values.TryAdd(key, value)
                || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
        }

        if (_pathOptions.Any(key => !values.ContainsKey(key)))
        {
            failureCode = "input.cli.missing-required";
            return false;
        }

        if (!TryTimeout(values, "--startup-timeout-seconds", 15, out TimeSpan startupTimeout)
            || !TryTimeout(values, "--request-timeout-seconds", 10, out TimeSpan requestTimeout)
            || !TryTimeout(values, "--cleanup-timeout-seconds", 10, out TimeSpan cleanupTimeout))
        {
            failureCode = "input.cli.timeout-invalid";
            return false;
        }

        options = new ProviderVerificationOptions(
            values["--pact-directory"],
            values["--manifest"],
            values["--provider-state-catalog"],
            values["--identity-record"],
            values["--identity-evidence-directory"],
            values["--report-output"],
            startupTimeout,
            requestTimeout,
            cleanupTimeout);
        failureCode = string.Empty;
        return true;
    }

    private static bool TryTimeout(
        IReadOnlyDictionary<string, string> values,
        string key,
        int defaultSeconds,
        out TimeSpan timeout)
    {
        timeout = TimeSpan.FromSeconds(defaultSeconds);
        if (!values.TryGetValue(key, out string? value))
        {
            return true;
        }

        if (!int.TryParse(value, out int seconds) || seconds < 1 || seconds > 120)
        {
            return false;
        }

        timeout = TimeSpan.FromSeconds(seconds);
        return true;
    }
}

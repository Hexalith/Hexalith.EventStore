namespace Hexalith.EventStore.ProviderVerification;

internal sealed record ProviderVerificationOptions(
    ProviderVerificationMode Mode,
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

    private static readonly HashSet<string> _valueOptions = new(StringComparer.Ordinal)
    {
        "--verification-mode",
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
            if ((!_pathOptions.Contains(key) && !_timeoutOptions.Contains(key) && !_valueOptions.Contains(key))
                || !values.TryAdd(key, value)
                || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
        }

        ProviderVerificationMode mode = ProviderVerificationMode.HistoricalAuthorization;
        if (values.TryGetValue("--verification-mode", out string? modeValue))
        {
            mode = modeValue switch
            {
                "historical-authorization" => ProviderVerificationMode.HistoricalAuthorization,
                "live-compatibility" => ProviderVerificationMode.LiveCompatibility,
                _ => (ProviderVerificationMode)(-1),
            };
            if (!Enum.IsDefined(mode))
            {
                failureCode = "input.cli.mode-invalid";
                return false;
            }
        }

        string[] requiredPaths = mode == ProviderVerificationMode.LiveCompatibility
            ? ["--pact-directory", "--manifest", "--provider-state-catalog", "--report-output"]
            : [.. _pathOptions];
        if (requiredPaths.Any(key => !values.ContainsKey(key)))
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
            mode,
            values["--pact-directory"],
            values["--manifest"],
            values["--provider-state-catalog"],
            values.GetValueOrDefault("--identity-record", string.Empty),
            values.GetValueOrDefault("--identity-evidence-directory", string.Empty),
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

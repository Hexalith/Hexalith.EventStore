using System.CommandLine;
using System.CommandLine.Parsing;

using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli;

/// <summary>
/// Factory that creates all global <see cref="Option{T}"/> instances and resolves them from a parse result.
/// </summary>
public record GlobalOptionsBinding(
    Option<string> UrlOption,
    Option<string?> TokenOption,
    Option<string> FormatOption,
    Option<string?> OutputOption,
    Option<string?> ProfileOption)
{
    /// <summary>
    /// Gets an optional profile path override. When null, uses the default path.
    /// Used for test injection — production callers leave this null.
    /// </summary>
    internal string? ProfilePath { get; init; }

    /// <summary>
    /// Creates the global option definitions with environment-variable fallbacks and validation.
    /// </summary>
    public static GlobalOptionsBinding Create()
    {
        Option<string> urlOption = new("--url", "-u") { Description = "Admin API base URL", Recursive = true };
        urlOption.DefaultValueFactory = _ =>
            Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_URL") ?? "http://localhost:5002";

        Option<string?> tokenOption = new("--token", "-t") { Description = "JWT Bearer token or API key", Recursive = true };
        tokenOption.DefaultValueFactory = _ =>
            Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_TOKEN");

        Option<string> formatOption = new("--format", "-f") { Description = "Output format", Recursive = true };
        formatOption.DefaultValueFactory = _ =>
            Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_FORMAT") ?? "table";
        formatOption.AcceptOnlyFromAmong("json", "csv", "table");

        Option<string?> outputOption = new("--output", "-o") { Description = "Redirect output to file", Recursive = true };

        Option<string?> profileOption = new("--profile", "-p") { Description = "Connection profile name", Recursive = true };

        return new GlobalOptionsBinding(urlOption, tokenOption, formatOption, outputOption, profileOption);
    }

    /// <summary>
    /// Resolves parsed option values from a parse result, applying profile-aware 4-layer priority.
    /// </summary>
    public GlobalOptions Resolve(ParseResult parseResult)
    {
        (GlobalOptions options, _) = ResolveInternal(parseResult, trackSources: false);
        return options;
    }

    /// <summary>
    /// Resolves parsed option values with per-field source attribution.
    /// Used only by <c>ConfigCurrentCommand</c>.
    /// </summary>
    public (GlobalOptions Options, Dictionary<string, string> Sources) ResolveWithSources(ParseResult parseResult)
        => ResolveInternal(parseResult, trackSources: true);

    private static bool IsExplicitlyProvided(ParseResult parseResult, Option option)
    {
        OptionResult? result = parseResult.GetResult(option);
        if (result is null)
        {
            return false;
        }

        // If the option has tokens on the command line, the user explicitly typed it.
        // Fallback for System.CommandLine versions that don't expose IsImplicit.
        return result.Tokens.Count > 0;
    }

    private (GlobalOptions Options, Dictionary<string, string> Sources) ResolveInternal(ParseResult parseResult, bool trackSources)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        Dictionary<string, string> sources = [];

        // Load profile (once)
        string? profileName = parseResult.GetValue(ProfileOption);
        ConnectionProfile? profile = null;

        try
        {
            ProfileStore store = ProfileManager.Load(ProfilePath);

            if (profileName is not null)
            {
                // Explicit --profile flag
                if (!store.Profiles.TryGetValue(profileName, out profile))
                {
                    throw new ProfileNotFoundException(profileName);
                }
            }
            else if (store.ActiveProfile is not null && store.Profiles.TryGetValue(store.ActiveProfile, out profile))
            {
                // Implicit active profile
                profileName = store.ActiveProfile;
            }
        }
        catch (ProfileStoreVersionException)
        {
            throw;
        }
        catch (ProfileNotFoundException)
        {
            throw;
        }
        catch (InvalidOperationException) when (ProfilePath is null)
        {
            // Cannot determine home directory — skip profile loading
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // File locked, permission denied, etc. — skip profile loading
            Console.Error.WriteLine($"Warning: Could not load profile store: {ex.Message}");
        }

        // Resolve URL: CLI > env > profile > default
        string url = ResolveField(
            parseResult,
            UrlOption,
            "EVENTSTORE_ADMIN_URL",
            profile?.Url,
            profileName,
            "url",
            sources,
            trackSources) ?? "http://localhost:5002";

        // Resolve Token: CLI > env > profile > default
        string? token = ResolveField(
            parseResult,
            TokenOption,
            "EVENTSTORE_ADMIN_TOKEN",
            profile?.Token,
            profileName,
            "token",
            sources,
            trackSources);

        // Resolve Format: CLI > env > profile > default
        string format = ResolveField(
            parseResult,
            FormatOption,
            "EVENTSTORE_ADMIN_FORMAT",
            profile?.Format,
            profileName,
            "format",
            sources,
            trackSources) ?? "table";

        string? outputFile = parseResult.GetValue(OutputOption);

        return (new GlobalOptions(url, token, format, outputFile, profileName), sources);
    }

    private static string? ResolveField(
        ParseResult parseResult,
        Option option,
        string envVarName,
        string? profileValue,
        string? profileName,
        string fieldName,
        Dictionary<string, string> sources,
        bool trackSources)
    {
        // Layer 1: explicit CLI flag
        if (IsExplicitlyProvided(parseResult, option))
        {
            OptionResult? result = parseResult.GetResult(option);
            string? cliValue = result?.GetValueOrDefault<object>()?.ToString();
            if (trackSources)
            {
                sources[fieldName] = "cli";
            }

            return cliValue;
        }

        // Layer 2: environment variable (explicit check — DefaultValueFactory combines env+default)
        string? envValue = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrEmpty(envValue))
        {
            if (trackSources)
            {
                sources[fieldName] = $"env: {envVarName}";
            }

            return envValue;
        }

        // Layer 3: profile value
        if (profileValue is not null)
        {
            if (trackSources)
            {
                sources[fieldName] = $"profile: {profileName}";
            }

            return profileValue;
        }

        // Layer 4: hardcoded default
        if (trackSources)
        {
            sources[fieldName] = "default";
        }

        return null;
    }
}

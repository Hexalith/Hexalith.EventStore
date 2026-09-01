namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.ComponentModel;
using System.Diagnostics;

using global::Aspire.Hosting.ApplicationModel;
using global::Aspire.Hosting.Testing;

using Hexalith.EventStore.Aspire;

[Collection(AspireEnvironmentMutationCollection.Name)]
public sealed class AspireSecurityResourceNamingTests
{
    private const string ReferenceRelationshipType = "Reference";
    private const string SecurityResourceName = "security";

    private static readonly string[] _appHostEnvironmentKeys =
    [
        "SKIP_PREREQUISITE_CHECK",
        HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
        HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey,
        HexalithEventStoreSecurityOptions.DefaultHttpPortConfigurationKey,
        HexalithEventStoreSecurityOptions.DefaultManagementPortConfigurationKey,
    ];

    // A pattern guaranteed to match tracked text inside the audited pathspec. Without it, a
    // mis-resolved repository root or a pathspec that no longer matches any file would make
    // `git grep` exit 1 (no match) and the negative audit would pass vacuously.
    private const string PositiveControlPattern = "HexalithEventStoreSecurityOptions";

    // Root-owned trees that carry operator- and agent-facing guidance. `references` (submodules)
    // and `_bmad`/`_bmad-output` (workflow artifacts that quote these patterns as data) are out of
    // scope. `CHANGELOG.md` is generated from commit subjects and is not hand-correctable.
    // `.artifacts`, `bin`, `obj` and `*.lscache` hold tracked build/restore output: generated for
    // the same reason `CHANGELOG.md` is excluded, so a stale identity there is not hand-correctable.
    private static readonly string[] _auditPathspec =
    [
        ".agents",
        ".claude",
        ".codex",
        ".github",
        ".opencode",
        "deploy",
        "docs",
        "perf",
        "samples",
        "scripts",
        "src",
        "tests",
        "tools",
        "aspire.config.json",
        ":(glob,top)*.md",
        ":(exclude)docs/api/**",
        ":(exclude,glob,top)CHANGELOG.md",
        ":(exclude,glob)**/.artifacts/**",
        ":(exclude,glob)**/bin/**",
        ":(exclude,glob)**/obj/**",
        ":(exclude,glob)**/*.lscache",
    ];

    // The pathspec — not the pattern list — is the load-bearing half of this audit: the stale
    // identity it was widened for lived in `.claude`, which the original `src tests deploy docs`
    // pathspec structurally could not reach. `PositiveControlPattern` cannot detect that
    // regression on its own, because it occurs only under `src` and `tests`, so a re-narrowed
    // pathspec would keep the control green. Every tree below must therefore still contribute
    // tracked files to the scan, which makes re-narrowing fail loudly instead of silently.
    private static readonly string[] _requiredAuditTrees =
    [
        ".agents",
        ".claude",
        ".codex",
        ".github",
        ".opencode",
        "deploy",
        "docs",
        "perf",
        "samples",
        "scripts",
        "src",
        "tests",
        "tools",
    ];

    [Fact]
    public async Task AppHostModel_WhenSecurityEnabled_UsesSecurityResourceAndDependencyEdges()
    {
        IReadOnlyDictionary<string, string?> originalEnvironment = CaptureAppHostEnvironment();

        try
        {
            ConfigureDeterministicAppHostEnvironment(enableKeycloak: true);

            await using IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
                .ConfigureAwait(true);

            IResource security = builder.Resources.Single(
                static resource => string.Equals(
                    resource.Name,
                    SecurityResourceName,
                    StringComparison.Ordinal));

            HexalithEventStoreSecurityOptions.DefaultResourceName.ShouldBe(SecurityResourceName);
            security.ShouldBeOfType<KeycloakResource>();
            builder.Resources.ShouldNotContain(
                static resource => string.Equals(resource.Name, ObsoleteRoleName(), StringComparison.OrdinalIgnoreCase));

            // Reference-edge counts are derived, not chosen: each dependent gets exactly one from
            // `WithSecurityDependency`'s own `WithReference`, plus one per environment variable
            // whose value is a `ReferenceExpression` over the realm URL. `WithJwtBearerSecurity`
            // supplies two such variables (authority and issuer) and
            // `WithEventStoreAuthenticationValidation` one (authority) -- hence 3 and 2. Adding or
            // removing a realm-URL-valued variable legitimately changes these numbers; update them
            // deliberately rather than assuming identity drift.
            Dictionary<string, int> expectedReferenceCounts = new(StringComparer.Ordinal)
            {
                ["eventstore"] = 3,
                ["eventstore-admin"] = 3,
                ["eventstore-admin-ui"] = 2,
                ["sample-api"] = 2,
                ["sample-blazor-ui"] = 2,
                ["tenants"] = 3,
                ["tenants-api"] = 2,
            };

            string[] expectedDependents = expectedReferenceCounts.Keys.Order(StringComparer.Ordinal).ToArray();
            builder.Resources
                .Where(resource => resource.Annotations
                    .OfType<ResourceRelationshipAnnotation>()
                    .Any(annotation => string.Equals(annotation.Type, ReferenceRelationshipType, StringComparison.Ordinal)
                        && ReferenceEquals(annotation.Resource, security)))
                .Select(static resource => resource.Name)
                .Order(StringComparer.Ordinal)
                .ShouldBe(expectedDependents);
            builder.Resources
                .Where(resource => resource.Annotations
                    .OfType<WaitAnnotation>()
                    .Any(annotation => ReferenceEquals(annotation.Resource, security)))
                .Select(static resource => resource.Name)
                .Order(StringComparer.Ordinal)
                .ShouldBe(expectedDependents);

            foreach (KeyValuePair<string, int> expected in expectedReferenceCounts)
            {
                IResource dependent = builder.Resources.Single(resource => string.Equals(
                    resource.Name,
                    expected.Key,
                    StringComparison.Ordinal));
                dependent.Annotations
                    .OfType<ResourceRelationshipAnnotation>()
                    .Count(annotation => string.Equals(annotation.Type, ReferenceRelationshipType, StringComparison.Ordinal)
                        && ReferenceEquals(annotation.Resource, security))
                    .ShouldBe(expected.Value, $"Expected the exact Reference edge count for {expected.Key}.");
                dependent.Annotations
                    .OfType<WaitAnnotation>()
                    .Count(annotation => ReferenceEquals(annotation.Resource, security))
                    .ShouldBe(1, $"Expected exactly one WaitFor edge from {expected.Key} to {SecurityResourceName}.");
            }
        }
        finally
        {
            RestoreAppHostEnvironment(originalEnvironment);
        }
    }

    [Fact]
    public async Task AppHostModel_WhenSecurityDisabled_HasNoSecurityResourceOrDependencyEdges()
    {
        IReadOnlyDictionary<string, string?> originalEnvironment = CaptureAppHostEnvironment();

        try
        {
            ConfigureDeterministicAppHostEnvironment(enableKeycloak: false);

            await using IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
                .ConfigureAwait(true);

            builder.Resources.ShouldNotContain(
                static resource => string.Equals(resource.Name, SecurityResourceName, StringComparison.Ordinal));

            builder.Resources
                .SelectMany(static resource => resource.Annotations
                    .OfType<ResourceRelationshipAnnotation>()
                    .Select(annotation => new { Dependent = resource.Name, Target = annotation.Resource.Name }))
                .Where(static edge => string.Equals(edge.Target, SecurityResourceName, StringComparison.Ordinal))
                .Select(static edge => edge.Dependent)
                .ShouldBeEmpty();
            builder.Resources
                .SelectMany(static resource => resource.Annotations
                    .OfType<WaitAnnotation>()
                    .Select(annotation => new { Dependent = resource.Name, Target = annotation.Resource.Name }))
                .Where(static edge => string.Equals(edge.Target, SecurityResourceName, StringComparison.Ordinal))
                .Select(static edge => edge.Dependent)
                .ShouldBeEmpty();
        }
        finally
        {
            RestoreAppHostEnvironment(originalEnvironment);
        }
    }

    [Fact]
    public async Task RootOwnedSources_WhenScanned_ContainNoStaleSecurityRoleIdentities()
    {
        string repositoryRoot = RepositoryProjectPaths.GetRepositoryRoot();
        string[] staleIdentityPatterns = CreateStaleIdentityPatterns();

        // Positive control first: prove the scan actually reaches tracked repository text, so a
        // clean negative result cannot be produced by a scan that inspected nothing.
        string[] controlMatches = await FindTrackedMatchesAsync(repositoryRoot, [PositiveControlPattern])
            .ConfigureAwait(true);
        controlMatches.ShouldNotBeEmpty(
            "The stale-identity audit scanned no tracked text; its repository root or pathspec is wrong.");

        // Coverage control second: the pattern control above only proves `src`/`tests` are
        // reachable, so it cannot see a pathspec re-narrowed away from the agent-, CI- and
        // operator-facing trees. Require every audited tree to still contribute tracked files.
        string[] trackedPaths = await ListTrackedPathsAsync(repositoryRoot).ConfigureAwait(true);
        foreach (string tree in _requiredAuditTrees)
        {
            string prefix = tree + "/";
            trackedPaths.ShouldContain(
                path => path.StartsWith(prefix, StringComparison.Ordinal),
                $"The stale-identity audit no longer reaches '{tree}'; obsolete role identities there would pass unnoticed.");
        }

        trackedPaths.ShouldContain(
            static path => !path.Contains('/', StringComparison.Ordinal)
                && path.EndsWith(".md", StringComparison.OrdinalIgnoreCase),
            "The stale-identity audit no longer reaches root-level Markdown.");
        trackedPaths.ShouldContain(
            "aspire.config.json",
            "The stale-identity audit no longer reaches the root Aspire operator configuration.");

        string[] staleMatches = await FindTrackedMatchesAsync(repositoryRoot, staleIdentityPatterns)
            .ConfigureAwait(true);

        staleMatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task StaleIdentityPatterns_WhenForbiddenFormsAreTracked_ReportEveryPathAndLine()
    {
        string implementationName = ObsoleteRoleName();
        string[] forbiddenForms =
        [
            $@"builder.AddKeycloak(""{implementationName}"")",
            $@"fixture.CreateHttpClient(""{implementationName}"")",
            $@"https://{implementationName}:8080/realms/hexalith",
            $@"`{implementationName}`",
            $@"{implementationName}:",
            $@"docker compose logs {implementationName}",
            $@"name={implementationName}",
            $@"WaitFor({implementationName})",
            $$"""{"to":"{{implementationName}}"}""",
            $@"SecurityResourceName = ""{implementationName}""",
            $@"| All services | {implementationName} |",
            $@"aspire wait {implementationName}",
            $@"aspire wait {implementationName} --non-interactive",
            $@"aspire wait --timeout 30 {implementationName}",
            $@"aspire wait --status Healthy {implementationName}",
            $@"aspire wait --apphost src/AppHost.csproj {implementationName}",
            $@"aspire wait ""{implementationName}"" --non-interactive",
            $@"aspire wait '{implementationName}';",
            $@"aspire wait {implementationName}; echo ready",
            $@"aspire wait {implementationName}.",
        ];
        string repositoryRoot = Directory.CreateTempSubdirectory("eventstore-stale-identity-audit-").FullName;
        try
        {
            string documentationDirectory = Path.Combine(repositoryRoot, "docs");
            Directory.CreateDirectory(documentationDirectory);
            string operatorSurface = Path.Combine(documentationDirectory, "operator.md");
            await File.WriteAllLinesAsync(operatorSurface, forbiddenForms).ConfigureAwait(true);

            (int initExitCode, _, string initError) = await RunGitAsync(
                repositoryRoot,
                ["init", "--quiet"],
                includeAuditPathspec: false).ConfigureAwait(true);
            initExitCode.ShouldBe(0, initError);
            (int addExitCode, _, string addError) = await RunGitAsync(
                repositoryRoot,
                ["add", "docs/operator.md"],
                includeAuditPathspec: false).ConfigureAwait(true);
            addExitCode.ShouldBe(0, addError);

            string[] matches = await FindTrackedMatchesAsync(repositoryRoot, CreateStaleIdentityPatterns())
                .ConfigureAwait(true);

            matches.Length.ShouldBe(forbiddenForms.Length);
            for (int index = 0; index < forbiddenForms.Length; index++)
            {
                matches.ShouldContain(
                    match => match.StartsWith($"docs/operator.md:{index + 1}:", StringComparison.Ordinal),
                    $"Expected the audit to report docs/operator.md line {index + 1}: {forbiddenForms[index]}");
            }
        }
        finally
        {
            Directory.Delete(repositoryRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AppHostModel_WhenHostileCallerEnvironmentIsSeeded_RestoresEveryExactValue()
    {
        IReadOnlyDictionary<string, string?> callerEnvironment = CaptureAppHostEnvironment();
        IReadOnlyDictionary<string, string?> seededEnvironment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SKIP_PREREQUISITE_CHECK"] = null,
            [HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey] = "hostile-caller-value",
            [HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey] = "true",
            [HexalithEventStoreSecurityOptions.DefaultHttpPortConfigurationKey] = "invalid-hostile-http-port",
            [HexalithEventStoreSecurityOptions.DefaultManagementPortConfigurationKey] = "invalid-hostile-management-port",
        };

        try
        {
            RestoreAppHostEnvironment(seededEnvironment);
            IReadOnlyDictionary<string, string?> seededSnapshot = CaptureAppHostEnvironment();
            try
            {
                ConfigureDeterministicAppHostEnvironment(enableKeycloak: true);

                await using IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                    .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
                    .ConfigureAwait(true);

                KeycloakResource security = builder.Resources
                    .OfType<KeycloakResource>()
                    .Single(static resource => string.Equals(
                        resource.Name,
                        SecurityResourceName,
                        StringComparison.Ordinal));
                security.Annotations
                    .OfType<ContainerLifetimeAnnotation>()
                    .Select(static annotation => annotation.Lifetime)
                    .ShouldNotContain(ContainerLifetime.Persistent);
            }
            finally
            {
                RestoreAppHostEnvironment(seededSnapshot);
            }

            foreach (KeyValuePair<string, string?> seededValue in seededEnvironment)
            {
                Environment.GetEnvironmentVariable(seededValue.Key).ShouldBe(
                    seededValue.Value,
                    $"Expected exact restoration of {seededValue.Key}, including null values.");
            }
        }
        finally
        {
            RestoreAppHostEnvironment(callerEnvironment);
        }
    }

    private static IReadOnlyDictionary<string, string?> CaptureAppHostEnvironment()
        => _appHostEnvironmentKeys.ToDictionary(
            static key => key,
            static key => Environment.GetEnvironmentVariable(key),
            StringComparer.Ordinal);

    private static void ConfigureDeterministicAppHostEnvironment(bool enableKeycloak)
    {
        Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", "true");
        Environment.SetEnvironmentVariable(
            HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
            enableKeycloak ? "true" : "false");
        Environment.SetEnvironmentVariable(
            HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey,
            "false");
        Environment.SetEnvironmentVariable(
            HexalithEventStoreSecurityOptions.DefaultHttpPortConfigurationKey,
            "hostile-not-a-port");
        Environment.SetEnvironmentVariable(
            HexalithEventStoreSecurityOptions.DefaultManagementPortConfigurationKey,
            "hostile-not-a-port");
    }

    private static void RestoreAppHostEnvironment(IReadOnlyDictionary<string, string?> originalEnvironment)
    {
        foreach (KeyValuePair<string, string?> variable in originalEnvironment)
        {
            Environment.SetEnvironmentVariable(variable.Key, variable.Value);
        }
    }

    private static string[] CreateStaleIdentityPatterns()
    {
        string implementationName = ObsoleteRoleName();
        string optionalNamedArgument = @"(?:[A-Za-z_][A-Za-z0-9_]*\s*:\s*)?";
        return
        [
            $@"AddKeycloak\s*\(\s*{optionalNamedArgument}""{implementationName}""",
            $@"(?:GetEndpoint|CreateHttpClient|WaitForResourceHealthyAsync)\s*\(\s*{optionalNamedArgument}""{implementationName}""",
            $@"https?://{implementationName}(?=[:/""'\s]|$)",
            $@"`{implementationName}`",
            $@"^\s*{implementationName}\s*:",
            $@"compose\s+(?:ps|logs)\s+{implementationName}(?=\s|$)",
            $@"name\s*=\s*{implementationName}(?=[""'\s)]|$)",
            $@"WaitFor\s*\(\s*{implementationName}\s*\)",
            $@"""to""\s*:\s*""{implementationName}""",
            $@"SecurityResourceName\s*=\s*""{implementationName}""",
            $@"^\s*\|\s*All\s+services\s*\|\s*{implementationName}\s*\|",
            $@"\baspire[^\S\r\n]+wait[^\r\n]*?(?<![\w-])(?:""{implementationName}""|'{implementationName}'|{implementationName})(?=$|[^\w-])",
        ];
    }

    /// <summary>
    /// Builds the obsolete role identity at run time. The audit's own patterns are interpolated
    /// from this value for the same reason: a literal in this file would match the patterns it
    /// defines and turn the audit permanently red against itself. Do not inline it.
    /// </summary>
    private static string ObsoleteRoleName() => "key" + "cloak";

    private static async Task<string[]> FindTrackedMatchesAsync(string repositoryRoot, IEnumerable<string> patterns)
    {
        List<string> arguments = ["grep", "--line-number", "--full-name", "--ignore-case", "--perl-regexp", "-I"];
        foreach (string pattern in patterns)
        {
            arguments.Add("-e");
            arguments.Add(pattern);
        }

        (int exitCode, string standardOutput, string standardError) = await RunGitAsync(repositoryRoot, arguments)
            .ConfigureAwait(true);

        // `git grep` exits 1 when nothing matched, which is the clean result for a negative audit.
        return exitCode switch
        {
            1 => [],
            0 => SplitLines(standardOutput),
            _ => throw new InvalidOperationException(
                $"Tracked stale-identity audit failed with git exit code {exitCode}: {standardError.Trim()}"),
        };
    }

    private static async Task<string[]> ListTrackedPathsAsync(string repositoryRoot)
    {
        (int exitCode, string standardOutput, string standardError) = await RunGitAsync(repositoryRoot, ["ls-files"])
            .ConfigureAwait(true);

        return exitCode == 0
            ? SplitLines(standardOutput)
            : throw new InvalidOperationException(
                $"Listing the audited pathspec failed with git exit code {exitCode}: {standardError.Trim()}");
    }

    private static string[] SplitLines(string output) => output.Split(
        ['\r', '\n'],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Runs git over the audited pathspec and returns its exit code with both drained streams.
    /// </summary>
    /// <param name="repositoryRoot">Working directory for the git invocation.</param>
    /// <param name="arguments">Git subcommand and options, without the pathspec separator.</param>
    /// <returns>The exit code, standard output and standard error of the git process.</returns>
    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunGitAsync(
        string repositoryRoot,
        IEnumerable<string> arguments,
        bool includeAuditPathspec = true)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (includeAuditPathspec)
        {
            startInfo.ArgumentList.Add("--");
            foreach (string pathspec in _auditPathspec)
            {
                startInfo.ArgumentList.Add(pathspec);
            }
        }

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start git for the tracked stale-identity audit.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "git must be available on PATH to run the tracked stale-identity audit.",
                exception);
        }

        using (process)
        {
            // Drain both pipes concurrently before waiting: reading them one after the other
            // deadlocks whenever the unread pipe fills its buffer.
            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().ConfigureAwait(true);
            string standardOutput = await standardOutputTask.ConfigureAwait(true);
            string standardError = await standardErrorTask.ConfigureAwait(true);

            return (process.ExitCode, standardOutput, standardError);
        }
    }
}

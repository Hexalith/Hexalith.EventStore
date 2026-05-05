using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Hexalith.EventStore.DeferredWorkGovernance.Tests;

internal static class Dw6GovernanceCheckerInvokerFactory {
    public static IDw6GovernanceCheckerInvoker Create() {
        string? entrypoint = ReadEntrypointDeclaration();
        if (string.IsNullOrWhiteSpace(entrypoint)) {
            throw new Dw6GovernanceCheckerNotConfiguredException(
                $"DW6 governance checker entrypoint not declared. Create '{Dw6TestPaths.EntrypointPath}' " +
                "with one line: 'pwsh:<script-path>', 'sh:<script-path>', or " +
                "'dotnet:<type-qualified-name>'. Keep ATDD scaffolds skipped until an implementation task activates them.");
        }

        if (entrypoint.StartsWith("pwsh:", StringComparison.Ordinal)) {
            return new Dw6ShellGovernanceCheckerInvoker("pwsh", entrypoint["pwsh:".Length..]);
        }

        if (entrypoint.StartsWith("sh:", StringComparison.Ordinal)) {
            return new Dw6ShellGovernanceCheckerInvoker("sh", entrypoint["sh:".Length..]);
        }

        if (entrypoint.StartsWith("dotnet:", StringComparison.Ordinal)) {
            return new Dw6InProcessGovernanceCheckerInvoker(entrypoint["dotnet:".Length..]);
        }

        throw new Dw6GovernanceCheckerNotConfiguredException(
            $"Unknown DW6 checker entrypoint scheme '{entrypoint}'. Use 'pwsh:', 'sh:', or 'dotnet:'.");
    }

    private static string? ReadEntrypointDeclaration() {
        string fullPath = Path.Combine(Dw6TestPaths.LocateRepoRoot(), Dw6TestPaths.EntrypointPath);
        if (!File.Exists(fullPath)) {
            return null;
        }

        string content = File.ReadAllText(fullPath).Trim();
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }
}

internal sealed class Dw6GovernanceCheckerNotConfiguredException : InvalidOperationException {
    public Dw6GovernanceCheckerNotConfiguredException(string message)
        : base(message) {
    }
}

internal sealed class Dw6ShellGovernanceCheckerInvoker(string shell, string scriptPath) : IDw6GovernanceCheckerInvoker {
    public async Task<Dw6GovernanceReport> CheckAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default) {
        string repoRoot = Dw6TestPaths.LocateRepoRoot();
        string fullScriptPath = Path.Combine(repoRoot, scriptPath);
        string joinedArguments = string.Join(" ", arguments.Select(Quote));
        string commandArguments = shell.Equals("pwsh", StringComparison.OrdinalIgnoreCase)
            ? $"-NoProfile -File {Quote(fullScriptPath)} --json {joinedArguments}"
            : $"{Quote(fullScriptPath)} --json {joinedArguments}";

        ProcessStartInfo startInfo = new(shell, commandArguments) {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start DW6 governance checker process.");

        string stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(stdout)) {
            return new Dw6GovernanceReport(
                process.ExitCode,
                EmptyCounts(),
                [new(Dw6TestPaths.DeferredWorkPath, "dw6-checker-no-json-output", null, string.Empty, stderr, null, "Checker emitted no JSON output.")]);
        }

        return ParseReport(stdout, process.ExitCode);
    }

    private static string Quote(string value)
        => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static Dw6GovernanceReport ParseReport(string json, int fallbackExitCode) {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        int exitCode = root.TryGetProperty("exitCode", out JsonElement exit)
            ? exit.GetInt32()
            : fallbackExitCode;

        Dictionary<string, int> counts = EmptyCounts();
        if (root.TryGetProperty("counts", out JsonElement countsElement)) {
            foreach (JsonProperty property in countsElement.EnumerateObject()) {
                counts[property.Name] = property.Value.GetInt32();
            }
        }

        List<Dw6GovernanceDiagnostic> diagnostics = [];
        if (root.TryGetProperty("diagnostics", out JsonElement diagnosticsElement)) {
            foreach (JsonElement item in diagnosticsElement.EnumerateArray()) {
                diagnostics.Add(new(
                    GetString(item, "file") ?? Dw6TestPaths.DeferredWorkPath,
                    GetString(item, "rule") ?? "dw6-diagnostic-rule-missing",
                    GetString(item, "disposition"),
                    GetString(item, "heading") ?? string.Empty,
                    GetString(item, "excerpt") ?? string.Empty,
                    item.TryGetProperty("line", out JsonElement line) && line.ValueKind == JsonValueKind.Number ? line.GetInt32() : null,
                    GetString(item, "hint") ?? string.Empty));
            }
        }

        return new Dw6GovernanceReport(exitCode, counts, diagnostics);
    }

    private static string? GetString(JsonElement item, string name)
        => item.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static Dictionary<string, int> EmptyCounts()
        => Dw6RuleVocabulary.CountBuckets.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);
}

internal sealed class Dw6InProcessGovernanceCheckerInvoker(string typeQualifiedName) : IDw6GovernanceCheckerInvoker {
    public Task<Dw6GovernanceReport> CheckAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default) {
        Type checkerType = Type.GetType(typeQualifiedName, throwOnError: true)
            ?? throw new InvalidOperationException($"Could not load DW6 checker type '{typeQualifiedName}'.");

        MethodInfo method = checkerType.GetMethod(
            "Check",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(IEnumerable<string>)],
            modifiers: null)
            ?? throw new InvalidOperationException("DW6 checker type must expose public static Check(IEnumerable<string>).");

        object? result = method.Invoke(null, [arguments]);
        if (result is Dw6GovernanceReport report) {
            return Task.FromResult(report);
        }

        throw new InvalidOperationException("DW6 in-process checker returned an unsupported result shape.");
    }
}

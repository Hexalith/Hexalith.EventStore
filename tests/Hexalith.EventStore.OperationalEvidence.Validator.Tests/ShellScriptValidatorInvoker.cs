using System.Diagnostics;
using System.Text.Json;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// Invokes a PowerShell or shell-script validator and parses its JSON output
/// into <see cref="Dw4ValidationOutcome"/>.
///
/// Contract the dev's script must satisfy:
/// - Exit code <c>0</c> on pass, non-zero on any error-level diagnostic
///   emission. Informational diagnostics (e.g. <c>evidence-file-skipped</c>
///   added in DW9) do not affect exit code.
/// - Standard output: a single JSON object matching:
///   <code>
///   { "diagnostics": [
///       { "file": "...", "schema": "query-operational-evidence/v1",
///         "rule": "placeholder-unreplaced", "section": "Run Identity",
///         "field": "evidence_run_id", "line": 42, "hint": "...",
///         "level": "error" },
///       ...
///   ] }
///   </code>
/// - <c>level</c> is one of <c>"error"</c> or <c>"info"</c>. Diagnostics that
///   pre-date the field default to <c>"error"</c>.
/// - One JSON object per invocation. Diagnostic ordering must be the canonical
///   <c>(file, schema, rule, section, field, line)</c> sort.
///
/// CLI args passed to the script: <c>--json &lt;fixture-path&gt; [&lt;more-paths&gt;...]</c>.
///
/// In the red phase no such script exists. <see cref="Validate"/> will throw
/// <see cref="Dw4ValidatorNotConfiguredException"/>; tests stay
/// <c>[Fact(Skip = ...)]</c>.
/// </summary>
internal sealed class ShellScriptValidatorInvoker : IDw4ValidatorInvoker {
    private readonly string _shell;
    private readonly string _scriptPath;
    private readonly string _repoRoot;

    public ShellScriptValidatorInvoker(string shell, string scriptPath) {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _scriptPath = scriptPath ?? throw new ArgumentNullException(nameof(scriptPath));
        _repoRoot = LocateRepoRoot();
    }

    public string EntrypointDescription => $"{_shell}:{_scriptPath}";

    public Dw4ValidationOutcome Validate(IEnumerable<string> fixturePaths) {
        string resolvedScriptPath = Path.IsPathRooted(_scriptPath)
            ? _scriptPath
            : Path.Combine(_repoRoot, _scriptPath);
        if (!File.Exists(resolvedScriptPath)) {
            throw new Dw4ValidatorNotConfiguredException(
                $"Script '{resolvedScriptPath}' not found. Either fix the path in " +
                "entrypoint.txt or commit the validator script.");
        }

        ProcessStartInfo psi = new() {
            FileName = _shell,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.WorkingDirectory = _repoRoot;
        psi.ArgumentList.Add(resolvedScriptPath);
        psi.ArgumentList.Add("--json");
        foreach (string path in fixturePaths) {
            psi.ArgumentList.Add(path);
        }

        using Process process = new() { StartInfo = psi };
        if (!process.Start()) {
            throw new Dw4ValidatorNotConfiguredException(
                $"Failed to start validator process '{_shell} {_scriptPath}'.");
        }

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        IReadOnlyList<Dw4Diagnostic> diagnostics = ParseDiagnostics(stdout, stderr);
        return new Dw4ValidationOutcome(process.ExitCode, diagnostics);
    }

    private static IReadOnlyList<Dw4Diagnostic> ParseDiagnostics(string stdout, string stderr) {
        if (string.IsNullOrWhiteSpace(stdout)) {
            // Empty stdout with non-zero exit means the script emitted no JSON
            // — a contract violation we surface as a red-phase configuration error
            // so dev sees it immediately.
            return [];
        }

        using JsonDocument doc = JsonDocument.Parse(stdout);
        if (!doc.RootElement.TryGetProperty("diagnostics", out JsonElement arr) ||
            arr.ValueKind != JsonValueKind.Array) {
            throw new Dw4ValidatorNotConfiguredException(
                $"Validator stdout did not contain a 'diagnostics' array. " +
                $"stdout=<{stdout}> stderr=<{stderr}>");
        }

        List<Dw4Diagnostic> result = [];
        foreach (JsonElement el in arr.EnumerateArray()) {
            result.Add(new Dw4Diagnostic(
                File: el.GetProperty("file").GetString() ?? string.Empty,
                Schema: el.TryGetProperty("schema", out JsonElement s) ? s.GetString() : null,
                Rule: el.GetProperty("rule").GetString() ?? string.Empty,
                Section: el.TryGetProperty("section", out JsonElement sec) ? sec.GetString() : null,
                Field: el.TryGetProperty("field", out JsonElement f) ? f.GetString() : null,
                Line: el.TryGetProperty("line", out JsonElement l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : null,
                Hint: el.TryGetProperty("hint", out JsonElement h) ? h.GetString() ?? string.Empty : string.Empty,
                Level: el.TryGetProperty("level", out JsonElement lv) ? lv.GetString() ?? "error" : "error"));
        }

        return result;
    }

    private static string LocateRepoRoot() {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null) {
            if (File.Exists(Path.Combine(dir, "Hexalith.EventStore.slnx"))) {
                return dir;
            }

            DirectoryInfo? parent = Directory.GetParent(dir);
            if (parent is null) {
                break;
            }

            dir = parent.FullName;
        }

        throw new Dw4ValidatorNotConfiguredException(
            "Could not locate repo root (Hexalith.EventStore.slnx not found).");
    }
}

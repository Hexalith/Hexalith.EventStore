namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// Selects the validator invoker based on the entrypoint declaration recorded
/// by dev. This indirection lets the ATDD scaffolds compile and remain stable
/// regardless of which shape the dev picks for the validator (script vs
/// .NET tool vs schema-plus-lint companion).
///
/// During the red phase (entrypoint.txt absent), every invocation throws
/// <see cref="Dw4ValidatorNotConfiguredException"/>. Tests use
/// <c>[Fact(Skip = ...)]</c> so this exception is never thrown in CI; once a
/// dev removes Skip without declaring an entrypoint, the test fails with a
/// clear remediation message.
/// </summary>
internal static class Dw4ValidatorInvokerFactory {
    public const string EntrypointFilePath
        = "_bmad-output/test-artifacts/operational-evidence-validator/entrypoint.txt";

    public static IDw4ValidatorInvoker Create() {
        string? entrypoint = ReadEntrypointDeclaration();
        if (string.IsNullOrWhiteSpace(entrypoint)) {
            throw new Dw4ValidatorNotConfiguredException(
                $"DW4 validator entrypoint not declared. Create '{EntrypointFilePath}' " +
                "with a single line: either an assembly-qualified .NET type for an " +
                "in-process validator, or 'pwsh:<script-path>' / 'sh:<script-path>' " +
                "for a shell-script validator. Until then, ATDD red-phase scaffolds " +
                "must remain skipped.");
        }

        // Resolution rules — kept narrow on purpose. Dev expands these as the
        // validator implementation matures.
        if (entrypoint.StartsWith("pwsh:", StringComparison.Ordinal)) {
            return new ShellScriptValidatorInvoker(
                shell: "pwsh",
                scriptPath: entrypoint["pwsh:".Length..]);
        }

        if (entrypoint.StartsWith("sh:", StringComparison.Ordinal)) {
            return new ShellScriptValidatorInvoker(
                shell: "sh",
                scriptPath: entrypoint["sh:".Length..]);
        }

        if (entrypoint.StartsWith("dotnet:", StringComparison.Ordinal)) {
            return new InProcessValidatorInvoker(typeQualifiedName: entrypoint["dotnet:".Length..]);
        }

        throw new Dw4ValidatorNotConfiguredException(
            $"Unknown entrypoint scheme '{entrypoint}'. Use 'pwsh:', 'sh:', or 'dotnet:'.");
    }

    private static string? ReadEntrypointDeclaration() {
        string repoRoot = LocateRepoRoot();
        string fullPath = Path.Combine(repoRoot, EntrypointFilePath);
        if (!File.Exists(fullPath)) {
            return null;
        }

        string content = File.ReadAllText(fullPath).Trim();
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    private static string LocateRepoRoot() {
        // Walk up from this assembly's location until we find a folder
        // containing Hexalith.EventStore.slnx (the project's pinned solution
        // file). This avoids hard-coded relative paths from the test output dir.
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
            "Could not locate repo root (Hexalith.EventStore.slnx not found). " +
            "DW4 ATDD scaffolds expect to run from inside the repo working tree.");
    }
}

/// <summary>
/// Thrown when the DW4 validator entrypoint is missing or malformed. Surfacing
/// a distinct exception type lets tests assert "red phase wired but validator
/// not declared" vs "validator declared but failed" cleanly.
/// </summary>
internal sealed class Dw4ValidatorNotConfiguredException : InvalidOperationException {
    public Dw4ValidatorNotConfiguredException(string message)
        : base(message) {
    }
}

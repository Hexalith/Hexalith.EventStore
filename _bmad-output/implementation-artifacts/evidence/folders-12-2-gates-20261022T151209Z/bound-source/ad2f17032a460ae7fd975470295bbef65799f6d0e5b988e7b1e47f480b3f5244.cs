namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// Validator-shape-neutral invocation seam. The DW4 story explicitly defers
/// the implementation choice (PowerShell/shell script, .NET test/tool, or
/// JSON Schema + lint companion). Tests target this interface so the same
/// scaffolds work regardless of which shape the dev picks.
///
/// Two implementations ship in this project:
/// - <see cref="InProcessValidatorInvoker"/> — for a .NET validator library.
/// - <see cref="ShellScriptValidatorInvoker"/> — for a PowerShell or shell script.
///
/// The factory selects between them by reading
/// <c>_bmad-output/test-artifacts/operational-evidence-validator/entrypoint.txt</c>
/// (one line: assembly-qualified type name OR path-to-script with optional args).
/// If the file is absent, all DW4 tests are skipped (red phase) and the dev
/// is prompted to declare the chosen shape.
/// </summary>
internal interface IDw4ValidatorInvoker {
    /// <summary>
    /// Validate the given fixture files and return the aggregated outcome.
    /// </summary>
    /// <param name="fixturePaths">Absolute or repo-rooted fixture file paths.</param>
    Dw4ValidationOutcome Validate(IEnumerable<string> fixturePaths);

    /// <summary>
    /// Validator's stable identity (e.g. <c>scripts/validate-evidence.ps1</c>
    /// or <c>Hexalith.EventStore.OperationalEvidenceValidator.Validator</c>).
    /// Recorded in test output so reviewers can see which entrypoint produced
    /// the diagnostics.
    /// </summary>
    string EntrypointDescription { get; }
}

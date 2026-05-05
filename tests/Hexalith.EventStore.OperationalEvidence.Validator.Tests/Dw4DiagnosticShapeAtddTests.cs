using System.Text.RegularExpressions;

using Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for AC #13 — diagnostic shape and ordering
/// guarantees. Every emitted diagnostic carries a stable rule id, names its
/// source file, identifies its schema, and (where applicable) carries section
/// and field. Sort order is deterministic so a future automation summary can
/// produce stable output.
/// </summary>
public class Dw4DiagnosticShapeAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";

    private static readonly Regex _ruleIdPattern = new(
        @"^[a-z][a-z0-9-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact(Skip = _baseSkip + "AC#13 — every emitted rule id must match the naming contract. Remove Skip when implementing.")]
    public void DiagnosticRuleIds_MatchNamingContract() {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        // Run validator across every negative fixture and collect every emitted
        // rule id. Each must conform to the naming contract.
        IEnumerable<string> negativeFixturePaths = Dw4FixtureCatalog.All
            .Where(f => f.ExpectedVerdict == Dw4FixtureCatalog.Verdict.Fail)
            .Select(f => Path.Combine(Dw4FixtureCatalog.FixtureRoot, f.FileName));

        Dw4ValidationOutcome outcome = invoker.Validate(negativeFixturePaths);

        foreach (string ruleId in outcome.EmittedRuleIds) {
            _ruleIdPattern.IsMatch(ruleId).ShouldBeTrue(
                $"Rule id '{ruleId}' must match ^[a-z][a-z0-9-]*$ (AC #13 naming contract).");
            ruleId.Length.ShouldBeLessThanOrEqualTo(64,
                $"Rule id '{ruleId}' must be ≤ 64 chars.");
        }
    }

    [Fact(Skip = _baseSkip + "AC#13 — every diagnostic must declare file, rule, hint at minimum. Remove Skip when implementing.")]
    public void DiagnosticShape_RequiredFieldsPresent() {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        IEnumerable<string> negativeFixturePaths = Dw4FixtureCatalog.All
            .Where(f => f.ExpectedVerdict == Dw4FixtureCatalog.Verdict.Fail)
            .Select(f => Path.Combine(Dw4FixtureCatalog.FixtureRoot, f.FileName));

        Dw4ValidationOutcome outcome = invoker.Validate(negativeFixturePaths);

        foreach (Dw4Diagnostic d in outcome.Diagnostics) {
            d.File.ShouldNotBeNullOrWhiteSpace("Diagnostic.File is required (AC #13).");
            d.Rule.ShouldNotBeNullOrWhiteSpace("Diagnostic.Rule is required (AC #13).");
            d.Hint.ShouldNotBeNullOrWhiteSpace(
                $"Diagnostic.Hint is required (AC #13) — rule '{d.Rule}' must include a remediation hint.");
        }
    }

    [Fact(Skip = _baseSkip + "AC#13 — diagnostic ordering must be deterministic across runs. Remove Skip when implementing.")]
    public void DiagnosticOrdering_IsDeterministicAcrossRuns() {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        IEnumerable<string> negativeFixturePaths = Dw4FixtureCatalog.All
            .Where(f => f.ExpectedVerdict == Dw4FixtureCatalog.Verdict.Fail)
            .Select(f => Path.Combine(Dw4FixtureCatalog.FixtureRoot, f.FileName))
            .ToArray();

        Dw4ValidationOutcome firstRun = invoker.Validate(negativeFixturePaths);
        Dw4ValidationOutcome secondRun = invoker.Validate(negativeFixturePaths);

        firstRun.Diagnostics.Count.ShouldBe(secondRun.Diagnostics.Count,
            "Two invocations against the same input must produce the same number of diagnostics.");

        for (int i = 0; i < firstRun.Diagnostics.Count; i++) {
            Dw4Diagnostic a = firstRun.Diagnostics[i];
            Dw4Diagnostic b = secondRun.Diagnostics[i];
            a.ShouldBe(b, $"Diagnostic #{i} differs between two runs — ordering is not deterministic.");
        }
    }

    [Fact(Skip = _baseSkip + "AC#13 — diagnostic order matches canonical (file, schema, rule, section, field, line) sort. Remove Skip when implementing.")]
    public void DiagnosticOrdering_MatchesCanonicalSort() {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        IEnumerable<string> negativeFixturePaths = Dw4FixtureCatalog.All
            .Where(f => f.ExpectedVerdict == Dw4FixtureCatalog.Verdict.Fail)
            .Select(f => Path.Combine(Dw4FixtureCatalog.FixtureRoot, f.FileName))
            .ToArray();

        Dw4ValidationOutcome outcome = invoker.Validate(negativeFixturePaths);

        IReadOnlyList<Dw4Diagnostic> emitted = outcome.Diagnostics;
        IReadOnlyList<Dw4Diagnostic> sorted = [.. emitted.OrderBy(d => d, Dw4DiagnosticComparer.Instance)];

        for (int i = 0; i < emitted.Count; i++) {
            emitted[i].ShouldBe(sorted[i],
                $"Emitted diagnostics are not in canonical (file, schema, rule, section, field, line) order " +
                $"at index {i}. Expected '{sorted[i].Rule}' got '{emitted[i].Rule}'.");
        }
    }

    [Fact(Skip = _baseSkip + "AC#13 — every committed rule id in vocabulary obeys family-prefix rules. Remove Skip when implementing.")]
    public void RuleVocabulary_FamilyPrefixesAreDistinct() {
        // Compile-time invariant test against the static vocabulary in
        // Dw4RuleVocabulary. This catches drift inside the test project itself
        // and runs even when no validator is wired (no Dw4ValidatorInvokerFactory call).
        // NOTE: this Fact is currently skipped to keep the suite uniform with the
        // rest of the red phase, but it can be activated independently of
        // entrypoint.txt because it has no validator dependency.
        foreach (string ruleId in Dw4RuleVocabulary.ParserFamily) {
            ruleId.StartsWith("parse-", StringComparison.Ordinal).ShouldBeTrue(
                $"Parser-family rule '{ruleId}' must start with 'parse-'.");
        }

        foreach (string ruleId in Dw4RuleVocabulary.SchemaIdentificationFamily) {
            ruleId.StartsWith("schema-version-", StringComparison.Ordinal).ShouldBeTrue(
                $"Schema-id-family rule '{ruleId}' must start with 'schema-version-'.");
        }

        IEnumerable<string> businessRules = Dw4RuleVocabulary.All
            .Where(r => !Dw4RuleVocabulary.ParserFamily.Contains(r)
                     && !Dw4RuleVocabulary.SchemaIdentificationFamily.Contains(r));
        foreach (string ruleId in businessRules) {
            ruleId.StartsWith("parse-", StringComparison.Ordinal).ShouldBeFalse(
                $"Business-rule '{ruleId}' must not start with 'parse-' (reserved for parser family).");
            ruleId.StartsWith("schema-version-", StringComparison.Ordinal).ShouldBeFalse(
                $"Business-rule '{ruleId}' must not start with 'schema-version-' (reserved for schema-id family).");
        }
    }
}

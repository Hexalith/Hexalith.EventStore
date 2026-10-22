namespace Hexalith.EventStore.DeferredWorkGovernance.Tests;

/// <summary>
/// DW6 pins the disposition vocabulary and report buckets that the future
/// deferred-work checker must use. The constants live in the test project so
/// the red-phase scaffolds compile before the implementation shape is chosen.
/// </summary>
internal static class Dw6RuleVocabulary {
    public const string StoryKey = "post-epic-deferred-dw6-deferred-work-governance";

    public static readonly IReadOnlyList<string> CanonicalDispositions = [
        "OPEN",
        "STORY:<id>",
        "ACCEPTED-DEBT",
        "RESOLVED",
        "DUPLICATE",
        "NO-ACTION",
    ];

    public static readonly IReadOnlyList<string> CountBuckets = [
        "OPEN",
        "STORY",
        "ACCEPTED-DEBT",
        "RESOLVED",
        "DUPLICATE",
        "NO-ACTION",
        "unclassified",
    ];

    public static readonly IReadOnlyList<string> RequiredOpenMetadata = [
        "owner",
        "next-review-date",
        "grouping",
    ];

    public static readonly IReadOnlyList<string> AcceptedLegacyForms = [
        "STORY:",
        "STORY:<id> / ACCEPTED-DEBT",
        "DW1 disposition",
        "DW2 disposition",
        "DW3 disposition",
        "DW4 disposition",
        "DW5 disposition",
        "RESOLVED-IN-",
        "checkmark-prefixed resolved lines",
        "free-text notes documented as legacy-advisory",
    ];

    public static readonly IReadOnlyList<string> BlockingRuleIds = [
        "dw6-unclassified-live-bullet",
        "dw6-open-missing-owner",
        "dw6-open-missing-next-review-date",
        "dw6-story-missing-owner",
        "dw6-story-missing-next-review-date",
        "dw6-missing-grouping",
    ];

    public static bool HasCanonicalDisposition(string text)
        => CanonicalDispositions.Any(d =>
            text.Contains(d.Replace(":<id>", ":"), StringComparison.OrdinalIgnoreCase)
            || text.Contains(d, StringComparison.OrdinalIgnoreCase));
}

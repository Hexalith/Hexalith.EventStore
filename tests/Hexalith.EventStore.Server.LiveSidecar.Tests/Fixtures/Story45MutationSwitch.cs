namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>
/// Reads the Story 4.5 perturbation switch. A perturbation changes what the harness does -- it
/// never inverts an assertion -- so a conjunct that is true by program construction cannot produce
/// a passing mutation receipt. An unrecognized value fails closed rather than silently running the
/// unperturbed harness and producing a receipt that looks like a real mutation.
/// </summary>
public static class Story45MutationSwitch
{
    /// <summary>The environment variable that arms one named perturbation.</summary>
    public const string EnvironmentVariable = "HEXALITH_STORY_4_5_MUTATION";

    /// <summary>The closed set of perturbations this story recognizes.</summary>
    public static readonly IReadOnlySet<string> KnownMutations = new HashSet<string>(StringComparer.Ordinal)
    {
        "gate-hold",
        "gate-targeting",
        "intermediate-raw-durability",
        "key-addressability",
        "final-state-classified",
        "conflict-retry-classification",
        "infrastructure-free",
        "generic-409-semantics",
        "retained-generic-value",
    };

    /// <summary>Gets the armed perturbation name, or <see langword="null"/> when none is armed.</summary>
    /// <exception cref="InvalidOperationException">The variable holds an unrecognized name.</exception>
    public static string? Armed
    {
        get
        {
            string? value = Environment.GetEnvironmentVariable(EnvironmentVariable);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return KnownMutations.Contains(value)
                ? value
                : throw new InvalidOperationException(
                    $"'{EnvironmentVariable}' is set to unrecognized perturbation '{value}'. "
                    + $"Recognized perturbations: {string.Join(", ", KnownMutations.Order(StringComparer.Ordinal))}.");
        }
    }

    /// <summary>Indicates whether the named perturbation is armed.</summary>
    /// <param name="mutationName">The perturbation name.</param>
    /// <returns><see langword="true"/> when the perturbation is armed.</returns>
    public static bool IsArmed(string mutationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutationName);
        if (!KnownMutations.Contains(mutationName))
        {
            throw new InvalidOperationException($"'{mutationName}' is not a recognized Story 4.5 perturbation.");
        }

        return string.Equals(Armed, mutationName, StringComparison.Ordinal);
    }
}

using System.Text;

namespace Hexalith.EventStore.ErrorHandling;

/// <summary>
/// Provides the stable ProblemDetails mapping for domain rejection events.
/// </summary>
public static class DomainRejectionProblemCatalog {
    /// <summary>
    /// Creates documented ProblemDetails metadata from a domain rejection event type name.
    /// </summary>
    public static DomainRejectionProblem FromRejectionType(string rejectionType) {
        ArgumentException.ThrowIfNullOrWhiteSpace(rejectionType);

        string rejectionName = GetShortRejectionName(rejectionType);
        return Create(rejectionName, ToReasonCode(SplitWords(rejectionName)));
    }

    /// <summary>
    /// Creates documented ProblemDetails metadata from a stable rejection reason code.
    /// </summary>
    public static DomainRejectionProblem FromReasonCode(string reasonCode) {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);

        IReadOnlyList<string> words = SplitReasonCode(reasonCode);
        string normalizedReasonCode = string.Join('-', words.Select(static word => word.ToLowerInvariant()));
        string rejectionName = string.Concat(words.Select(static word => char.ToUpperInvariant(word[0]) + word[1..]));
        return Create(rejectionName, normalizedReasonCode);
    }

    private static DomainRejectionProblem Create(string rejectionName, string reasonCode) {
        int statusCode = GetStatusCode(reasonCode);
        return new DomainRejectionProblem(
            ReasonCode: reasonCode,
            Title: ToTitle(SplitReasonCode(reasonCode)),
            StatusCode: statusCode,
            Explanation: GetExplanation(statusCode),
            CorrectiveAction: GetCorrectiveAction(reasonCode, statusCode),
            RejectionName: rejectionName);
    }

    private static int GetStatusCode(string reasonCode) {
        if (reasonCode.Contains("not-found", StringComparison.Ordinal)) {
            return StatusCodes.Status404NotFound;
        }

        if (reasonCode.Contains("already", StringComparison.Ordinal)
            || reasonCode.Contains("duplicate", StringComparison.Ordinal)) {
            return StatusCodes.Status409Conflict;
        }

        return StatusCodes.Status422UnprocessableEntity;
    }

    private static string GetExplanation(int statusCode)
        => statusCode switch {
            StatusCodes.Status404NotFound => "The command referenced a domain resource that does not exist in the requested tenant context.",
            StatusCodes.Status409Conflict => "The command conflicts with the current domain state and was not applied.",
            _ => "The command was syntactically valid but rejected by domain business rules or validation.",
        };

    private static string GetCorrectiveAction(string reasonCode, int statusCode)
        => statusCode switch {
            StatusCodes.Status404NotFound => "Verify the identifier and tenant context, then retry with an existing resource.",
            StatusCodes.Status409Conflict => "Use a different identifier or treat the existing resource as the current state.",
            _ when reasonCode.Contains("cannot", StringComparison.Ordinal)
                || reasonCode.Contains("invalid", StringComparison.Ordinal)
                || reasonCode.Contains("mismatch", StringComparison.Ordinal)
                => "Correct the command payload and retry.",
            _ => "Review the rejection detail, correct the request, and retry when appropriate.",
        };

    private static string GetShortRejectionName(string rejectionType) {
        int lastDot = rejectionType.LastIndexOf('.');
        return lastDot < 0 || lastDot == rejectionType.Length - 1
            ? rejectionType
            : rejectionType[(lastDot + 1)..];
    }

    private static string ToTitle(IReadOnlyList<string> words)
        => string.Join(' ', words.Select(static word => char.ToUpperInvariant(word[0]) + word[1..]));

    private static string ToReasonCode(IReadOnlyList<string> words)
        => string.Join('-', words.Select(static word => word.ToLowerInvariant()));

    private static IReadOnlyList<string> SplitReasonCode(string value) {
        var words = new List<string>();
        var builder = new StringBuilder();

        foreach (char current in value) {
            if (char.IsLetterOrDigit(current)) {
                _ = builder.Append(char.ToLowerInvariant(current));
                continue;
            }

            FlushWord();
        }

        FlushWord();
        return words.Count == 0 ? ["domain", "rejection"] : words;

        void FlushWord() {
            if (builder.Length == 0) {
                return;
            }

            words.Add(builder.ToString());
            _ = builder.Clear();
        }
    }

    private static IReadOnlyList<string> SplitWords(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return ["domain", "rejection"];
        }

        var words = new List<string>();
        var builder = new StringBuilder();
        char previous = '\0';
        foreach (char current in value) {
            if (!char.IsLetterOrDigit(current)) {
                FlushWord();
                previous = '\0';
                continue;
            }

            if (builder.Length > 0
                && char.IsUpper(current)
                && (char.IsLower(previous) || char.IsDigit(previous))) {
                FlushWord();
            }

            _ = builder.Append(current);
            previous = current;
        }

        FlushWord();
        return words.Count == 0 ? ["domain", "rejection"] : words;

        void FlushWord() {
            if (builder.Length == 0) {
                return;
            }

            words.Add(builder.ToString());
            _ = builder.Clear();
        }
    }
}

/// <summary>
/// Documented ProblemDetails metadata for one domain rejection.
/// </summary>
public sealed record DomainRejectionProblem(
    string ReasonCode,
    string Title,
    int StatusCode,
    string Explanation,
    string CorrectiveAction,
    string RejectionName) {
    public string TypeUri => $"{ProblemTypeUris.DomainRejection}/{ReasonCode}";
}

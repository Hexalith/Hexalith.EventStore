using System.Net;

namespace Hexalith.EventStore.Contracts.Problems;

/// <summary>Defines the platform-owned ProblemDetails values for domain command rejections.</summary>
public static class DomainRejectionProblemDetails
{
    /// <summary>The base problem type URI, followed by a slash and a rejection reason code.</summary>
    public const string TypeUri = "https://hexalith.io/problems/domain-rejections";

    /// <summary>Checks the reason-code shape emitted by the domain rejection catalog.</summary>
    /// <param name="value">The reason code supplied by the gateway.</param>
    /// <returns>Whether it consists of lower-case letter or digit groups separated by single hyphens.</returns>
    public static bool IsCanonicalReasonCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        bool needsLetterOrDigit = true;
        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character) && char.ToLowerInvariant(character) == character)
            {
                needsLetterOrDigit = false;
            }
            else if (character == '-' && !needsLetterOrDigit)
            {
                needsLetterOrDigit = true;
            }
            else
            {
                return false;
            }
        }

        return !needsLetterOrDigit;
    }

    /// <summary>Gets the published corrective action for a domain rejection.</summary>
    /// <param name="statusCode">The rejection's HTTP status code.</param>
    /// <param name="reasonCode">The rejection's canonical reason code.</param>
    /// <returns>The platform's corrective action for the status and reason.</returns>
    public static string GetCorrectiveAction(int statusCode, string reasonCode)
    {
        ArgumentNullException.ThrowIfNull(reasonCode);

        return statusCode switch
        {
            (int)HttpStatusCode.NotFound => "Verify the identifier and tenant context, then retry with an existing resource.",
            (int)HttpStatusCode.Conflict => "Use a different identifier or treat the existing resource as the current state.",
            _ when reasonCode.Contains("cannot", StringComparison.Ordinal)
                || reasonCode.Contains("invalid", StringComparison.Ordinal)
                || reasonCode.Contains("mismatch", StringComparison.Ordinal)
                => "Correct the command payload and retry.",
            _ => "Review the rejection detail, correct the request, and retry when appropriate.",
        };
    }
}

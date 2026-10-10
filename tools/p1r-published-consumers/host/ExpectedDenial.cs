#if P1R_CAPABILITIES
using Dapr.Actors;

/// <summary>Recognizes only the existing fence and gateway-proof denial contracts.</summary>
internal static class ExpectedDenial
{
    /// <summary>The exact existing fence denial.</summary>
    internal const string Fence = "The idempotency execution fence is missing, stale, or invalid.";

    /// <summary>The exact admission actor denial of superseded execution authority.</summary>
    internal const string StaleAuthority = "The idempotency execution authority is no longer current.";

    /// <summary>The exact existing gateway-proof denial.</summary>
    internal const string GatewayProof = "Trusted effect gateway proof is invalid.";

    /// <summary>Checks local or actor-transported denial without accepting infrastructure failures.</summary>
    internal static bool Matches(Exception error, string expected)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            bool expectedType = current is InvalidOperationException
                || current is ActorInvokeException actor && (actor.ActualExceptionType == typeof(InvalidOperationException).FullName
                    || actor.ActualExceptionType == nameof(InvalidOperationException));
            if (expectedType && string.Equals(current.Message, expected, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Retains bounded exception types and known safe denial messages.</summary>
    internal static object Describe(Exception error)
    {
        List<object> records = [];
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            string? message = current.Message is Fence or StaleAuthority or GatewayProof or "Trusted effect denial audit is unavailable."
                ? current.Message : null;
            records.Add(new
            {
                wrapper_type = current.GetType().FullName,
                actual_exception_type = (current as ActorInvokeException)?.ActualExceptionType,
                denial_message = message,
                message_disclosed = message is not null,
            });
        }

        return records;
    }
}
#endif

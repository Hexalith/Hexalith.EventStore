// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 5, 11, 15.
using System.Net.Sockets;
using System.Security.Authentication;
using Azure.Identity;
using Azure;
using Microsoft.Identity.Client;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Constructs outcomes from typed status/cancellation/dispatch evidence, never exception text or provider 404 as deletion.</summary>
internal static class AzureKeyVaultFailureClassifier
{
    /// <summary>Classifies an affirmative service rejection without interpreting its body.</summary>
    internal static PayloadBackendFailure Status(int status, bool wrap) => status switch
    {
        401 or 403 => PayloadBackendFailure.Denied,
        408 or 429 => PayloadBackendFailure.Unavailable,
        >= 500 and <= 599 => wrap ? PayloadBackendFailure.Ambiguous : PayloadBackendFailure.Unavailable,
        _ => PayloadBackendFailure.ConsistencyMismatch,
    };
    /// <summary>Distinguishes pre-dispatch authentication failures from possibly completed wraps.</summary>
    internal static PayloadBackendFailure Exception(Exception exception, bool dispatched, bool wrap)
    {
        if (exception is AuthenticationException || exception is CredentialUnavailableException)
        {
            return PayloadBackendFailure.Denied;
        }

        if (exception is RequestFailedException response)
        {
            return Status(response.Status, wrap && dispatched);
        }

        if (exception is AuthenticationFailedException authentication)
        {
            return authentication.InnerException switch
            {
                RequestFailedException inner when inner.Status is 408 or 429 or >= 500 and <= 599 => PayloadBackendFailure.Unavailable,
                MsalServiceException inner when inner.StatusCode is 408 or 429 or >= 500 and <= 599 => PayloadBackendFailure.Unavailable,
                AzureKeyVaultTransportException inner when inner.Failure is PayloadBackendFailure.Unavailable or PayloadBackendFailure.Ambiguous => PayloadBackendFailure.Unavailable,
                IOException or SocketException or OperationCanceledException => PayloadBackendFailure.Unavailable,
                _ => PayloadBackendFailure.Denied,
            };
        }

        if (exception is OperationCanceledException or IOException or SocketException)
        {
            return wrap && dispatched ? PayloadBackendFailure.Ambiguous : PayloadBackendFailure.Unavailable;
        }

        return PayloadBackendFailure.ConsistencyMismatch;
    }

    /// <summary>Accepts only structurally parsed nonnegative delays at most two seconds.</summary>
    internal static TimeSpan? RetryAfter(string? value, DateTimeOffset now)
    {
        if (value is null)
        {
            return null;
        }

        if (int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int seconds))
        {
            return seconds is >= 0 and <= 2 ? TimeSpan.FromSeconds(seconds) : null;
        }

        if (DateTimeOffset.TryParseExact(value, "r", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out DateTimeOffset date))
        {
            TimeSpan delay = date - now;
            return delay >= TimeSpan.Zero && delay <= TimeSpan.FromSeconds(2) ? delay : null;
        }

        return null;
    }
}

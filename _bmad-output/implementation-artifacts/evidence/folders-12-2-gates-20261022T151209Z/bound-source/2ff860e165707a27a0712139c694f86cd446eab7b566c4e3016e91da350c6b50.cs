using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.Operations.Security;

/// <summary>
/// Restricts the application port to a Dapr sidecar that holds the configured app token.
/// </summary>
internal sealed class DaprAppChannelTokenMiddleware(RequestDelegate next, string token)
{
    internal const string HeaderName = "dapr-api-token";

    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly byte[] _token = Encoding.UTF8.GetBytes(
        !string.IsNullOrWhiteSpace(token) ? token : throw new ArgumentException("Token is required.", nameof(token)));

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!HasValidToken(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private bool HasValidToken(HttpRequest request)
    {
        Microsoft.Extensions.Primitives.StringValues values = request.Headers[HeaderName];
        string? value = values.Count == 1 ? values[0] : null;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        byte[] candidate = Encoding.UTF8.GetBytes(value);
        bool valid = candidate.Length == _token.Length
            && CryptographicOperations.FixedTimeEquals(candidate, _token);
        CryptographicOperations.ZeroMemory(candidate);
        return valid;
    }
}

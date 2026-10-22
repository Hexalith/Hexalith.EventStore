using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// First callback-admission step: every call to the reminder actor type's routes must carry the Dapr
/// app-channel token that matches <c>APP_API_TOKEN</c>. The token is required outside Development, and a host
/// without one fails closed. Denials return an empty <c>401</c> and are audited by reason code only.
/// </summary>
internal sealed class ReminderCallbackTokenFilter(
    IHostEnvironment environment,
    IConfiguration configuration,
    IOptions<EventStoreReminderOptions> options,
    ILogger<ReminderCallbackTokenFilter> logger) : IMiddleware
{
    /// <summary>The configuration key of the app-channel token.</summary>
    public const string ConfigurationKey = "APP_API_TOKEN";

    /// <summary>The header the Dapr sidecar uses to present the app-channel token.</summary>
    public const string HeaderName = "dapr-api-token";

    private readonly IHostEnvironment _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly ILogger<ReminderCallbackTokenFilter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _actorTypeName = (options ?? throw new ArgumentNullException(nameof(options))).Value.ActorTypeName;

    /// <summary>Gets a value indicating whether reminder callbacks can be admitted on this host.</summary>
    public bool IsConfigured => _environment.IsDevelopment() || !string.IsNullOrWhiteSpace(_configuration[ConfigurationKey]);

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        // The filter runs before routing and before any UsePathBase, so it inspects the whole path.
        if (!IsReminderActorPath(context.Request.PathBase.Add(context.Request.Path)))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        string? denial = GetDenialReason(context.Request);
        if (denial is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        ReminderLog.OriginDenied(_logger, denial);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }

    /// <summary>
    /// Returns whether the path targets the reminder actor type, ignoring case like routing does. The
    /// <c>actors/{ActorTypeName}</c> segment pair may appear anywhere, so a host path base cannot hide it.
    /// </summary>
    /// <param name="path">The full request path, including any path base.</param>
    /// <returns><see langword="true"/> when an <c>actors</c> segment is followed by the reminder actor type.</returns>
    public bool IsReminderActorPath(PathString path)
    {
        string[] segments = (path.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < segments.Length - 1; index++)
        {
            if (string.Equals(segments[index], "actors", StringComparison.OrdinalIgnoreCase)
                && string.Equals(segments[index + 1], _actorTypeName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the bounded denial reason, or <see langword="null"/> when the caller is admitted.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A reason code, or <see langword="null"/>.</returns>
    public string? GetDenialReason(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? expected = _configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(expected))
        {
            return _environment.IsDevelopment() ? null : "token-unconfigured";
        }

        Microsoft.Extensions.Primitives.StringValues header = request.Headers[HeaderName];
        if (header.Count != 1 || string.IsNullOrEmpty(header[0]))
        {
            return "token-missing";
        }

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] actualBytes = Encoding.UTF8.GetBytes(header[0]!);
        try
        {
            return expectedBytes.Length == actualBytes.Length
                && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes)
                ? null
                : "token-invalid";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedBytes);
            CryptographicOperations.ZeroMemory(actualBytes);
        }
    }
}

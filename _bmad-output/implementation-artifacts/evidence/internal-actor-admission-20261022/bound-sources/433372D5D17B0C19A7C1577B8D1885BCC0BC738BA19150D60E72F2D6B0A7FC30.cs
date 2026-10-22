using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Records invocation routing metadata without retaining protected request or response content.</summary>
/// <param name="logger">The support-safe diagnostic logger.</param>
internal sealed partial class Oq8InvocationDiagnosticHandler(ILogger<Oq8InvocationDiagnosticHandler> logger) : DelegatingHandler
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string errorCode = "none";
        if (!response.IsSuccessStatusCode)
        {
            try
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument document = JsonDocument.Parse(content);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("errorCode", out JsonElement code)
                    && code.ValueKind == JsonValueKind.String
                    && Regex.IsMatch(code.GetString()!, "^ERR_[A-Z_]{1,80}$", RegexOptions.CultureInvariant))
                {
                    errorCode = code.GetString()!;
                }
            }
            catch (JsonException)
            {
                errorCode = "non-json";
            }
        }

        LogInvocation(logger, request.Method.Method, request.RequestUri?.GetLeftPart(UriPartial.Path) ?? "absent", (int)response.StatusCode, errorCode);
        return response;
    }

    [LoggerMessage(EventId = 8501, Level = LogLevel.Warning,
        Message = "OQ8 invocation routing: Method={Method}, Endpoint={Endpoint}, StatusCode={StatusCode}, ErrorCode={ErrorCode}")]
    private static partial void LogInvocation(ILogger logger, string method, string endpoint, int statusCode, string errorCode);
}

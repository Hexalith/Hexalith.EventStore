using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Client.Gateway;

/// <summary>
/// HTTP implementation of <see cref="IEventStoreGatewayClient"/>.
/// </summary>
public sealed class EventStoreGatewayClient : IEventStoreGatewayClient {
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _httpClient;
    private readonly EventStoreGatewayClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreGatewayClient"/> class.
    /// </summary>
    public EventStoreGatewayClient(HttpClient httpClient, IOptions<EventStoreGatewayClientOptions> options) {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<SubmitCommandResponse> SubmitCommandAsync(
        SubmitCommandRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(CreateRelativeUri(_options.CommandPath), request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) {
            await ThrowGatewayExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }

        SubmitCommandResponse? result = await response.Content
            .ReadFromJsonAsync<SubmitCommandResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (result is null || string.IsNullOrWhiteSpace(result.CorrelationId)) {
            throw new EventStoreGatewayException(
                (int)response.StatusCode,
                response.ReasonPhrase ?? "Accepted",
                detail: "Command response did not contain a valid correlationId.");
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<EventStoreQueryResult> SubmitQueryAsync(
        SubmitQueryRequest request,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, CreateRelativeUri(_options.QueryPath)) {
            Content = JsonContent.Create(request, options: JsonOptions),
        };

        if (!string.IsNullOrWhiteSpace(ifNoneMatch)) {
            httpRequest.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        }

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        string? eTag = GetETag(response);
        if (response.StatusCode == HttpStatusCode.NotModified) {
            return new EventStoreQueryResult(null, null, IsNotModified: true, eTag);
        }

        if (!response.IsSuccessStatusCode) {
            await ThrowGatewayExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }

        SubmitQueryResponse? result = await response.Content
            .ReadFromJsonAsync<SubmitQueryResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (result is null || string.IsNullOrWhiteSpace(result.CorrelationId)) {
            throw new EventStoreGatewayException(
                (int)response.StatusCode,
                response.ReasonPhrase ?? "OK",
                detail: "Query response did not contain a valid correlationId.");
        }

        return new EventStoreQueryResult(result.CorrelationId, result.Payload, IsNotModified: false, eTag);
    }

    /// <inheritdoc />
    public async Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(
        SubmitQueryRequest request,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default) {
        EventStoreQueryResult result = await SubmitQueryAsync(request, ifNoneMatch, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsNotModified) {
            return new EventStoreQueryResult<T>(result.CorrelationId, default, IsNotModified: true, result.ETag);
        }

        if (result.Payload is null) {
            throw new EventStoreGatewayException(
                StatusCodes.Ok,
                "OK",
                detail: "Query response did not contain a payload.");
        }

        T? payload = result.Payload.Value.Deserialize<T>(JsonOptions);
        if (payload is null) {
            throw new EventStoreGatewayException(
                StatusCodes.Ok,
                "OK",
                detail: "Query response payload could not be deserialized.");
        }

        return new EventStoreQueryResult<T>(result.CorrelationId, payload, IsNotModified: false, result.ETag);
    }

    private static Uri CreateRelativeUri(string path) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new Uri(path.TrimStart('/'), UriKind.Relative);
    }

    private static string? GetETag(HttpResponseMessage response)
        => response.Headers.ETag?.Tag.Trim('"');

    private static async Task ThrowGatewayExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken) {
        string? type = null;
        string? title = null;
        string? detail = null;
        string? correlationId = null;
        int statusCode = (int)response.StatusCode;

        if (IsJsonResponse(response)) {
            try {
                using JsonDocument document = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                JsonElement root = document.RootElement;
                if (root.TryGetProperty("status", out JsonElement statusElement)
                    && statusElement.TryGetInt32(out int problemStatus)) {
                    statusCode = problemStatus;
                }

                type = GetString(root, "type");
                title = GetString(root, "title");
                detail = GetString(root, "detail");
                correlationId = GetString(root, "correlationId");
            }
            catch (JsonException) {
                // Fall back to HTTP status metadata below.
            }
        }

        throw new EventStoreGatewayException(
            statusCode,
            title ?? response.ReasonPhrase ?? "EventStore gateway error",
            type,
            detail,
            correlationId);
    }

    private static bool IsJsonResponse(HttpResponseMessage response) {
        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        return mediaType is not null
            && (mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("problem+json", StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static class StatusCodes {
        public const int Ok = 200;
    }
}

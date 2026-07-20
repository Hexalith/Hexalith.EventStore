using System.Collections.Frozen;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Client.Gateway;

/// <summary>
/// HTTP implementation of <see cref="IEventStoreGatewayClient"/>.
/// </summary>
public sealed class EventStoreGatewayClient : IEventStoreGatewayClient {
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = {
            new ProjectionLifecycleStateJsonConverter(),
            new QueryResponseProvenanceJsonConverter(),
            new JsonStringEnumConverter(),
        },
    };

    private readonly HttpClient _httpClient;
    private readonly EventStoreGatewayClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreGatewayClient"/> class.
    /// </summary>
    /// <remarks>
    /// DEC6: this constructor mutates <see cref="HttpClient.MaxResponseContentBufferSize"/> on
    /// the supplied <paramref name="httpClient"/>. Because <see cref="IHttpClientFactory"/>
    /// instances are commonly shared across consumers, the buffer cap applies to ALL endpoints
    /// invoked via the same client. Configure a typed client per gateway when other endpoints
    /// (e.g., large query responses) need a higher cap.
    /// <para>
    /// P16-8P (pass-8): the buffer cap is now applied min-only — the constructor reduces the cap
    /// when the option is smaller than the caller-configured existing value, but it never raises
    /// a lower caller cap. This restores the pre-DEC6-7P behavior and prevents silent behavior
    /// change when callers pre-size HttpClient.MaxResponseContentBufferSize for narrow workloads.
    /// </para>
    /// <para>
    /// P22-7P (pass-7 MEDIUM): when multiple gateway clients are constructed concurrently against
    /// the same shared <see cref="HttpClient"/> with different
    /// <see cref="EventStoreGatewayClientOptions.MaxStreamReadResponseBytes"/> values, the min-only
    /// assignment is still a race (last-writer-wins on the equal-or-smaller branch). The
    /// recommended deployment pattern is one typed client per gateway via named DI options
    /// (<c>services.AddHttpClient&lt;EventStoreGatewayClient&gt;(...)</c>), giving each gateway
    /// its own HttpClient instance. The shared-HttpClient pattern is supported for compatibility
    /// but not optimal under multi-tenant DI configuration.
    /// </para>
    /// </remarks>
    public EventStoreGatewayClient(HttpClient httpClient, IOptions<EventStoreGatewayClientOptions> options) {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        if (_options.MaxStreamReadResponseBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxStreamReadResponseBytes must be greater than zero.");
        }

        // P17/DEC6: HttpClient.MaxResponseContentBufferSize is an int. Validate the option
        // value fits before assignment so misconfiguration surfaces as ArgumentOutOfRange at
        // startup rather than as a runtime exception on the first stream read.
        if (_options.MaxStreamReadResponseBytes > int.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"MaxStreamReadResponseBytes must be <= {int.MaxValue} (HttpClient.MaxResponseContentBufferSize is int).");
        }

        // P16-8P (pass-8): apply min-only. If the caller pre-configured a lower buffer (e.g.,
        // for a narrow query workload), do NOT raise it. Only lower the cap when the option
        // value is more restrictive than the current setting.
        if (_options.MaxStreamReadResponseBytes < _httpClient.MaxResponseContentBufferSize) {
            _httpClient.MaxResponseContentBufferSize = _options.MaxStreamReadResponseBytes;
        }
    }

    /// <inheritdoc />
    public async Task<SubmitCommandResponse> SubmitCommandAsync(
        SubmitCommandRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        using HttpResponseMessage response = await SendTranslatingAsync(
            () => _httpClient.PostAsJsonAsync(CreateRelativeUri(_options.CommandPath), request, JsonOptions, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) {
            await ThrowGatewayExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }

        SubmitCommandResponse? result;
        try {
            result = await response.Content
                .ReadFromJsonAsync<SubmitCommandResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex) {
            throw new EventStoreGatewayException(
                (int)response.StatusCode,
                response.ReasonPhrase ?? "Accepted",
                detail: "Command response body could not be parsed.",
                innerException: ex);
        }

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

        string? normalizedIfNoneMatch = NormalizeIfNoneMatch(ifNoneMatch);
        if (normalizedIfNoneMatch is not null) {
            httpRequest.Headers.IfNoneMatch.ParseAdd(normalizedIfNoneMatch);
        }

        using HttpResponseMessage response = await SendTranslatingAsync(
            () => _httpClient.SendAsync(httpRequest, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        // P2: check 304 and non-success BEFORE extracting ETag so a weak ETag on an error
        // response does not shadow the real ProblemDetails exception.
        if (response.StatusCode == HttpStatusCode.NotModified) {
            string? notModifiedETag = GetETag(response);
            QueryResponseProvenance provenance = GetProvenanceHeader(response);
            if (provenance != QueryResponseProvenance.ProjectionBacked
                || string.IsNullOrWhiteSpace(notModifiedETag)) {
                throw new EventStoreGatewayException(
                    StatusCodes.BadGateway,
                    "Bad Gateway",
                    detail: "Not-modified query response requires projection-backed provenance and a strong ETag.");
            }

            ProjectionLifecycleState lifecycle = ProjectionLifecyclePolicy.Normalize(
                GetLifecycleHeader(response),
                provenance);

            return new EventStoreQueryResult(null, null, IsNotModified: true, notModifiedETag) {
                Metadata = new QueryResponseMetadata(
                    ETag: notModifiedETag,
                    IsNotModified: true,
                    IsStale: ProjectionLifecyclePolicy.ProjectIsStale(lifecycle),
                    IsDegraded: ProjectionLifecyclePolicy.ProjectIsDegraded(lifecycle)) {
                    Provenance = QueryResponseProvenance.ProjectionBacked,
                    Lifecycle = lifecycle,
                },
            };
        }

        if (!response.IsSuccessStatusCode) {
            await ThrowGatewayExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }

        string? eTag = GetETag(response);

        SubmitQueryResponse? result = await ReadQueryResponseAsync(response, cancellationToken)
            .ConfigureAwait(false);

        if (result is null || string.IsNullOrWhiteSpace(result.CorrelationId)) {
            throw new EventStoreGatewayException(
                (int)response.StatusCode,
                response.ReasonPhrase ?? "OK",
                detail: "Query response did not contain a valid correlationId.");
        }

        if (!result.Success) {
            throw new EventStoreGatewayException(
                StatusCodes.Ok,
                "Query semantic failure",
                detail: result.ErrorMessage,
                correlationId: result.CorrelationId);
        }

        QueryResponseMetadata metadata = NormalizeMetadata(
            result.Metadata,
            GetProvenanceHeader(response),
            GetLifecycleHeader(response),
            eTag,
            isNotModified: false);

        string? normalizedETag = metadata.Provenance == QueryResponseProvenance.ProjectionBacked
            ? metadata.ETag
            : null;
        return new EventStoreQueryResult(result.CorrelationId, result.Payload, IsNotModified: false, normalizedETag) {
            Metadata = metadata,
        };
    }

    /// <inheritdoc />
    public async Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(
        SubmitQueryRequest request,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default) {
        EventStoreQueryResult result = await SubmitQueryAsync(request, ifNoneMatch, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsNotModified) {
            return new EventStoreQueryResult<T>(result.CorrelationId, default, IsNotModified: true, result.ETag) {
                Metadata = result.Metadata,
            };
        }

        if (result.Payload is null) {
            throw new EventStoreGatewayException(
                StatusCodes.Ok,
                "OK",
                detail: "Query response did not contain a payload.");
        }

        T? payload;
        try {
            payload = result.Payload.Value.Deserialize<T>(JsonOptions);
        }
        catch (JsonException ex) {
            throw new EventStoreGatewayException(
                StatusCodes.Ok,
                "OK",
                detail: "Query response payload could not be deserialized.",
                innerException: ex);
        }

        if (payload is null) {
            throw new EventStoreGatewayException(
                StatusCodes.Ok,
                "OK",
                detail: "Query response payload could not be deserialized.");
        }

        return new EventStoreQueryResult<T>(result.CorrelationId, payload, IsNotModified: false, result.ETag) {
            Metadata = result.Metadata,
        };
    }

    /// <inheritdoc />
    public async Task<StreamReadPage> ReadStreamAsync(
        StreamReadRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        using HttpResponseMessage response = await SendTranslatingAsync(
            () => _httpClient.PostAsJsonAsync(CreateRelativeUri(_options.StreamReadPath), request, JsonOptions, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) {
            await ThrowGatewayExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }

        try {
            StreamReadPage? result = await response.Content
                .ReadFromJsonAsync<StreamReadPage>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return result ?? throw new EventStoreGatewayException(
                (int)response.StatusCode,
                response.ReasonPhrase ?? "OK",
                detail: "Stream read response body was empty.");
        }
        catch (JsonException ex) {
            throw new EventStoreGatewayException(
                (int)response.StatusCode,
                response.ReasonPhrase ?? "OK",
                detail: "Stream read response body could not be parsed.",
                innerException: ex);
        }
    }

    // Translates transport-level faults (connection failure, DNS failure, HttpClient timeout) into
    // an EventStoreGatewayException so a gateway outage surfaces as a handled 503 instead of a raw
    // HttpRequestException/timeout escaping into the caller (e.g. a Blazor circuit). Genuine caller
    // cancellation propagates unchanged.
    private static async Task<HttpResponseMessage> SendTranslatingAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken) {
        try {
            return await send().ConfigureAwait(false);
        }
        catch (HttpRequestException ex) {
            throw CreateTransportException(ex, timedOut: false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested) {
            throw CreateTransportException(ex, timedOut: true);
        }
    }

    private static EventStoreGatewayException CreateTransportException(Exception inner, bool timedOut)
        => new(
            503,
            "EventStore gateway unavailable",
            detail: timedOut
                ? "The EventStore gateway did not respond before the request timed out."
                : "The EventStore gateway could not be reached.",
            reason: timedOut ? "gateway-timeout" : "gateway-unreachable",
            innerException: inner);

    private static QueryResponseMetadata NormalizeMetadata(
        QueryResponseMetadata? metadata,
        QueryResponseProvenance headerProvenance,
        ProjectionLifecycleState headerLifecycle,
        string? eTag,
        bool isNotModified) {
        QueryResponseProvenance bodyProvenance = metadata?.Provenance ?? QueryResponseProvenance.Unknown;
        QueryResponseProvenance provenance = bodyProvenance == headerProvenance
            ? bodyProvenance
            : QueryResponseProvenance.Unknown;
        ProjectionLifecycleState bodyLifecycle = ProjectionLifecyclePolicy.Normalize(
            metadata?.Lifecycle ?? ProjectionLifecycleState.Unknown,
            provenance);
        ProjectionLifecycleState normalizedHeaderLifecycle = ProjectionLifecyclePolicy.Normalize(
            headerLifecycle,
            provenance);
        ProjectionLifecycleState lifecycle = bodyLifecycle == normalizedHeaderLifecycle
            ? bodyLifecycle
            : ProjectionLifecycleState.Unknown;
        QueryResponseMetadata normalized = (metadata ?? new QueryResponseMetadata()) with {
            Provenance = provenance,
            Lifecycle = lifecycle,
            IsStale = ProjectionLifecyclePolicy.ProjectIsStale(lifecycle, metadata?.IsStale),
            IsDegraded = ProjectionLifecyclePolicy.ProjectIsDegraded(lifecycle, metadata?.IsDegraded),
        };

        return provenance == QueryResponseProvenance.ProjectionBacked
            ? normalized with {
                ETag = eTag ?? normalized.ETag,
                IsNotModified = isNotModified,
            }
            : normalized with {
                ETag = null,
                IsNotModified = null,
                IsStale = null,
                ProjectionVersion = null,
                Lifecycle = ProjectionLifecycleState.Unknown,
            };
    }

    private static QueryResponseProvenance GetProvenanceHeader(HttpResponseMessage response) {
        if (!response.Headers.TryGetValues("X-Hexalith-Query-Provenance", out IEnumerable<string>? values)) {
            return QueryResponseProvenance.Unknown;
        }

        string[] provenanceValues = values.ToArray();
        if (provenanceValues.Length != 1) {
            return QueryResponseProvenance.Unknown;
        }

        return provenanceValues[0] switch {
            nameof(QueryResponseProvenance.ProjectionBacked) => QueryResponseProvenance.ProjectionBacked,
            nameof(QueryResponseProvenance.HandlerComputed) => QueryResponseProvenance.HandlerComputed,
            _ => QueryResponseProvenance.Unknown,
        };
    }

    private static ProjectionLifecycleState GetLifecycleHeader(HttpResponseMessage response) {
        if (!response.Headers.TryGetValues(ProjectionLifecyclePolicy.HeaderName, out IEnumerable<string>? values)) {
            return ProjectionLifecycleState.Unknown;
        }

        string[] lifecycleValues = values.ToArray();
        if (lifecycleValues.Length != 1) {
            return ProjectionLifecycleState.Unknown;
        }

        return lifecycleValues[0] switch {
            nameof(ProjectionLifecycleState.Current) => ProjectionLifecycleState.Current,
            nameof(ProjectionLifecycleState.Stale) => ProjectionLifecycleState.Stale,
            nameof(ProjectionLifecycleState.Rebuilding) => ProjectionLifecycleState.Rebuilding,
            nameof(ProjectionLifecycleState.Degraded) => ProjectionLifecycleState.Degraded,
            nameof(ProjectionLifecycleState.Unavailable) => ProjectionLifecycleState.Unavailable,
            nameof(ProjectionLifecycleState.LocalOnly) => ProjectionLifecycleState.LocalOnly,
            _ => ProjectionLifecycleState.Unknown,
        };
    }

    private static Uri CreateRelativeUri(string path) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new Uri(path.TrimStart('/'), UriKind.Relative);
    }

    private static string? GetETag(HttpResponseMessage response) {
        EntityTagHeaderValue? eTag = response.Headers.ETag;
        if (eTag is null) {
            return null;
        }

        if (eTag.IsWeak) {
            throw new EventStoreGatewayException(
                (int)response.StatusCode,
                response.ReasonPhrase ?? "OK",
                detail: "Query response contained an unsupported weak ETag.");
        }

        if (string.Equals(eTag.Tag, "*", StringComparison.Ordinal)) {
            return null;
        }

        return eTag.Tag.Trim('"');
    }

    private static string? NormalizeIfNoneMatch(string? ifNoneMatch) {
        if (string.IsNullOrWhiteSpace(ifNoneMatch)) {
            return null;
        }

        string value = ifNoneMatch.Trim();

        // The gateway only revalidates against a single strong ETag. RFC 9110 conditional forms
        // this client cannot express — wildcard ("*"), weak validators (W/…), and tag lists — are
        // degraded to an unconditional request (null) rather than raising, so a caching hint never
        // turns a GET into a hard 400. The server responds with the full body, which is always safe.
        if (value == "*") {
            return null;
        }

        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        if (value.StartsWith('"')) {
            if (!EntityTagHeaderValue.TryParse(value, out EntityTagHeaderValue? parsed)
                || parsed is null
                || parsed.IsWeak
                || parsed.Tag == "*") {
                return null;
            }

            return parsed.Tag;
        }

        if (value.Any(static c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '"' or ',')) {
            return null;
        }

        return $"\"{value}\"";
    }

    private static async Task<SubmitQueryResponse?> ReadQueryResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) {
        try {
            return await response.Content
                .ReadFromJsonAsync<SubmitQueryResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex) {
            throw new EventStoreGatewayException(
                (int)response.StatusCode,
                response.ReasonPhrase ?? "OK",
                detail: "Query response body could not be parsed.",
                innerException: ex);
        }
    }

    private static async Task ThrowGatewayExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken) {
        string? type = null;
        string? title = null;
        string? detail = null;
        string? correlationId = null;
        string? tenantId = null;
        string? reason = null;
        string? reasonCode = null;
        string? code = null;
        string? category = null;
        bool? retryable = null;
        string? clientAction = null;
        string? retryAfter = response.Headers.RetryAfter?.ToString();
        IReadOnlyDictionary<string, string>? errors = null;
        IReadOnlyDictionary<string, JsonElement>? extensions = null;
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
                correlationId = GetString(root, GatewayProblemDetailsExtensions.CorrelationId);
                tenantId = GetString(root, GatewayProblemDetailsExtensions.TenantId);
                reason = GetString(root, GatewayProblemDetailsExtensions.Reason);
                reasonCode = GetString(root, GatewayProblemDetailsExtensions.ReasonCode);
                code = GetString(root, GatewayProblemDetailsExtensions.Code);
                category = GetString(root, GatewayProblemDetailsExtensions.Category);
                retryable = GetBoolean(root, GatewayProblemDetailsExtensions.Retryable);
                clientAction = GetString(root, GatewayProblemDetailsExtensions.ClientAction);

                // P3: only override the HTTP header value when the JSON field is non-empty.
                string? jsonRetryAfter = GetString(root, GatewayProblemDetailsExtensions.RetryAfter);
                if (!string.IsNullOrWhiteSpace(jsonRetryAfter)) {
                    retryAfter = jsonRetryAfter;
                }

                errors = GetErrors(root);
                extensions = GetExtensions(root);
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
            correlationId,
            tenantId,
            errors,
            reason,
            retryAfter,
            extensions,
            reasonCode,
            code: code,
            category: category,
            retryable: retryable,
            clientAction: clientAction);
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

    private static bool? GetBoolean(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.GetBoolean()
                : null;

    private static IReadOnlyDictionary<string, string> GetErrors(JsonElement root) {
        if (!root.TryGetProperty(GatewayProblemDetailsExtensions.Errors, out JsonElement errorsElement)
            || errorsElement.ValueKind != JsonValueKind.Object) {
            return FrozenDictionary<string, string>.Empty;
        }

        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty property in errorsElement.EnumerateObject()) {
            // P5: fall back to raw JSON when array contains only non-string items so the key is not silently dropped.
            string? value = property.Value.ValueKind switch {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Array => JoinStringArrayOrRaw(property.Value),
                _ => property.Value.GetRawText(),
            };

            if (!string.IsNullOrWhiteSpace(value)) {
                errors[property.Name] = value;
            }
        }

        return errors;
    }

    private static string JoinStringArrayOrRaw(JsonElement arrayElement) {
        string joined = string.Join(
            "; ",
            arrayElement.EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.String)
                .Select(static item => item.GetString())
                .Where(static item => !string.IsNullOrWhiteSpace(item)));
        return string.IsNullOrEmpty(joined) ? arrayElement.GetRawText() : joined;
    }

    private static IReadOnlyDictionary<string, JsonElement> GetExtensions(JsonElement root) {
        var extensions = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject()) {
            if (IsKnownProblemDetailsProperty(property.Name)) {
                continue;
            }

            extensions[property.Name] = property.Value.Clone();
        }

        return extensions;
    }

    // P1: exclude both RFC 7807 standard fields AND the stable extension names so they
    // do not appear twice (once as typed properties, once in Extensions).
    private static bool IsKnownProblemDetailsProperty(string propertyName)
        => propertyName is "type" or "title" or "status" or "detail" or "instance"
            or GatewayProblemDetailsExtensions.CorrelationId
            or GatewayProblemDetailsExtensions.TenantId
            or GatewayProblemDetailsExtensions.Errors
            or GatewayProblemDetailsExtensions.Reason
            or GatewayProblemDetailsExtensions.ReasonCode
            or GatewayProblemDetailsExtensions.RetryAfter;

    private static class StatusCodes {
        public const int Ok = 200;
        public const int BadGateway = 502;
    }
}

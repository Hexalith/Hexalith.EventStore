
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Helpers;

/// <summary>
/// Shared helpers for Tier 3 contract tests that submit commands and poll status
/// against the CommandApi via the Aspire topology. Centralizes patterns that would
/// otherwise be duplicated across test classes in the AspireContractTests collection.
/// </summary>
internal static class ContractTestHelpers {
    public static readonly TimeSpan DefaultPollTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan DefaultRetryTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Probes the CommandApi with a status query to verify it is reachable.
    /// Retries until a 200 OK or 404 NotFound is received, which proves the
    /// application layer is processing requests.
    /// </summary>
    public static async Task AssertCommandApiResponsiveAsync(
        HttpClient client,
        TimeSpan? timeout = null) {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout ?? DefaultRetryTimeout);
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline) {
            try {
                string token = TestJwtTokenGenerator.GenerateToken(
                    tenants: ["tenant-a"],
                    domains: ["counter"],
                    permissions: ["command:query"]);

                string probeId = Guid.NewGuid().ToString("N");
                using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/commands/status/{probeId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using HttpResponseMessage response = await client
                    .SendAsync(request).ConfigureAwait(false);

                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.OK) {
                    return;
                }

                lastError = new InvalidOperationException(
                    $"Unexpected probe status: {(int)response.StatusCode} {response.StatusCode}");
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                lastError = ex;
            }

            await Task.Delay(DefaultPollInterval).ConfigureAwait(false);
        }

        throw new ShouldAssertException(
            "CommandApi control-plane endpoint did not remain responsive during domain service restart cycle.",
            lastError);
    }

    public static async Task<string> SubmitCommandAndGetCorrelationIdAsync(
        HttpClient client,
        string tenant,
        string domain,
        string aggregateId,
        string commandType) {
        using HttpRequestMessage request = CreateCommandRequest(tenant, domain, aggregateId, commandType);
        using HttpResponseMessage response = await client
            .SendAsync(request).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Accepted) {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new ShouldAssertException(
                $"Expected 202 Accepted but was {(int)response.StatusCode} {response.StatusCode}.\nBody:\n{body}");
        }

        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
        return result.GetProperty("correlationId").GetString()!;
    }

    public static async Task<string> SubmitCommandAndGetCorrelationIdWithRetryAsync(
        HttpClient client,
        string tenant,
        string domain,
        string aggregateId,
        string commandType,
        TimeSpan? retryTimeout = null) {
        TimeSpan effectiveTimeout = retryTimeout ?? DefaultRetryTimeout;
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(effectiveTimeout);
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline) {
            try {
                return await SubmitCommandAndGetCorrelationIdAsync(client, tenant, domain, aggregateId, commandType)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                lastError = ex;
                await Task.Delay(DefaultPollInterval).ConfigureAwait(false);
            }
        }

        throw new TimeoutException(
            $"Unable to submit command during restart window within {effectiveTimeout}.",
            lastError);
    }

    public static async Task<JsonElement> PollUntilTerminalStatusAsync(
        HttpClient client,
        string correlationId,
        string tenant,
        TimeSpan? timeout = null,
        List<string>? observedStatuses = null) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenant],
            domains: ["counter"],
            permissions: ["command:submit", "command:query"]);

        TimeSpan effectiveTimeout = timeout ?? DefaultPollTimeout;
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(effectiveTimeout);
        JsonElement lastStatus = default;

        while (DateTimeOffset.UtcNow < deadline) {
            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/commands/status/{correlationId}");
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage statusResponse = await client
                .SendAsync(statusRequest).ConfigureAwait(false);

            if (statusResponse.StatusCode == HttpStatusCode.OK) {
                lastStatus = await statusResponse.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
                string statusValue = lastStatus.GetProperty("status").GetString()!;

                if (observedStatuses is not null && (observedStatuses.Count == 0 || observedStatuses[^1] != statusValue)) {
                    observedStatuses.Add(statusValue);
                }

                if (statusValue is "Completed" or "Rejected" or "PublishFailed" or "TimedOut") {
                    return lastStatus;
                }
            }

            await Task.Delay(DefaultPollInterval).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Command {correlationId} did not reach terminal status within {effectiveTimeout}. "
            + $"Last status: {lastStatus}");
    }

    public static HttpRequestMessage CreateCommandRequest(
        string tenant,
        string domain,
        string aggregateId,
        string commandType) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenant],
            domains: [domain],
            permissions: ["command:submit", "command:query"]);

        var body = new {
            MessageId = Guid.NewGuid().ToString(),
            Tenant = tenant,
            Domain = domain,
            AggregateId = aggregateId,
            CommandType = commandType,
            Payload = new { id = Guid.NewGuid().ToString() },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public static HttpRequestMessage CreateQueryRequest(
        string tenant,
        string domain,
        string aggregateId,
        string queryType,
        object? payload = null) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenant],
            domains: [domain],
            permissions: ["query:read"]);

        var body = new {
            Tenant = tenant,
            Domain = domain,
            AggregateId = aggregateId,
            QueryType = queryType,
            Payload = payload ?? new { id = Guid.NewGuid().ToString() },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public static HttpRequestMessage CreateCommandValidationRequest(
        string tenant,
        string domain,
        string commandType,
        string? aggregateId = null) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenant],
            domains: [domain],
            permissions: ["command:submit"]);

        var body = new {
            Tenant = tenant,
            Domain = domain,
            CommandType = commandType,
            AggregateId = aggregateId,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands/validate") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public static HttpRequestMessage CreateQueryValidationRequest(
        string tenant,
        string domain,
        string queryType,
        string? aggregateId = null) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenant],
            domains: [domain],
            permissions: ["query:read"]);

        var body = new {
            Tenant = tenant,
            Domain = domain,
            QueryType = queryType,
            AggregateId = aggregateId,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries/validate") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

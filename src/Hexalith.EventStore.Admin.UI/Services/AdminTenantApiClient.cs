using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// HTTP client for Admin.Server tenant REST API endpoints.
/// Wraps the "AdminApi" named HttpClient for tenant management operations.
/// </summary>
public class AdminTenantApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminTenantApiClient> logger)
{
    // ---- Read methods ----

    /// <summary>
    /// Lists all tenants.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of tenant summaries.</returns>
    public virtual async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        const string url = "api/v1/admin/tenants";
        try
        {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            HandleErrorStatus(response);
            IReadOnlyList<TenantSummary>? result = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<TenantSummary>>(ct)
                .ConfigureAwait(false);
            return result ?? [];
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch tenants from {Url}", url);
            throw new ServiceUnavailableException("Unable to load tenants.");
        }
    }

    /// <summary>
    /// Gets detailed tenant information including quotas.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tenant detail, or null if not found.</returns>
    public virtual async Task<TenantDetail?> GetTenantDetailAsync(string tenantId, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}";
        try
        {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            HandleErrorStatus(response);
            return await response.Content
                .ReadFromJsonAsync<TenantDetail>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch tenant detail from {Url}", url);
            throw new ServiceUnavailableException("Unable to load tenant details.");
        }
    }

    /// <summary>
    /// Gets users assigned to a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of tenant users.</returns>
    public virtual async Task<IReadOnlyList<TenantUser>> GetTenantUsersAsync(string tenantId, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}/users";
        try
        {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            HandleErrorStatus(response);
            IReadOnlyList<TenantUser>? result = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<TenantUser>>(ct)
                .ConfigureAwait(false);
            return result ?? [];
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch tenant users from {Url}", url);
            throw new ServiceUnavailableException("Unable to load tenant users.");
        }
    }

    // ---- Write methods ----

    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    /// <param name="request">The create tenant request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        const string url = "api/v1/admin/tenants";
        try
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(url, request, ct).ConfigureAwait(false);
            HandleErrorStatus(response);
            return await response.Content
                .ReadFromJsonAsync<AdminOperationResult>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to create tenant at {Url}", url);
            throw new ServiceUnavailableException("Unable to create tenant.");
        }
    }

    /// <summary>
    /// Disables (suspends) an active tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> DisableTenantAsync(string tenantId, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}/disable";
        try
        {
            using HttpResponseMessage response = await client.PostAsync(url, null, ct).ConfigureAwait(false);
            HandleErrorStatus(response);
            return await response.Content
                .ReadFromJsonAsync<AdminOperationResult>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to disable tenant at {Url}", url);
            throw new ServiceUnavailableException("Unable to disable tenant.");
        }
    }

    /// <summary>
    /// Enables a suspended tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> EnableTenantAsync(string tenantId, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}/enable";
        try
        {
            using HttpResponseMessage response = await client.PostAsync(url, null, ct).ConfigureAwait(false);
            HandleErrorStatus(response);
            return await response.Content
                .ReadFromJsonAsync<AdminOperationResult>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to enable tenant at {Url}", url);
            throw new ServiceUnavailableException("Unable to enable tenant.");
        }
    }

    /// <summary>
    /// Adds a user to a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="role">The user's role.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> AddUserToTenantAsync(string tenantId, string userId, string role, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}/users";
        try
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(url, new AddTenantUserRequest(userId, role), ct).ConfigureAwait(false);
            HandleErrorStatus(response);
            return await response.Content
                .ReadFromJsonAsync<AdminOperationResult>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to add user to tenant at {Url}", url);
            throw new ServiceUnavailableException("Unable to add user to tenant.");
        }
    }

    /// <summary>
    /// Removes a user from a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> RemoveUserFromTenantAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}/remove-user";
        try
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(url, new RemoveTenantUserRequest(userId), ct).ConfigureAwait(false);
            HandleErrorStatus(response);
            return await response.Content
                .ReadFromJsonAsync<AdminOperationResult>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to remove user from tenant at {Url}", url);
            throw new ServiceUnavailableException("Unable to remove user from tenant.");
        }
    }

    /// <summary>
    /// Changes a user's role within a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="newRole">The new role to assign.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> ChangeUserRoleAsync(string tenantId, string userId, string newRole, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}/change-role";
        try
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(url, new ChangeTenantUserRoleRequest(userId, newRole), ct).ConfigureAwait(false);
            HandleErrorStatus(response);
            return await response.Content
                .ReadFromJsonAsync<AdminOperationResult>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to change user role at {Url}", url);
            throw new ServiceUnavailableException("Unable to change user role.");
        }
    }

    private static void HandleErrorStatus(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        HttpStatusCode statusCode = response.StatusCode;
        string? reasonPhrase = response.ReasonPhrase;

        throw statusCode switch
        {
            HttpStatusCode.Unauthorized => new UnauthorizedAccessException(
                "Authentication required. Please sign in again."),
            HttpStatusCode.Forbidden => new ForbiddenAccessException(
                "Access denied. Insufficient permissions to access this resource."),
            HttpStatusCode.UnprocessableEntity => new InvalidOperationException(
                reasonPhrase ?? "The operation was rejected by the server."),
            HttpStatusCode.ServiceUnavailable => new ServiceUnavailableException(
                "The admin backend service is temporarily unavailable."),
            _ => new HttpRequestException(
                $"Admin API returned {(int)statusCode}: {reasonPhrase}",
                null,
                statusCode),
        };
    }
}

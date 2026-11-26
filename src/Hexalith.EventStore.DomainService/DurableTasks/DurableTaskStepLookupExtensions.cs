using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Opts a G-1 host into the authenticated, owner-scoped Durable Task step lookup.</summary>
public static class DurableTaskStepLookupExtensions
{
    /// <summary>The v1 POST route for committed owner step lookup.</summary>
    public const string StepLookupPath = "/internal/durable-tasks/v1/steps/lookup";

    /// <summary>The receiver settings for the G-1 workload assertion audience and caller allow-list.</summary>
    public const string WorkloadSection = "Authentication:DurableTaskStepLookup";

    private const string Scheme = "EventStoreDurableTaskStepLookup";
    private const string Operation = EventStoreWorkloadOperations.DurableTaskStepLookup;

    /// <summary>Registers the dedicated operation policy and shared workload assertion verifier, including its Dapr channel check.</summary>
    /// <param name="services">The host services.</param>
    /// <returns>The services. The host also registers its <see cref="DurableTaskAdmission"/>.</returns>
    public static IServiceCollection AddDurableTaskStepLookup(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddAuthentication().AddEventStoreWorkloadScheme(Scheme, WorkloadSection);
        return services.AddEventStoreWorkloadPolicies(Scheme, [Operation]);
    }

    /// <summary>Maps the committed-step lookup with the exact grants supplied by the host.</summary>
    /// <param name="endpoints">The host endpoints.</param>
    /// <param name="grants">The distinct owner workload, operation, step and domain grants.</param>
    /// <returns>The endpoint builder.</returns>
    public static RouteHandlerBuilder MapDurableTaskStepLookup(this IEndpointRouteBuilder endpoints, params DurableTaskStepLookupGrant[] grants)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(grants);
        foreach (DurableTaskStepLookupGrant grant in grants)
        {
            ArgumentNullException.ThrowIfNull(grant);
            ArgumentException.ThrowIfNullOrWhiteSpace(grant.Workload);
            ArgumentException.ThrowIfNullOrWhiteSpace(grant.AdmissionOperation);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(grant.StepNumber);
            ArgumentException.ThrowIfNullOrWhiteSpace(grant.TargetDomain);
            ArgumentNullException.ThrowIfNull(grant.DeriveTargetId);
        }

        var registered = grants.ToDictionary(grant => (grant.Workload, grant.AdmissionOperation, grant.StepNumber, grant.TargetDomain));
        return endpoints.MapPost(StepLookupPath, LookupAsync)
            .RequireAuthorization(EventStoreWorkloadAuthenticationDefaults.OperationPolicy(Scheme, Operation));

        async Task<IResult> LookupAsync(HttpContext context, DurableTaskAdmission admission, DurableTaskStepLookupRequest request,
            CancellationToken cancellationToken)
        {
            AuthenticateResult authenticated = await context.AuthenticateAsync(Scheme).ConfigureAwait(false);
            if (!authenticated.Succeeded || authenticated.Principal is not { } principal
                || !principal.HasClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, Operation)
                || principal.FindFirst(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType) is not { Value: var workload }
                || principal.FindFirst(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType) is not { Value: var tenant }
                || string.IsNullOrWhiteSpace(request.TargetDomain)
                || !principal.HasClaim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType, request.TargetDomain)
                || string.IsNullOrWhiteSpace(request.TaskId) || string.IsNullOrWhiteSpace(request.StepKey)
                || string.IsNullOrWhiteSpace(request.AdmissionOperation) || string.IsNullOrWhiteSpace(request.TargetId)
                || !DurableTaskAdmission.IsSha256(request.RequestSha256)
                || !DurableTaskEffectStep.TryParseKey(request.TaskId, request.StepKey, out int number)
                || !registered.TryGetValue((workload, request.AdmissionOperation, number, request.TargetDomain), out DurableTaskStepLookupGrant? grant))
            {
                return Results.NotFound();
            }

            if (await admission.ResolveStepAsync(request.TaskId, request.StepKey, cancellationToken).ConfigureAwait(false) is not { } binding
                || binding.Task.Scope.TenantId != tenant || binding.Task.Scope.Operation != grant.AdmissionOperation
                || binding.Task.RequestSha256 != request.RequestSha256
                || grant.DeriveTargetId(binding.Task, binding.Key) != request.TargetId
                || request.Fence <= 0 || binding.Fence != request.Fence)
            {
                return Results.NotFound();
            }

            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new DurableTaskStepLookupResult(
                binding.Task.Scope.TenantId, binding.Task.Scope.ActorId, binding.Task.Scope.Operation, binding.Task.RequestSha256,
                binding.Task.TaskId, binding.Key, binding.Status, binding.Receipt, binding.AcceptsExecution(request.Fence)));
        }
    }
}

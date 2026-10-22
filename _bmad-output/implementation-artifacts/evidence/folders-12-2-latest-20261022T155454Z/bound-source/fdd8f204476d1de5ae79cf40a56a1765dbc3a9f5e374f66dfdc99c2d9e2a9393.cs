using System.Reflection;

using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Admin.Server.Tests.IntegrationTests;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.Admin.Server.Tests.Authorization;

public class AdminActionSecurityMetadataTests {
    private static readonly IReadOnlyList<(Type Controller, string Action, string Policy, bool TenantFilter, long? BodyLimit)> ExpectedActions =
    [
        (typeof(AdminTypeCatalogController), "ListEventTypes", AdminAuthorizationPolicies.ReadOnly, false, null),
        (typeof(AdminTypeCatalogController), "ListCommandTypes", AdminAuthorizationPolicies.ReadOnly, false, null),
        (typeof(AdminTypeCatalogController), "ListAggregateTypes", AdminAuthorizationPolicies.ReadOnly, false, null),
        (typeof(AdminStreamsController), "BisectAggregateStateAsync", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStreamsController), "DiffAggregateStateAsync", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStreamsController), "GetAggregateBlameAsync", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStreamsController), "GetAggregateStateAsync", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStreamsController), "GetEventDetailAsync", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStreamsController), "GetEventStepFrameAsync", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStreamsController), "GetRecentCommandsAsync", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStreamsController), "GetRecentlyActiveStreamsAsync", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStreamsController), "GetStreamTimelineAsync", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStreamsController), "SandboxCommandAsync", AdminAuthorizationPolicies.ReadOnly, true, AdminRequestSizeLimits.OrdinaryJsonBody),
        (typeof(AdminStreamsController), "TraceCausationChainAsync", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStorageController), "GetStorageOverview", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStorageController), "GetHotStreams", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStorageController), "GetSnapshotPolicies", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStorageController), "GetCompactionJobs", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminStorageController), "TriggerCompaction", AdminAuthorizationPolicies.Operator, true, null),
        (typeof(AdminStorageController), "CreateSnapshot", AdminAuthorizationPolicies.Operator, true, null),
        (typeof(AdminStorageController), "SetSnapshotPolicy", AdminAuthorizationPolicies.Operator, true, null),
        (typeof(AdminStorageController), "DeleteSnapshotPolicy", AdminAuthorizationPolicies.Operator, true, null),
        (typeof(AdminProjectionsController), "ListProjections", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminProjectionsController), "GetProjectionDetail", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminProjectionsController), "PauseProjection", AdminAuthorizationPolicies.Operator, true, null),
        (typeof(AdminProjectionsController), "ResumeProjection", AdminAuthorizationPolicies.Operator, true, null),
        (typeof(AdminProjectionsController), "ResetProjection", AdminAuthorizationPolicies.Operator, true, AdminRequestSizeLimits.OrdinaryJsonBody),
        (typeof(AdminProjectionsController), "ReplayProjection", AdminAuthorizationPolicies.Operator, true, AdminRequestSizeLimits.OrdinaryJsonBody),
        (typeof(AdminHealthController), "GetSystemHealth", AdminAuthorizationPolicies.ReadOnly, false, null),
        (typeof(AdminHealthController), "GetDaprComponentStatus", AdminAuthorizationPolicies.ReadOnly, false, null),
        (typeof(AdminHealthController), "GetComponentHealthHistoryAsync", AdminAuthorizationPolicies.ReadOnly, false, null),
        (typeof(AdminDeadLettersController), "GetDeadLetterCount", AdminAuthorizationPolicies.Admin, false, null),
        (typeof(AdminDeadLettersController), "ListDeadLetters", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminDeadLettersController), "RetryDeadLetters", AdminAuthorizationPolicies.Operator, true, AdminRequestSizeLimits.OrdinaryJsonBody),
        (typeof(AdminDeadLettersController), "SkipDeadLetters", AdminAuthorizationPolicies.Operator, true, AdminRequestSizeLimits.OrdinaryJsonBody),
        (typeof(AdminDeadLettersController), "ArchiveDeadLetters", AdminAuthorizationPolicies.Operator, true, AdminRequestSizeLimits.OrdinaryJsonBody),
        (typeof(AdminDaprController), "GetComponents", AdminAuthorizationPolicies.ReadOnly, false, null),
        (typeof(AdminDaprController), "GetSidecar", AdminAuthorizationPolicies.ReadOnly, false, null),
        (typeof(AdminDaprController), "GetInfrastructureOverview", AdminAuthorizationPolicies.ReadOnly, false, null),
        (typeof(AdminDaprController), "GetActorRuntimeInfoAsync", AdminAuthorizationPolicies.ReadOnly, false, null),
        (typeof(AdminDaprController), "GetActorInstanceStateAsync", AdminAuthorizationPolicies.Admin, false, null),
        (typeof(AdminDaprController), "GetPubSubOverviewAsync", AdminAuthorizationPolicies.ReadOnly, false, null),
        (typeof(AdminDaprController), "GetResiliencySpecAsync", AdminAuthorizationPolicies.ReadOnly, false, null),
        (typeof(AdminConsistencyController), "GetCheckResult", AdminAuthorizationPolicies.Admin, false, null),
        (typeof(AdminConsistencyController), "GetChecks", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminConsistencyController), "TriggerCheck", AdminAuthorizationPolicies.Operator, true, AdminRequestSizeLimits.OrdinaryJsonBody),
        (typeof(AdminConsistencyController), "CancelCheck", AdminAuthorizationPolicies.Admin, false, null),
        (typeof(AdminBackupsController), "GetBackupJobs", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminBackupsController), "TriggerBackup", AdminAuthorizationPolicies.Admin, true, null),
        (typeof(AdminBackupsController), "ValidateBackup", AdminAuthorizationPolicies.Admin, false, null),
        (typeof(AdminBackupsController), "TriggerRestore", AdminAuthorizationPolicies.Admin, false, null),
        (typeof(AdminBackupsController), "ExportStream", AdminAuthorizationPolicies.Admin, false, AdminRequestSizeLimits.OrdinaryJsonBody),
        (typeof(AdminBackupsController), "ImportStream", AdminAuthorizationPolicies.Admin, false, AdminRequestSizeLimits.BackupImportJsonBody),
        (typeof(AdminBackupsController), "SubmitAdmission", AdminAuthorizationPolicies.Admin, false, AdminRequestSizeLimits.OrdinaryJsonBody),
        (typeof(AdminBackupsController), "SubmitAdmissionDecision", AdminAuthorizationPolicies.Admin, false, null),
        (typeof(AdminBackupsController), "GetAdmission", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminBackupsController), "SubmitCryptoShreddingWorkflow", AdminAuthorizationPolicies.Admin, false, AdminRequestSizeLimits.OrdinaryJsonBody),
        (typeof(AdminBackupsController), "GetCryptoShreddingWorkflow", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminTracesController), "GetCorrelationTraceMap", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminTenantsController), "AddUserToTenantAsync", AdminAuthorizationPolicies.Admin, false, AdminRequestSizeLimits.OrdinaryJsonBody),
        (typeof(AdminTenantsController), "ChangeUserRoleAsync", AdminAuthorizationPolicies.Admin, false, AdminRequestSizeLimits.OrdinaryJsonBody),
        (typeof(AdminTenantsController), "CreateTenantAsync", AdminAuthorizationPolicies.Admin, false, AdminRequestSizeLimits.OrdinaryJsonBody),
        (typeof(AdminTenantsController), "DisableTenantAsync", AdminAuthorizationPolicies.Admin, false, null),
        (typeof(AdminTenantsController), "EnableTenantAsync", AdminAuthorizationPolicies.Admin, false, null),
        (typeof(AdminTenantsController), "GetTenantDetailAsync", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminTenantsController), "GetTenantUsersAsync", AdminAuthorizationPolicies.ReadOnly, true, null),
        (typeof(AdminTenantsController), "ListTenantsAsync", AdminAuthorizationPolicies.Admin, false, null),
        (typeof(AdminTenantsController), "RemoveUserFromTenantAsync", AdminAuthorizationPolicies.Admin, false, AdminRequestSizeLimits.OrdinaryJsonBody),
    ];

    [Fact]
    public void PublicAdminActions_HaveExplicitExhaustivePolicyTenantAndBodyClassification() {
        using var host = new AdminTestHost();
        EndpointDataSource endpointDataSource = host.GetService<EndpointDataSource>();
        (RouteEndpoint Endpoint, ControllerActionDescriptor Action)[] actualActions = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => (Endpoint: endpoint, Action: endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()))
            .Where(item => item.Action?.ControllerTypeInfo.Assembly == typeof(AdminStreamsController).Assembly)
            .Select(item => (item.Endpoint, item.Action!))
            .ToArray();

        string[] actualKeys = actualActions.Select(item => GetKey(item.Action.MethodInfo)).OrderBy(key => key).ToArray();
        string[] expectedKeys = ExpectedActions.Select(item => GetKey(item.Controller, item.Action)).OrderBy(key => key).ToArray();
        actualKeys.ShouldBe(expectedKeys);

        foreach ((Type controller, string action, string expectedPolicy, bool expectedTenantFilter, long? expectedBodyLimit) in ExpectedActions) {
            (RouteEndpoint endpoint, ControllerActionDescriptor descriptor) = actualActions.Single(item =>
                item.Action.ControllerTypeInfo.AsType() == controller
                && string.Equals(item.Action.MethodInfo.Name, action, StringComparison.Ordinal));
            endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>().ShouldBeNull();

            string[] policies = endpoint.Metadata.GetOrderedMetadata<AuthorizeAttribute>()
                .Select(attribute => attribute.Policy)
                .Where(policy => !string.IsNullOrWhiteSpace(policy))
                .Cast<string>()
                .ToArray();
            policies.ShouldContain(expectedPolicy);
            policies.Max(GetPolicyRank).ShouldBe(GetPolicyRank(expectedPolicy));

            bool hasTenantFilter = endpoint.Metadata.GetOrderedMetadata<ServiceFilterAttribute>()
                .Any(attribute => attribute.ServiceType == typeof(AdminTenantAuthorizationFilter));
            hasTenantFilter.ShouldBe(expectedTenantFilter, GetKey(controller, action));

            Microsoft.AspNetCore.Mvc.Abstractions.ParameterDescriptor[] bodyParameters = descriptor.Parameters
                .Where(parameter => parameter.BindingInfo?.BindingSource?.CanAcceptDataFrom(BindingSource.Body) == true)
                .ToArray();
            IRequestSizeLimitMetadata? limit = endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>();
            (limit?.MaxRequestBodySize).ShouldBe(expectedBodyLimit, GetKey(controller, action));
            bodyParameters.Length.ShouldBe(expectedBodyLimit.HasValue ? 1 : 0, GetKey(controller, action));

            bool advertises413 = endpoint.Metadata.GetOrderedMetadata<ProducesResponseTypeAttribute>()
                .Any(attribute => attribute.StatusCode == StatusCodes.Status413PayloadTooLarge
                    && attribute.Type == typeof(ProblemDetails));
            advertises413.ShouldBe(expectedBodyLimit.HasValue, GetKey(controller, action));
        }
    }

    private static string GetKey(MethodInfo method) => GetKey(method.DeclaringType!, method.Name);

    private static string GetKey(Type controller, string action) => $"{controller.Name}.{action}";

    private static int GetPolicyRank(string policy)
        => policy switch {
            AdminAuthorizationPolicies.ReadOnly => 0,
            AdminAuthorizationPolicies.Operator => 1,
            AdminAuthorizationPolicies.Admin => 2,
            _ => throw new InvalidOperationException($"Unclassified Admin policy '{policy}'."),
        };
}

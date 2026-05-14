using System.Text.Json;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Validation;

namespace Hexalith.EventStore.Contracts.Tests.Validation;

public class PreflightValidationResultTests {
    [Fact]
    public void DeniedResultShouldSerializeStableReasonCode() {
        var result = new PreflightValidationResult(
            false,
            "Tenant is disabled.",
            AuthorizationReasonCodes.TenantDisabled);

        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.ShouldContain("\"isAuthorized\":false");
        json.ShouldContain("\"reason\":\"Tenant is disabled.\"");
        json.ShouldContain("\"reasonCode\":\"tenant_disabled\"");
    }

    [Fact]
    public void AuthorizedResultShouldPreserveExistingNullReasonBehavior() {
        var result = new PreflightValidationResult(true);

        result.IsAuthorized.ShouldBeTrue();
        result.Reason.ShouldBeNull();
        result.ReasonCode.ShouldBeNull();
    }
}

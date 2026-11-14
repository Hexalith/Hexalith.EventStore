using System.Globalization;
using System.Security.Claims;

using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Primitives;

using Shouldly;

namespace Hexalith.EventStore.ServiceDefaults.Tests.Authentication;

/// <summary>Qualifies historical API names independently from workload authentication authority.</summary>
public sealed class EventStoreWorkloadAuthenticationDefaultsTests
{
    /// <summary>Retains the literal field that existing source and binary consumers reference.</summary>
    [Fact]
    public void LegacyCallerClaimNameRemainsAnExportedPublicConstant()
    {
        var field = typeof(EventStoreWorkloadAuthenticationDefaults)
            .GetField(nameof(EventStoreWorkloadAuthenticationDefaults.LegacyCallerClaimType))
            .ShouldNotBeNull();

        field.IsLiteral.ShouldBeTrue();
        field.GetRawConstantValue().ShouldBe("dapr_caller_app_id");
    }

    /// <summary>Requires the authorized-party proof and strips the historical attribution claim.</summary>
    /// <param name="includeAuthorizedParty">Whether the validated assertion carries its authoritative caller.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyCallerClaimDoesNotSupplyOrSurviveAuthenticatedCallerIdentity(bool includeAuthorizedParty)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<Claim> claims =
        [
            new("iat", now.AddSeconds(-1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new("exp", now.AddSeconds(59).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new(EventStoreWorkloadAuthenticationDefaults.LegacyCallerClaimType, "untrusted-legacy-caller"),
            new(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, "test-operation"),
        ];
        if (includeAuthorizedParty)
        {
            claims.Add(new(EventStoreWorkloadAuthenticationDefaults.CallerClaimType, "trusted-workload"));
        }

        var validated = new ClaimsPrincipal(new ClaimsIdentity(claims, "validated-assertion"));
        var options = new WorkloadAuthenticationOptions { Audience = "receiver", AllowedCallers = ["trusted-workload"] };

        WorkloadAssertionEvaluation result = WorkloadAssertionEvaluator.Evaluate(
            validated,
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            options,
            null,
            StringValues.Empty,
            now,
            TimeSpan.Zero);

        result.Succeeded.ShouldBe(includeAuthorizedParty);
        if (includeAuthorizedParty)
        {
            result.Caller.ShouldBe("trusted-workload");
            ClaimsPrincipal principal = result.Principal.ShouldNotBeNull();
            principal.FindFirst(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType).ShouldNotBeNull().Value.ShouldBe("trusted-workload");
            principal.HasClaim(claim => claim.Type == EventStoreWorkloadAuthenticationDefaults.LegacyCallerClaimType).ShouldBeFalse();
        }
        else
        {
            result.ReasonCode.ShouldBe(WorkloadAuthenticationReasons.CallerMissing);
            result.Principal.ShouldBeNull();
        }
    }
}

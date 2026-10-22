using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Fails startup of a workload that must prove itself to other workloads when, in authority mode, it has no
/// client-credentials registration to obtain its assertions.
/// </summary>
/// <remarks>
/// Without this check such a host starts healthy and every outbound internal call is then denied at runtime. In
/// symmetric mode the shared contract signs assertions locally, so no client is needed. Messages name settings only,
/// never configured values.
/// </remarks>
public sealed class ValidateWorkloadAssertionIssuerOptions(
    IOptionsMonitor<JwtBearerAuthenticationOptions> contractOptions,
    IHostEnvironment environment) : IValidateOptions<WorkloadAssertionIssuerOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, WorkloadAssertionIssuerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        JwtBearerAuthenticationOptions contract = contractOptions.Get(EventStoreWorkloadAuthenticationDefaults.JwtContractOptionsName);
        if (string.IsNullOrWhiteSpace(contract.Authority))
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (!options.HasClientCredentials())
        {
            failures.Add(
                $"{WorkloadAssertionIssuerOptions.SectionName}:ClientId and {WorkloadAssertionIssuerOptions.SectionName}:ClientSecret must be configured when {EventStoreWorkloadAuthenticationDefaults.JwtContractSection}:Authority is set; without them every outbound internal call is denied.");
        }

        if (string.IsNullOrWhiteSpace(options.AudienceScopePrefix) || string.IsNullOrWhiteSpace(options.OperationScopePrefix))
        {
            failures.Add(
                $"{WorkloadAssertionIssuerOptions.SectionName}:AudienceScopePrefix and OperationScopePrefix must not be blank.");
        }

        if (!string.IsNullOrWhiteSpace(options.TokenEndpoint)
            && (!Uri.TryCreate(options.TokenEndpoint.Trim(), UriKind.Absolute, out Uri? endpoint)
                || !(string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                    || (environment.IsDevelopment() && string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))))
        {
            failures.Add(
                $"{WorkloadAssertionIssuerOptions.SectionName}:TokenEndpoint must be an absolute HTTPS URI outside Development.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

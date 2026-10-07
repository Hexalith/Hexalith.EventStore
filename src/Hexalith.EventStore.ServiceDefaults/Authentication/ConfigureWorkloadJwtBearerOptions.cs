using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Applies the shared JWT validation contract to every registered workload-assertion scheme and narrows the
/// accepted audience to the receiver's own workload audience.
/// </summary>
/// <remarks>
/// When the shared contract or the receiver settings are unusable, the scheme is marked unconfigured and every
/// request it authenticates is denied with <see cref="WorkloadAuthenticationReasons.VerifierUnconfigured"/>.
/// </remarks>
public sealed class ConfigureWorkloadJwtBearerOptions(
    IEnumerable<WorkloadSchemeRegistration> registrations,
    IOptionsMonitor<JwtBearerAuthenticationOptions> contractOptions,
    IOptionsMonitor<WorkloadAuthenticationOptions> workloadOptions,
    IHostEnvironment environment) : IConfigureNamedOptions<JwtBearerOptions>
{
    /// <summary>Gets the token-validation property-bag key marking a usable workload scheme.</summary>
    public const string ConfiguredMarkerKey = "eventstore:workload-scheme-configured";

    private readonly WorkloadSchemeRegistration[] _registrations = [.. registrations ?? throw new ArgumentNullException(nameof(registrations))];

    /// <summary>
    /// Gets whether bearer options were configured as a usable workload scheme.
    /// </summary>
    /// <param name="options">The bearer options.</param>
    /// <returns><see langword="true"/> when the scheme can validate assertions.</returns>
    public static bool IsConfigured(JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.TokenValidationParameters?.PropertyBag is { } bag
            && bag.TryGetValue(ConfiguredMarkerKey, out object? marker)
            && marker is true;
    }

    /// <inheritdoc />
    public void Configure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (name is null || !_registrations.Any(registration => string.Equals(registration.SchemeName, name, StringComparison.Ordinal)))
        {
            return;
        }

        options.EventsType = typeof(WorkloadJwtBearerEvents);
        options.SaveToken = false;
        options.IncludeErrorDetails = false;

        JwtBearerAuthenticationOptions contract = contractOptions.Get(EventStoreWorkloadAuthenticationDefaults.JwtContractOptionsName);
        WorkloadAuthenticationOptions workload = workloadOptions.Get(name);
        bool usable = workload.GetValidationFailure() is null
            && JwtBearerAuthenticationContract.Validate(
                contract,
                environment,
                EventStoreWorkloadAuthenticationDefaults.JwtContractSection).Succeeded;
        if (!usable)
        {
            // Fail closed: no key, no issuer, and an explicit unconfigured marker the events check first.
            options.Authority = null;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                RequireSignedTokens = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
            };
            options.TokenValidationParameters.PropertyBag = new Dictionary<string, object> { [ConfiguredMarkerKey] = false };
            return;
        }

        JwtBearerAuthenticationContract.Configure(options, contract);
        options.TokenValidationParameters.ValidAudience = null;
        options.TokenValidationParameters.ValidAudiences = [workload.Audience!.Trim()];
        options.TokenValidationParameters.PropertyBag = new Dictionary<string, object> { [ConfiguredMarkerKey] = true };
    }

    /// <inheritdoc />
    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);
}

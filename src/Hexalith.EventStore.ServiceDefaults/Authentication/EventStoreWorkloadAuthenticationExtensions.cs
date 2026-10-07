using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Registers the shared internal-credential schemes and their authorization policies.
/// </summary>
public static class EventStoreWorkloadAuthenticationExtensions
{
    /// <summary>
    /// Adds a workload-assertion scheme: the application-channel token plus a short-lived assertion from the
    /// trusted JWT issuer, validated by the shared JWT contract and the receiver settings bound from
    /// <paramref name="configurationSectionPath"/>.
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="schemeName">The scheme name.</param>
    /// <param name="configurationSectionPath">The section holding <see cref="WorkloadAuthenticationOptions"/>.</param>
    /// <param name="applyDefaults">Optional defaults applied after binding, such as the receiver's own audience.</param>
    /// <returns>The authentication builder.</returns>
    public static AuthenticationBuilder AddEventStoreWorkloadScheme(
        this AuthenticationBuilder builder,
        string schemeName,
        string configurationSectionPath,
        Action<WorkloadAuthenticationOptions, IServiceProvider>? applyDefaults = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationSectionPath);

        IServiceCollection services = builder.Services;
        if (services.Any(descriptor => descriptor.ServiceType == typeof(WorkloadSchemeRegistration)
            && descriptor.ImplementationInstance is WorkloadSchemeRegistration registration
            && string.Equals(registration.SchemeName, schemeName, StringComparison.Ordinal)))
        {
            return builder;
        }

        _ = services.AddSingleton(new WorkloadSchemeRegistration(schemeName));
        AddSharedWorkloadServices(services);
        OptionsBuilder<WorkloadAuthenticationOptions> options = services
            .AddOptions<WorkloadAuthenticationOptions>(schemeName)
            .BindConfiguration(configurationSectionPath);
        if (applyDefaults is not null)
        {
            _ = options.PostConfigure<IServiceProvider>((value, serviceProvider) => applyDefaults(value, serviceProvider));
        }

        return builder.AddJwtBearer(schemeName, static _ => { });
    }

    /// <summary>
    /// Adds the scheme that authenticates only the Dapr application channel.
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <returns>The authentication builder.</returns>
    public static AuthenticationBuilder AddEventStoreSidecarChannelScheme(this AuthenticationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Services.Any(static descriptor => descriptor.ServiceType == typeof(SidecarChannelSchemeMarker)))
        {
            return builder;
        }

        _ = builder.Services.AddSingleton<SidecarChannelSchemeMarker>();
        return builder.AddScheme<DaprSidecarChannelAuthenticationOptions, DaprSidecarChannelAuthenticationHandler>(
            EventStoreWorkloadAuthenticationDefaults.SidecarChannelScheme,
            displayName: null,
            configureOptions: null);
    }

    /// <summary>
    /// Adds the operation policies of one workload scheme, its any-workload policy, and the sidecar-channel policy.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="schemeName">The workload scheme.</param>
    /// <param name="operations">The operations that receive a dedicated policy.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddEventStoreWorkloadPolicies(
        this IServiceCollection services,
        string schemeName,
        IEnumerable<string> operations)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemeName);
        ArgumentNullException.ThrowIfNull(operations);
        string[] operationList = [.. operations.Distinct(StringComparer.Ordinal)];
        _ = services.AddAuthorization();
        _ = services.Configure<AuthorizationOptions>(options =>
        {
            foreach (string operation in operationList)
            {
                string policyName = EventStoreWorkloadAuthenticationDefaults.OperationPolicy(schemeName, operation);
                if (options.GetPolicy(policyName) is null)
                {
                    options.AddPolicy(policyName, policy => policy
                        .AddAuthenticationSchemes(schemeName)
                        .RequireAuthenticatedUser()
                        .RequireClaim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType)
                        .RequireClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, operation));
                }
            }

            string anyWorkloadPolicy = EventStoreWorkloadAuthenticationDefaults.AnyWorkloadPolicy(schemeName);
            if (options.GetPolicy(anyWorkloadPolicy) is null)
            {
                options.AddPolicy(anyWorkloadPolicy, CreateAnyWorkloadPolicy(schemeName));
            }

            if (options.GetPolicy(EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy) is null)
            {
                options.AddPolicy(EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy, policy => policy
                    .AddAuthenticationSchemes(EventStoreWorkloadAuthenticationDefaults.SidecarChannelScheme)
                    .RequireAuthenticatedUser()
                    .RequireClaim(EventStoreWorkloadAuthenticationDefaults.ChannelClaimType));
            }
        });
        return services;
    }

    /// <summary>
    /// Creates the policy requiring any authenticated workload from one scheme.
    /// </summary>
    /// <param name="schemeName">The workload scheme.</param>
    /// <returns>The policy.</returns>
    public static AuthorizationPolicy CreateAnyWorkloadPolicy(string schemeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemeName);
        return new AuthorizationPolicyBuilder(schemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType)
            .RequireClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType)
            .Build();
    }

    /// <summary>
    /// Adds the outbound workload-assertion issuer bound from <see cref="WorkloadAssertionIssuerOptions.SectionName"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="applyDefaults">Optional defaults applied after binding, such as this workload's identity.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddEventStoreWorkloadAssertionIssuer(
        this IServiceCollection services,
        Action<WorkloadAssertionIssuerOptions>? applyDefaults = null)
        => AddEventStoreWorkloadAssertionIssuer(services, configuration: null, applyDefaults);

    /// <summary>
    /// Adds the outbound workload-assertion issuer, binding its settings and the shared JWT contract from an
    /// explicit configuration when one is supplied.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration to bind, or <see langword="null"/> to bind the registered one.</param>
    /// <param name="applyDefaults">Optional defaults applied after binding, such as this workload's identity.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddEventStoreWorkloadAssertionIssuer(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration? configuration,
        Action<WorkloadAssertionIssuerOptions>? applyDefaults = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        AddWorkloadJwtContractOptions(services, configuration);
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(WorkloadIssuerMarker)))
        {
            if (applyDefaults is not null)
            {
                _ = services.AddOptions<WorkloadAssertionIssuerOptions>().PostConfigure(applyDefaults);
            }

            return services;
        }

        _ = services.AddSingleton<WorkloadIssuerMarker>();
        OptionsBuilder<WorkloadAssertionIssuerOptions> options = services.AddOptions<WorkloadAssertionIssuerOptions>();
        options = configuration is null
            ? options.BindConfiguration(WorkloadAssertionIssuerOptions.SectionName)
            : options.Bind(configuration.GetSection(WorkloadAssertionIssuerOptions.SectionName));
        if (applyDefaults is not null)
        {
            _ = options.PostConfigure(applyDefaults);
        }

        _ = services.AddHttpClient(JwtWorkloadAssertionIssuer.HttpClientName);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(WorkloadSecurityClock.System);
        services.TryAddSingleton<IWorkloadAssertionIssuer, JwtWorkloadAssertionIssuer>();
        return services;
    }

    /// <summary>
    /// Fails host startup when the shared contract names an OIDC authority and
    /// <see cref="WorkloadAssertionIssuerOptions.SectionName"/> lacks the client credentials needed to obtain workload
    /// assertions. A host whose internal calls cannot be proven must not start healthy.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection RequireEventStoreWorkloadIssuerClientCredentials(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<WorkloadAssertionIssuerOptions>, ValidateWorkloadAssertionIssuerOptions>());
        _ = services.AddOptions<WorkloadAssertionIssuerOptions>().ValidateOnStart();
        return services;
    }

    private static void AddWorkloadJwtContractOptions(
        IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
    {
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(WorkloadContractMarker)))
        {
            return;
        }

        _ = services.AddSingleton<WorkloadContractMarker>();
        OptionsBuilder<JwtBearerAuthenticationOptions> contract = services
            .AddOptions<JwtBearerAuthenticationOptions>(EventStoreWorkloadAuthenticationDefaults.JwtContractOptionsName);
        _ = configuration is null
            ? contract.BindConfiguration(EventStoreWorkloadAuthenticationDefaults.JwtContractSection)
            : contract.Bind(configuration.GetSection(EventStoreWorkloadAuthenticationDefaults.JwtContractSection));
    }

    private static void AddSharedWorkloadServices(IServiceCollection services)
    {
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(SharedWorkloadServicesMarker)))
        {
            return;
        }

        _ = services.AddSingleton<SharedWorkloadServicesMarker>();
        AddWorkloadJwtContractOptions(services);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<JwtBearerOptions>, ConfigureWorkloadJwtBearerOptions>());
        services.TryAddTransient<WorkloadJwtBearerEvents>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(WorkloadSecurityClock.System);
        services.TryAddSingleton<WorkloadAssertionValidator>();
    }

    private sealed class SharedWorkloadServicesMarker;

    private sealed class WorkloadContractMarker;

    private sealed class WorkloadIssuerMarker;

    private sealed class SidecarChannelSchemeMarker;
}

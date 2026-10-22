using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Provisions domain services that submit trusted effects to EventStore (Story 5.5, FR28).
/// </summary>
/// <remarks>
/// <para>
/// A trusted-effect submission is an internal call: the submitting domain service proves its own workload identity
/// with a short-lived assertion granting <c>eventstore:trusted-effect</c> for the <c>eventstore</c> audience, and
/// EventStore admits only workloads listed in <c>Authentication:DaprInternal:AllowedCallers</c>. A Dapr application id
/// never authenticates on its own.
/// </para>
/// <para>
/// In symmetric (Development) mode the submitting domain service needs only the shared signing key it already uses to
/// validate EventStore's assertions; its workload identity is its Dapr application id. In authority mode it needs its own
/// confidential service-account client whose <c>azp</c> equals its application id and which may request the
/// <c>eventstore-audience.eventstore</c> and <c>eventstore-operation.eventstore.trusted-effect</c> scopes; supply it with
/// <see cref="WithEventStoreWorkloadClientCredentials"/>.
/// </para>
/// </remarks>
public static class HexalithEventStoreTrustedEffectExtensions
{
    /// <summary>Gets the environment-variable prefix of EventStore's internal caller allow-list.</summary>
    public const string AllowedCallersVariablePrefix = "Authentication__DaprInternal__AllowedCallers__";

    /// <summary>Gets the environment variable holding a workload's client identifier.</summary>
    public const string WorkloadClientIdVariable = "Authentication__WorkloadIssuer__ClientId";

    /// <summary>Gets the environment variable holding a workload's client secret.</summary>
    public const string WorkloadClientSecretVariable = "Authentication__WorkloadIssuer__ClientSecret";

    /// <summary>
    /// Allow-lists a domain service as a trusted-effect submitter on the EventStore resource.
    /// </summary>
    /// <param name="eventStore">The EventStore project resource.</param>
    /// <param name="workload">The submitting domain service's workload identity (its Dapr application id).</param>
    /// <returns>The EventStore resource builder.</returns>
    public static IResourceBuilder<ProjectResource> WithEventStoreTrustedEffectSubmitter(
        this IResourceBuilder<ProjectResource> eventStore,
        string workload)
    {
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(workload);
        EventStoreTrustedEffectSubmittersAnnotation? annotation = eventStore.Resource.Annotations
            .OfType<EventStoreTrustedEffectSubmittersAnnotation>()
            .SingleOrDefault();
        if (annotation is null)
        {
            annotation = new EventStoreTrustedEffectSubmittersAnnotation();
            eventStore.Resource.Annotations.Add(annotation);
            EventStoreTrustedEffectSubmittersAnnotation captured = annotation;
            _ = eventStore.WithEnvironment(context =>
            {
                for (int index = 0; index < captured.Workloads.Count; index++)
                {
                    context.EnvironmentVariables[AllowedCallersVariablePrefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture)]
                        = captured.Workloads[index];
                }
            });
        }

        string trimmed = workload.Trim();
        if (!annotation.Workloads.Contains(trimmed, StringComparer.Ordinal))
        {
            annotation.Workloads.Add(trimmed);
        }

        return eventStore;
    }

    /// <summary>
    /// Gives a workload the client registration it uses to obtain its own assertions from the OIDC authority.
    /// </summary>
    /// <param name="workload">The workload project resource (EventStore or a submitting domain service).</param>
    /// <param name="clientId">The client identifier parameter.</param>
    /// <param name="clientSecret">The client secret parameter; it must be a secret parameter.</param>
    /// <returns>The workload resource builder.</returns>
    /// <exception cref="ArgumentException">The client secret parameter is not marked secret.</exception>
    public static IResourceBuilder<ProjectResource> WithEventStoreWorkloadClientCredentials(
        this IResourceBuilder<ProjectResource> workload,
        IResourceBuilder<ParameterResource> clientId,
        IResourceBuilder<ParameterResource> clientSecret)
    {
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(clientSecret);
        if (!clientSecret.Resource.Secret)
        {
            throw new ArgumentException("The workload client secret must be a secret parameter.", nameof(clientSecret));
        }

        return workload
            .WithEnvironment(WorkloadClientIdVariable, clientId)
            .WithEnvironment(WorkloadClientSecretVariable, clientSecret);
    }

    /// <summary>
    /// Gives a workload a fixed client identifier and a secret client-secret parameter.
    /// </summary>
    /// <param name="workload">The workload project resource.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="clientSecret">The client secret parameter; it must be a secret parameter.</param>
    /// <returns>The workload resource builder.</returns>
    /// <exception cref="ArgumentException">The client secret parameter is not marked secret.</exception>
    public static IResourceBuilder<ProjectResource> WithEventStoreWorkloadClientCredentials(
        this IResourceBuilder<ProjectResource> workload,
        string clientId,
        IResourceBuilder<ParameterResource> clientSecret)
    {
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(clientSecret);
        if (!clientSecret.Resource.Secret)
        {
            throw new ArgumentException("The workload client secret must be a secret parameter.", nameof(clientSecret));
        }

        return workload
            .WithEnvironment(WorkloadClientIdVariable, clientId.Trim())
            .WithEnvironment(WorkloadClientSecretVariable, clientSecret);
    }
}

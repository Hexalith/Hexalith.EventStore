namespace Hexalith.EventStore.Server.DomainServices;

using System.Text.Json;
using System.Text.RegularExpressions;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// DAPR-based domain service invoker. Uses DaprClient.InvokeMethodAsync for service invocation (D7).
/// Created per-actor-call (same pattern as IdempotencyChecker, TenantValidator, EventStreamReader).
/// No custom retry logic — DAPR resiliency handles transient failures (enforcement rule #4).
/// </summary>
public partial class DaprDomainServiceInvoker(
    DaprClient daprClient,
    IDomainServiceResolver resolver,
    IOptions<DomainServiceOptions> options,
    ILogger<DaprDomainServiceInvoker> logger) : IDomainServiceInvoker
{
    /// <summary>
    /// The command extension key used to specify the domain service version.
    /// </summary>
    public const string DomainServiceVersionExtensionKey = "domain-service-version";

    private const string DefaultVersion = "v1";

    [GeneratedRegex(@"^v[0-9]+$")]
    private static partial Regex VersionFormatRegex();

    /// <inheritdoc/>
    public async Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Extract version from command extensions, default to "v1" (AC #7, ADR-4)
        string version = ExtractVersion(command, logger);

        // Resolve domain service registration with extracted version
        DomainServiceRegistration? registration = await resolver
            .ResolveAsync(command.TenantId, command.Domain, version, cancellationToken)
            .ConfigureAwait(false);

        if (registration is null)
        {
            throw new DomainServiceNotFoundException(command.TenantId, command.Domain, version);
        }

        logger.LogDebug(
            "Invoking domain service: AppId={AppId}, Method={MethodName}, Tenant={TenantId}, Domain={Domain}, CorrelationId={CorrelationId}",
            registration.AppId,
            registration.MethodName,
            command.TenantId,
            command.Domain,
            command.CorrelationId);

        // Invoke via DAPR service invocation (D7)
        // DAPR resiliency policies handle retries, circuit breaker, timeout (rule #4)
        var request = new DomainServiceRequest(command, currentState);

        DomainResult result = await daprClient
            .InvokeMethodAsync<DomainServiceRequest, DomainResult>(
                registration.AppId,
                registration.MethodName,
                request,
                cancellationToken)
            .ConfigureAwait(false);

        // Validate response size limits (AC #6)
        ValidateResponseLimits(result, command.TenantId, command.Domain, options.Value);

        // Log no-op results as warning for telemetry (silent failure detection)
        if (result.IsNoOp)
        {
            logger.LogWarning(
                "Domain service returned no-op (empty events): AppId={AppId}, Tenant={TenantId}, Domain={Domain}, CorrelationId={CorrelationId}",
                registration.AppId,
                command.TenantId,
                command.Domain,
                command.CorrelationId);
        }

        logger.LogInformation(
            "Domain service completed: AppId={AppId}, ResultType={ResultType}, EventCount={EventCount}, Tenant={TenantId}, CorrelationId={CorrelationId}",
            registration.AppId,
            result.IsSuccess ? "Success" : result.IsRejection ? "Rejection" : "NoOp",
            result.Events.Count,
            command.TenantId,
            command.CorrelationId);

        return result;
    }

    /// <summary>
    /// Extracts and validates the domain service version from the command envelope extensions.
    /// Returns "v1" if not specified in extensions.
    /// </summary>
    /// <param name="command">The command envelope.</param>
    /// <param name="log">The logger for audit trail.</param>
    /// <returns>The validated, lowercase-normalized version string.</returns>
    /// <exception cref="ArgumentException">Thrown when the version format is invalid.</exception>
    public static string ExtractVersion(CommandEnvelope command, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Extensions is null ||
            !command.Extensions.TryGetValue(DomainServiceVersionExtensionKey, out string? versionValue) ||
            string.IsNullOrWhiteSpace(versionValue))
        {
            log.LogInformation(
                "Domain service version not specified in command extensions, defaulting to {Version}: Tenant={TenantId}, Domain={Domain}, CorrelationId={CorrelationId}",
                DefaultVersion,
                command.TenantId,
                command.Domain,
                command.CorrelationId);
            return DefaultVersion;
        }

        string normalized = versionValue.ToLowerInvariant();
        ValidateVersionFormat(normalized);
        return normalized;
    }

    /// <summary>
    /// Validates that a version string matches the required format: v followed by one or more digits (e.g., "v1", "v2", "v10").
    /// </summary>
    /// <param name="version">The version string to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the version format is invalid.</exception>
    public static void ValidateVersionFormat(string version)
    {
        if (!VersionFormatRegex().IsMatch(version))
        {
            throw new ArgumentException(
                $"Invalid domain service version format '{version}'. Version must match pattern 'v{{number}}' (e.g., v1, v2, v10).",
                nameof(version));
        }
    }

    /// <summary>
    /// Validates that a domain service response does not exceed configured limits (AC #6).
    /// </summary>
    /// <param name="result">The domain result to validate.</param>
    /// <param name="tenantId">The tenant identifier for error reporting.</param>
    /// <param name="domain">The domain name for error reporting.</param>
    /// <param name="opts">The domain service options with configured limits.</param>
    /// <exception cref="DomainServiceException">Thrown when the response exceeds configured limits.</exception>
    public static void ValidateResponseLimits(DomainResult result, string tenantId, string domain, DomainServiceOptions opts)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(opts);

        // Check event count limit
        if (result.Events.Count > opts.MaxEventsPerResult)
        {
            throw new DomainServiceException(
                tenantId,
                domain,
                $"Response contains {result.Events.Count} events, exceeding maximum of {opts.MaxEventsPerResult}",
                eventCount: result.Events.Count);
        }

        // Check individual event payload size (reuse stream to avoid per-event byte[] allocations)
        using var sizeStream = new MemoryStream();
        foreach (IEventPayload eventPayload in result.Events)
        {
            sizeStream.SetLength(0);
            JsonSerializer.Serialize(sizeStream, eventPayload, eventPayload.GetType());
            if (sizeStream.Length > opts.MaxEventSizeBytes)
            {
                throw new DomainServiceException(
                    tenantId,
                    domain,
                    $"Event payload of type '{eventPayload.GetType().Name}' is {sizeStream.Length} bytes, exceeding maximum of {opts.MaxEventSizeBytes} bytes",
                    eventSizeBytes: (int)sizeStream.Length);
            }
        }
    }
}

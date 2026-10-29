
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;
/// <summary>
/// Routes commands to the correct aggregate actor based on canonical identity derivation.
/// SECURITY: Always derive actor ID from AggregateIdentity.ActorId. Never construct actor IDs
/// via manual string concatenation. The chain of custody from CommandEnvelope through
/// AggregateIdentity to actor ID must be unbroken. See FR15, FR28.
/// </summary>
public partial class CommandRouter : ICommandRouter {
    private IActorProxyFactory actorProxyFactory { get; }
    private IOptions<EventStoreActorOptions> actorOptions { get; }
    private ILogger<CommandRouter> logger { get; }
    private IEnumerable<ICoordinatedCommandPolicy>? coordinationPolicies { get; }
    private Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority { get; }

    /// <summary>Creates a catalog-enforced runtime entry point.</summary>
    public CommandRouter(
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority authority,
        IActorProxyFactory actorProxyFactory,
        IOptions<EventStoreActorOptions> actorOptions,
        ILogger<CommandRouter> logger,
        IEnumerable<ICoordinatedCommandPolicy>? coordinationPolicies = null)
        : this(actorProxyFactory, actorOptions, logger, coordinationPolicies, authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
    }

    /// <summary>Explicit friend-only composition seam for existing isolated protocol tests.</summary>
    internal CommandRouter(
        IActorProxyFactory actorProxyFactory,
        IOptions<EventStoreActorOptions> actorOptions,
        ILogger<CommandRouter> logger,
        IEnumerable<ICoordinatedCommandPolicy>? coordinationPolicies = null,
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority = null)
    {
        this.actorProxyFactory = actorProxyFactory;
        this.actorOptions = actorOptions;
        this.logger = logger;
        this.coordinationPolicies = coordinationPolicies;
        this.catalogAuthority = catalogAuthority;

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandRouter"/> class with the default actor type name.
    /// </summary>
    /// <param name="actorProxyFactory">The DAPR actor proxy factory.</param>
    /// <param name="logger">The logger.</param>
    internal CommandRouter(IActorProxyFactory actorProxyFactory, ILogger<CommandRouter> logger)
        : this(actorProxyFactory, Options.Create(new EventStoreActorOptions()), logger) {
    }

    /// <inheritdoc/>
    public async Task<CommandProcessingResult> RouteCommandAsync(
        SubmitCommand command,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease).ConfigureAwait(false);
        if (catalogLease is not null) _ = catalogLease.Snapshot.Resolve("command", command.Tenant, command.Domain, command.CommandType);

        var identity = new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId);
        string actorId = identity.ActorId;
        string causationId = command.MessageId;

        Log.CommandRouting(logger, command.CorrelationId, causationId, command.Tenant, command.Domain, command.AggregateId, command.CommandType, actorId);

        var envelope = command.ToCommandEnvelope();
        string? coordinationActorId = GetCoordinationActorId(envelope);

        try {
            if (coordinationActorId is not null) {
                ICoordinatedCommandActor coordinator = actorProxyFactory.CreateActorProxy<ICoordinatedCommandActor>(
                    new ActorId(coordinationActorId), CoordinatedCommandActor.ActorTypeName);
                return command.HumanAuthorityReceipt is null
                    ? await RouteCatalogRemoteCall.InvokeAsync(() => coordinator.ProcessCommandAsync(envelope)).ConfigureAwait(false)
                    : await RouteCatalogRemoteCall.InvokeAsync(() => coordinator.ProcessAuthorizedCommandAsync(new AuthorizedCommandEnvelope(envelope, command.HumanAuthorityReceipt))).ConfigureAwait(false);
            }

            IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(actorId),
                actorOptions.Value.AggregateActorTypeName);

            return command.HumanAuthorityReceipt is null
                ? await RouteCatalogRemoteCall.InvokeAsync(() => proxy.ProcessCommandAsync(envelope)).ConfigureAwait(false)
                : await RouteCatalogRemoteCall.InvokeAsync(() => proxy.ProcessAuthorizedCommandAsync(new AuthorizedCommandEnvelope(envelope, command.HumanAuthorityReceipt))).ConfigureAwait(false);
        }
        catch (Exception ex) {
            Log.ActorInvocationFailed(logger, ex, command.CorrelationId, causationId, command.Tenant, command.Domain, command.AggregateId, command.CommandType, actorId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<CommandProcessingResult> RouteFencedCommandAsync(
        SubmitCommand command,
        IdempotencyExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(executionContext);
        cancellationToken.ThrowIfCancellationRequested();
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease).ConfigureAwait(false);
        if (catalogLease is not null) _ = catalogLease.Snapshot.Resolve("command", command.Tenant, command.Domain, command.CommandType);
        if (executionContext.FencingToken <= 0
            || !string.Equals(executionContext.MessageId, command.MessageId, StringComparison.Ordinal)
            || !string.Equals(executionContext.CorrelationId, command.CorrelationId, StringComparison.Ordinal)
            || !string.Equals(executionContext.Tenant, command.Tenant, StringComparison.Ordinal)
            || !string.Equals(executionContext.Domain, command.Domain, StringComparison.Ordinal)
            || !string.Equals(executionContext.AggregateId, command.AggregateId, StringComparison.Ordinal)
            || !string.Equals(executionContext.CommandType, command.CommandType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The idempotency execution fence is missing, stale, or invalid.");
        }

        var identity = new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId);
        string actorId = identity.ActorId;
        Log.CommandRouting(
            logger,
            command.CorrelationId,
            command.MessageId,
            command.Tenant,
            command.Domain,
            command.AggregateId,
            command.CommandType,
            actorId);
        var request = new FencedCommandEnvelope(command.ToCommandEnvelope(), executionContext, command.HumanAuthorityReceipt);
        string? coordinationActorId = GetCoordinationActorId(request.Command);
        try
        {
            if (coordinationActorId is not null)
            {
                ICoordinatedCommandActor coordinator = actorProxyFactory.CreateActorProxy<ICoordinatedCommandActor>(
                    new ActorId(coordinationActorId), CoordinatedCommandActor.ActorTypeName);
                return await RouteCatalogRemoteCall.InvokeAsync(() => coordinator.ProcessFencedCommandAsync(request)).ConfigureAwait(false);
            }

            IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(actorId),
                actorOptions.Value.AggregateActorTypeName);
            return await RouteCatalogRemoteCall.InvokeAsync(() => proxy.ProcessFencedCommandAsync(request)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.ActorInvocationFailed(
                logger,
                ex,
                command.CorrelationId,
                command.MessageId,
                command.Tenant,
                command.Domain,
                command.AggregateId,
                command.CommandType,
                actorId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IdempotencyCheckResult> ReconcileFencedCommandAsync(
        SubmitCommand command,
        IdempotencyExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(executionContext);
        cancellationToken.ThrowIfCancellationRequested();
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease).ConfigureAwait(false);
        if (catalogLease is not null) _ = catalogLease.Snapshot.Resolve("command", command.Tenant, command.Domain, command.CommandType);
        if (executionContext.FencingToken <= 0
            || !string.Equals(executionContext.MessageId, command.MessageId, StringComparison.Ordinal)
            || !string.Equals(executionContext.CorrelationId, command.CorrelationId, StringComparison.Ordinal)
            || !string.Equals(executionContext.Tenant, command.Tenant, StringComparison.Ordinal)
            || !string.Equals(executionContext.Domain, command.Domain, StringComparison.Ordinal)
            || !string.Equals(executionContext.AggregateId, command.AggregateId, StringComparison.Ordinal)
            || !string.Equals(executionContext.CommandType, command.CommandType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The idempotency execution fence is missing, stale, or invalid.");
        }

        var identity = new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId);
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            actorOptions.Value.AggregateActorTypeName);
        var request = new FencedCommandEnvelope(command.ToCommandEnvelope(), executionContext);
        return await RouteCatalogRemoteCall.InvokeAsync(() => proxy.ReconcileFencedCommandAsync(request)).ConfigureAwait(false);
    }

    private string? GetCoordinationActorId(CommandEnvelope command)
    {
        ICoordinatedCommandPolicy[] claiming = [.. (coordinationPolicies ?? [])
            .Where(policy => policy.Claims(command.Domain, command.CommandType)).Take(2)];
        if (claiming.Length == 0)
        {
            return null;
        }

        if (claiming.Length != 1)
        {
            throw new InvalidOperationException("Multiple policies claim one coordinated command.");
        }

        return claiming[0].GetScope(command).Source.ActorId;
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1100,
            Level = LogLevel.Debug,
            Message = "Routing command to actor: CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, ActorId={ActorId}, Stage=CommandRouting")]
        public static partial void CommandRouting(
            ILogger logger,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType,
            string actorId);

        [LoggerMessage(
            EventId = 1101,
            Level = LogLevel.Error,
            Message = "Actor invocation failed: CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, ActorId={ActorId}, Stage=ActorInvocationFailed")]
        public static partial void ActorInvocationFailed(
            ILogger logger,
            Exception ex,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType,
            string actorId);
    }
}

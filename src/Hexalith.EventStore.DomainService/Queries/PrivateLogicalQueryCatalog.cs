using System.Collections.Frozen;
using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService.Queries;
/// <summary>Composes supplied dormant logical query descriptors and isolated platform-owned DI intake.</summary>
internal sealed class PrivateLogicalQueryCatalog : IDisposable
{
    private readonly FrozenDictionary<string, LogicalQueryDescriptor> _descriptors;
    private ServiceProvider? _privateProvider;
    private readonly Func<QueryEnvelope, IReadOnlyList<LogicalQueryReadPlanEntry>, EventBufferBudget, CancellationToken, Task<PrivateLogicalQueryInput>> _acquire;
    /// <summary>Freezes one explicit local catalog and actual actor-owned input acquisition.</summary>
    internal PrivateLogicalQueryCatalog(LogicalQueryRouteTable routes, IReadOnlyList<LogicalQueryDescriptor> descriptors, Func<QueryEnvelope, IReadOnlyList<LogicalQueryReadPlanEntry>, EventBufferBudget, CancellationToken, Task<PrivateLogicalQueryInput>> acquire)
    {
        Routes = routes;
        _acquire = acquire;
        _descriptors = descriptors.ToFrozenDictionary(static x => x.Domain + "\0" + x.QueryType, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets the exact supplied classification table used before DI/cache access.</summary>
    internal LogicalQueryRouteTable Routes { get; }

    /// <summary>Registers the private scoped holder/store after refusing captive or bypass-capable handler registration.</summary>
    internal void Install(IServiceCollection services)
    {
        if (_privateProvider is not null || services.Any(static x => x.ServiceType == typeof(PrivateLogicalQueryHolder)))
        {
            throw new InvalidOperationException("ReadModelRouteContextRequired: captured/alternate stores are not eligible.");
        }

        foreach (LogicalQueryDescriptor descriptor in _descriptors.Values)
        {
            ServiceDescriptor[] registration = services.Where(x => x.ServiceType == descriptor.HandlerType).ToArray();
            if (registration.Length != 1 || registration[0].Lifetime != ServiceLifetime.Scoped || registration[0].ImplementationType != descriptor.HandlerType || descriptor.HandlerType.GetConstructors().Length != 1 || descriptor.HandlerType.GetConstructors()[0].GetParameters().Any(static p => p.ParameterType != typeof(IReadModelStore) && p.ParameterType != typeof(IReadModelBulkStore)))
            {
                throw new InvalidOperationException("ReadModelRouteContextRequired: handler dependencies/registration are not eligible.");
            }
        }

        IServiceCollection privateServices = new ServiceCollection();
        foreach (ServiceDescriptor service in services)
        {
            if (_descriptors.Values.Any(descriptor => service.ServiceType == descriptor.HandlerType))
            {
                privateServices.Add(service);
            }
        }

        privateServices.AddScoped<PrivateLogicalQueryHolder>();
        privateServices.AddScoped<PrivateLogicalQueryStore>();
        privateServices.AddScoped<IReadModelStore>(static provider => provider.GetRequiredService<PrivateLogicalQueryStore>());
        privateServices.AddScoped<IReadModelBulkStore>(static provider => provider.GetRequiredService<PrivateLogicalQueryStore>());
        _privateProvider = privateServices.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        services.AddSingleton(this);
    }

    /// <summary>Executes one admitted query with private callbacks, fixed-root reads and final response ownership.</summary>
    internal async Task<QueryResult> ExecuteAsync(IServiceProvider provider, QueryEnvelope request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (request.GetType() != typeof(QueryEnvelope))
        {
            throw new InvalidOperationException("ReadModelRouteContextRequired: logical queries require the exact admitted envelope type.");
        }

        if (!_descriptors.TryGetValue(request.Domain + "\0" + request.QueryType, out LogicalQueryDescriptor? descriptor))
        {
            throw new InvalidOperationException("ReadModelRouteContextRequired: missing logical query descriptor.");
        }

        RequireMetadata(request);
        if (request.Payload.Length > 16 * 1024 * 1024)
        {
            throw new InvalidOperationException("ReadModelQueryLimit: request exceeds its local admitted ceiling.");
        }

        using var budget = new EventBufferBudget();
        int metadataBytes;
        using (EventBufferReservation metadata = budget.Reserve(8 * 1024 * 1024))
        {
            byte[] encoded = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request with { Payload = [] });
            try
            {
                metadataBytes = encoded.Length;
                if (metadataBytes > 512 * 1024)
                {
                    throw new InvalidOperationException("ReadModelQueryLimit: encoded request metadata exceeds 512 KiB.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(encoded);
            }
        }

        using EventBufferReservation working = budget.Reserve(checked(3 * request.Payload.Length + descriptor.WorkingBytes + metadataBytes * 16 + 1024 * 1024));
        using EventBufferReservation output = budget.Reserve(checked(3 * descriptor.MaximumResponseBytes));
        QueryEnvelope query = request with
        {
            Domain = descriptor.Domain,
            QueryType = descriptor.QueryType,
            Payload = request.Payload.ToArray(),
            Scopes = request.Scopes,
            Audience = request.Audience
        };
        byte[]? hash = null;
        PrivateLogicalQueryInput? input = null;
        PrivateLogicalQuerySession? session = null;
        byte[]? candidate = null;
        byte[]? producedBytes = null;
        byte[]? producedHash = null;
        try
        {
            hash = PrivateLogicalQuerySession.HashRequest(query, budget, token);
            using (var payload = new ImmutablePayload(query.Payload.ToArray(), query.Payload.Length, token))
            {
                Task PreparationFence(CancellationToken cancellation)
                {
                    RequireRequestPin(query, hash, budget, cancellation);
                    descriptor.RequireCurrent(cancellation);
                    return Task.CompletedTask;
                }

                LogicalQueryReadPlanEntry[] plan = await descriptor.PrepareAsync(query, payload, budget, token, PreparationFence).ConfigureAwait(false);
                RequireRequestPin(query, hash, budget, token);
                try
                {
                    input = await _acquire(query, Array.AsReadOnly(plan), budget, token).ConfigureAwait(false);
                }
                finally
                {
                    token.ThrowIfCancellationRequested();
                }

                RequireRequestPin(query, hash, budget, token);
                descriptor.RequireCurrent(token);
                descriptor.RequireRoot(input, query, plan, budget, token);
                session = new PrivateLogicalQuerySession(descriptor, query, hash, plan, input, budget, token);
            }

            input = null;
            hash = null;
            using IServiceScope scope = (_privateProvider ?? throw new InvalidOperationException("ReadModelRouteContextRequired: private catalog is not installed.")).CreateScope();
            scope.ServiceProvider.GetRequiredService<PrivateLogicalQueryHolder>().Install(session);
            await session.CompleteAsync(token).ConfigureAwait(false);
            IDomainQueryHandler handler = await descriptor.CreateAsync(scope.ServiceProvider, session.RequireCurrentAsync, budget, token).ConfigureAwait(false);
            await session.RequireCurrentAsync(token).ConfigureAwait(false);
            QueryResult result;
            try
            {
                result = await handler.ExecuteAsync(session.Query, token).ConfigureAwait(false);
                producedBytes = result.PayloadBytes;
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }

            if (result.Metadata is not null || result.ErrorMessage is not null || result.ProjectionType is not null || !result.Success || producedBytes is null || producedBytes.Length > descriptor.MaximumResponseBytes)
            {
                throw new InvalidOperationException("ReadModelQueryLimit: logical result is missing, failed or exceeds its admitted schema capacity.");
            }

            producedHash = SHA256.HashData(producedBytes);
            candidate = producedBytes.ToArray();
            await session.RequireCurrentAsync(token).ConfigureAwait(false);
            RequireResultPin(producedBytes, producedHash, token);
            using (var payload = new ImmutablePayload(candidate.ToArray(), candidate.Length, token))
            {
                await descriptor.ValidateResponseAsync(payload, session.RequireCurrentAsync, budget, token).ConfigureAwait(false);
            }

            await session.CompleteAsync(token).ConfigureAwait(false);
            RequireResultPin(producedBytes, producedHash, token);
            QueryResult sealedResult = new(true, candidate);
            candidate = null;
            return sealedResult;
        }
        finally
        {
            session?.Dispose();
            input?.Dispose();
            if (hash is not null)
            {
                CryptographicOperations.ZeroMemory(hash);
            }

            PrivateLogicalQuerySession.ClearOwnedRequest(query);
            if (candidate is not null)
            {
                CryptographicOperations.ZeroMemory(candidate);
            }

            if (producedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(producedBytes);
            }

            if (producedHash is not null)
            {
                CryptographicOperations.ZeroMemory(producedHash);
            }

            token.ThrowIfCancellationRequested();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => Interlocked.Exchange(ref _privateProvider, null)?.Dispose();
    private static void RequireResultPin(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> expected, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        byte[] actual = SHA256.HashData(bytes);
        try
        {
            if (!actual.AsSpan().SequenceEqual(expected))
            {
                throw new InvalidOperationException("QueryCapabilityChanged: retained handler result changed.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
        }
    }

    private static void RequireMetadata(QueryEnvelope query)
    {
        if ((query.Scopes?.Count ?? 0) > 256 || (query.Audience?.Count ?? 0) > 256)
        {
            throw new InvalidOperationException("ReadModelQueryLimit: request scope/audience count exceeds 256.");
        }

        int bytes = 0;
        foreach (string? value in new[]
        {
            query.TenantId,
            query.Domain,
            query.QueryType,
            query.AggregateId,
            query.CorrelationId,
            query.UserId,
            query.EntityId,
            query.OriginalActorId,
            query.AuthenticatedWorkloadId,
            query.DelegationId,
            query.IdentityAdmissionProof,
            query.Paging?.Cursor
        }.Concat(query.Scopes ?? []).Concat(query.Audience ?? []))
        {
            if (value is not null)
            {
                bytes = checked(bytes + System.Text.Encoding.UTF8.GetByteCount(value));
            }

            if (bytes > 64 * 1024)
            {
                throw new InvalidOperationException("ReadModelQueryLimit: request metadata exceeds 64 KiB.");
            }
        }
    }

    private static void RequireRequestPin(QueryEnvelope query, ReadOnlySpan<byte> expected, EventBufferBudget budget, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        byte[] current = PrivateLogicalQuerySession.HashRequest(query, budget, token);
        try
        {
            if (!current.AsSpan().SequenceEqual(expected))
            {
                throw new InvalidOperationException("QueryCapabilityChanged: resolver changed the private request.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(current);
        }
    }
}

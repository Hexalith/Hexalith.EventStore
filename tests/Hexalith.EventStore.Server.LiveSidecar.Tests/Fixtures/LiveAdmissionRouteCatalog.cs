using System.Buffers.Binary;
using System.Text.Json;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>
/// Immutable authority for the private Testing admission workers. Real admission, signed capabilities,
/// dispatch leases, receiver validation, authentication and persistence remain production implementations.
/// Production trust and distributed catalog activation are qualified by the separate hosted catalog tests.
/// </summary>
internal sealed class LiveAdmissionRouteCatalog : IRouteCatalogAuthority
{
    private readonly LiveAdmissionCatalogBinding _binding;
    private readonly IOptions<RouteCatalogHostOptions> _options;
    private readonly IOptions<IdempotencyAdmissionOptions> _admission;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpContextAccessor _http;
    private readonly Dictionary<string, CommittedRouteCatalogSnapshot> _snapshots;
    private readonly string _revision;

    public LiveAdmissionRouteCatalog(LiveAdmissionCatalogBinding binding, IOptions<RouteCatalogHostOptions> options,
        IOptions<IdempotencyAdmissionOptions> admission, IWebHostEnvironment environment, IHttpContextAccessor http)
    {
        binding.Validate();
        // Copy every launch collection: neither an environment update nor a caller can mutate this authority.
        _binding = binding with { Tenants = [.. binding.Tenants], Readers = [.. binding.Readers] };
        _revision = _binding.Revision;
        _options = options;
        _admission = admission;
        _environment = environment;
        _http = http;
        _snapshots = _binding.Tenants.ToDictionary(tenant => tenant, CreateSnapshot, StringComparer.Ordinal);
    }

    public async Task<RouteCatalogDispatchLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateBinding();
        CommittedRouteCatalogSnapshot snapshot = await SelectSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return new RouteCatalogDispatchLease(snapshot, static () => Task.CompletedTask);
    }

    public async Task<RouteCatalogGenerationReadback> ReadbackAsync(CancellationToken cancellationToken = default)
    {
        await using RouteCatalogDispatchLease lease = await AcquireAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = lease.Snapshot;
        var envelope = snapshot.Envelope;
        return new(snapshot.Epoch, envelope.Generation, snapshot.RootDigest, envelope.RouteFacetDigest,
            envelope.IdempotencyFacetDigest, envelope.Profile, [], true);
    }

    public Task PrepareAsync(RouteCatalogActivation candidate, CancellationToken cancellationToken = default) => Task.FromException(Refusal());
    public Task<RouteCatalogHostAttestation> ReadyAsync(string attemptId, CancellationToken cancellationToken = default) => Task.FromException<RouteCatalogHostAttestation>(Refusal());
    public Task CommitAsync(string attemptId, CancellationToken cancellationToken = default) => Task.FromException(Refusal());
    public Task RollbackAsync(RouteCatalogRollbackRequest request, CancellationToken cancellationToken = default) => Task.FromException(Refusal());
    public Task ReconcileAsync(RouteCatalogQuiescenceRequest request, CancellationToken cancellationToken = default) => Task.FromException(Refusal());

    private void ValidateBinding()
    {
        if (_environment.EnvironmentName != "Testing" || _options.Value.Profile != _binding.Profile
            || _options.Value.DevelopmentFixture || _options.Value.Identity != _binding.Identity
            || _binding.Revision != _revision)
            throw Refusal();
        if (_binding.AppId == _binding.ConsumerAppId)
        {
            var policy = _admission.Value;
            if (!policy.Enabled || policy.ActiveDigestKeyVersion != _binding.ActiveGeneration
                || !policy.ReaderDigestKeyVersions.SequenceEqual(_binding.Readers, StringComparer.Ordinal))
                throw Refusal();
        }
    }

    private async Task<CommittedRouteCatalogSnapshot> SelectSnapshotAsync(CancellationToken cancellationToken)
    {
        HttpRequest? request = _http.HttpContext?.Request;
        string? root = request?.Headers[RouteCatalogInvocationHandler.RootHeader].SingleOrDefault();
        if (root is not null)
            return _snapshots.Values.SingleOrDefault(snapshot => snapshot.RootDigest == root) ?? throw Refusal();
        string? tenant = LiveAdmissionCatalogScope.Tenant;
        if (tenant is null && request is not null)
        {
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            // Dapr actor arguments are not necessarily JSON. Bind their typed actor partition first.
            string[] path = Uri.UnescapeDataString(request.Path.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (path is ["actors", _, string actorId, "method", _])
                foreach (string known in _binding.Tenants)
                    if (actorId.StartsWith(known + ":", StringComparison.Ordinal)) candidates.Add(known);
            if (candidates.Count == 0 && request.Body.CanSeek && request.Body.Length > 0)
            {
                long position = request.Body.Position;
                request.Body.Position = 0;
                try
                {
                    using JsonDocument body = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken).ConfigureAwait(false);
                    CollectTenants(body.RootElement, candidates);
                }
                catch (JsonException) { throw Refusal(); }
                finally { request.Body.Position = position; }
            }
            if (candidates.Count == 1) tenant = candidates.Single();
        }
        return tenant is not null && _snapshots.TryGetValue(tenant, out var selected) ? selected : throw Refusal();
    }

    private static void CollectTenants(JsonElement value, HashSet<string> tenants)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if ((property.Name.Equals("tenant", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("tenantId", StringComparison.OrdinalIgnoreCase)) && property.Value.ValueKind == JsonValueKind.String)
                    tenants.Add(property.Value.GetString()!);
                else CollectTenants(property.Value, tenants);
            }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray()) CollectTenants(item, tenants);
    }

    private CommittedRouteCatalogSnapshot CreateSnapshot(string tenant)
    {
        var command = LiveIncrementCounterIdempotencyIntentAdapter.Policy;
        RouteContractDeclaration[] declarations = [command.Route, SampleCatalog.Projection];
        RouteOperationContract[] operations = [
            RouteOperationSchema.Create(command.Route, typeof(SubmitCommand), typeof(CommandProcessingResult), "public", "UI-only", JsonSerializerOptions.Web),
            RouteOperationSchema.Create(SampleCatalog.Projection, typeof(ProjectionRequest), typeof(ProjectionResponse), "internal", "UI-only", JsonSerializerOptions.Web)];
        long generation = BinaryPrimitives.ReadInt64BigEndian(Convert.FromHexString(_revision).AsSpan(0, 8)) & 9007199254740991;
        if (generation == 0) throw Refusal();
        RouteCatalogEntry[] routes = [.. declarations.Select(declaration => new RouteCatalogEntry(declaration.RouteId,
            generation, RouteCatalogCodec.ComputeDeclarationDigest(declaration), _binding.SampleAppId,
            declaration.Kind == "command" ? "process" : "project", "v1", "workload", declaration.Operation,
            false, tenant, operations.Single(operation => operation.RouteId == declaration.RouteId).Digest))];
        var readers = new[] { _binding.ActiveGeneration }.Concat(_binding.Readers).ToArray();
        IdempotencyCatalogEntry admission = DevelopmentIdempotencyBindings.Bind(command, generation) with
        {
            ActiveDigestKeyGeneration = _binding.ActiveGeneration, ReaderDigestKeyGenerations = readers,
            SecretMapGeneration = _binding.ActiveGeneration, KeyMap = [.. readers.Select(reader => new DigestKeyMapReference(reader, reader))],
            ConsumerAppId = _binding.ConsumerAppId, ContentDigest = string.Empty,
        };
        admission = admission with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(admission) };
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Seal(new(3, generation, _binding.Profile, false,
            declarations, routes, [admission], string.Empty, string.Empty, operations, []));
        return new(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), generation,
            [new(_binding.SampleAppId, "replay-state", EventStoreOperationIdentifiers.DomainServiceReplayState, "counter"),
                new(_binding.SampleAppId, "admin/operational-index-metadata", EventStoreOperationIdentifiers.DomainServiceMetadata, "counter")],
            localAppId: _binding.AppId);
    }

    private static RouteCatalogUnavailableException Refusal() => new("The request is outside the immutable live-admission fixture binding.");
}

using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>Admits ephemeral origin only from the exact authenticated EventStore transport workload.</summary>
internal static class DomainServiceTrustedEffectOrigin
{
    /// <summary>The single bounded rolling denial document; repeated commands never append per-command records.</summary>
    internal const string DenialKey = "eventstore:trusted-effect-origin:v1:denials";

    /// <summary>Validates source identity and the complete addressed command before exposing its origin.</summary>
    internal static async Task<TrustedEffectOrigin?> ValidateAsync(IServiceProvider services, DomainServiceRequest request, CancellationToken cancellationToken)
    {
        TrustedEffectOrigin? origin = request.TrustedEffectOrigin;
        if (origin is null) return null;
        HttpContext? http = services.GetService<IHttpContextAccessor>()?.HttpContext;
        string expected = services.GetService<IConfiguration>()?["EventStore:TrustedEffectOrigin:EventStoreWorkloadId"] ?? "eventstore";
        System.Security.Claims.ClaimsIdentity? transport = http?.User.Identities.SingleOrDefault(identity => identity.IsAuthenticated
            && identity.AuthenticationType == EventStoreWorkloadAuthenticationDefaults.WorkloadScheme);
        string? workload = transport?.FindFirst(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType)?.Value;
        CommandEnvelope command = request.Command;
        string digest = EffectIdentityCodec.RenderDigest(SHA256.HashData(command.Payload));
        if (transport is null || string.IsNullOrWhiteSpace(expected)
            || !string.Equals(workload, expected, StringComparison.Ordinal)
            || origin.Identity is null || string.IsNullOrWhiteSpace(origin.Purpose)
            || string.IsNullOrWhiteSpace(origin.Workload) || string.IsNullOrWhiteSpace(origin.CausationId)
            || !string.Equals(command.MessageId, EffectIdentityCodec.ComputeMessageId(origin.Identity), StringComparison.Ordinal)
            || !string.Equals(command.TenantId, origin.Identity.Tenant, StringComparison.Ordinal)
            || !string.Equals(command.Domain, origin.Identity.TargetDomain, StringComparison.Ordinal)
            || !string.Equals(command.AggregateId, origin.Identity.TargetAggregate, StringComparison.Ordinal)
            || !string.Equals(command.CommandType, origin.CommandType, StringComparison.Ordinal)
            || !string.Equals(command.CausationId, origin.CausationId, StringComparison.Ordinal)
            || !string.Equals(command.UserId, origin.Workload, StringComparison.Ordinal)
            || !string.Equals(origin.CommandDigest, digest, StringComparison.Ordinal))
        {
            await RecordDenialAsync(services, command, origin, digest, "origin-binding-denied", cancellationToken).ConfigureAwait(false);
            throw new UnauthorizedAccessException("Trusted effect origin is not bound to the authenticated command invocation.");
        }

        return origin;
    }
    /// <summary>Retains a safe disposition before acknowledging a domain-adapter authorization denial.</summary>
    internal static Task RecordAdapterDenialAsync(IServiceProvider services, DomainServiceRequest request, CancellationToken cancellationToken)
        => RecordDenialAsync(services, request.Command, request.TrustedEffectOrigin,
            EffectIdentityCodec.RenderDigest(SHA256.HashData(request.Command.Payload)), "adapter-authorization-denied", cancellationToken);

    private static async Task RecordDenialAsync(IServiceProvider services, CommandEnvelope command,
        TrustedEffectOrigin? origin, string payloadDigest, string reason, CancellationToken cancellationToken)
    {
        try
        {
            IReadModelStore store = services.GetRequiredService<IReadModelStore>();
            string storeName = services.GetService<IConfiguration>()?["EventStore:TrustedEffectOrigin:StateStoreName"] ?? "statestore";
            string commandBinding = Digest(new
            {
                command.TenantId, command.Domain, command.AggregateId, command.MessageId, command.CommandType,
                command.CausationId, command.UserId, PayloadDigest = payloadDigest,
            });
            string originBinding = Digest(new
            {
                origin?.Identity, origin?.Workload, origin?.Purpose, origin?.CausationId, origin?.CommandType, origin?.CommandDigest,
            });
            // Reuse SDK bounded control-worker admission. Noncooperative providers retain their
            // finite worker slot until actual completion; unavailable audit never admits domain work.
            await ProcessReadDeadline.RunControlAsync(async ct =>
            {
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    ReadModelEntry<TrustedEffectOriginDenialRecord> current = await store.GetAsync<TrustedEffectOriginDenialRecord>(
                        storeName, DenialKey, ct).ConfigureAwait(false);
                    long count = current.Value?.Count ?? 0;
                    var next = new TrustedEffectOriginDenialRecord(reason, commandBinding, originBinding,
                        count >= long.MaxValue ? long.MaxValue : Math.Max(count, 0) + 1, DateTimeOffset.UtcNow);
                    ct.ThrowIfCancellationRequested();
                    if (await store.TrySaveAsync(storeName, DenialKey, next, current.ETag ?? string.Empty, ct).ConfigureAwait(false)) return true;
                }
                throw new DomainServiceTrustedEffectAuditUnavailableException();
            }, TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { throw new DomainServiceTrustedEffectAuditUnavailableException(); }
    }

    private static string Digest<T>(T metadata)
        => EffectIdentityCodec.RenderDigest(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(metadata)));

}

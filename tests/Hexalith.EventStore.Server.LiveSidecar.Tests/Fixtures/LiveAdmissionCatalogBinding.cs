using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Private worker launch contract; never production enrollment or a source of credentials.</summary>
internal sealed record LiveAdmissionCatalogBinding(string RunId, string HostId, string AppId,
    string ConsumerAppId, string SampleAppId, string[] Tenants, string ActiveGeneration, string[] Readers)
{
    internal const string EnvironmentVariable = "HEXALITH_LIVE_ADMISSION_CATALOG";

    internal string Revision => Digest(JsonSerializer.Serialize(new
    {
        RunId, ConsumerAppId, SampleAppId, Tenants, ActiveGeneration, Readers,
        Schema = LiveIncrementCounterIdempotencyIntentAdapter.DeclarationDigest,
    }));

    internal string Profile => "Testing-" + Revision;

    internal RouteCatalogHostIdentity Identity => new(HostId, AppId,
        LiveIncrementCounterIdempotencyIntentAdapter.DeclarationDigest,
        Revision, Digest("Testing:live-admission:" + RunId));

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(RunId) || string.IsNullOrWhiteSpace(HostId)
            || string.IsNullOrWhiteSpace(AppId) || string.IsNullOrWhiteSpace(ConsumerAppId)
            || string.IsNullOrWhiteSpace(SampleAppId) || Tenants.Length == 0
            || Tenants.Any(t => string.IsNullOrWhiteSpace(t) || t == "*")
            || Tenants.Distinct(StringComparer.Ordinal).Count() != Tenants.Length
            || string.IsNullOrWhiteSpace(ActiveGeneration) || Readers.Contains(ActiveGeneration, StringComparer.Ordinal)
            || Readers.Distinct(StringComparer.Ordinal).Count() != Readers.Length)
            throw new InvalidOperationException("The private live-admission catalog launch binding is incomplete or unbounded.");
    }

    internal static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

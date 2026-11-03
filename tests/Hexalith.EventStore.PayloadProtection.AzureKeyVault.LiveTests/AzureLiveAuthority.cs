// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 5-7; separately authorized isolated environment required.
using System.Security.Cryptography;
using Hexalith.EventStore.PayloadProtection.AzureKeyVault.Conformance;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests;
/// <summary>A separately supplied exact-content authorization and isolated-environment receipt; never printed as a whole.</summary>
internal sealed class AzureLiveAuthority
{
    public required string Owner { get; init; }
    public required string SourceSha { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public required string ResourceIdSha256 { get; init; }
    public required string IdentityIdSha256 { get; init; }
    public required string NetworkIdSha256 { get; init; }
    public required string Region { get; init; }
    public required string VaultUri { get; init; }
    public required string KeyName { get; init; }
    public required string ManagedIdentityClientId { get; init; }
    public required string DeniedManagedIdentityClientId { get; init; }
    public required string InitialVersion { get; init; }
    public required string RotationVersion { get; init; }
    public required string DisabledVersion { get; init; }
    public required string ExpiredVersion { get; init; }
    public required string MissingVersion { get; init; }
    public required string ExceptionDigest { get; init; }
    public required string CorrectionDigest { get; init; }
    public required string DiagnosticPolicyDigest { get; init; }
    public required string CustodyReceiptSha256 { get; init; }
    public required string[] Operations { get; init; }
    public required string[] Faults { get; init; }
    public required AzureFaultWindow[] FaultWindows { get; init; }

    /// <summary>Checks the explicit switch and externally supplied content hash before any fixture or Azure call.</summary>
    internal static AzureLiveAuthority Load()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PAYLOAD_AZURE_LIVE") != "authorized")
        {
            throw Missing();
        }

        string? path = Environment.GetEnvironmentVariable("HEXALITH_PAYLOAD_AZURE_AUTHORITY");
        string? digest = Environment.GetEnvironmentVariable("HEXALITH_PAYLOAD_AZURE_AUTHORITY_SHA256");
        try
        {
            byte[] bytes = File.ReadAllBytes(path ?? "");
            if (bytes.Length > 16384 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != digest)
            {
                throw Missing();
            }

            var authority = ParsePolicyBoundDocument(bytes);
            if (string.IsNullOrWhiteSpace(authority.Owner) || authority.ExpiresAtUtc <= DateTimeOffset.UtcNow || authority.ExpiresAtUtc > DateTimeOffset.UtcNow.AddDays(1) || authority.SourceSha != Environment.GetEnvironmentVariable("HEXALITH_PAYLOAD_AZURE_SOURCE_SHA") || authority.SourceSha.Length != 40 || authority.SourceSha.Any(c => !Uri.IsHexDigit(c)) || authority.ExceptionDigest != "fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394" || authority.CorrectionDigest != "360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e" || new[]
            {
                authority.ResourceIdSha256,
                authority.IdentityIdSha256,
                authority.NetworkIdSha256,
                authority.CustodyReceiptSha256
            }.Any(value => value.Length != 64 || value.Any(c => !Uri.IsHexDigit(c))))
            {
                throw Missing();
            }

            var required = new HashSet<string>(["get-key", "wrapkey", "unwrapkey", "isolated-dapr-persistence", "engine-rewrap", "read-only-rollback", "cleanup-isolated-dapr", "retain-all-azure-keys"], StringComparer.Ordinal);
            if (!required.SetEquals(authority.Operations) || !new HashSet<string>(["401", "403", "404", "disabled", "expired", "429", "timeout", "dns", "5xx", "cancellation", "breaker", "lost-wrap-response"], StringComparer.Ordinal).SetEquals(authority.Faults))
            {
                throw Missing();
            }

            if (authority.FaultWindows.Length != 4 || !new HashSet<string>(["429", "5xx", "timeout", "dns"], StringComparer.Ordinal).SetEquals(authority.FaultWindows.Select(window => window.Fault)) || authority.FaultWindows.Any(window => window.NotAfterUtc <= window.NotBeforeUtc || window.NotAfterUtc > authority.ExpiresAtUtc || window.NotAfterUtc - window.NotBeforeUtc > TimeSpan.FromMinutes(3)))
            {
                throw Missing();
            }

            AzureFaultWindow[] orderedWindows = authority.FaultWindows.OrderBy(window => window.NotBeforeUtc).ToArray();
            if (orderedWindows.Zip(orderedWindows.Skip(1)).Any(pair => pair.First.NotAfterUtc.AddSeconds(35) > pair.Second.NotBeforeUtc))
            {
                throw Missing();
            }

            AzureKeyVaultPayloadProtectionOptions options = authority.Options();
            _ = AzureKeyVaultOptionsValidator.Validate(options);
            if (!Guid.TryParseExact(authority.DeniedManagedIdentityClientId, "D", out _))
            {
                throw Missing();
            }

            string[] versions = [authority.InitialVersion, authority.RotationVersion, authority.DisabledVersion, authority.ExpiredVersion, authority.MissingVersion];
            if (versions.Distinct(StringComparer.Ordinal).Count() != versions.Length)
            {
                throw Missing();
            }

            foreach (string version in versions)
            {
                _ = AzureKeyVaultKeyProfile.Version(options, version);
            }

            byte[] receipt = File.ReadAllBytes(Environment.GetEnvironmentVariable("HEXALITH_PAYLOAD_AZURE_CUSTODY_RECEIPT") ?? "");
            if (receipt.Length > 16384 || Convert.ToHexStringLower(SHA256.HashData(receipt)) != authority.CustodyReceiptSha256)
            {
                throw Missing();
            }

            AzureCustodyReceipt.Validate(receipt, authority);
            return authority;
        }
        catch
        {
            throw Missing();
        }
    }

    internal void RequirePhase(string expected)
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PAYLOAD_AZURE_PHASE") != expected || (expected == "healthy" && FaultWindows.Min(window => window.NotBeforeUtc) <= DateTimeOffset.UtcNow.AddMinutes(5)))
        {
            throw new InvalidOperationException("The explicit Azure phase and five-minute healthy-window margin are required before provider calls.");
        }
    }

    internal AzureKeyVaultPayloadProtectionOptions Options(string? identity = null) => new()
    {
        VaultUri = new(VaultUri),
        KeyName = KeyName,
        ManagedIdentityClientId = identity ?? ManagedIdentityClientId
    };
    internal AzureKeyVaultPayloadProtectionBackend Backend(string? identity = null, Azure.Core.TokenCredential? credential = null)
    {
        var options = Options(identity);
        credential ??= Credential(options);
        var transport = new AzureKeyVaultSafeTransport(options);
        return new(options, new(options, credential, transport), new(options, credential, transport));
    }

    /// <summary>Retains exact primary/negative-test identity selection through construction and token acquisition.</summary>
    internal Azure.Core.TokenCredential Credential(AzureKeyVaultPayloadProtectionOptions options)
    {
        if (options.ManagedIdentityClientId != ManagedIdentityClientId && options.ManagedIdentityClientId != DeniedManagedIdentityClientId)
            throw Missing();
        using var selectedIdentity = new AzureIdentityDiagnosticScope(options.ManagedIdentityClientId);
        return new AzureLiveCredential(AzureKeyVaultCredentialFactory.Create(options), options.ManagedIdentityClientId);
    }

    /// <summary>Checks the closed document schema and exact diagnostic-policy binding before external receipts or calls.</summary>
    internal static AzureLiveAuthority ParsePolicyBoundDocument(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || document.RootElement.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal).Any(group => group.Count() != 1))
                throw Missing();
            var authority = JsonSerializer.Deserialize<AzureLiveAuthority>(bytes,
                new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow }) ?? throw Missing();
            if (authority.DiagnosticPolicyDigest != AzureIdentityDiagnosticPolicy.Digest) throw Missing();
            return authority;
        }
        catch (JsonException) { throw Missing(); }
    }

    private static InvalidOperationException Missing() => new("Explicit unexpired source-bound Azure conformance authority and custody receipt are required. No Azure call is authorized by the default test run.");
    public override string ToString() => nameof(AzureLiveAuthority);
}

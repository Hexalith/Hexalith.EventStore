using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.EventStore.Server.Security;

/// <summary>Offline byte/signature interoperability probe compiled against the owning source primitives.</summary>
internal static class CapabilityInterop
{
    private static void Main()
    {
        using var input = JsonDocument.Parse(Console.In.ReadToEnd());
        var root = input.RootElement;
        var payload = root.GetProperty("Capability").Deserialize<DeletionBatchCapabilityV1>()!;
        var targets = root.GetProperty("Targets").Deserialize<ProtectionTarget[]>()!;
        var profile = new DeletionCapabilityTrustProfile(payload.Issuer, payload.Audience, payload.TenantId,
            payload.CapabilityKeyVersion, root.GetProperty("PublicAnchorId").GetString()!, root.GetProperty("PublicAnchorVersion").GetString()!);
        using var publicKey = ECDsa.Create();
        publicKey.ImportFromPem(File.ReadAllText(root.GetProperty("PublicAnchorFile").GetString()!));
        using var offlineKey = ECDsa.Create();
        offlineKey.ImportFromPem(File.ReadAllText(root.GetProperty("OfflinePrivateKeyFile").GetString()!));
        string signature = root.GetProperty("DetachedJws").GetString()!;
        var captured = DeletionConsumptionIdentity.Capture(root.Deserialize<DeletionBatchConsumptionRequest>()!);
        var captureChecks = new List<bool>();
        foreach (var candidate in root.GetProperty("CaptureCandidates").EnumerateArray())
        {
            try
            {
                _ = DeletionConsumptionIdentity.Capture(candidate.Deserialize<DeletionBatchConsumptionRequest>()!);
                captureChecks.Add(true);
            }
            catch (ArgumentException)
            {
                captureChecks.Add(false);
            }
        }
        var result = new Dictionary<string, object>
        {
            ["canonicalBase64"] = Convert.ToBase64String(DeletionBatchCapabilityIdentity.CanonicalPayload(payload)),
            ["signingRequestId"] = DeletionBatchCapabilityIdentity.SigningRequestId(payload),
            ["nativeTargetsBase64"] = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(targets.Select(target =>
                new[] { target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias }).ToArray())),
            ["manifestDigest"] = DeletionBatchCapabilityIdentity.TargetManifestDigest(targets),
            ["pythonSignatureVerified"] = DeletionBatchCapabilityCodec.Verify(payload, profile, signature, publicKey),
            ["changedPayloadRejected"] = !DeletionBatchCapabilityCodec.Verify(payload with { DeletionRequestId = payload.DeletionRequestId + "changed" }, profile, signature, publicKey),
            ["dotnetDetachedJws"] = DeletionBatchCapabilityCodec.Sign(payload, profile, offlineKey),
            ["nativeCaptureAccepted"] = captured.Capability == payload,
            ["nativeCaptureChecks"] = captureChecks,
        };
        Console.WriteLine(JsonSerializer.Serialize(result));
    }
}

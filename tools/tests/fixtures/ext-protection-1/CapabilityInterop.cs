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
        if (root.TryGetProperty("NativeSigningOriginals", out var rawOriginals))
        {
            var originals = rawOriginals.Deserialize<DeletionCapabilitySigningOutcome[]>()!;
            var receipts = root.GetProperty("NativeNoIssueOriginals").Deserialize<GovernanceProtocolReceipt[]>()!;
            var recoveries = root.GetProperty("NativeSigningRecoveries").Deserialize<GovernanceSigningRecovery[]>()!;
            result["nativeNoIssueIntents"] = root.GetProperty("NativeNoIssueIntents").EnumerateArray().Select(value =>
            {
                byte[] bytes = Convert.FromBase64String(value.GetProperty("BytesBase64").GetString()!);
                using var document = JsonDocument.Parse(bytes);
                var transition = document.RootElement.GetProperty("Transition").Deserialize<GovernanceGuardTransition>()!;
                string targetMutationDigest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Array.Empty<GuardedStateMutation>())));
                return new { BytesBase64 = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new { Transition = transition, TargetMutationDigest = targetMutationDigest })) };
            }).ToArray();
            result["nativeSigningOriginals"] = originals;
            result["nativeNoIssueOriginals"] = receipts;
            result["nativeSigningRecoveries"] = recoveries;
            result["nativeIssueOperationIds"] = originals.Take(originals.Length - 1).Select(original =>
                "issue-" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[]
                {
                    original.SigningRequestId,
                    Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(original.DetachedJws!))),
                })))).ToArray();
            result["nativeSuccessors"] = originals.Take(originals.Length - 1).Zip(recoveries).Select(pair =>
                DeletionCapabilitySigningSuccessor.Create(pair.First,
                    new DeletionCapabilityNoIssueProof(pair.Second.Payload, pair.Second.SigningRequestId, pair.Second.DetachedJwsDigest,
                        pair.Second.NoIssueReceiptId, pair.Second.CurrentGuardRevision, pair.Second.CurrentHealthyKeyVersion,
                        pair.Second.AuthorityRevision, pair.Second.ObservedAt, pair.Second.ValidUntil), pair.Second.ObservedAt)).ToArray();
        }
        Console.WriteLine(JsonSerializer.Serialize(result));
    }
}

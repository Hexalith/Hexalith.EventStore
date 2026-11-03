// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 5-7; separately authorized isolated environment required.
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests;
/// <summary>Explicit-service qualification through logical Dapr persistence; no production Server/Parties or all-copy erasure claim.</summary>
[Collection("Authorized Azure service")]
public sealed class AzureKeyVaultConformanceTests(AzureDaprFixture fixture) : IClassFixture<AzureDaprFixture>
{
    [Fact]
    public async Task V079_V060_V061_V125_PersistedEventSnapshotRestartRewrapAndReadOnlyRollbackRetainHistory()
    {
        fixture.Authority.RequirePhase("healthy");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var token = deadline.Token;
        await using var env = await AzureLiveEnvironment.CreateAsync(fixture, "history", token);
        byte[] plain = "{\"secret\":\"authorized-live-canary\"}"u8.ToArray();
        var write = await env.Reservations.ProtectEventAsync(plain, ["/secret"], env.Context(), token);
        string eventKey = "azure-conformance:event:" + env.Scope;
        await env.Reservations.AcquireCompletionAsync(write.Completion!, token);
        await fixture.Client.SaveByteStateAsync("statestore", eventKey, write.Result.PayloadBytes, cancellationToken: token);
        await env.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted, token);
        var snapshotContext = new PayloadProtectionContext(env.Identity, "hx-snapshot-v1:azure-qualification", PayloadProtectionPayloadKind.Snapshot, 1);
        var snapshot = await env.Reservations.ProtectSnapshotAsync(plain, snapshotContext, token);
        string snapshotKey = "azure-conformance:snapshot:" + env.Scope;
        await env.Reservations.AcquireCompletionAsync(snapshot.Completion, token);
        await fixture.Client.SaveByteStateAsync("statestore", snapshotKey, JsonSerializer.SerializeToUtf8Bytes(snapshot.Result), cancellationToken: token);
        await env.Reservations.CompleteAsync(snapshot.Completion, PayloadProtectionPersistenceOutcome.Persisted, token);
        byte[] persisted = (await fixture.Client.GetByteStateAsync("statestore", eventKey, ConsistencyMode.Strong, cancellationToken: token)).ToArray();
        persisted.AsSpan().SequenceEqual(write.Result.PayloadBytes).ShouldBeTrue();
        WrappedDekV1 before = await env.KeyAsync(write.Completion!, token);
        before.State.ShouldBe("Active");
        (before.ProviderKeyVersionId == fixture.Authority.InitialVersion).ShouldBeTrue();
        Base64UrlCodec.Decode(before.WrappedDek).Length.ShouldBe(384);
        await env.RestartAsync(token);
        await env.AssertReadableAsync(persisted, token);
        var storedSnapshot = JsonSerializer.Deserialize<ProtectedSnapshotPayloadV2>((await fixture.Client.GetByteStateAsync("statestore", snapshotKey, ConsistencyMode.Strong, cancellationToken: token)).Span)!;
        var resolver = env.Resolver();
        var restored = await new PayloadProtectionCore().TryUnprotectSnapshotAsync(storedSnapshot, snapshotContext, (key, version, ct) => resolver.ResolveAsync(env.Identity, key, version, new CryptographicPayloadProtectionEntropy().CreateKeyReference(), ct), token);
        restored.IsReadable.ShouldBeTrue();
        restored.PayloadBytes!.AsSpan().SequenceEqual(plain).ShouldBeTrue();
        CryptographicOperations.ZeroMemory(restored.PayloadBytes!);
        env.Backend.Selected = fixture.Authority.RotationVersion;
        var rotation = new PayloadKeyRotationCoordinator(fixture.Store, env.Roots, env.Leases, env.Backend, new(), _ => true, "authorized-qualification");
        (await rotation.VerifyInventoryAsync(fixture.Authority.InitialVersion, token)).Head!.Value.EntryCount.ShouldBe(2ul);
        (await rotation.RewrapAsync(env.Identity, write.Completion!.KeyReference, 1, "01J00000000000000000000862", fixture.Authority.RotationVersion, token)).ShouldBeTrue();
        (await rotation.RewrapAsync(env.Identity, snapshot.Completion.KeyReference, 1, "01J00000000000000000000863", fixture.Authority.RotationVersion, token)).ShouldBeTrue();
        (await rotation.VerifyInventoryAsync(fixture.Authority.InitialVersion, token)).Head!.Value.EntryCount.ShouldBe(0ul);
        (await rotation.VerifyInventoryAsync(fixture.Authority.RotationVersion, token)).Head!.Value.EntryCount.ShouldBe(2ul);
        ((await env.KeyAsync(write.Completion!, token)).ProviderKeyVersionId == fixture.Authority.RotationVersion).ShouldBeTrue();
        (await fixture.Client.GetByteStateAsync("statestore", eventKey, ConsistencyMode.Strong, cancellationToken: token)).Span.SequenceEqual(persisted).ShouldBeTrue();
        await env.RestartAsync(token);
        await env.AssertReadableAsync(persisted, token);
        var deployments = new PayloadProtectionDeploymentCoordinator(fixture.Store, env.Roots);
        var oldFence = (await fixture.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(env.Root), token))!.Value;
        await deployments.CloseAsync(env.Identity, 1, token);
        var generation = (await fixture.Store.ReadAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(env.Root), token))!.Value;
        await deployments.OpenSuccessorAsync(env.Identity, generation with { Generation = 2, Status = "Open" }, token);
        await env.RestartAsync(token);
        await deployments.AdvanceFenceAsync(env.Identity, oldFence with { ApprovedWriteMode = "ReadOnly", ApprovalEpoch = 2, ApprovalId = "01J00000000000000000000864", DeploymentGeneration = 2 }, token);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => env.Reservations.ProtectEventAsync(plain, ["/secret"], env.Context(2), token).AsTask());
        await env.AssertReadableAsync(persisted, token);
        var fence = (await fixture.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(env.Root), token))!.Value;
        fence.HighestWrittenFormat.ShouldBe("V2");
        fence.HighestWrittenEpoch.ShouldBe(oldFence.HighestWrittenEpoch);
        byte[] snapshotAfter = (await fixture.Client.GetByteStateAsync("statestore", snapshotKey, ConsistencyMode.Strong, cancellationToken: token)).ToArray();
        snapshotAfter.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(snapshot.Result)).ShouldBeTrue();
        Console.WriteLine("Persisted event/snapshot/key/index state verified through Dapr. ReadOnly rollback retains v2. Azure keys retained; no all-copy erasure or Server/Parties claim.");
        CryptographicOperations.ZeroMemory(plain);
        CryptographicOperations.ZeroMemory(persisted);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("disabled")]
    [InlineData("expired")]
    public async Task V083_V084_RealExactVersionFailuresNeverFallbackOrManufactureDeletion(string condition)
    {
        fixture.Authority.RequirePhase("healthy");
        var authority = fixture.Authority;
        string version = condition switch
        {
            "missing" => authority.MissingVersion,
            "disabled" => authority.DisabledVersion,
            _ => authority.ExpiredVersion
        };
        using var result = await authority.Backend().UnwrapAsync("01J00000000000000000000865", version, new byte[384], CancellationToken.None);
        result.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch);
    }

    [Fact]
    public async Task V080_V082_RealServiceRejectsInvalidTokenAndKeyScopedDeniedIdentity()
    {
        fixture.Authority.RequirePhase("healthy");
        var authority = fixture.Authority;
        var options = authority.Options();
        var invalidClient = new AzureKeyVaultMetadataClient(options, new AzureInvalidToken(), new AzureKeyVaultSafeTransport(options));
        (await Should.ThrowAsync<Azure.RequestFailedException>(() => invalidClient.GetAsync(AzureKeyVaultKeyProfile.Version(options, authority.RotationVersion), CancellationToken.None).AsTask())).Status.ShouldBe(401);
        var deniedOptions = authority.Options(authority.DeniedManagedIdentityClientId);
        var deniedClient = new AzureKeyVaultMetadataClient(deniedOptions, AzureKeyVaultCredentialFactory.Create(deniedOptions), new AzureKeyVaultSafeTransport(deniedOptions));
        (await Should.ThrowAsync<Azure.RequestFailedException>(() => deniedClient.GetAsync(AzureKeyVaultKeyProfile.Version(options, authority.RotationVersion), CancellationToken.None).AsTask())).Status.ShouldBe(403);
        using var deniedToken = await authority.Backend(credential: new AzureInvalidToken()).WrapExactAsync("01J00000000000000000000866", authority.RotationVersion, new byte[32], CancellationToken.None);
        deniedToken.Failure.ShouldBe(PayloadBackendFailure.Denied);
        using var deniedIdentity = await authority.Backend(authority.DeniedManagedIdentityClientId).WrapExactAsync("01J00000000000000000000867", authority.RotationVersion, new byte[32], CancellationToken.None);
        deniedIdentity.Failure.ShouldBe(PayloadBackendFailure.Denied);
    }

    [Theory]
    [InlineData("429")]
    [InlineData("dns")]
    [InlineData("timeout")]
    [InlineData("5xx")]
    [InlineData("lost-wrap-response")]
    public async Task V085_V087_V097_EngineBoundaryFaultsRetainRealPriorStateWithoutClaimingServiceFaultProof(string fault)
    {
        fixture.Authority.RequirePhase("healthy");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var token = timeout.Token;
        await using var env = await AzureLiveEnvironment.CreateAsync(fixture, "fault-" + fault, token);
        var write = await env.Reservations.ProtectEventAsync("{\"secret\":\"authorized-live-canary\"}"u8.ToArray(), ["/secret"], env.Context(), token);
        await env.Reservations.AcquireCompletionAsync(write.Completion!, token);
        await fixture.Client.SaveByteStateAsync("statestore", "azure-conformance:event:" + env.Scope, write.Result.PayloadBytes, cancellationToken: token);
        await env.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted, token);
        WrappedDekV1 before = await env.KeyAsync(write.Completion!, token);
        env.Backend.Fault = fault;
        await Should.ThrowAsync<PayloadProtectionStateException>(() => env.Reservations.ProtectEventAsync("{\"secret\":\"second-live-canary\"}"u8.ToArray(), ["/secret"], env.Context(2), token).AsTask());
        (await env.KeyAsync(write.Completion!, token)).ShouldBe(before);
        WorkCatalogSnapshot work = await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(env.Root, token);
        if (fault is "timeout" or "5xx" or "lost-wrap-response")
        {
            work.Head.Value.EntryCount.ShouldBeGreaterThan(0u);
        }

        env.Backend.Fault = null;
        await env.RestartAsync(token);
        await env.AssertReadableAsync(write.Result.PayloadBytes, token);
        Console.WriteLine("Boundary fault injected; persisted prior material retained. Lost responses remain pending; no provider absence or key deletion inferred.");
    }

    [Fact]
    public async Task V089_CancellationBeforeAzureDispatchHasNoMutation()
    {
        fixture.Authority.RequirePhase("healthy");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => fixture.Authority.Backend().WrapAsync("01J00000000000000000000868", new byte[32], cancellation.Token).AsTask());
    }
}

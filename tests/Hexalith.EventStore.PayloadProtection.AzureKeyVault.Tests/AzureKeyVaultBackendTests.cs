// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V079-V106, V127-V132.
using System.Security.Cryptography;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
public sealed class AzureKeyVaultBackendTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task V059_V060_EmitsExplicitRemoteVersionedPostAndBindsReturnedKid(bool wrap)
    {
        byte[] output = Enumerable.Repeat((byte)0x6d, wrap ? 384 : 32).ToArray();
        var credential = new AdapterTestCredential();
        var cleared = new List<byte[]>();
        var transport = new AdapterTestTransport(async message =>
        {
            message.BufferResponse.ShouldBeFalse();
            message.Request.Method.ShouldBe(RequestMethod.Post);
            message.Request.Uri.ToUri().AbsoluteUri.ShouldBe(AdapterFixture.Historical + (wrap ? "/wrapkey" : "/unwrapkey") + "?api-version=2025-07-01");
            byte[] request = await AdapterFixture.ReadRequestAsync(message);
            using var json = JsonDocument.Parse(request);
            json.RootElement.GetProperty("alg").GetString().ShouldBe("RSA-OAEP-256");
            json.RootElement.GetProperty("value").GetString()!.Length.ShouldBe(wrap ? 43 : 512);
            CryptographicOperations.ZeroMemory(request);
            message.Response = AdapterFixture.Response(200, AdapterFixture.Crypto(AdapterFixture.Historical, output));
        });
        var data = new AzureKeyVaultDataPlane(AdapterFixture.Options, credential, transport, cleared: cleared.Add);
        using PayloadBackendResult response = await data.ExecuteAsync(AdapterFixture.Historical, new byte[wrap ? 32 : 384], wrap, CancellationToken.None);
        response.Failure.ShouldBe(PayloadBackendFailure.None);
        response.ProviderKeyVersionId.ShouldBe(AdapterFixture.Historical);
        byte[] transferred = response.TakeBytes();
        transferred.ShouldBe(output);
        CryptographicOperations.ZeroMemory(transferred);
        credential.LastContext.Scopes.ShouldBe(["https://vault.azure.net/.default"]);
        transport.Count.ShouldBe(1);
        cleared.Count.ShouldBe(3);
        cleared.ShouldAllBe(bytes => bytes.All(value => value == 0));
    }

    [Fact]
    public async Task V080_CryptoCaeChallengeIsDenialWithoutTokenRefreshOrReplay()
    {
        var credential = new AdapterTestCredential();
        var transport = new AdapterTestTransport(message =>
        {
            message.Response = AdapterFixture.Response(401, [], new(StringComparer.OrdinalIgnoreCase) { ["WWW-Authenticate"] = "Bearer error=\"insufficient_claims\", claims=\"eyJhY2Nlc3NfdG9rZW4iOnt9fQ==\"" });
            return ValueTask.CompletedTask;
        });
        using var result = await new AzureKeyVaultDataPlane(AdapterFixture.Options, credential, transport).ExecuteAsync(AdapterFixture.Current, new byte[32], true, CancellationToken.None);
        result.Failure.ShouldBe(PayloadBackendFailure.Denied);
        transport.Count.ShouldBe(1);
        credential.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(400, false, (int)PayloadBackendFailure.ConsistencyMismatch)]
    [InlineData(401, true, (int)PayloadBackendFailure.Denied)]
    [InlineData(403, false, (int)PayloadBackendFailure.Denied)]
    [InlineData(404, true, (int)PayloadBackendFailure.ConsistencyMismatch)]
    [InlineData(408, true, (int)PayloadBackendFailure.Unavailable)]
    [InlineData(429, true, (int)PayloadBackendFailure.Unavailable)]
    [InlineData(500, true, (int)PayloadBackendFailure.Ambiguous)]
    [InlineData(501, true, (int)PayloadBackendFailure.Ambiguous)]
    [InlineData(502, true, (int)PayloadBackendFailure.Ambiguous)]
    [InlineData(503, true, (int)PayloadBackendFailure.Ambiguous)]
    [InlineData(504, true, (int)PayloadBackendFailure.Ambiguous)]
    [InlineData(599, true, (int)PayloadBackendFailure.Ambiguous)]
    [InlineData(500, false, (int)PayloadBackendFailure.Unavailable)]
    [InlineData(599, false, (int)PayloadBackendFailure.Unavailable)]
    public async Task V080_V087_TypedStatusMatrix(int status, bool wrap, int expected)
    {
        var transport = new AdapterTestTransport(message =>
        {
            message.Response = AdapterFixture.Response(status, []);
            return ValueTask.CompletedTask;
        });
        using var result = await new AzureKeyVaultDataPlane(AdapterFixture.Options, new AdapterTestCredential(), transport).ExecuteAsync(AdapterFixture.Current, new byte[wrap ? 32 : 384], wrap, CancellationToken.None);
        result.Failure.ShouldBe((PayloadBackendFailure)expected);
        transport.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("-1", null)]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("3", null)]
    [InlineData("canary", null)]
    public void V086_RetryAfterIsStructurallyBounded(string text, int? seconds) => AzureKeyVaultFailureClassifier.RetryAfter(text, DateTimeOffset.UtcNow).ShouldBe(seconds is int delay ? TimeSpan.FromSeconds(delay) : null);
    [Fact]
    public async Task V081_PreDispatchTokenFailureUsesExecutorThreeAttemptBudget()
    {
        var key = await AdapterFixture.KeyAsync();
        var credential = new AdapterTestCredential
        {
            Failure = new RequestFailedException(503, "private-error-canary")
        };
        var transport = new AdapterTestTransport(_ => throw new InvalidOperationException("No provider dispatch is permitted."));
        var backend = new AzureKeyVaultPayloadProtectionBackend(AdapterFixture.Options, new((_, _) => ValueTask.FromResult(key)), new(AdapterFixture.Options, credential, transport));
        var pauses = new List<TimeSpan>();
        var executor = new PayloadBackendExecutor(delay: (pause, _) =>
        {
            pauses.Add(pause);
            return ValueTask.CompletedTask;
        });
        using var result = await executor.ExecuteAsync(backend, token => backend.WrapAsync(AdapterFixture.Operation, new byte[32], token));
        result.Failure.ShouldBe(PayloadBackendFailure.Unavailable);
        transport.Count.ShouldBe(0);
        credential.Count.ShouldBe(3);
        pauses.Count.ShouldBe(2);
        pauses[0].ShouldBeInRange(TimeSpan.FromMilliseconds(160), TimeSpan.FromMilliseconds(240));
        pauses[1].ShouldBeInRange(TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(600));
    }

    [Fact]
    public async Task V097_PostDispatchAmbiguityIsNeverReplayedOrReconciledAsMissing()
    {
        var key = await AdapterFixture.KeyAsync();
        var transport = new AdapterTestTransport(message =>
        {
            ((AzureKeyVaultRequest)message.Request).Dispatched = true;
            throw new IOException("provider-network-canary");
        });
        var backend = new AzureKeyVaultPayloadProtectionBackend(AdapterFixture.Options, new((_, _) => ValueTask.FromResult(key)), new(AdapterFixture.Options, new AdapterTestCredential(), transport));
        using var result = await new PayloadBackendExecutor().ExecuteAsync(backend, token => backend.WrapAsync(AdapterFixture.Operation, new byte[32], token));
        result.Failure.ShouldBe(PayloadBackendFailure.Ambiguous);
        transport.Count.ShouldBe(1);
        using var reconciled = await backend.ReconcileAsync(AdapterFixture.Operation, CancellationToken.None);
        reconciled.Failure.ShouldBe(PayloadBackendFailure.Ambiguous);
        transport.Count.ShouldBe(1);
    }

    [Fact]
    public async Task V059_ExpiredSelectionCannotServeNewWrapAfterRefreshFailureButExactHistoryStillReads()
    {
        var current = await AdapterFixture.KeyAsync();
        var old = await AdapterFixture.KeyAsync(AdapterFixture.Metadata(AdapterFixture.Historical));
        var clock = new AdapterTestClock();
        bool failCurrent = false;
        var versions = new List<string?>();
        var metadata = new AzureKeyVaultMetadataClient((version, _) =>
        {
            versions.Add(version);
            if (version is null && failCurrent)
            {
                throw new RequestFailedException(503, "metadata-canary");
            }

            return ValueTask.FromResult(version is null ? current : old);
        });
        var transport = new AdapterTestTransport(message =>
        {
            bool wrap = message.Request.Uri.ToUri().AbsolutePath.EndsWith("wrapkey", StringComparison.Ordinal) && !message.Request.Uri.ToUri().AbsolutePath.EndsWith("unwrapkey", StringComparison.Ordinal);
            string id = wrap ? AdapterFixture.Current : AdapterFixture.Historical;
            message.Response = AdapterFixture.Response(200, AdapterFixture.Crypto(id, new byte[wrap ? 384 : 32]));
            return ValueTask.CompletedTask;
        });
        var backend = new AzureKeyVaultPayloadProtectionBackend(AdapterFixture.Options, metadata, new(AdapterFixture.Options, new AdapterTestCredential(), transport), clock);
        using var first = await backend.WrapAsync(AdapterFixture.Operation, new byte[32], CancellationToken.None);
        first.Failure.ShouldBe(PayloadBackendFailure.None);
        clock.Advance(TimeSpan.FromMinutes(5));
        failCurrent = true;
        using var stale = await backend.WrapAsync(AdapterFixture.Operation, new byte[32], CancellationToken.None);
        stale.Failure.ShouldBe(PayloadBackendFailure.Unavailable);
        transport.Count.ShouldBe(1);
        using var historical = await backend.UnwrapAsync(AdapterFixture.Operation, AdapterFixture.Historical, new byte[384], CancellationToken.None);
        historical.Failure.ShouldBe(PayloadBackendFailure.None);
        versions.ShouldBe([null, null, "00000000000000000000000000000001"]);
    }

    [Fact]
    public async Task V061_RetainedTargetWrapIgnoresCurrentAliasAndValidatesExactTarget()
    {
        var key = await AdapterFixture.KeyAsync(AdapterFixture.Metadata(AdapterFixture.Historical));
        string? queried = null;
        var metadata = new AzureKeyVaultMetadataClient((version, _) =>
        {
            queried = version;
            return ValueTask.FromResult(key);
        });
        var transport = new AdapterTestTransport(message =>
        {
            message.Response = AdapterFixture.Response(200, AdapterFixture.Crypto(AdapterFixture.Historical, new byte[384]));
            return ValueTask.CompletedTask;
        });
        var backend = new AzureKeyVaultPayloadProtectionBackend(AdapterFixture.Options, metadata, new(AdapterFixture.Options, new AdapterTestCredential(), transport));
        using var result = await backend.WrapExactAsync(AdapterFixture.Operation, AdapterFixture.Historical, new byte[32], CancellationToken.None);
        result.Failure.ShouldBe(PayloadBackendFailure.None);
        queried.ShouldBe("00000000000000000000000000000001");
    }

    [Fact]
    public async Task V089_CallerCancellationRetainsOwnedInputsUntilActualCompletion()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[]? request = null;
        var cleared = new List<byte[]>();
        var transport = new AdapterTestTransport(async message =>
        {
            request = await AdapterFixture.ReadRequestAsync(message);
            started.SetResult();
            await release.Task;
            message.Response = AdapterFixture.Response(200, AdapterFixture.Crypto(AdapterFixture.Current, new byte[384]));
        });
        var data = new AzureKeyVaultDataPlane(AdapterFixture.Options, new AdapterTestCredential(), transport, cleared: cleared.Add);
        var key = await AdapterFixture.KeyAsync();
        var backend = new AzureKeyVaultPayloadProtectionBackend(AdapterFixture.Options, new((_, _) => ValueTask.FromResult(key)), data);
        using var cancellation = new CancellationTokenSource();
        Task? drain = null;
        Task<PayloadBackendResult> call = new PayloadBackendExecutor().ExecuteAsync(backend, token => backend.WrapAsync(AdapterFixture.Operation, new byte[32], token), cancellation.Token, retainAbandoned: task => drain = task).AsTask();
        await started.Task;
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => await call);
        cleared.ShouldBeEmpty();
        _ = drain.ShouldNotBeNull();
        drain!.IsCompleted.ShouldBeFalse();
        release.SetResult();
        await drain.WaitAsync(TimeSpan.FromSeconds(5));
        cleared.Count.ShouldBe(3);
        cleared.ShouldAllBe(bytes => bytes.All(value => value == 0));
        request.ShouldNotBeNull();
        CryptographicOperations.ZeroMemory(request!);
    }

    [Fact]
    public async Task V085_CombinedTenSecondBudgetRejectsAndClearsLateSuccess()
    {
        var clock = new AdapterTestClock();
        byte[]? returned = null;
        var key = await AdapterFixture.KeyAsync();
        var transport = new AdapterTestTransport(message =>
        {
            returned = AdapterFixture.Crypto(AdapterFixture.Current, new byte[384]);
            message.Response = AdapterFixture.Response(200, returned);
            clock.Advance(TimeSpan.FromSeconds(10));
            return ValueTask.CompletedTask;
        });
        var backend = new AzureKeyVaultPayloadProtectionBackend(AdapterFixture.Options, new((_, _) => ValueTask.FromResult(key)), new(AdapterFixture.Options, new AdapterTestCredential(), transport), clock);
        using var result = await new PayloadBackendExecutor(clock).ExecuteAsync(backend, token => backend.WrapAsync(AdapterFixture.Operation, new byte[32], token));
        result.Failure.ShouldBe(PayloadBackendFailure.Ambiguous);
        transport.Count.ShouldBe(1);
        returned.ShouldNotBeNull();
        returned!.ShouldAllBe(value => value == 0);
    }

    [Fact]
    public async Task V091_ReadinessUsesFresh32BytesAndRejectsWrongUnwrap()
    {
        var key = await AdapterFixture.KeyAsync();
        var transport = new AdapterTestTransport(message =>
        {
            bool wrap = !message.Request.Uri.ToUri().AbsolutePath.EndsWith("unwrapkey", StringComparison.Ordinal);
            message.Response = AdapterFixture.Response(200, AdapterFixture.Crypto(AdapterFixture.Current, new byte[wrap ? 384 : 32]));
            return ValueTask.CompletedTask;
        });
        var backend = new AzureKeyVaultPayloadProtectionBackend(AdapterFixture.Options, new((_, _) => ValueTask.FromResult(key)), new(AdapterFixture.Options, new AdapterTestCredential(), transport));
        var result = await new AzureKeyVaultReadinessProbe(backend).ProbeAsync();
        result.Ready.ShouldBeFalse();
        result.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch);
        transport.Count.ShouldBe(2);
    }

    [Fact]
    public async Task V079_ReadinessUsesFreshRemoteRoundTripsAndClearsEveryProbeArray()
    {
        var key = await AdapterFixture.KeyAsync();
        byte[]? probe = null;
        var inputs = new List<byte[]>();
        var cleared = new List<byte[]>();
        var transport = new AdapterTestTransport(async message =>
        {
            bool wrap = !message.Request.Uri.ToUri().AbsolutePath.EndsWith("unwrapkey", StringComparison.Ordinal);
            if (wrap)
            {
                byte[] request = await AdapterFixture.ReadRequestAsync(message);
                using var json = JsonDocument.Parse(request);
                probe = System.Buffers.Text.Base64Url.DecodeFromChars(json.RootElement.GetProperty("value").GetString()!);
                inputs.Add(probe.ToArray());
                CryptographicOperations.ZeroMemory(request);
            }

            message.Response = AdapterFixture.Response(200, AdapterFixture.Crypto(AdapterFixture.Current, wrap ? new byte[384] : probe!));
        });
        var backend = new AzureKeyVaultPayloadProtectionBackend(AdapterFixture.Options, new((_, _) => ValueTask.FromResult(key)), new(AdapterFixture.Options, new AdapterTestCredential(), transport));
        var readiness = new AzureKeyVaultReadinessProbe(backend, cleared: cleared.Add);
        (await readiness.ProbeAsync()).Ready.ShouldBeTrue();
        (await readiness.ProbeAsync()).Ready.ShouldBeTrue();
        inputs.Count.ShouldBe(2);
        inputs[0].SequenceEqual(inputs[1]).ShouldBeFalse();
        transport.Count.ShouldBe(4);
        cleared.Count.ShouldBe(6);
        cleared.ShouldAllBe(bytes => bytes.All(value => value == 0));
        foreach (byte[] bytes in inputs)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }

        CryptographicOperations.ZeroMemory(probe!);
    }
}

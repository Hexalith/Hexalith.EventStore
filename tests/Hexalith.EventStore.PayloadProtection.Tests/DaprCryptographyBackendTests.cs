// Approved Dapr Kubernetes cryptography supplement: e31a6b1d147f9e71bcbad97edc39d556e384fa587fdbdd391ce7e290ecff3034; sections 2-4 and 7.
// Test-only use of the Alpha cryptography API; the production suppression stays confined to the backend source file.
#pragma warning disable DAPR_CRYPTOGRAPHY
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using Dapr;
using Dapr.Client;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Supplement sections 2-4 and 7: closed options, inventory refusal, failure mapping, cancellation, clearing and no-leak behavior.</summary>
/// <remarks>Runs in the non-parallel diagnostics collection because it observes the process-wide provider-failure counter.</remarks>
[Collection(DiagnosticsCollection.Name)]
public sealed class DaprCryptographyBackendTests
{
    private const string Operation = "01J00000000000000000000801";
    private const string Current = "pp-keys/kek-v2/key.pem";
    private const string Retained = "pp-keys/kek-v1/key.pem";
    private const string Foreign = "pp-keys/kek-foreign/key.pem";
    private const string Canary = "PRIVATE_DAPR_CRYPTO_CANARY";

    [Fact]
    public async Task WrapUsesOnlyTheCurrentAddressVerifiesTheRoundTripAndClearsEveryOwnedAndReturnedBuffer()
    {
        var sidecar = new DaprCryptoTestSidecar(Current, Retained);
        var backend = Backend(sidecar);
        byte[] caller = Enumerable.Repeat((byte)0x83, 32).ToArray();
        using PayloadBackendResult result = await backend.WrapAsync(Operation, caller, default);
        result.Failure.ShouldBe(PayloadBackendFailure.None);
        result.ProviderKeyVersionId.ShouldBe(Current);
        byte[] envelope = result.TakeBytes();
        WrappedDekV2.IsEnvelope(envelope).ShouldBeTrue(); envelope.Length.ShouldBeGreaterThan(512);
        sidecar.Calls.ShouldBe([(true, Current), (false, Current)]);
        caller.ShouldAllBe(b => b == 0x83);
        // The owned DEK copy is cleared; the verified envelope array is exactly the one transferred to the caller.
        sidecar.Inputs[0].ShouldAllBe(b => b == 0);
        sidecar.Inputs[1].ShouldBeSameAs(envelope);
        sidecar.Outputs.ShouldAllBe(buffer => buffer.All(b => b == 0));
        backend.BackendId.ShouldBe("dapr-crypto-kubernetes-secrets-v1");
        backend.WrapAlgorithmId.ShouldBe("dapr-enc-v1-rsa-oaep-256-aes-gcm");
        backend.KeyRecordSchemaVersion.ShouldBe(2);
        PayloadProtectionStateKeys.IsHex(backend.EndpointFingerprint, 64).ShouldBeTrue();
        CryptographicOperations.ZeroMemory(envelope);
    }

    [Fact]
    public async Task UnwrapDecryptsWithTheRecordedAddressOnlyAndRefusesAddressesOutsideTheInventoryBeforeTransport()
    {
        var sidecar = new DaprCryptoTestSidecar(Current, Retained, Foreign);
        var backend = Backend(sidecar);
        byte[] dek = RandomNumberGenerator.GetBytes(32);
        byte[] historical = sidecar.Seal(Retained, dek);
        using (PayloadBackendResult read = await backend.UnwrapAsync(Operation, Retained, historical, default))
        {
            read.Failure.ShouldBe(PayloadBackendFailure.None); read.ProviderKeyVersionId.ShouldBe(Retained);
            byte[] plaintext = read.TakeBytes(); plaintext.ShouldBe(dek); CryptographicOperations.ZeroMemory(plaintext);
        }
        sidecar.Calls.ShouldBe([(false, Retained)]);
        byte[] header = historical.ToArray(); header[WrappedDekV2.SchemePrefix.Length + 1] ^= 0x01;
        using (PayloadBackendResult tampered = await backend.UnwrapAsync(Operation, Retained, header, default)) { tampered.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch); }
        // Wrong recorded address with a different key: decrypt names the recorded address, so the envelope cannot redirect it.
        using (PayloadBackendResult mismatch = await backend.UnwrapAsync(Operation, Current, historical, default)) { mismatch.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch); }
        sidecar.Calls[^1].ShouldBe((false, Current));
        int calls = sidecar.Calls.Count;
        using (PayloadBackendResult outside = await backend.UnwrapAsync(Operation, Foreign, sidecar.Seal(Foreign, dek), default)) { outside.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch); }
        using (PayloadBackendResult exact = await backend.WrapExactAsync(Operation, Foreign, dek, default)) { exact.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch); }
        using (PayloadBackendResult unprefixed = await backend.UnwrapAsync(Operation, Retained, historical.AsMemory(1), default)) { unprefixed.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch); }
        using (PayloadBackendResult oversized = await backend.UnwrapAsync(Operation, Retained, Prefixed(2049), default)) { oversized.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch); }
        using (PayloadBackendResult shortDek = await backend.WrapAsync(Operation, new byte[31], default)) { shortDek.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch); }
        sidecar.Calls.Count.ShouldBe(calls);
        using PayloadBackendResult retained = await backend.WrapExactAsync(Operation, Retained, dek, default);
        retained.Failure.ShouldBe(PayloadBackendFailure.None); retained.ProviderKeyVersionId.ShouldBe(Retained);
        sidecar.Outputs.ShouldAllBe(buffer => buffer.All(b => b == 0));
        CryptographicOperations.ZeroMemory(dek);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task MalformedTruncatedOrUnverifiableWrapOutputIsConsistencyMismatchAndReturnsNoBytes(int fault)
    {
        var sidecar = new DaprCryptoTestSidecar(Current);
        sidecar.Fault = (encrypt, _, input, _) => Task.FromResult<byte[]?>(fault switch
        {
            0 when encrypt => [],
            1 when encrypt => Prefixed(2049),
            2 when encrypt => "dapr.io/enc/v2\nxxxxxxxx"u8.ToArray(),
            // The SDK returns whatever arrived before a stream failure: a prefix-valid but incomplete envelope fails verification.
            3 when !encrypt => [],
            _ => null,
        });
        using PayloadBackendResult result = await Backend(sidecar).WrapAsync(Operation, new byte[32], default);
        result.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch);
        Should.Throw<PayloadProtectionFormatException>(() => result.TakeBytes());
        sidecar.Inputs.ShouldAllBe(buffer => buffer.All(b => b == 0));
        sidecar.Outputs.ShouldAllBe(buffer => buffer.All(b => b == 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    public async Task UnwrapOutputThatIsNotExactly32BytesIsConsistencyMismatch(int length)
    {
        var sidecar = new DaprCryptoTestSidecar(Current) { Fault = (_, _, _, _) => Task.FromResult<byte[]?>(new byte[length]) };
        using PayloadBackendResult result = await Backend(sidecar).UnwrapAsync(Operation, Current, Prefixed(64), default);
        result.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch);
    }

    [Theory]
    [InlineData(StatusCode.PermissionDenied, (int)PayloadBackendFailure.Denied, false)]
    [InlineData(StatusCode.Unauthenticated, (int)PayloadBackendFailure.Denied, true)]
    [InlineData(StatusCode.Unavailable, (int)PayloadBackendFailure.Unavailable, false)]
    [InlineData(StatusCode.DeadlineExceeded, (int)PayloadBackendFailure.Unavailable, true)]
    [InlineData(StatusCode.ResourceExhausted, (int)PayloadBackendFailure.Unavailable, false)]
    [InlineData(StatusCode.Aborted, (int)PayloadBackendFailure.Unavailable, false)]
    [InlineData(StatusCode.Cancelled, (int)PayloadBackendFailure.Unavailable, true)]
    [InlineData(StatusCode.NotFound, (int)PayloadBackendFailure.ConsistencyMismatch, false)]
    [InlineData(StatusCode.Internal, (int)PayloadBackendFailure.ConsistencyMismatch, true)]
    [InlineData(StatusCode.Unknown, (int)PayloadBackendFailure.ConsistencyMismatch, false)]
    [InlineData(StatusCode.InvalidArgument, (int)PayloadBackendFailure.ConsistencyMismatch, false)]
    public async Task StructuralStatusAloneClassifiesDirectAndSdkWrappedFailuresWithoutMessageText(StatusCode status, int failure, bool wrapped)
    {
        var expected = (PayloadBackendFailure)failure;
        var rpc = new RpcException(new Status(status, Canary), Canary);
        var sidecar = new DaprCryptoTestSidecar(Current) { Fault = (_, _, _, _) => throw (wrapped ? new DaprException(Canary, rpc) : rpc) };
        var backend = Backend(sidecar);
        using PayloadBackendResult wrap = await backend.WrapAsync(Operation, new byte[32], default);
        using PayloadBackendResult unwrap = await backend.UnwrapAsync(Operation, Current, Prefixed(64), default);
        wrap.Failure.ShouldBe(expected); unwrap.Failure.ShouldBe(expected);
        wrap.ToString().ShouldBe(nameof(PayloadBackendResult)); wrap.ProviderKeyVersionId.ShouldBeNull();
        sidecar.Inputs.ShouldAllBe(buffer => buffer.All(b => b == 0));
    }

    [Fact]
    public async Task UnstructuredExceptionsAreConsistencyMismatchAndNonCallerCancellationIsUnavailable()
    {
        var sidecar = new DaprCryptoTestSidecar(Current) { Fault = (_, _, _, _) => throw new InvalidOperationException(Canary) };
        using (PayloadBackendResult unknown = await Backend(sidecar).WrapAsync(Operation, new byte[32], default)) { unknown.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch); }
        sidecar.Fault = (_, _, _, _) => throw new TaskCanceledException(Canary);
        using PayloadBackendResult cancelled = await Backend(sidecar).UnwrapAsync(Operation, Current, Prefixed(64), default);
        cancelled.Failure.ShouldBe(PayloadBackendFailure.Unavailable);
    }

    [Fact]
    public async Task ExpiryOfTheCombinedBudgetIsUnavailableAndClearsALateSuccessfulEnvelope()
    {
        var clock = new LifecycleTestClock();
        var sidecar = new DaprCryptoTestSidecar(Current);
        sidecar.Fault = (encrypt, _, _, _) => { if (!encrypt) { clock.Advance(TimeSpan.FromSeconds(11)); } return Task.FromResult<byte[]?>(null); };
        using PayloadBackendResult result = await new DaprCryptographyPayloadProtectionBackend(sidecar.Client, Options(), clock).WrapAsync(Operation, new byte[32], default);
        result.Failure.ShouldBe(PayloadBackendFailure.Unavailable);
        Should.Throw<PayloadProtectionFormatException>(() => result.TakeBytes());
        sidecar.Calls.Count.ShouldBe(2);
        sidecar.Outputs.ShouldAllBe(buffer => buffer.All(b => b == 0));
    }

    [Fact]
    public async Task CallerCancellationWaitsForTheSdkCallThenClearsOwnedBuffersBeforeRaisingCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sidecar = new DaprCryptoTestSidecar(Current);
        sidecar.Fault = async (_, _, _, _) => { entered.SetResult(); await release.Task; return null; };
        byte[] caller = Enumerable.Repeat((byte)0x42, 32).ToArray();
        Task<PayloadBackendResult> pending = Backend(sidecar).WrapAsync(Operation, caller, cancellation.Token).AsTask();
        await entered.Task; cancellation.Cancel();
        pending.IsCompleted.ShouldBeFalse();
        // The owned DEK copy handed to the SDK is not cleared while the SDK call is still in flight.
        sidecar.Inputs.Single().ShouldAllBe(b => b == 0x42);
        release.SetResult();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        sidecar.Inputs.ShouldAllBe(buffer => buffer.All(b => b == 0));
        sidecar.Outputs.ShouldAllBe(buffer => buffer.All(b => b == 0));
        sidecar.Calls.Count.ShouldBe(1);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => Backend(sidecar).UnwrapAsync(Operation, Current, Prefixed(64), cancelled.Token).AsTask());
        sidecar.Calls.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ReconcileIsAlwaysMissingWithoutTransportWhileCallerCancellationIsHonoured()
    {
        var sidecar = new DaprCryptoTestSidecar(Current);
        var backend = Backend(sidecar);
        using PayloadBackendResult result = await backend.ReconcileAsync(Operation, default);
        result.Failure.ShouldBe(PayloadBackendFailure.Missing);
        using PayloadBackendResult executed = await new PayloadBackendExecutor().ExecuteAsync(backend, ct => backend.ReconcileAsync(Operation, ct),
            allowRetry: false, operation: PayloadProtectionOperation.Reconcile);
        executed.Failure.ShouldBe(PayloadBackendFailure.Missing);
        sidecar.Calls.ShouldBeEmpty();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => backend.ReconcileAsync(Operation, cancelled.Token).AsTask());
        // Absence is affirmed only for an exact canonical operation identity.
        foreach (string? noncanonical in new[] { null, "", "not-a-ulid", Operation.ToLowerInvariant() })
        {
            Should.Throw<PayloadProtectionFormatException>(() => backend.ReconcileAsync(noncanonical!, default));
            using PayloadBackendResult refused = await new PayloadBackendExecutor().ExecuteAsync(backend, ct => backend.ReconcileAsync(noncanonical!, ct),
                allowRetry: false, operation: PayloadProtectionOperation.Reconcile);
            refused.Failure.ShouldNotBe(PayloadBackendFailure.Missing);
        }
        sidecar.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingReconcileReleasesAHalfOpenProbeWithoutReopeningOrClosingTheBreaker()
    {
        var clock = new LifecycleTestClock();
        var executor = new PayloadBackendExecutor(clock);
        var backend = Backend(new DaprCryptoTestSidecar(Current));
        int calls = 0;
        ValueTask<PayloadBackendResult> Fail(CancellationToken _) { calls++; return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Unavailable)); }
        for (int i = 0; i < 5; i++) { using PayloadBackendResult _ = await executor.ExecuteAsync(backend, Fail, allowRetry: false); }
        clock.Advance(TimeSpan.FromSeconds(30));
        using (PayloadBackendResult reconcile = await executor.ExecuteAsync(backend, ct => backend.ReconcileAsync(Operation, ct),
            allowRetry: false, operation: PayloadProtectionOperation.Reconcile)) { reconcile.Failure.ShouldBe(PayloadBackendFailure.Missing); }
        // Not reopened: the next request is admitted as the single half-open probe at the original interval.
        using (PayloadBackendResult probe = await executor.ExecuteAsync(backend, Fail, allowRetry: false)) { probe.Failure.ShouldBe(PayloadBackendFailure.Unavailable); }
        calls.ShouldBe(6);
        // Not closed: that failed probe reopens the breaker, so the next request is rejected without transport.
        using (PayloadBackendResult rejected = await executor.ExecuteAsync(backend, Fail, allowRetry: false)) { rejected.Failure.ShouldBe(PayloadBackendFailure.Unavailable); }
        calls.ShouldBe(6);
    }

    [Fact]
    public async Task MissingReconcileRecordsNoProviderFailureMeasurement()
    {
        var sidecar = new DaprCryptoTestSidecar(Current) { Fault = (_, _, _, _) => Task.FromResult<byte[]?>([]) };
        var backend = Backend(sidecar);
        int failures = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == PayloadProtectionDiagnostics.Name && instrument.Name == "eventstore.payload_protection.provider_failures") { current.EnableMeasurementEvents(instrument); }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => Interlocked.Increment(ref failures));
        listener.Start();
        var executor = new PayloadBackendExecutor();
        using (PayloadBackendResult reconcile = await executor.ExecuteAsync(backend, ct => backend.ReconcileAsync(Operation, ct),
            allowRetry: false, operation: PayloadProtectionOperation.Reconcile)) { reconcile.Failure.ShouldBe(PayloadBackendFailure.Missing); }
        failures.ShouldBe(0);
        // Control: a real provider failure on the same listener is measured.
        using (PayloadBackendResult wrap = await executor.ExecuteAsync(backend, ct => backend.WrapAsync(Operation, new byte[32], ct), allowRetry: false))
        { wrap.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch); }
        failures.ShouldBe(1);
    }

    [Fact]
    public void BackendOutputBoundIsPerSchemaAndUnknownSchemasAreRefused()
    {
        using (var v2 = new PayloadBackendResult(PayloadBackendFailure.None, new byte[2048], Current, keyRecordSchemaVersion: 2)) { v2.TakeBytes().Length.ShouldBe(2048); }
        byte[] oversizedV2 = Enumerable.Repeat((byte)7, 2049).ToArray();
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadBackendResult(PayloadBackendFailure.None, oversizedV2, Current, keyRecordSchemaVersion: 2));
        oversizedV2.ShouldAllBe(b => b == 0);
        byte[] v1 = Enumerable.Repeat((byte)7, 513).ToArray();
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadBackendResult(PayloadBackendFailure.None, v1));
        v1.ShouldAllBe(b => b == 0);
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], Current, keyRecordSchemaVersion: 3));
    }

    [Fact]
    public void ClosedOptionsLoadOnlyTheApprovedFieldsAndSelectTheThirdProfile()
    {
        PayloadProtectionOptions options = PayloadProtectionOptions.Load(Configuration());
        options.Backend.ShouldBe("DaprKubernetesCrypto");
        options.AuthorityProfile.ShouldBeSameAs(PayloadProtectionAuthorityProfile.DaprKubernetesCrypto);
        options.DaprKubernetesCrypto!.ComponentName.ShouldBe("kubernetes-crypto");
        options.DaprKubernetesCrypto.CurrentWriteKey.ShouldBe(Current);
        options.DaprKubernetesCrypto.RetainedKeys.ShouldBe([Retained]);
        options.DaprKubernetesCrypto.ToString().ShouldBe(nameof(DaprCryptographyPayloadProtectionOptions));
        options.PlatformCustody.ShouldBeNull();
        PayloadProtectionOptions.Load(Configuration(("DaprKubernetesCrypto:RetainedKeys:0", null))).DaprKubernetesCrypto!.RetainedKeys.ShouldBeEmpty();
        PayloadProtectionOptions disabled = PayloadProtectionOptions.Load(Configuration(("Mode", "Disabled")));
        disabled.Enabled.ShouldBeFalse(); disabled.StaleBackendConfiguration.ShouldBeTrue(); disabled.DaprKubernetesCrypto.ShouldBeNull();
        disabled.AuthorityProfile.ShouldBeSameAs(PayloadProtectionAuthorityProfile.Predecessor);
        // Only DaprKubernetesCrypto keys remain: that section alone marks the disabled registration stale.
        PayloadProtectionOptions staleSection = PayloadProtectionOptions.Load(Configuration(("Mode", "Disabled"), ("Backend", null)));
        staleSection.Backend.ShouldBeNull(); staleSection.StaleBackendConfiguration.ShouldBeTrue();
    }

    [Theory]
    [InlineData("DaprKubernetesCrypto:Endpoint", "https://private-sidecar")]
    [InlineData("DaprKubernetesCrypto:Credentials", Canary)]
    [InlineData("DaprKubernetesCrypto:ComponentName", "Kubernetes-Crypto")]
    [InlineData("DaprKubernetesCrypto:ComponentName", "")]
    [InlineData("DaprKubernetesCrypto:CurrentWriteKey", "kek-v2/key.pem")]
    [InlineData("DaprKubernetesCrypto:CurrentWriteKey", Retained)]
    [InlineData("DaprKubernetesCrypto:RetainedKeys:0", Current)]
    [InlineData("DaprKubernetesCrypto:RetainedKeys:2", "pp-keys/kek-v0/key.pem")]
    [InlineData("DaprKubernetesCrypto:RetainedKeys:0:Nested", "x")]
    [InlineData("DaprKubernetesCrypto:RetainedKeys", "pp-keys/kek-v0/key.pem")]
    [InlineData("Backend", "DaprKubernetes")]
    [InlineData("AzureKeyVault", Canary)]
    [InlineData("AuthorityDigest", "2c7c509921cf611cae5ec394c9ba093951d9e3693e8740f9a5894b6df915c20d")]
    public void ClosedOptionsRejectUnknownProviderPrivateAndNoncanonicalSettings(string field, string value)
    {
        Should.Throw<InvalidOperationException>(() => PayloadProtectionOptions.Load(Configuration((field, value)))).Message
            .ShouldBe("Payload protection options are invalid.");
    }

    [Fact]
    public void InventoryIsBoundedToSixtyFourDistinctRetainedAddressesAndUnknownFieldsFailEvenWhenDisabled()
    {
        var values = Enumerable.Range(0, 64).Select(i => ($"DaprKubernetesCrypto:RetainedKeys:{i}", (string?)$"pp-keys/kek-r{i}/key.pem")).ToArray();
        PayloadProtectionOptions.Load(Configuration(values)).DaprKubernetesCrypto!.RetainedKeys.Count.ShouldBe(64);
        Should.Throw<InvalidOperationException>(() => PayloadProtectionOptions.Load(Configuration([.. values, ("DaprKubernetesCrypto:RetainedKeys:64", "pp-keys/kek-r64/key.pem")])));
        Should.Throw<InvalidOperationException>(() => PayloadProtectionOptions.Load(Configuration([.. values[..2], ("DaprKubernetesCrypto:RetainedKeys:1", "pp-keys/kek-r0/key.pem")])));
        Should.Throw<InvalidOperationException>(() => PayloadProtectionOptions.Load(Configuration(("Mode", "Disabled"), ("DaprKubernetesCrypto:Token", Canary))));
        Should.Throw<InvalidOperationException>(() => PayloadProtectionOptions.Load(Configuration(("Mode", "Disabled"), ("DaprKubernetesCrypto:RetainedKeys:Token", Canary)))).Message
            .ShouldBe("Payload protection options are invalid.");
    }

    [Theory]
    [InlineData("a/b/c", true)]
    [InlineData("pp-keys/kek.v1.prod/KEY_1-a.pem", true)]
    [InlineData("pp-keys/kek-v1", false)]
    [InlineData("pp-keys/kek-v1/key.pem/extra", false)]
    [InlineData("PP-keys/kek-v1/key.pem", false)]
    [InlineData("-pp/kek-v1/key.pem", false)]
    [InlineData("pp-/kek-v1/key.pem", false)]
    [InlineData("pp/kek..v1/key.pem", false)]
    [InlineData("pp/.kek/key.pem", false)]
    [InlineData("pp/kek_v1/key.pem", false)]
    [InlineData("pp/kek-v1/key pem", false)]
    [InlineData("pp/kek-v1/key/pem", false)]
    [InlineData("pp/kek-v1/", false)]
    [InlineData("pp/kek-v1/kéy", false)]
    public void KeyAddressGrammarIsExactlyNamespaceSecretNameAndDataKey(string address, bool valid)
    {
        if (valid) { DaprCryptographyPayloadProtectionOptions.ValidateKeyAddress(address); }
        else { Should.Throw<PayloadProtectionFormatException>(() => DaprCryptographyPayloadProtectionOptions.ValidateKeyAddress(address)); }
    }

    [Fact]
    public void KeyAddressLengthBoundsMatchKubernetesSegmentsAndTheWholeAddress()
    {
        string maximum = new string('n', 63) + "/" + new string('s', 128) + "/" + new string('d', 63);
        maximum.Length.ShouldBe(256);
        DaprCryptographyPayloadProtectionOptions.ValidateKeyAddress(maximum);
        foreach (string invalid in new[] { new string('n', 64) + "/s/d", "n/" + new string('s', 129) + "/d", "n/s/" + new string('d', 64), "n/s/", "a/b" })
        {
            Should.Throw<PayloadProtectionFormatException>(() => DaprCryptographyPayloadProtectionOptions.ValidateKeyAddress(invalid));
        }
    }

    [Fact]
    public void EnabledDaprKubernetesCryptoRemainsBlockedBeforeAnyHostedServiceOrClientResolution()
    {
        var services = new ServiceCollection();
        bool resolved = false;
        services.AddSingleton<DaprClient>(_ => { resolved = true; throw new InvalidOperationException(); });
        Should.Throw<InvalidOperationException>(() => services.AddEventStorePayloadProtection(Configuration())).Message
            .ShouldBe("Payload protection startup admission is unavailable.");
        services.ShouldNotContain(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)); resolved.ShouldBeFalse();
    }

    private static DaprCryptographyPayloadProtectionBackend Backend(DaprCryptoTestSidecar sidecar) => new(sidecar.Client, Options());
    private static DaprCryptographyPayloadProtectionOptions Options() => new("kubernetes-crypto", Current, [Retained]);
    private static byte[] Prefixed(int length)
    {
        byte[] bytes = new byte[length];
        WrappedDekV2.SchemePrefix.CopyTo(bytes);
        return bytes;
    }

    private static PayloadProtectionTestConfiguration Configuration(params (string Key, string? Value)[] replacements)
    {
        var values = new Dictionary<string, string?>
        {
            ["Mode"] = "Enabled", ["Backend"] = "DaprKubernetesCrypto", ["DaprKubernetesCrypto:ComponentName"] = "kubernetes-crypto",
            ["DaprKubernetesCrypto:CurrentWriteKey"] = Current, ["DaprKubernetesCrypto:RetainedKeys:0"] = Retained,
        };
        foreach ((string key, string? value) in replacements)
        {
            if (value is null) { values.Remove(key); } else { values[key] = value; }
        }

        return new(values);
    }
}

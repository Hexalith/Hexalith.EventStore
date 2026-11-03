// Approved Azure exception sections 6-7; actual service/network observations, never synthetic response proof.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests;
/// <summary>Observes externally prepared isolated faults within exact approved windows; this suite never performs fault/admin mutations.</summary>
[Collection("Authorized Azure service")]
public sealed class AzurePreparedFaultTests(AzureDaprFixture fixture) : IClassFixture<AzureDaprFixture>
{
    [Fact]
    public async Task V085_V087_RealPreparedFaultWindowsHaveActualDispatchStatusAndRetainedDaprEndState()
    {
        fixture.Authority.RequirePhase("prepared-faults");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(40));
        var token = deadline.Token;
        await using var env = await AzureLiveEnvironment.CreateAsync(fixture, "prepared-faults", token);
        var write = await env.Reservations.ProtectEventAsync("{\"secret\":\"authorized-live-canary\"}"u8.ToArray(), ["/secret"], env.Context(), token);
        await env.Reservations.AcquireCompletionAsync(write.Completion!, token);
        await fixture.Client.SaveByteStateAsync("statestore", "azure-conformance:event:" + env.Scope, write.Result.PayloadBytes, cancellationToken: token);
        await env.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted, token);
        WrappedDekV1 before = await env.KeyAsync(write.Completion!, token);
        foreach (AzureFaultWindow window in fixture.Authority.FaultWindows.OrderBy(window => window.NotBeforeUtc))
        {
            TimeSpan wait = window.NotBeforeUtc - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, token);
            }

            (DateTimeOffset.UtcNow < window.NotAfterUtc).ShouldBeTrue("The operator-prepared fault window must still be active.");
            var options = fixture.Authority.Options();
            var credential = AzureKeyVaultCredentialFactory.Create(options);
            var observed = new AzureObservedTransport(new AzureKeyVaultSafeTransport(options));
            var backend = new AzureKeyVaultPayloadProtectionBackend(options, new(options, credential, observed), new(options, credential, observed));
            var executor = new PayloadBackendExecutor();
            Task? abandoned = null;
            using PayloadBackendResult result = await executor.ExecuteAsync(backend, ct => backend.WrapExactAsync(new CryptographicPayloadProtectionEntropy().CreateKeyReference(), fixture.Authority.RotationVersion, new byte[32], ct), token, allowRetry: false, retainAbandoned: task => abandoned = task);
            if (abandoned is not null)
            {
                await abandoned.WaitAsync(TimeSpan.FromSeconds(5), token);
            }

            observed.Calls.ShouldBeGreaterThan(0);
            if (window.Fault == "429")
            {
                observed.CryptoStatuses.ShouldContain(429);
                result.Failure.ShouldBe(PayloadBackendFailure.Unavailable);
                for (int failure = 1; failure < 5; failure++)
                {
                    using var transient = await executor.ExecuteAsync(backend, ct => backend.WrapExactAsync(new CryptographicPayloadProtectionEntropy().CreateKeyReference(), fixture.Authority.RotationVersion, new byte[32], ct), token, allowRetry: false);
                    transient.Failure.ShouldBe(PayloadBackendFailure.Unavailable);
                }

                int beforeOpen = observed.Calls;
                using var open = await executor.ExecuteAsync(backend, ct => backend.WrapExactAsync(new CryptographicPayloadProtectionEntropy().CreateKeyReference(), fixture.Authority.RotationVersion, new byte[32], ct), token);
                open.Failure.ShouldBe(PayloadBackendFailure.Unavailable);
                observed.Calls.ShouldBe(beforeOpen);
            }
            else if (window.Fault == "5xx")
            {
                observed.CryptoStatuses.Any(status => status is >= 500 and <= 599).ShouldBeTrue();
                result.Failure.ShouldBe(PayloadBackendFailure.Ambiguous);
            }
            else if (window.Fault == "timeout")
            {
                observed.CryptoDispatched.ShouldBeTrue();
                observed.CryptoStatuses.ShouldBeEmpty();
                result.Failure.ShouldBe(PayloadBackendFailure.Ambiguous);
            }
            else
            {
                observed.CryptoDispatched.ShouldBeFalse();
                observed.CryptoStatuses.ShouldBeEmpty();
                result.Failure.ShouldBe(PayloadBackendFailure.Unavailable);
            }

            (DateTimeOffset.UtcNow <= window.NotAfterUtc).ShouldBeTrue("The complete observation must be bound to the approved window.");
            Console.WriteLine($"Prepared real fault={window.Fault}; calls={observed.Calls}; dispatched={observed.CryptoDispatched}; outcome={result.Failure}.");
            TimeSpan recovery = window.NotAfterUtc - DateTimeOffset.UtcNow;
            if (recovery > TimeSpan.Zero)
            {
                await Task.Delay(recovery, token);
            }

            if (window.Fault == "429")
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token);
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Task<PayloadBackendResult> probe = executor.ExecuteAsync(backend, async ct =>
                {
                    entered.SetResult();
                    await release.Task.WaitAsync(ct);
                    return await backend.WrapExactAsync(new CryptographicPayloadProtectionEntropy().CreateKeyReference(), fixture.Authority.RotationVersion, new byte[32], ct);
                }, token).AsTask();
                await entered.Task;
                int calls = observed.Calls;
                for (int contender = 0; contender < 99; contender++)
                {
                    using var blocked = await executor.ExecuteAsync(backend, ct => backend.WrapExactAsync(new CryptographicPayloadProtectionEntropy().CreateKeyReference(), fixture.Authority.RotationVersion, new byte[32], ct), token);
                    blocked.Failure.ShouldBe(PayloadBackendFailure.Unavailable);
                }

                observed.Calls.ShouldBe(calls);
                release.SetResult();
                using var recovered = await probe;
                recovered.Failure.ShouldBe(PayloadBackendFailure.None);
            }

            (await env.KeyAsync(write.Completion!, token)).ShouldBe(before);
            await env.RestartAsync(token);
            await env.AssertReadableAsync(write.Result.PayloadBytes, token);
        }
    }
}

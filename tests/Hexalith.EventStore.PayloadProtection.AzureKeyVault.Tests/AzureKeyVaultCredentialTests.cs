// Approved Azure exception sections 5-6; Core correction's combined authentication budget requirement.
using Azure.Core;
using Azure.Identity;
using Azure;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
public sealed class AzureKeyVaultCredentialTests
{
    [Fact]
    public async Task V081_RefreshHintPreservesActualExpiryAndForcesForegroundRefresh()
    {
        DateTimeOffset expiry = DateTimeOffset.UtcNow.AddMinutes(2);
        var credential = new AdapterTestCredential
        {
            Acquire = (_, _) => ValueTask.FromResult(new AccessToken("near-expiry-canary", expiry))
        };
        TokenCredential bounded = AzureKeyVaultCredentialFactory.Bound(credential);
        AccessToken token = await bounded.GetTokenAsync(new TokenRequestContext(["https://vault.azure.net/.default"]), CancellationToken.None);
        token.ExpiresOn.ShouldBe(expiry);
        token.RefreshOn.ShouldBe(expiry);
        token.Token.ShouldBe("near-expiry-canary");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V081_NearExpiryCryptoAndMetadataDoNotStartBackgroundCredentialWork(bool metadata)
    {
        DateTimeOffset expiry = DateTimeOffset.UtcNow.AddSeconds(1);
        var credential = new AdapterTestCredential
        {
            Acquire = (_, _) => ValueTask.FromResult(new AccessToken("near-expiry-canary", expiry))
        };
        var transport = new AdapterTestTransport(message =>
        {
            if (metadata && !message.Request.Headers.Contains("Authorization"))
            {
                message.Response = AdapterFixture.Response(401, [], new(StringComparer.OrdinalIgnoreCase) { ["WWW-Authenticate"] = "Bearer authorization=\"https://login.microsoftonline.com/00000000-0000-0000-0000-000000000001\", resource=\"https://vault.azure.net\"" });
            }
            else
            {
                message.Response = AdapterFixture.Response(200, metadata ? AdapterFixture.Metadata() : AdapterFixture.Crypto(AdapterFixture.Current, new byte[384]));
            }

            return ValueTask.CompletedTask;
        });
        var client = new AzureKeyVaultMetadataClient(AdapterFixture.Options, credential, transport);
        var data = new AzureKeyVaultDataPlane(AdapterFixture.Options, credential, transport);
        async Task CallAsync(CancellationToken token)
        {
            if (metadata)
            {
                _ = await client.GetAsync(null, token);
            }
            else
            {
                using var result = await data.ExecuteAsync(AdapterFixture.Current, new byte[32], true, token);
                result.Failure.ShouldBe(PayloadBackendFailure.None);
            }
        }

        await CallAsync(CancellationToken.None);
        await CallAsync(CancellationToken.None);
        credential.Count.ShouldBe(1);
        await Task.Delay(TimeSpan.FromMilliseconds(1100));
        bool receivedCancellation = false;
        credential.Acquire = async (_, token) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            catch (OperationCanceledException)
            {
                receivedCancellation = token.IsCancellationRequested;
                throw;
            }

            throw new InvalidOperationException("Unreachable token path.");
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Should.ThrowAsync<OperationCanceledException>(() => CallAsync(cancellation.Token));
        credential.Count.ShouldBe(2);
        receivedCancellation.ShouldBeTrue();
    }

    [Theory]
    [InlineData("socket")]
    [InlineData("io")]
    [InlineData("timeout")]
    [InlineData("transport")]
    public void V081_WrappedPreDispatchTokenTransientsRemainUnavailable(string kind)
    {
        Exception inner = kind switch
        {
            "socket" => new System.Net.Sockets.SocketException(),
            "io" => new IOException(),
            "timeout" => new OperationCanceledException(),
            _ => new AzureKeyVaultTransportException(PayloadBackendFailure.Unavailable)};
        AzureKeyVaultFailureClassifier.Exception(new AuthenticationFailedException("private-canary", inner), false, true).ShouldBe(PayloadBackendFailure.Unavailable);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public void V080_WrappedIdentityDenialRemainsPermanent(int status) => AzureKeyVaultFailureClassifier.Exception(new AuthenticationFailedException("private-canary", new RequestFailedException(status, "private-canary")), false, true).ShouldBe(PayloadBackendFailure.Denied);
    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(501)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    [InlineData(599)]
    public void V081_ActualMsalServiceStatusIsClassifiedWithoutText(int status)
    {
        var exception = new AuthenticationFailedException("private-canary", new Microsoft.Identity.Client.MsalServiceException("private-code", "private-message", status));
        AzureKeyVaultFailureClassifier.Exception(exception, false, true).ShouldBe(PayloadBackendFailure.Unavailable);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("closed")]
    [InlineData("cancelled")]
    public async Task V081_MissingClosedOrCancelledAcquisitionCannotAuthorizePhysicalIo(string state)
    {
        if (state == "missing")
        {
            Should.Throw<AzureKeyVaultTransportException>(() => AzureKeyVaultTokenAcquisition.RequireSingleRequest()).Failure.ShouldBe(PayloadBackendFailure.Unavailable);
            return;
        }

        using var cancellation = new CancellationTokenSource();
        if (state == "cancelled")
        {
            using var acquisition = new AzureKeyVaultTokenAcquisition(cancellation.Token);
            cancellation.Cancel();
            Should.Throw<OperationCanceledException>(() => AzureKeyVaultTokenAcquisition.RequireSingleRequest());
            return;
        }

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task delayed;
        using (var acquisition = new AzureKeyVaultTokenAcquisition(CancellationToken.None))
        {
            delayed = Task.Run(async () =>
            {
                await release.Task;
                Should.Throw<AzureKeyVaultTransportException>(() => AzureKeyVaultTokenAcquisition.RequireSingleRequest()).Failure.ShouldBe(PayloadBackendFailure.Unavailable);
            });
        }

        release.SetResult();
        await delayed;
    }
}

// Approved Azure exception section 6: prove real pinned managed-identity SDK retries cannot multiply physical token requests.
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Azure.Core;
using Hexalith.EventStore.PayloadProtection.AzureKeyVault.Conformance;
using Hexalith.EventStore.Testing.Security;
using Azure.Identity;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
[Collection("Azure diagnostic observers")]
public sealed class AzureManagedIdentityRetryTests
{
    [Fact]
    public async Task V081_ManagedIdentityHasOnePhysicalRequestPerAcquisition()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_AZURE_IDENTITY_RETRY_CHILD") == "true")
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var capture = new AzureDiagnosticObserver();
            using var diagnostics = System.Diagnostics.DiagnosticListener.AllListeners.Subscribe(capture);
            EventHandler<FirstChanceExceptionEventArgs> observer = (_, args) => capture.Record("FirstChance:" + args.Exception.ToString());
            AppDomain.CurrentDomain.FirstChanceException += observer;
            try
            {
                const string clientId = "11111111-2222-3333-4444-555555555555";
                using var selectedIdentity = new AzureIdentityDiagnosticScope(clientId);
                var credential = AzureKeyVaultCredentialFactory.Create(new() { VaultUri = AdapterFixture.Options.VaultUri, KeyName = AdapterFixture.Options.KeyName, ManagedIdentityClientId = clientId });
                var exception = await Should.ThrowAsync<AuthenticationFailedException>(() => credential.GetTokenAsync(new TokenRequestContext(["https://vault.azure.net/.default"]), deadline.Token).AsTask());
                AzureKeyVaultFailureClassifier.Exception(exception, false, true).ShouldBe(PayloadBackendFailure.Unavailable);
                ProtectedDataLeakSentinel.AssertNoLeak(capture.Values);
                try
                {
                    string summary = AzureIdentityDiagnosticPolicy.AssertNativeEvidence(capture.Records, [clientId],
                        ["unit-identity-header", AdapterFixture.Options.VaultUri.AbsoluteUri, AdapterFixture.Options.VaultUri.Host,
                            AdapterFixture.Options.KeyName, AdapterFixture.Current, AdapterFixture.Historical, ..ProtectedDataLeakSentinel.All()]);
                    Console.Error.WriteLine("SDK diagnostic matching categories: " + summary);
                }
                catch (InvalidOperationException failure)
                {
                    // The shared policy exposes only a closed category and fingerprint.
                    Console.Error.WriteLine("SDK diagnostic matching categories: " + failure.Message);
                    throw;
                }
                return;
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= observer;
            }
        }

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        int requests = 0;
        Task server = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    using TcpClient peer = await listener.AcceptTcpClientAsync(lifetime.Token);
                    Interlocked.Increment(ref requests);
                    using NetworkStream stream = peer.GetStream();
                    byte[] request = new byte[8192];
                    _ = await stream.ReadAsync(request, lifetime.Token);
                    await stream.WriteAsync("HTTP/1.1 503 Unavailable\r\nContent-Length: 24\r\n\r\n{\"error\":\"server_error\"}"u8.ToArray(), lifetime.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("-method");
            start.ArgumentList.Add(GetType().FullName + ".V081_ManagedIdentityHasOnePhysicalRequestPerAcquisition");
            start.Environment["HEXALITH_AZURE_IDENTITY_RETRY_CHILD"] = "true";
            start.Environment["IDENTITY_ENDPOINT"] = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/msi/token";
            start.Environment["IDENTITY_HEADER"] = "unit-identity-header";
            start.Environment.Remove("MSI_ENDPOINT");
            start.Environment.Remove("MSI_SECRET");
            start.Environment.Remove("IMDS_ENDPOINT");
            using Process child = Process.Start(start)!;
            Task<string> output = child.StandardOutput.ReadToEndAsync(lifetime.Token);
            Task<string> error = child.StandardError.ReadToEndAsync(lifetime.Token);
            try
            {
                await child.WaitForExitAsync(lifetime.Token);
            }
            finally
            {
                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync();
                }
            }

            _ = await output;
            string childError = await error;
            string categorySummary = childError.Split('\n').FirstOrDefault(line => line.StartsWith("SDK diagnostic matching categories:", StringComparison.Ordinal)) ?? "";
            child.ExitCode.ShouldBe(0, "The isolated pinned managed-identity subprocess must pass its typed failure check. " + categorySummary);
            requests.ShouldBe(1);
            TestContext.Current.TestOutputHelper?.WriteLine(categorySummary + "; physical-requests=" + requests);
        }
        finally
        {
            lifetime.Cancel();
            listener.Stop();
            await server;
        }
    }
}

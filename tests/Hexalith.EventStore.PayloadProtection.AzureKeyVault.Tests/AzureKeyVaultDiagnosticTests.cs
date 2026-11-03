// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V127-V132.
using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Azure.Core.Pipeline;
using Azure.Core;
using Hexalith.EventStore.Testing.Security;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
[Collection("Azure diagnostic observers")]
public sealed class AzureKeyVaultDiagnosticTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(403)]
    public async Task V127_V132_OwnedTransportDoesNotEmitHttpSdkActivityMetricLogOrExceptionCanaries(int status)
    {
        string bearerCanary = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        using var capture = new AzureDiagnosticObserver();
        using var diagnostics = DiagnosticListener.AllListeners.Subscribe(capture);
        using var activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("Azure.", StringComparison.Ordinal) || source.Name.StartsWith("System.Net.Http", StringComparison.Ordinal) || source.Name == "Hexalith.EventStore.PayloadProtection",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => capture.Record(activity.DisplayName + string.Join(";", activity.Tags) + string.Join(";", activity.Events.Select(item => item.ToString()))),
        };
        ActivitySource.AddActivityListener(activities);
        using var metrics = new MeterListener();
        metrics.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name.Contains("Http", StringComparison.Ordinal) || instrument.Meter.Name.Contains("Azure", StringComparison.Ordinal))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        metrics.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            foreach (var tag in tags)
            {
                capture.Record(instrument.Name + tag.Key + tag.Value);
            }
        });
        metrics.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            foreach (var tag in tags)
            {
                capture.Record(instrument.Name + tag.Key + tag.Value);
            }
        });
        metrics.Start();
        using var logs = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(new AdapterLogProvider(capture.Record)));
        EventHandler<FirstChanceExceptionEventArgs> exceptionObserver = (_, args) => capture.Record(args.Exception.ToString());
        AppDomain.CurrentDomain.FirstChanceException += exceptionObserver;
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                byte[] body = Encoding.UTF8.GetBytes(ProtectedDataLeakSentinel.ProtectedProviderExceptionText);
                Task server = Task.Run(async () =>
                {
                    using TcpClient peer = await listener.AcceptTcpClientAsync();
                    using NetworkStream stream = peer.GetStream();
                    byte[] receive = new byte[4096];
                    _ = await stream.ReadAsync(receive);
                    byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " OK\r\nContent-Length: " + body.Length + "\r\n\r\n");
                    await stream.WriteAsync(head);
                    await stream.WriteAsync(body);
                    CryptographicOperations.ZeroMemory(receive);
                });
                var transport = new AzureKeyVaultSafeTransport(AdapterFixture.Options, identity: true);
                var options = new KeyClientOptionsForPipeline();
                AzureKeyVaultCredentialFactory.Configure(options, transport);
                var pipeline = HttpPipelineBuilder.Build(options);
                using HttpMessage message = pipeline.CreateMessage();
                message.BufferResponse = false;
                message.Request.Method = RequestMethod.Post;
                message.Request.Uri.Reset(new Uri("http://127.0.0.1:" + port + "/keys/key-name-canary/version-canary/wrapkey"));
                message.Request.Content = new AzureKeyVaultOwnedRequestContent(Encoding.UTF8.GetBytes(ProtectedDataLeakSentinel.ProtectedPayloadPlaintext));
                message.Request.Headers.SetValue("Authorization", "Bearer " + bearerCanary);
                if (status == 200)
                {
                    using var acquisition = new AzureKeyVaultTokenAcquisition(CancellationToken.None);
                    await pipeline.SendAsync(message, CancellationToken.None);
                    message.Response.Status.ShouldBe(status);
                    message.Response.Dispose();
                }
                else
                {
                    (await Should.ThrowAsync<AzureKeyVaultTransportException>(() => SendScopedAsync(pipeline, message))).Failure.ShouldBe(PayloadBackendFailure.Denied);
                }

                await server;
            }
            finally
            {
                listener.Stop();
            }
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= exceptionObserver;
        }

        _ = capture.AssertSafe([], [..ProtectedDataLeakSentinel.All(), "key-name-canary", "version-canary", "request-id-canary", AdapterFixture.Current, AdapterFixture.Options.VaultUri.Host]);
        ProtectedDataLeakSentinel.AssertNoLeak(capture.Values);
        foreach (string value in capture.Values)
        {
            value.Contains("key-name-canary", StringComparison.Ordinal).ShouldBeFalse("key-name category");
            value.Contains("version-canary", StringComparison.Ordinal).ShouldBeFalse("version category");
            value.Contains(bearerCanary, StringComparison.Ordinal).ShouldBeFalse("access-token category");
            value.Contains(AdapterFixture.Current, StringComparison.Ordinal).ShouldBeFalse("provider-key URI category");
            value.Contains(AdapterFixture.Options.VaultUri.Host, StringComparison.Ordinal).ShouldBeFalse("vault-host category");
        }
    }

    [Fact]
    public async Task V127_MetadataFailureIsSanitizedBeforeSdkConstructsExceptions()
    {
        using var capture = new AzureDiagnosticObserver();
        EventHandler<FirstChanceExceptionEventArgs> observer = (_, args) => capture.Record(args.Exception.ToString());
        AppDomain.CurrentDomain.FirstChanceException += observer;
        try
        {
            var transport = new AdapterTestTransport(message =>
            {
                byte[] raw = Encoding.ASCII.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: " + ProtectedDataLeakSentinel.ProtectedProviderExceptionText.Length + "\r\nx-ms-request-id: request-id-canary\r\n\r\n" + ProtectedDataLeakSentinel.ProtectedProviderExceptionText);
                message.Response = AzureKeyVaultSafeTransport.ParseResponse(raw);
                CryptographicOperations.ZeroMemory(raw);
                return ValueTask.CompletedTask;
            });
            var client = new AzureKeyVaultMetadataClient(AdapterFixture.Options, new AdapterTestCredential(AdapterFixture.CreateToken()), transport);
            await Should.ThrowAsync<Azure.RequestFailedException>(() => client.GetAsync(null, CancellationToken.None).AsTask());
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= observer;
        }

        _ = capture.AssertSafe([], [..ProtectedDataLeakSentinel.All(), "key-name-canary", "version-canary", "request-id-canary", AdapterFixture.Current, AdapterFixture.Options.VaultUri.Host]);
        ProtectedDataLeakSentinel.AssertNoLeak(capture.Values);
        capture.Values.Any(value => value.Contains("request-id-canary", StringComparison.Ordinal)).ShouldBeFalse("provider request-ID category");
    }

    private static async Task SendScopedAsync(HttpPipeline pipeline, HttpMessage message)
    {
        using var acquisition = new AzureKeyVaultTokenAcquisition(CancellationToken.None);
        await pipeline.SendAsync(message, CancellationToken.None);
    }

    /// <summary>Both transport consumers use this same actual observer gate before their no-leak assertions.</summary>
    [Theory]
    [InlineData("opaque")]
    [InlineData("throwing-getter")]
    [InlineData("unsupported")]
    public void ActualConsumerRefusesOpaqueOrUnreadableProtectedPayload(string kind)
    {
        string canary = ProtectedDataLeakSentinel.ProtectedPayloadPlaintext;
        object payload = kind switch
        {
            "opaque" => new OpaqueDiagnosticPayload(canary),
            "throwing-getter" => new ThrowingDiagnosticPayload(canary),
            _ => new { Text = canary, Unsupported = (Action)(() => { }) },
        };
        using var capture = new AzureDiagnosticObserver();
        capture.OnNext(new KeyValuePair<string, object?>("Payload", payload));
        var error = Should.Throw<InvalidOperationException>(() => capture.AssertSafe([], ProtectedDataLeakSentinel.All()));
        error.Message.Contains(canary, StringComparison.Ordinal).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => ProtectedDataLeakSentinel.AssertNoLeak(capture.Values));
    }

}

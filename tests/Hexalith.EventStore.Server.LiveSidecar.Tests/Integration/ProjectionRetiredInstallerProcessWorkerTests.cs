using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Executes only as an explicitly selected exited-installer or replacement-recovery worker.</summary>
public sealed class ProjectionRetiredInstallerProcessWorkerTests
{
    [Fact]
    public async Task InstallOrRecoverExactRetiredParticipant()
    {
        string? input = Environment.GetEnvironmentVariable("REVIEW4_INSTALLER_INPUT");
        if (input is null) { Assert.Skip("Launched only by the real-provider retired-installer controller."); return; }
        string endpoint = Environment.GetEnvironmentVariable("REVIEW4_INSTALLER_ENDPOINT")!;
        string tenant = Environment.GetEnvironmentVariable("REVIEW4_INSTALLER_TENANT")!;
        string phase = Environment.GetEnvironmentVariable("REVIEW4_INSTALLER_PHASE")!;
        int ordinal = int.Parse(Environment.GetEnvironmentVariable("REVIEW4_INSTALLER_ORDINAL")!, System.Globalization.CultureInfo.InvariantCulture);
        byte[] manifest = await File.ReadAllBytesAsync(input + ".manifest");
        var scope = new ReadModelBatchScope("statestore", tenant, "widget", "review4", "review4", "retained");
        ReadModelBatch batch = ReadModelBatchManifestCodec.Decode(manifest, scope, null, null,
            ReadModelBatchFingerprint.ComputeFromManifest(manifest), retainManifestForRecovery: true);
        using DaprClient client = new DaprClientBuilder().UseGrpcEndpoint(endpoint).Build();
        string key = batch.Operations[ordinal].Key;
        if (phase == "install")
        {
            var accessor = new ReviewFourLateInstallerAccessor(new DaprReadModelBatchStateAccessor(client, scope.StoreName), key, async token =>
            {
                await File.WriteAllTextAsync(input + ".ready", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), token);
                while (!File.Exists(input + ".release")) await Task.Delay(25, token);
            });
            ReadModelBatchResult result = await new ReadModelBatchProtocol(accessor, new(), NullLogger.Instance).ExecuteAsync(batch, null, TestContext.Current.CancellationToken);
            result.IsSuccess.ShouldBeFalse();
        }
        else
        {
            var store = new DaprReadModelStore(client);
            (await store.RecoverPendingAsync(scope, key)).IsResolved.ShouldBeTrue();
            (await store.RecoverPendingAsync(scope, key)).IsResolved.ShouldBeTrue();
        }
        RawStateEntry row = await new DaprReadModelBatchStateAccessor(client, scope.StoreName).ReadAsync(key, default);
        TestContext.Current.TestOutputHelper!.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        { phase, pid = Environment.ProcessId, ordinal, exists = row.Exists, raw_base64 = Convert.ToBase64String(row.Value.Span) }));
    }
}

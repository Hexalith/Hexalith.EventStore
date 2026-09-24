using System.Diagnostics;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Microsoft.AspNetCore.DataProtection;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Proves the read model and exact gapped watermark survive a domain worker process restart.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class ProjectionWatermarkProcessRestartTests(DaprTestContainerFixture fixture) {
    [Fact]
    [Trait("Tier", "3")]
    public async Task PersistedEventHistory_RebuildInNewProcess_ConvergesReadModelAndWatermark() {
        fixture.ThrowIfHostStopped();
        string unique = Guid.NewGuid().ToString("N");
        string tenantId = $"p2-{unique}";
        string aggregateId = $"widget-{unique}";
        string historyKey = $"{tenantId}:widget:{aggregateId}:events";
        string stateKey = $"{tenantId}:widget:{aggregateId}:watermark";
        EventEnvelope[] history = [Event(tenantId, aggregateId, 1, 101),
            Event(tenantId, aggregateId, 2, 104), Event(tenantId, aggregateId, 3, 109)];

        using DaprClient client = new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
        await client.SaveStateAsync(
            "statestore", historyKey, history, cancellationToken: TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        (await client.GetStateAsync<EventEnvelope[]>(
            "statestore", historyKey, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ShouldNotBeNull()
            .Select(static value => value.GlobalPosition)
            .ShouldBe([101L, 104L, 109L]);

        int firstPid = await RunWorkerAsync(
            "initial", fixture.DaprGrpcEndpoint, historyKey, stateKey, tenantId, aggregateId).ConfigureAwait(true);
        var store = new DaprReadModelStore(client);
        ProjectionWatermarkProcessState beforeRestart = (await store
            .GetAsync<ProjectionWatermarkProcessState>("statestore", stateKey, TestContext.Current.CancellationToken)
            .ConfigureAwait(true)).Value.ShouldNotBeNull();
        beforeRestart.ShouldBe(new ProjectionWatermarkProcessState(3, 109));

        IQueryCursorCodec codec = new QueryCursorCodec(
            new EphemeralDataProtectionProvider(), "Hexalith.EventStore.Tests.ProcessWatermark.v1");
        string scope = QueryCursorScope.Create()
            .Add("tenant", tenantId)
            .AddProjectionWatermark(beforeRestart.Watermark)
            .Build();
        string cursor = codec.Encode("list-widgets", scope, aggregateId);

        int rebuiltPid = await RunWorkerAsync(
            "rebuild", fixture.DaprGrpcEndpoint, historyKey, stateKey, tenantId, aggregateId).ConfigureAwait(true);
        rebuiltPid.ShouldNotBe(firstPid);
        ProjectionWatermarkProcessState afterRestart = (await store
            .GetAsync<ProjectionWatermarkProcessState>("statestore", stateKey, TestContext.Current.CancellationToken)
            .ConfigureAwait(true)).Value.ShouldNotBeNull();
        afterRestart.ShouldBe(beforeRestart);
        (await client.GetStateAsync<EventEnvelope[]>(
            "statestore", historyKey, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ShouldNotBeNull()
            .Select(static value => value.GlobalPosition)
            .ShouldBe([101L, 104L, 109L]);

        codec.TryDecode(cursor, "list-widgets", scope, out string? position, out string? failure)
            .ShouldBeTrue();
        position.ShouldBe(aggregateId);
        failure.ShouldBeNull();
        string otherTenantScope = QueryCursorScope.Create()
            .Add("tenant", "other-tenant")
            .AddProjectionWatermark(afterRestart.Watermark)
            .Build();
        codec.TryDecode(cursor, "list-widgets", otherTenantScope, out string? deniedPosition, out string? deniedFailure)
            .ShouldBeFalse();
        deniedPosition.ShouldBeNull();
        deniedFailure.ShouldBe("wrong-scope");
    }

    private static EventEnvelope Event(string tenantId, string aggregateId, long sequence, long position) => new(
        MessageId: $"message-{sequence}",
        AggregateId: aggregateId,
        AggregateType: "Widget",
        TenantId: tenantId,
        Domain: "widget",
        SequenceNumber: sequence,
        GlobalPosition: position,
        Timestamp: DateTimeOffset.UnixEpoch.AddSeconds(sequence),
        CorrelationId: "correlation-1",
        CausationId: "causation-1",
        UserId: "user-1",
        DomainServiceVersion: "v1",
        EventTypeName: "WidgetChanged",
        MetadataVersion: 1,
        SerializationFormat: "json",
        Payload: [(byte)sequence],
        Extensions: null);

    private static async Task<int> RunWorkerAsync(
        string phase,
        string endpoint,
        string historyKey,
        string stateKey,
        string tenantId,
        string aggregateId) {
        var start = new ProcessStartInfo("dotnet") {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(typeof(ProjectionWatermarkProcessWorkerTests).Assembly.Location);
        start.ArgumentList.Add("-class");
        start.ArgumentList.Add(typeof(ProjectionWatermarkProcessWorkerTests).FullName!);
        start.Environment["P2_WATERMARK_PHASE"] = phase;
        start.Environment["P2_WATERMARK_DAPR_ENDPOINT"] = endpoint;
        start.Environment["P2_WATERMARK_HISTORY_KEY"] = historyKey;
        start.Environment["P2_WATERMARK_STATE_KEY"] = stateKey;
        start.Environment["P2_WATERMARK_TENANT"] = tenantId;
        start.Environment["P2_WATERMARK_AGGREGATE"] = aggregateId;

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException($"Unable to launch the {phase} projection worker.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromMinutes(2)).ConfigureAwait(true);
        string output = await outputTask.ConfigureAwait(true);
        string error = await errorTask.ConfigureAwait(true);
        process.ExitCode.ShouldBe(0, $"{phase} worker failed. stdout: {output}; stderr: {error}");
        return process.Id;
    }
}

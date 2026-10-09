using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Exercises closure-review boundaries without importing later-story behavior.</summary>
[Collection(DiagnosticsCollection.Name)]
public sealed class ClosureRegressionTests
{
    /// <summary>Initial observer publication faults cannot poison later diagnostics or affect core outcomes.</summary>
    [Theory]
    [InlineData("activity")]
    [InlineData("meter")]
    public async Task DiagnosticPublicationFailureIsRetryableAsync(string kind)
    {
        const string probeVariable = "HEXALITH_STORY83_DIAGNOSTIC_STARTUP_PROBE";
        string? probeKind = Environment.GetEnvironmentVariable(probeVariable);
        if (probeKind is not null && probeKind != kind)
        {
            return;
        }

        if (probeKind is null)

        {
            var start = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(typeof(ClosureRegressionTests).Assembly.Location);
            start.ArgumentList.Add("-method");
            start.ArgumentList.Add($"{typeof(ClosureRegressionTests).FullName}.{nameof(DiagnosticPublicationFailureIsRetryableAsync)}");
            start.ArgumentList.Add("-noColor");
            start.Environment[probeVariable] = kind;
            using Process process = Process.Start(start)!;
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            process.ExitCode.ShouldBe(0, await stdout + await stderr);
            return;
        }

        if (kind == "activity")

        {
            using var faulty = new ActivityListener
            {
                ShouldListenTo = source => source.Name == PayloadProtectionDiagnostics.Name
                    ? throw new InvalidOperationException("test observer") : false,
            };
            ActivitySource.AddActivityListener(faulty);
            _ = TestFixture.Protect();
        }
        else
        {
            using var faulty = new MeterListener
            {
                InstrumentPublished = (instrument, _) =>
                {
                    if (instrument.Meter.Name == PayloadProtectionDiagnostics.Name)
                    {
                        throw new InvalidOperationException("test observer");
                    }
                },
            };
            faulty.Start();
            _ = TestFixture.Protect();
        }

        long samples = 0;
        int activities = 0;
        using var healthyActivity = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PayloadProtectionDiagnostics.Name,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = _ => activities++,
        };
        ActivitySource.AddActivityListener(healthyActivity);
        using var healthyMeter = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == PayloadProtectionDiagnostics.Name)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        healthyMeter.SetMeasurementEventCallback<long>((_, value, _, _) => samples += value);
        healthyMeter.Start();
        CoreProtectionResult result = TestFixture.Protect();
        (await TestFixture.UnprotectAsync(result.PayloadBytes)).IsReadable.ShouldBeTrue();
        samples.ShouldBe(2);
        activities.ShouldBe(2);
    }

    /// <summary>Authenticated snapshots cannot replay into another aggregate scope or sequence.</summary>
    [Theory]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    [InlineData("sequence")]
    public async Task SnapshotScopeSubstitutionFailsAtomicallyAsync(string field)
    {
        ProtectedSnapshotPayloadV2 snapshot = TestFixture.ProtectSnapshot();
        PayloadProtectionContext context = TestFixture.SnapshotContext();
        context = field switch
        {
            "tenant" => context with { Identity = new AggregateIdentity("other", "parties", "party-01") },
            "domain" => context with { Identity = new AggregateIdentity("tenant-a", "other", "party-01") },
            "aggregate" => context with { Identity = new AggregateIdentity("tenant-a", "parties", "other") },
            _ => context with { RecordSequence = 2 },
        };
        byte[] key = TestFixture.Dek();
        RecordingBufferObserver observer = new();
        CoreUnprotectionResult read = await TestFixture.UnprotectSnapshotAsync(snapshot, context,
            observer: observer, keyResolver: (_, _, _) => ValueTask.FromResult<byte[]?>(key));
        read.PayloadBytes.ShouldBeNull();
        read.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        key.ShouldAllBe(static value => value == 0);
        observer.Observed.ShouldContain(SensitiveBufferKind.DecryptedPlaintext);
    }

    /// <summary>The immutable snapshot byte ceiling round-trips, and its first excess never requests material.</summary>
    [Fact]
    public async Task SnapshotImmutableByteMaximumRoundTripsAsync()
    {
        byte[] payload = Encoding.UTF8.GetBytes("\"" + new string('x', PayloadProtectionLimits.CiphertextBytes - 2) + "\"");
        ProtectedSnapshotPayloadV2 snapshot = TestFixture.ProtectSnapshot(payload);
        CoreUnprotectionResult read = await TestFixture.UnprotectSnapshotAsync(snapshot);
        read.PayloadBytes.ShouldBe(payload);
        int calls = 0;
        byte[] excess = Encoding.UTF8.GetBytes("\"" + new string('x', PayloadProtectionLimits.CiphertextBytes - 1) + "\"");
        Should.Throw<PayloadProtectionFormatException>(() => TestFixture.ProtectSnapshot(excess,
            materialFactory: () => { calls++; return TestFixture.Material(); }));
        calls.ShouldBe(0);
    }

    /// <summary>Snapshot readers accept exact shared structure maxima; first-over-limit input fails before material.</summary>
    [Theory]
    [InlineData("depth", false)]
    [InlineData("depth", true)]
    [InlineData("nodes", false)]
    [InlineData("nodes", true)]
    public async Task SnapshotStructuralMaximumIsExactAsync(string dimension, bool excess)
    {
        int count = dimension == "depth" ? PayloadProtectionLimits.JsonDepth : PayloadProtectionLimits.JsonNodes - 1;
        count += excess ? 1 : 0;
        string json = dimension == "depth"
            ? new string('[', count) + "0" + new string(']', count)
            : "[" + string.Join(',', Enumerable.Repeat("0", count)) + "]";
        byte[] payload = Encoding.UTF8.GetBytes(json);
        int calls = 0;
        ProtectedSnapshotPayloadV2 Protect() => TestFixture.ProtectSnapshot(payload,
            materialFactory: () => { calls++; return TestFixture.Material(); });
        if (excess)
        {
            Should.Throw<PayloadProtectionFormatException>(() => Protect());
            calls.ShouldBe(0);
            return;
        }

        ProtectedSnapshotPayloadV2 snapshot = Protect();
        (await TestFixture.UnprotectSnapshotAsync(snapshot)).PayloadBytes.ShouldBe(payload);
        calls.ShouldBe(1);
    }

    /// <summary>Correct tags do not permit duplicate members or trailing JSON to escape either reader.</summary>
    [Theory]
    [InlineData("event", "{\"x\":1,\"x\":2}")]
    [InlineData("event", "0 1")]
    [InlineData("snapshot", "{\"x\":1,\"x\":2}")]
    [InlineData("snapshot", "0 1")]
    public async Task AuthenticatedMalformedPlaintextIsClearedAsync(string kind, string json)
    {
        bool snapshot = kind == "snapshot";
        PayloadProtectionContext context = snapshot ? TestFixture.SnapshotContext() : TestFixture.Context();
        string path = snapshot ? string.Empty : "/email";
        ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create([path], snapshot: snapshot);
        byte[] aad = AadCodec.Write(context, path, TestFixture.KeyReference, 1, 0, manifest.Commitment);
        PayloadProtectionEnvelope envelope = PayloadCryptography.Encrypt(Encoding.UTF8.GetBytes(json), aad,
            TestFixture.Dek(), TestFixture.KeyReference, 1, 0);
        string carrier = Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope));
        byte[] key = TestFixture.Dek();
        RecordingBufferObserver observer = new();
        CoreUnprotectionResult read = snapshot
            ? await TestFixture.UnprotectSnapshotAsync(new ProtectedSnapshotPayloadV2("json+pdenc-v2", context.PayloadTypeId, carrier),
                observer: observer, keyResolver: (_, _, _) => ValueTask.FromResult<byte[]?>(key))
            : await TestFixture.UnprotectAsync(TestFixture.WrapperPayloadBytes(carrier),
                observer: observer, keyResolver: (_, _, _) => ValueTask.FromResult<byte[]?>(key));
        read.PayloadBytes.ShouldBeNull();
        read.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        key.ShouldAllBe(static value => value == 0);
        observer.Observed.ShouldContain(SensitiveBufferKind.DecryptedPlaintext);
    }

    /// <summary>Wide core writer cancellation is observed before material even at later periodic checkpoints.</summary>
    [Theory]
    [InlineData(512)]
    [InlineData(768)]
    public void WideWriterCancellationPrecedesMaterial(int target)
    {
        byte[] payload = Encoding.UTF8.GetBytes("[" + string.Join(',', Enumerable.Repeat("0", 1024)) + "]");
        int calls = 0;
        using var cancellation = new CancellationTokenSource();
        OperationCanceledException exception = Should.Throw<OperationCanceledException>(() => new PayloadProtectionCore().ProtectEvent(
            payload, ["/0"], TestFixture.Context(), () => { calls++; return TestFixture.Material(); },
            cancellationToken: cancellation.Token, traversalCheckpoint: count =>
            {
                if (count == target)
                {
                    cancellation.Cancel();
                }
            }));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        calls.ShouldBe(0);
    }

    /// <summary>Concurrent operations cancel inside core traversal and clear their owned snapshots without lookup.</summary>
    [Fact]
    public async Task ConcurrentCoreCancellationClearsEveryOwnedInputAsync()
    {
        using var barrier = new Barrier(8);
        using var cancellation = new CancellationTokenSource();
        int calls = 0;
        Task[] operations = [.. Enumerable.Range(0, 8).Select(_ => Task.Factory.StartNew(async () =>
        {
            RecordingBufferObserver observer = new();
            OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
                await TestFixture.UnprotectAsync(TestFixture.WrapperPayloadBytes(), observer: observer,
                    cancellationToken: cancellation.Token, keyResolver: (_, _, _) =>
                    {
                        Interlocked.Increment(ref calls);
                        return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
                    }, traversalCheckpoint: count =>
                    {
                        if (count == 1)
                        {
                            barrier.SignalAndWait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                            cancellation.Cancel();
                        }
                    }));
            exception.CancellationToken.ShouldBe(cancellation.Token);
            observer.Observed.ShouldContain(SensitiveBufferKind.InputSnapshot);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap())];
        await Task.WhenAll(operations);
        calls.ShouldBe(0);
    }
}

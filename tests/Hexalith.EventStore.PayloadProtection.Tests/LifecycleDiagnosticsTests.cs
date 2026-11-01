// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 8-10, 13-15; lifecycle amendment sections 1-8.
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>V127-V128 engine mechanics emit only closed instruments and constructive failures without provider/policy detail.</summary>
[Collection(DiagnosticsCollection.Name)]
public sealed class LifecycleDiagnosticsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryMechanicsMetricRestoresAmbientActivityAfterListenerMutation(bool throws)
    {
        using var parent = new Activity("parent").Start();
        using var replacement = new Activity("listener-replacement").Start();
        Activity.Current = parent;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        { if (instrument.Meter.Name == PayloadProtectionDiagnostics.Name) { current.EnableMeasurementEvents(instrument); } };
        int callbacks = 0;
        listener.SetMeasurementEventCallback<long>((_, _, _, _) =>
        {
            callbacks++; Activity.Current = replacement;
            if (throws) { throw new InvalidOperationException("untrusted-observer"); }
        });
        listener.Start();
        Action[] records = [() => PayloadProtectionDiagnostics.ProviderFailure("fixture", PayloadBackendFailure.Unavailable),
            () => PayloadProtectionDiagnostics.CacheAccess(true), () => PayloadProtectionDiagnostics.CacheAccess(false),
            () => PayloadProtectionDiagnostics.CacheEntry(true), () => PayloadProtectionDiagnostics.LifecycleTransition()];
        foreach (Action record in records) { record(); Activity.Current.ShouldBeSameAs(parent); }
        callbacks.ShouldBe(5);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LateEventAndSnapshotAuthorityFailuresEmitUnavailableWithoutPlaintext(bool snapshot, bool release)
    {
        var protectedSnapshot = TestFixture.ProtectSnapshot();
        byte[] protectedEvent = TestFixture.WrapperPayloadBytes();
        var results = new List<string?>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        { if (instrument.Meter.Name == PayloadProtectionDiagnostics.Name && instrument.Name == "eventstore.payload_protection.operations") { current.EnableMeasurementEvents(instrument); } };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => results.Add(tags.ToArray().Single(tag => tag.Key == "result").Value as string));
        listener.Start();
        var observer = new RecordingBufferObserver();
        ValueTask FailAsync() => ValueTask.FromException(new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable));
        ValueTask<PayloadKeyResolution> Resolve(string _, uint __, CancellationToken ___) => ValueTask.FromResult(PayloadKeyResolution.Success(TestFixture.Dek(),
            authenticated: release ? null : _ => FailAsync(), releasedAsync: release ? FailAsync : null));
        var core = new PayloadProtectionCore(observer);
        var result = snapshot ? await core.TryUnprotectSnapshotAsync(protectedSnapshot, TestFixture.SnapshotContext(), Resolve)
            : await core.TryUnprotectEventAsync(protectedEvent, TestFixture.Context(), Resolve);
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        result.PayloadBytes.ShouldBeNull(); results.ShouldBe(["unavailable"]);
        observer.Observed.ShouldContain(SensitiveBufferKind.DecryptedPlaintext);
        observer.Observed.ShouldContain(SensitiveBufferKind.DataEncryptionKey);
    }

    [Fact]
    public async Task BackendAndCacheDiagnosticsExcludeKeyAndProviderCanaries()
    {
        const string canary = "https://private-provider.invalid/secret-version-91fa09";
        var activities = new List<Activity>();
        var measurements = new List<(string Instrument, KeyValuePair<string, object?>[] Tags)>();
        using var activityListener = new ActivityListener {
            ShouldListenTo = source => source.Name == PayloadProtectionDiagnostics.Name,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(activityListener);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) => { if (instrument.Meter.Name == PayloadProtectionDiagnostics.Name) { current.EnableMeasurementEvents(instrument); } };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => measurements.Add((instrument.Name, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => measurements.Add((instrument.Name, tags.ToArray())));
        listener.Start();
        var backend = new LifecycleTestBackend { Wrap = (_, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Denied, providerKeyVersionId: canary)) };
        using (PayloadBackendResult denied = await new PayloadBackendExecutor().ExecuteAsync(backend, token => backend.WrapAsync("01J00000000000000000000501", new byte[32], token)))
        {
            denied.Failure.ShouldBe(PayloadBackendFailure.Denied);
        }
        await using (var cache = new PayloadKeyCache())
        {
            var key = new PayloadKeyCacheKey(new('a', 64), "01J00000000000000000000502", 1, 1, canary);
            using (PayloadKeyResolution first = await cache.GetAsync(key, _ => ValueTask.FromResult(PayloadKeyResolution.Success(new byte[32])))) { }
            using (PayloadKeyResolution second = await cache.GetAsync(key, _ => throw new InvalidOperationException())) { }
        }
        activities.Select(a => a.OperationName).ShouldBe(["EventStore.PayloadProtection.Key.Wrap"]);
        activities.ShouldAllBe(a => !a.TagObjects.Any() && !a.Baggage.Any() && !a.Events.Any() && a.StatusDescription == null);
        string[] allowedNames = ["eventstore.payload_protection.operations", "eventstore.payload_protection.duration", "eventstore.payload_protection.provider_failures",
            "eventstore.payload_protection.key_cache_hits", "eventstore.payload_protection.key_cache_misses", "eventstore.payload_protection.key_cache_entries", "eventstore.payload_protection.lifecycle_transitions"];
        string[] allowedTags = ["operation", "result", "reason_code", "backend_id", "format_version", "cache_result"];
        foreach (var measurement in measurements)
        {
            allowedNames.ShouldContain(measurement.Instrument);
            foreach (KeyValuePair<string, object?> tag in measurement.Tags) { allowedTags.ShouldContain(tag.Key); tag.Value?.ToString().ShouldNotContain(canary); }
        }
        measurements.ShouldContain(m => m.Instrument.EndsWith("provider_failures", StringComparison.Ordinal));
        measurements.ShouldContain(m => m.Instrument.EndsWith("key_cache_hits", StringComparison.Ordinal));
        measurements.ShouldContain(m => m.Instrument.EndsWith("key_cache_misses", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AuditPermissionExceptionIsConstructiveAndPrecedesDurableAdmission()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        var lifecycle = new KeyLifecycleCoordinator(fixture.Store, new([new("tenant-a", "parties")]), fixture.Leases, cache,
            fixture.Backend.BackendId, _ => throw new InvalidOperationException("policy-private-canary-349eaa"), "fixture-actor", fixture.Clock);
        int commits = fixture.Store.Commits;
        PayloadProtectionStateException exception = await Should.ThrowAsync<PayloadProtectionStateException>(() => lifecycle.BeginAsync(fixture.Identity,
            LifecycleDrainTests.Approval(fixture.Identity), "01J00000000000000000000503", 2).AsTask());
        exception.Reason.ShouldBe(UnreadableProtectedDataReason.ProviderDenied);
        exception.InnerException.ShouldBeNull(); exception.Data.Count.ShouldBe(0);
        exception.ToString().ShouldNotContain("policy-private-canary-349eaa");
        fixture.Store.Commits.ShouldBe(commits); fixture.Backend.WrapCalls.ShouldBe(0);
    }
}

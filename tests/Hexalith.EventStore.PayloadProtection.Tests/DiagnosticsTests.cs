using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Verifies the constructive Story 8.3 diagnostic surface without claiming later-story V127/V128 evidence.
/// </summary>
public sealed class DiagnosticsTests {
    /// <summary>Verifies the exact source/activity names and absence of payload identity tags.</summary>
    [Fact]
    public void Activities_ExposeOnlyClosedCoreNames() {
        var observed = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener {
            ShouldListenTo = static source => source.Name == PayloadProtectionDiagnostics.Name,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = observed.Add,
        };
        ActivitySource.AddActivityListener(listener);

        _ = TestFixture.Protect();

        observed.ShouldContain(activity => activity.OperationName == "EventStore.PayloadProtection.Protect");
        observed.All(activity => activity.OperationName is
            "EventStore.PayloadProtection.Protect" or "EventStore.PayloadProtection.Unprotect").ShouldBeTrue();
        observed.All(static activity => activity.Source.Name == PayloadProtectionDiagnostics.Name).ShouldBeTrue();
        observed.All(static activity => activity.Kind == ActivityKind.Internal).ShouldBeTrue();
        observed.All(static activity => !activity.TagObjects.Any()
            && !activity.Events.Any()
            && !activity.Links.Any()
            && !activity.Baggage.Any()).ShouldBeTrue();
    }

    /// <summary>Verifies metrics use only the closed low-cardinality tag allowlist.</summary>
    [Fact]
    public async Task Metrics_ExposeOnlyClosedLowCardinalityTagsAsync() {
        var observedTags = new ConcurrentBag<KeyValuePair<string, object?>>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) => {
            if (instrument.Meter.Name == PayloadProtectionDiagnostics.Name) {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => {
            foreach (KeyValuePair<string, object?> tag in tags) {
                observedTags.Add(tag);
            }
        });
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) => {
            foreach (KeyValuePair<string, object?> tag in tags) {
                observedTags.Add(tag);
            }
        });
        listener.Start();

        _ = TestFixture.Protect();
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            "{\"malformed-canary\":"u8.ToArray(),
            ["/value"],
            TestFixture.Context(),
            TestFixture.Material));
        using (var cancelled = new CancellationTokenSource()) {
            cancelled.Cancel();
            Should.Throw<OperationCanceledException>(() => new PayloadProtectionCore().ProtectEvent(
                "{\"value\":1}"u8.ToArray(),
                ["/value"],
                TestFixture.Context(),
                TestFixture.Material,
                cancellationToken: cancelled.Token));
        }

        byte[] changedEnvelope = Convert.FromHexString(TestFixture.EnvelopeHex);
        changedEnvelope[^1] ^= 1;
        _ = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(Base64UrlCodec.Encode(changedEnvelope)));
        _ = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(),
            keyResolver: (_, _, _) => throw new InvalidOperationException("provider-secret-canary"));

        listener.Dispose();
        observedTags.ShouldNotBeEmpty();
        observedTags.Select(static tag => tag.Key).Distinct().Order().ShouldBe(
            new[] { "format_version", "operation", "result" });
        string rendered = string.Join('|', observedTags.Select(static tag => $"{tag.Key}={tag.Value}"));
        rendered.ShouldNotContain("tenant-a");
        rendered.ShouldNotContain("alice@example.com");
        rendered.ShouldNotContain(TestFixture.KeyReference);
        rendered.ShouldNotContain("malformed-canary");
        rendered.ShouldNotContain("provider-secret-canary");
        observedTags.Where(static tag => tag.Key == "operation").Select(static tag => tag.Value).Distinct()
            .Except(new object?[] { "protect", "unprotect" }).ShouldBeEmpty();
        observedTags.Where(static tag => tag.Key == "format_version").Select(static tag => tag.Value).Distinct()
            .ShouldBe(new object?[] { "v2" });
        object?[] results = observedTags.Where(static tag => tag.Key == "result").Select(static tag => tag.Value).Distinct().ToArray();
        results.Except(new object?[] { "success", "malformed", "authentication-failed", "cancelled", "unavailable" })
            .ShouldBeEmpty();
        results.ShouldContain("success");
        results.ShouldContain("malformed");
        results.ShouldContain("authentication-failed");
        results.ShouldContain("cancelled");
        results.ShouldContain("unavailable");
    }

    /// <summary>Verifies the execution manifest assigns exactly the inherited and Story 8.3-owned vector set.</summary>
    [Fact]
    public void ExecutionManifest_ContainsExactlyTheAuthorizedVectors() {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "vector-execution.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        string[] inherited = document.RootElement.GetProperty("inherited").EnumerateArray()
            .Select(static value => value.GetString()!).ToArray();
        string[] owned = document.RootElement.GetProperty("owned").EnumerateArray()
            .Select(static value => value.GetString()!).ToArray();
        inherited.ShouldBe(new[] { "V001", "V002", "V003" });
        owned.Length.ShouldBe(48);
        owned.Distinct(StringComparer.Ordinal).Count().ShouldBe(48);
        owned.ShouldContain("V004");
        owned.ShouldContain("V048");
        owned.ShouldContain("V135");
        owned.ShouldContain("V136");
        owned.ShouldContain("V138");
        owned.ShouldNotContain("V137");
    }

    /// <summary>Verifies cleared selected plaintext, decrypted plaintext, and transferred DEK buffers are observed as zero.</summary>
    [Fact]
    public async Task OwnedSensitiveBuffers_AreClearedOnProtectAndUnprotectAsync() {
        RecordingBufferObserver observer = new();
        CoreProtectionResult protectedResult = TestFixture.Protect(observer);
        CoreUnprotectionResult unprotected = await TestFixture.UnprotectAsync(protectedResult.PayloadBytes, observer: observer);
        unprotected.IsReadable.ShouldBeTrue();
        observer.Observed.ShouldContain(SensitiveBufferKind.SelectedPlaintext);
        observer.Observed.Count(kind => kind == SensitiveBufferKind.DataEncryptionKey).ShouldBe(2);
        observer.Observed.ShouldContain(SensitiveBufferKind.DecryptedPlaintext);
    }

    /// <summary>Verifies authentication failure clears and reports its unobserved destination buffer.</summary>
    [Fact]
    public async Task AuthenticationFailure_ClearsUnobservedPlaintextDestinationAsync() {
        byte[] envelope = Convert.FromHexString(TestFixture.EnvelopeHex);
        envelope[^1] ^= 1;
        RecordingBufferObserver observer = new();
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(Base64UrlCodec.Encode(envelope)),
            observer: observer);
        result.IsReadable.ShouldBeFalse();
        observer.Observed.ShouldContain(SensitiveBufferKind.DecryptedPlaintext);
        observer.Observed.ShouldContain(SensitiveBufferKind.DataEncryptionKey);
    }

    /// <summary>Verifies actual core exception messages and typed failures never retain hostile input or key material.</summary>
    [Fact]
    public async Task CoreFailureSurfaces_UseOnlyClosedMessagesAndReasonsAsync() {
        const string payloadCanary = "payload-secret-canary";
        const string providerCanary = "provider-secret-canary";
        PayloadProtectionFormatException malformed = Should.Throw<PayloadProtectionFormatException>(
            () => BoundedJsonDocument.Parse(Encoding.UTF8.GetBytes("{\"" + payloadCanary + "\":"), default));
        malformed.Message.ShouldBe("The protected payload is malformed or exceeds a supported limit.");
        malformed.Message.ShouldNotContain(payloadCanary);

        byte[] changedTag = TestFixture.Envelope().Tag.ToArray();
        changedTag[0] ^= 1;
        PayloadProtectionAuthenticationException authentication = Should.Throw<PayloadProtectionAuthenticationException>(
            () => PayloadCryptography.Decrypt(
                TestFixture.Envelope() with { Tag = changedTag },
                TestFixture.Aad(),
                TestFixture.Dek()));
        authentication.Message.ShouldBe("The protected payload could not be authenticated.");
        authentication.Message.ShouldNotContain(TestFixture.KeyReference);

        CoreUnprotectionResult unavailable = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(),
            keyResolver: (_, _, _) => throw new InvalidOperationException(providerCanary));
        unavailable.PayloadBytes.ShouldBeNull();
        unavailable.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        unavailable.ToString().ShouldNotContain(providerCanary);
        unavailable.ToString().ShouldNotContain(TestFixture.KeyReference);
    }
}

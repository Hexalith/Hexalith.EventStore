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
[Collection(DiagnosticsCollection.Name)]
public sealed class DiagnosticsTests
{
    /// <summary>Verifies both exact source/activity names and absence of payload identity tags.</summary>
    [Fact]
    public async Task Activities_ExposeOnlyClosedCoreNamesAsync()
    {
        var observed = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == PayloadProtectionDiagnostics.Name,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = observed.Add,
        };
        ActivitySource.AddActivityListener(listener);

        _ = TestFixture.Protect();
        _ = await TestFixture.UnprotectAsync(TestFixture.WrapperPayloadBytes());

        observed.Select(static activity => activity.OperationName).Order().ShouldBe([
            "EventStore.PayloadProtection.Protect",
            "EventStore.PayloadProtection.Unprotect",
        ]);
        observed.All(static activity => activity.Source.Name == PayloadProtectionDiagnostics.Name).ShouldBeTrue();
        observed.All(static activity => activity.Kind == ActivityKind.Internal).ShouldBeTrue();
        observed.All(static activity => !activity.TagObjects.Any()
            && !activity.Events.Any()
            && !activity.Links.Any()
            && !activity.Baggage.Any()).ShouldBeTrue();
    }

    /// <summary>Verifies both instruments emit exact closed tags for every constructible core outcome.</summary>
    [Fact]
    public async Task Metrics_ExposeOnlyClosedLowCardinalityTagsAsync()
    {
        var measurements = new ConcurrentBag<(
            string Meter,
            string Instrument,
            KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == PayloadProtectionDiagnostics.Name)
            {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            measurements.Add((instrument.Meter.Name, instrument.Name, tags.ToArray()));
        });
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
        {
            measurements.Add((instrument.Meter.Name, instrument.Name, tags.ToArray()));
        });
        listener.Start();

        _ = TestFixture.Protect();
        _ = await TestFixture.UnprotectAsync(TestFixture.WrapperPayloadBytes());
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            "{\"malformed-canary\":"u8.ToArray(),
            ["/value"],
            TestFixture.Context(),
            TestFixture.Material));
        using (var cancelled = new CancellationTokenSource())
        {
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
        _ = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(),
            keyResolver: (_, _, _) => ValueTask.FromResult<byte[]?>(null));
        _ = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(),
            keyResolver: (_, _, _) => ValueTask.FromResult<byte[]?>(new byte[31]));
        Should.Throw<PayloadProtectionCryptographicException>(() => new PayloadProtectionCore().ProtectEvent(
            "{\"value\":1}"u8.ToArray(),
            ["/value"],
            TestFixture.Context(),
            () => new PayloadProtectionMaterial("invalid", 1, TestFixture.Dek())));

        listener.Dispose();
        string[] instruments = [
            "eventstore.payload_protection.duration",
            "eventstore.payload_protection.operations",
        ];
        var expectedOutcomes = new (string Operation, string Result)[] {
            ("protect", "success"),
            ("protect", "malformed"),
            ("protect", "cancelled"),
            ("protect", "cryptographic-failure"),
            ("unprotect", "success"),
            ("unprotect", "authentication-failed"),
            ("unprotect", "unavailable"),
            ("unprotect", "missing-key"),
            ("unprotect", "consistency-mismatch"),
        };

        measurements.Count.ShouldBe(expectedOutcomes.Length * instruments.Length);
        measurements.Select(static measurement => measurement.Meter).Distinct().ShouldBe([
            PayloadProtectionDiagnostics.Name,
        ], ignoreOrder: true);
        measurements.Select(static measurement => measurement.Instrument).Distinct().ShouldBe(
            instruments,
            ignoreOrder: true);
        foreach ((string meter, string instrument, KeyValuePair<string, object?>[] tags) in measurements)
        {
            meter.ShouldBe(PayloadProtectionDiagnostics.Name);
            instruments.ShouldContain(instrument);
            tags.Length.ShouldBe(3);
            tags.Select(static tag => tag.Key).Order().ShouldBe(["format_version", "operation", "result"]);
            tags.Single(static tag => tag.Key == "format_version").Value.ShouldBe("v2");
        }

        foreach ((string operation, string result) in expectedOutcomes)
        {
            foreach (string instrument in instruments)
            {
                measurements.Count(measurement => measurement.Instrument == instrument
                    && measurement.Tags.Single(static tag => tag.Key == "operation").Value as string == operation
                    && measurement.Tags.Single(static tag => tag.Key == "result").Value as string == result)
                    .ShouldBe(1);
            }
        }

        string rendered = string.Join('|', measurements.SelectMany(static measurement => measurement.Tags)
            .Select(static tag => $"{tag.Key}={tag.Value}"));
        rendered.ShouldNotContain("tenant-a");
        rendered.ShouldNotContain("alice@example.com");
        rendered.ShouldNotContain(TestFixture.KeyReference);
        rendered.ShouldNotContain("malformed-canary");
        rendered.ShouldNotContain("provider-secret-canary");
    }

    /// <summary>Verifies the execution manifest assigns exactly the inherited and Story 8.3-owned vector set.</summary>
    [Fact]
    public void ExecutionManifest_ContainsExactlyTheAuthorizedVectors()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "vector-execution.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        string[] inherited = [.. document.RootElement.GetProperty("inherited").EnumerateArray().Select(static value => value.GetString()!)];
        string[] owned = [.. document.RootElement.GetProperty("owned").EnumerateArray().Select(static value => value.GetString()!)];
        inherited.ShouldBe(["V001", "V002", "V003"]);
        owned.Length.ShouldBe(48);
        owned.Distinct(StringComparer.Ordinal).Count().ShouldBe(48);
        owned.ShouldContain("V004");
        owned.ShouldContain("V048");
        owned.ShouldContain("V135");
        owned.ShouldContain("V136");
        owned.ShouldContain("V138");
        owned.ShouldNotContain("V137");

        string[] expected = [.. inherited.Concat(owned).OrderBy(ParseVectorNumber)];
        string[] discovered = [.. typeof(DiagnosticsTests).Assembly.GetTypes()
            .SelectMany(static type => type.GetMethods())
            .SelectMany(static method => method.CustomAttributes)
            .Where(static attribute => attribute.AttributeType.Name == "TraitAttribute"
                && attribute.ConstructorArguments.Count == 2
                && string.Equals(attribute.ConstructorArguments[0].Value as string, "Vector", StringComparison.Ordinal))
            .Select(static attribute => (string)attribute.ConstructorArguments[1].Value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ParseVectorNumber)];
        discovered.ShouldBe(expected);

        using JsonDocument ownership = TestFixture.ReadFrozenFixture("vector-ownership.json");
        JsonElement storyAssignment = ownership.RootElement.GetProperty("assignments").EnumerateArray()
            .Single(static assignment => assignment.GetProperty("ownerStory").GetString() == "8.3"
                && assignment.GetProperty("first").GetInt32() == 4);
        storyAssignment.GetProperty("last").GetInt32().ShouldBe(48);
        ownership.RootElement.GetProperty("normativeDigest").GetString().ShouldBe(
            document.RootElement.GetProperty("normativeDigest").GetString());
    }

    /// <summary>Verifies cleared selected plaintext, decrypted plaintext, and transferred DEK buffers are observed as zero.</summary>
    [Fact]
    public async Task OwnedSensitiveBuffers_AreClearedOnProtectAndUnprotectAsync()
    {
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
    public async Task AuthenticationFailure_ClearsUnobservedPlaintextDestinationAsync()
    {
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
    public async Task CoreFailureSurfaces_UseOnlyClosedMessagesAndReasonsAsync()
    {
        const string payloadCanary = "payload-secret-canary";
        const string providerCanary = "provider-secret-canary";
        PayloadProtectionFormatException malformed = Should.Throw<PayloadProtectionFormatException>(
            () => BoundedJsonDocument.Parse(Encoding.UTF8.GetBytes("{\"" + payloadCanary + "\":"), default));
        malformed.Message.ShouldBe("The protected payload is malformed or exceeds a supported limit.");
        malformed.Message.ShouldNotContain(payloadCanary);

        byte[] changedTag = [.. TestFixture.Envelope().Tag];
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

    private static int ParseVectorNumber(string vector)
        => int.Parse(vector.AsSpan(1), System.Globalization.CultureInfo.InvariantCulture);
}

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class ProjectionDeliveryFingerprintTests {
    private static readonly AggregateIdentity Identity = new("tenant-a", "sales", "order-42");

    [Fact]
    public void GoldenVectors_PinInitialEventAndTwoEventPrefix() {
        ProjectionEventDto first = Event(1, "01J00000000000000000000001", [0, 1, 2, 255]);
        ProjectionEventDto second = Event(2, "01J00000000000000000000002", [9, 8, 7]);

        string initial = ProjectionDeliveryFingerprint.ComputeInitial(Identity, "order-detail");
        string firstEvent = ProjectionDeliveryFingerprint.ComputeEvent(first);
        string firstPrefix = ProjectionDeliveryFingerprint.Extend(initial, firstEvent);
        string secondPrefix = ProjectionDeliveryFingerprint.Extend(
            firstPrefix,
            ProjectionDeliveryFingerprint.ComputeEvent(second));

        initial.ShouldBe("v1:HlKnBaSL_aNNfyEf_S2nRGFlMN1Vu8Ygd2qBtmLk3nY");
        firstEvent.ShouldBe("v1:AUjNqnwuth5YdRi6izvDAoGruJoA1jxoineWchtRqyI");
        secondPrefix.ShouldBe("v1:slbApP68YKBkQeUPd-v2O9utWvGc6U2678tt0chYqMg");
    }

    [Fact]
    public void ValidateHistory_RejectsMissingIdentityAndNonContiguousInput() {
        ProjectionEventDto missingIdentity = Event(1, " ", [1]);
        ProjectionEventDto gap = Event(3, "01J00000000000000000000003", [3]);

        _ = Should.Throw<ArgumentException>(
            () => ProjectionDeliveryFingerprint.ComputeHistory(Identity, "order-detail", [missingIdentity]));
        _ = Should.Throw<ArgumentException>(
            () => ProjectionDeliveryFingerprint.ComputeHistory(Identity, "order-detail", [Event(1, "message-1", [1]), gap]));
    }

    [Fact]
    public void Fingerprints_AreScopeProjectionCultureAndTimeZoneStable() {
        ProjectionEventDto value = Event(1, "message-1", [1, 2, 3]);
        string baseline = ProjectionDeliveryFingerprint.ComputeHistory(Identity, "order-detail", [value]).PrefixFingerprint;

        using var culture = new CultureScope("tr-TR");
        string repeated = ProjectionDeliveryFingerprint.ComputeHistory(Identity, "order-detail", [value]).PrefixFingerprint;
        string sibling = ProjectionDeliveryFingerprint.ComputeHistory(Identity, "order-index", [value]).PrefixFingerprint;

        repeated.ShouldBe(baseline);
        sibling.ShouldNotBe(baseline);
    }

    [Fact]
    public void EventFingerprint_DistinguishesNullEmptyBinaryAndEveryV1CanonicalField() {
        ProjectionEventDto baseline = Event(1, "message-1", [0, 128, 255]) with {
            UserId = null,
        };
        string fingerprint = ProjectionDeliveryFingerprint.ComputeEvent(baseline);
        ProjectionEventDto[] variants = [
            baseline with { SequenceNumber = 2 },
            baseline with { MessageId = "message-2" },
            baseline with { EventTypeName = "OrderRenamed" },
            baseline with { Payload = [0, 128, 254] },
            baseline with { SerializationFormat = "application/json" },
            baseline with { Timestamp = baseline.Timestamp.AddTicks(1) },
            baseline with { CorrelationId = "correlation-2" },
            baseline with { UserId = string.Empty },
        ];

        variants.Select(ProjectionDeliveryFingerprint.ComputeEvent)
            .ShouldAllBe(candidate => !string.Equals(candidate, fingerprint, StringComparison.Ordinal));
        variants.Select(ProjectionDeliveryFingerprint.ComputeEvent)
            .Distinct(StringComparer.Ordinal).Count().ShouldBe(variants.Length);
    }

    [Fact]
    public void V1EventFingerprint_IntentionallyIgnoresGlobalPositionForReceiptCompatibility() {
        ProjectionEventDto legacy = Event(1, "message-1", [1]) with { GlobalPosition = 0 };
        ProjectionEventDto authoritative = legacy with { GlobalPosition = 104 };

        ProjectionDeliveryFingerprint.ComputeEvent(authoritative)
            .ShouldBe(ProjectionDeliveryFingerprint.ComputeEvent(legacy));
        ProjectionDeliveryFingerprint.ComputeHistory(Identity, "order-detail", [authoritative])
            .PrefixFingerprint
            .ShouldBe(ProjectionDeliveryFingerprint.ComputeHistory(Identity, "order-detail", [legacy]).PrefixFingerprint);
    }

    [Fact]
    public void Fingerprints_UseUtcInstantAndSeparateEveryScopeSegment() {
        ProjectionEventDto plusTwo = Event(1, "message-1", [1]);
        ProjectionEventDto utc = plusTwo with { Timestamp = plusTwo.Timestamp.ToUniversalTime() };
        string eventFingerprint = ProjectionDeliveryFingerprint.ComputeEvent(plusTwo);

        ProjectionDeliveryFingerprint.ComputeEvent(utc).ShouldBe(eventFingerprint);
        string[] initialFingerprints = [
            ProjectionDeliveryFingerprint.ComputeInitial(Identity, "order-detail"),
            ProjectionDeliveryFingerprint.ComputeInitial(new AggregateIdentity("tenant-b", "sales", "order-42"), "order-detail"),
            ProjectionDeliveryFingerprint.ComputeInitial(new AggregateIdentity("tenant-a", "billing", "order-42"), "order-detail"),
            ProjectionDeliveryFingerprint.ComputeInitial(new AggregateIdentity("tenant-a", "sales", "order-43"), "order-detail"),
            ProjectionDeliveryFingerprint.ComputeInitial(Identity, "order-index"),
        ];
        initialFingerprints.Distinct(StringComparer.Ordinal).Count().ShouldBe(initialFingerprints.Length);
    }

    [Fact]
    public void HistoryFingerprint_IsOrderedAndDetectsRetainedOverlapChanges() {
        ProjectionEventDto first = Event(1, "message-1", [1]);
        ProjectionEventDto second = Event(2, "message-2", [2]);
        ProjectionDeliveryFingerprintHistory history = ProjectionDeliveryFingerprint.ComputeHistory(
            Identity,
            "order-detail",
            [first, second]);
        string manuallyExtended = ProjectionDeliveryFingerprint.Extend(
            ProjectionDeliveryFingerprint.Extend(
                history.InitialFingerprint,
                ProjectionDeliveryFingerprint.ComputeEvent(first)),
            ProjectionDeliveryFingerprint.ComputeEvent(second));

        history.PrefixFingerprint.ShouldBe(manuallyExtended);
        ProjectionDeliveryFingerprint.ComputeHistory(
                Identity,
                "order-detail",
                [first with { Payload = [9] }, second])
            .PrefixFingerprint.ShouldNotBe(history.PrefixFingerprint);
        _ = Should.Throw<ArgumentException>(() => ProjectionDeliveryFingerprint.ComputeHistory(
            Identity,
            "order-detail",
            [second, first]));
        _ = Should.Throw<ArgumentException>(() => ProjectionDeliveryFingerprint.ComputeHistory(
            Identity,
            "order-detail",
            [first, first with { SequenceNumber = 2 }]));
    }

    [Fact]
    public void MalformedFingerprintErrors_DoNotEchoIdentityOrDigestMaterial() {
        const string sensitive = "secret-message-identity";
        ArgumentException identityFailure = Should.Throw<ArgumentException>(() =>
            ProjectionDeliveryFingerprint.ComputeEvent(Event(1, " ", [1])));
        ArgumentException digestFailure = Should.Throw<ArgumentException>(() =>
            ProjectionDeliveryFingerprint.Extend("v1:not-a-digest", ProjectionDeliveryFingerprint.ComputeEvent(
                Event(1, sensitive, [1]))));

        identityFailure.Message.ShouldNotContain(sensitive);
        digestFailure.Message.ShouldNotContain(sensitive);
        digestFailure.Message.ShouldNotContain("v1:not-a-digest");
    }

    private static ProjectionEventDto Event(long sequence, string messageId, byte[] payload) => new(
        "OrderChanged",
        payload,
        "json",
        sequence,
        new DateTimeOffset(2026, 7, 14, 10, 11, 12, TimeSpan.FromHours(2)),
        "correlation-1",
        messageId,
        null);

    private sealed class CultureScope : IDisposable {
        private readonly System.Globalization.CultureInfo _culture = System.Globalization.CultureInfo.CurrentCulture;
        private readonly System.Globalization.CultureInfo _uiCulture = System.Globalization.CultureInfo.CurrentUICulture;

        public CultureScope(string cultureName) {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(cultureName);
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(cultureName);
        }

        public void Dispose() {
            System.Globalization.CultureInfo.CurrentCulture = _culture;
            System.Globalization.CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}

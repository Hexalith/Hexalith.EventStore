using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Streams;

/// <summary>Verifies retained Identity History Validator Tests.</summary>
public sealed class RetainedIdentityHistoryValidatorTests
{
    private static readonly AggregateIdentity _identity = new("tenant-a", "party", "party-a");
    private static readonly RetainedIdentityHistoryReadRequest _request = new(_identity, RetainedIdentityHistoryReadRequest.AttributionPurpose);
    private static readonly DateTimeOffset _now = DateTimeOffset.Parse("2026-10-06T12:00:00Z");

    /// <summary>Verifies sparse History Requires Every Excluded Original Position.</summary>
    [Fact]
    public void SparseHistoryRequiresEveryExcludedOriginalPosition()
    {
        RetainedIdentityHistoryStream source = Stream([Event(2), Event(4)], [1, 3]);
        RetainedIdentityHistoryValidator.IsComplete(_request, source, _now).ShouldBeTrue();
        RetainedIdentityHistoryValidator.IsComplete(_request, source with { ExcludedSequences = [1] }, _now).ShouldBeFalse();
        source.Events.Select(item => item.SequenceNumber).ShouldBe([2L, 4L]);
    }

    /// <summary>Verifies invalid Excluded Partitions Reject.</summary>
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(1, 1)]
    [InlineData(1, 5)]
    [InlineData(3, 1)]
    public void InvalidExcludedPartitionsReject(long first, long second)
        => RetainedIdentityHistoryValidator.IsComplete(_request, Stream([Event(2), Event(4)], [first, second]), _now).ShouldBeFalse();

    /// <summary>Verifies reordered History Rejects.</summary>
    [Fact]
    public void ReorderedHistoryRejects()
        => RetainedIdentityHistoryValidator.IsComplete(_request, Stream([Event(4), Event(2)], [1, 3]), _now).ShouldBeFalse();

    /// <summary>Verifies foreign Scope And Purpose Reject.</summary>
    [Fact]
    public void ForeignScopeAndPurposeReject()
    {
        RetainedIdentityHistoryStream source = Stream([Event(2), Event(4)], [1, 3]);
        RetainedIdentityHistoryValidator.IsComplete(_request, source with { Identity = new("tenant-b", "party", "party-a") }, _now).ShouldBeFalse();
        RetainedIdentityHistoryValidator.IsComplete(_request, source with { Purpose = "profile" }, _now).ShouldBeFalse();
        RetainedIdentityHistoryValidator.IsComplete(_request, source with { ObservationId = "" }, _now).ShouldBeFalse();
    }

    /// <summary>Verifies opaque History Cannot Become Readable Evidence.</summary>
    [Fact]
    public void OpaqueHistoryCannotBecomeReadableEvidence()
        => RetainedIdentityHistoryValidator.IsComplete(_request,
            Stream([Event(2) with { ProtectionMetadata = EventStorePayloadProtectionMetadata.ProviderOpaque() }, Event(4)], [1, 3]), _now).ShouldBeFalse();

    /// <summary>Verifies transit Cannot Extend Exclusive Expiry Or Invent Authority.</summary>
    [Fact]
    public void TransitCannotExtendExclusiveExpiryOrInventAuthority()
    {
        RetainedIdentityHistoryStream source = Stream([Event(2), Event(4)], [1, 3]);
        RetainedIdentityHistoryValidator.IsComplete(_request, source, source.ValidUntil).ShouldBeFalse();
        RetainedIdentityHistoryValidator.IsComplete(_request, source with { AuthorityRevision = null }, _now).ShouldBeFalse();
        RetainedIdentityHistoryValidator.IsComplete(_request, source, _now.AddSeconds(-1)).ShouldBeFalse();
    }

    /// <summary>Verifies private Trace Metadata Cannot Be Returned As Safe Attribution.</summary>
    [Fact]
    public void PrivateTraceMetadataCannotBeReturnedAsSafeAttribution()
        => RetainedIdentityHistoryValidator.IsComplete(_request,
            Stream([Event(2) with { UserId = "profile-subject" }, Event(4)], [1, 3]), _now).ShouldBeFalse();

    private static RetainedIdentityHistoryStream Stream(IReadOnlyList<StreamReadEvent> events, IReadOnlyList<long> excluded)
        => new(_identity, _request.Purpose, 4, _now, events, excluded, "source-observation")
        {
            AuthorityRevision = "source-authority-r1",
            ValidUntil = _now.AddMinutes(1),
        };

    private static StreamReadEvent Event(long sequence)
        => new(sequence, "History", "{}"u8.ToArray(), "json", 1, "", null, null,
            DateTimeOffset.Parse("2026-10-06T11:00:00Z"), null, EventStorePayloadProtectionMetadata.Unprotected());
}

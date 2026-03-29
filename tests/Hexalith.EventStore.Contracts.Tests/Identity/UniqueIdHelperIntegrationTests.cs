using Hexalith.Commons.UniqueIds;

namespace Hexalith.EventStore.Contracts.Tests.Identity;

public class UniqueIdHelperIntegrationTests {
    [Fact]
    public void GenerateSortableUniqueStringId_Returns26CharString() {
        string id = UniqueIdHelper.GenerateSortableUniqueStringId();

        id.ShouldNotBeNull();
        id.Length.ShouldBe(26);
    }

    [Fact]
    public void ExtractTimestamp_ReturnsTimestampCloseToNow() {
        string id = UniqueIdHelper.GenerateSortableUniqueStringId();

        DateTimeOffset timestamp = UniqueIdHelper.ExtractTimestamp(id);

        // Timestamp should be within 5 seconds of now
        TimeSpan difference = DateTimeOffset.UtcNow - timestamp;
        (difference.TotalSeconds < 5).ShouldBeTrue($"Timestamp {timestamp} is too far from now. Difference: {difference}");
    }

    [Fact]
    public void ToGuid_AndBack_RoundTrips() {
        string original = UniqueIdHelper.GenerateSortableUniqueStringId();

        var guid = UniqueIdHelper.ToGuid(original);
        string roundTripped = UniqueIdHelper.ToSortableUniqueId(guid);

        roundTripped.ShouldBe(original);
    }

    [Fact]
    public void LexicographicOrdering_SequentialUlids_MaintainSortOrder() {
        string first = UniqueIdHelper.GenerateSortableUniqueStringId();
        // Small delay to ensure distinct timestamps
        Thread.Sleep(2);
        string second = UniqueIdHelper.GenerateSortableUniqueStringId();

        int comparison = string.Compare(first, second, StringComparison.Ordinal);

        (comparison < 0).ShouldBeTrue($"Expected '{first}' < '{second}' lexicographically, but comparison was {comparison}");
    }

    [Fact]
    public void ExtractTimestamp_EmptyString_Throws() => Should.Throw<Exception>(() => UniqueIdHelper.ExtractTimestamp(string.Empty));

    [Fact]
    public void ExtractTimestamp_NullString_Throws() => Should.Throw<Exception>(() => UniqueIdHelper.ExtractTimestamp(null!));

    [Fact]
    public void ExtractTimestamp_TruncatedString_Throws() =>
        // 25-char string (one short)
        Should.Throw<Exception>(() => UniqueIdHelper.ExtractTimestamp("0123456789012345678901234"));

    [Fact]
    public void ExtractTimestamp_OverflowString_Throws() =>
        // 27-char string (one too many)
        Should.Throw<Exception>(() => UniqueIdHelper.ExtractTimestamp("012345678901234567890123456"));

    [Fact]
    public void ExtractTimestamp_NonBase32Characters_Throws() =>
        // Exactly 26 chars, all invalid for Crockford Base32
        Should.Throw<Exception>(() => UniqueIdHelper.ExtractTimestamp("!!!!!!!!!!!!!!!!!!!!!!!!!!"));
}

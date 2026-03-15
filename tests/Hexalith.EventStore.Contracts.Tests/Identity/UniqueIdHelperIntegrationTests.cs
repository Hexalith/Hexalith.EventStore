using Hexalith.Commons.UniqueIds;

namespace Hexalith.EventStore.Contracts.Tests.Identity;

public class UniqueIdHelperIntegrationTests {
    [Fact]
    public void GenerateSortableUniqueStringId_Returns26CharString() {
        string id = UniqueIdHelper.GenerateSortableUniqueStringId();

        Assert.NotNull(id);
        Assert.Equal(26, id.Length);
    }

    [Fact]
    public void ExtractTimestamp_ReturnsTimestampCloseToNow() {
        string id = UniqueIdHelper.GenerateSortableUniqueStringId();

        DateTimeOffset timestamp = UniqueIdHelper.ExtractTimestamp(id);

        // Timestamp should be within 5 seconds of now
        TimeSpan difference = DateTimeOffset.UtcNow - timestamp;
        Assert.True(difference.TotalSeconds < 5, $"Timestamp {timestamp} is too far from now. Difference: {difference}");
    }

    [Fact]
    public void ToGuid_AndBack_RoundTrips() {
        string original = UniqueIdHelper.GenerateSortableUniqueStringId();

        var guid = UniqueIdHelper.ToGuid(original);
        string roundTripped = UniqueIdHelper.ToSortableUniqueId(guid);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void LexicographicOrdering_SequentialUlids_MaintainSortOrder() {
        string first = UniqueIdHelper.GenerateSortableUniqueStringId();
        // Small delay to ensure distinct timestamps
        Thread.Sleep(2);
        string second = UniqueIdHelper.GenerateSortableUniqueStringId();

        int comparison = string.Compare(first, second, StringComparison.Ordinal);

        Assert.True(comparison < 0, $"Expected '{first}' < '{second}' lexicographically, but comparison was {comparison}");
    }

    [Fact]
    public void ExtractTimestamp_EmptyString_Throws() => Assert.ThrowsAny<Exception>(() => UniqueIdHelper.ExtractTimestamp(string.Empty));

    [Fact]
    public void ExtractTimestamp_NullString_Throws() => Assert.ThrowsAny<Exception>(() => UniqueIdHelper.ExtractTimestamp(null!));

    [Fact]
    public void ExtractTimestamp_TruncatedString_Throws() =>
        // 25-char string (one short)
        Assert.ThrowsAny<Exception>(() => UniqueIdHelper.ExtractTimestamp("0123456789012345678901234"));

    [Fact]
    public void ExtractTimestamp_OverflowString_Throws() =>
        // 27-char string (one too many)
        Assert.ThrowsAny<Exception>(() => UniqueIdHelper.ExtractTimestamp("012345678901234567890123456"));

    [Fact]
    public void ExtractTimestamp_NonBase32Characters_Throws() =>
        // Exactly 26 chars, all invalid for Crockford Base32
        Assert.ThrowsAny<Exception>(() => UniqueIdHelper.ExtractTimestamp("!!!!!!!!!!!!!!!!!!!!!!!!!!"));
}

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Identity;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Identity;

public class MessageIdGrammarTests
{
    private const string CanonicalUlid = "01ARZ3NDEKTSV4RRFFQ69G5FAV";

    [Theory]
    [InlineData("a")]
    [InlineData("Z")]
    [InlineData("7")]
    [InlineData("legacy-status-1")]
    [InlineData("A-b-9")]
    [InlineData("Mixed-Case-Legacy-42")]
    [InlineData("a--b")]
    [InlineData(CanonicalUlid)]
    [InlineData("01arz3ndektsv4rrffq69g5fav")]
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    public void LegacyIdentifierIsValidUnderVersionOne(string messageId)
        => MessageIdGrammar.IsValid(messageId, 1).ShouldBeTrue();

    [Fact]
    public void LegacyIdentifierAtMaximumLengthIsValidUnderVersionOne()
        => MessageIdGrammar.IsValid(new string('a', 128), 1).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-")]
    [InlineData("-legacy")]
    [InlineData("legacy-")]
    [InlineData("-legacy-")]
    [InlineData(" legacy")]
    [InlineData("legacy ")]
    [InlineData("legacy status")]
    [InlineData("legacy_status")]
    [InlineData("legacy.status")]
    [InlineData("legacy/status")]
    [InlineData("legacy:status")]
    [InlineData("legacy%2Fstatus")]
    [InlineData("legacy\n")]
    [InlineData("legacy\t1")]
    [InlineData("l\u00E9gacy")]
    [InlineData("\uFF41bc")]
    [InlineData("\u0661")]
    public void MalformedIdentifierIsInvalidUnderVersionOne(string? messageId)
        => MessageIdGrammar.IsValid(messageId, 1).ShouldBeFalse();

    [Fact]
    public void LegacyIdentifierLongerThanMaximumIsInvalidUnderVersionOne()
        => MessageIdGrammar.IsValid(new string('a', 129), 1).ShouldBeFalse();

    [Theory]
    [InlineData(CanonicalUlid)]
    [InlineData("01BX5ZZKBKACTAV9WEVGEMMVRZ")]
    [InlineData("00000000000000000000000000")]
    [InlineData("7ZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("0123456789ABCDEFGHJKMNPQRS")]
    [InlineData("0TVWXYZ0000000000000000000")]
    public void CanonicalUppercaseUlidIsValidUnderVersionTwo(string messageId)
        => MessageIdGrammar.IsValid(messageId, 2).ShouldBeTrue();

    [Fact]
    public void GeneratedSortableIdentifierIsValidUnderVersionTwo()
    {
        string messageId = UniqueIdHelper.GenerateSortableUniqueStringId();

        MessageIdGrammar.IsValid(messageId, 2).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("01arz3ndektsv4rrffq69g5fav")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FAv")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FAI")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FAL")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FAO")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FAU")]
    [InlineData("I1ARZ3NDEKTSV4RRFFQ69G5FAV")]
    [InlineData("O1ARZ3NDEKTSV4RRFFQ69G5FAV")]
    [InlineData("8ZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FA")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FAVX")]
    [InlineData(" 01ARZ3NDEKTSV4RRFFQ69G5FA")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FA ")]
    [InlineData("01ARZ3NDEK-SV4RRFFQ69G5FAV")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FA\u0662")]
    [InlineData("legacy-status-1")]
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    public void NonCanonicalOrLegacyIdentifierIsInvalidUnderVersionTwo(string? messageId)
        => MessageIdGrammar.IsValid(messageId, 2).ShouldBeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void UndeclaredVersionAcceptsNoIdentifier(int version)
    {
        MessageIdGrammar.IsValid(CanonicalUlid, version).ShouldBeFalse();
        MessageIdGrammar.IsValid("legacy-status-1", version).ShouldBeFalse();
    }
}

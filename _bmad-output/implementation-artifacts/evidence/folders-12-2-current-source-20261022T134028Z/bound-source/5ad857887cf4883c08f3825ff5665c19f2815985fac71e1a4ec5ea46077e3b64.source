using Hexalith.EventStore.Admin.Abstractions.Models.Common;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Common;

public class PagedResultTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var items = new List<string> { "item1", "item2" };
        var result = new PagedResult<string>(items, 10, "token-123");

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(10);
        result.ContinuationToken.ShouldBe("token-123");
    }

    [Fact]
    public void Constructor_WithEmptyList_CreatesInstance() {
        var result = new PagedResult<string>([], 0, null);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
        result.ContinuationToken.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithNullItems_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() =>
                                                                                    new PagedResult<string>(null!, 0, null));

    [Fact]
    public void Constructor_WithNullContinuationToken_Succeeds() {
        var result = new PagedResult<int>([1, 2, 3], 3, null);

        result.ContinuationToken.ShouldBeNull();
        result.Items.Count.ShouldBe(3);
    }

    [Fact]
    public void RecordEquality_SameListInstance_AreEqual() {
        var items = new List<string> { "a" };
        var result1 = new PagedResult<string>(items, 1, "tok");
        var result2 = new PagedResult<string>(items, 1, "tok");

        result1.ShouldBe(result2);
    }

    [Fact]
    public void RecordEquality_DifferentListInstances_AreNotEqual() {
        // Records use reference equality for IReadOnlyList<T> — this is expected behavior
        var result1 = new PagedResult<string>(["a"], 1, "tok");
        var result2 = new PagedResult<string>(["a"], 1, "tok");

        result1.ShouldNotBe(result2);
    }
}

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class AggregateStateDiffTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var changes = new List<FieldChange> { new("$.status", "\"open\"", "\"closed\"") };
        var diff = new AggregateStateDiff(1, 5, changes);

        diff.FromSequence.ShouldBe(1);
        diff.ToSequence.ShouldBe(5);
        _ = diff.ChangedFields.ShouldHaveSingleItem();
    }

    [Fact]
    public void Constructor_WithEmptyChangedFields_CreatesInstance() {
        var diff = new AggregateStateDiff(1, 1, []);

        diff.ChangedFields.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WithNullChangedFields_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() =>
                                                                                            new AggregateStateDiff(0, 1, null!));
}

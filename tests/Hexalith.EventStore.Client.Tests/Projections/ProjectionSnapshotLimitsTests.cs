using System.Text.Json;

using Hexalith.EventStore.Client.Projections;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class ProjectionSnapshotLimitsTests
{
    [Theory]
    [InlineData("plain")]
    [InlineData("é中\"\\\n")]
    public void EncodedByteBudgetAcceptsExactLimitAndRejectsOneByteLess(string value)
    {
        int encoded = JsonSerializer.SerializeToUtf8Bytes(value).Length;
        new ProjectionSnapshotLimits(1, encoded).Validate(value, 1);
        ProjectionSnapshotLimitExceededException failure = Should.Throw<ProjectionSnapshotLimitExceededException>(
            () => new ProjectionSnapshotLimits(1, encoded - 1).Validate(value, 1));
        failure.Limit.ShouldBe("serialized-bytes");
        failure.Maximum.ShouldBe(encoded - 1);
    }

    [Fact]
    public void EntryBudgetRejectsBeforeSerializingTheObjectGraph()
    {
        Dictionary<string, object> cyclic = [];
        cyclic["cycle"] = cyclic;
        ProjectionSnapshotLimitExceededException failure = Should.Throw<ProjectionSnapshotLimitExceededException>(
            () => new ProjectionSnapshotLimits(2, 100).Validate(cyclic, 3));
        failure.Limit.ShouldBe("entries");
        failure.Actual.ShouldBe(3);
    }

    [Fact]
    public void HugeSingleTokenIsRejectedByABoundedSerializerBuffer()
    {
        string value = new('中', 1_000_000);
        ProjectionSnapshotLimitExceededException failure = Should.Throw<ProjectionSnapshotLimitExceededException>(
            () => new ProjectionSnapshotLimits(1, 1024).Validate(value, 1));
        failure.Limit.ShouldBe("serialized-bytes");
    }
}

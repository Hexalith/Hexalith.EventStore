using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Pins each duplicate-identity clause of the trusted capture declaration index.</summary>
public sealed class FoldedSnapshotQualificationRegistryTests
{
    [Theory]
    [InlineData("other-aggregate", "profile")]
    [InlineData("aggregate", "other-profile")]
    public void SharedAggregateOrProfileWithinOneDomainIsRefused(string aggregateType, string profileId)
    {
        FoldedSnapshotQualification[] entries =
        [
            Qualification("d", "aggregate", "profile"),
            Qualification("d", aggregateType, profileId),
        ];

        Should.Throw<ArgumentException>(() => new FoldedSnapshotQualificationRegistry(entries))
            .Message.ShouldStartWith("SnapshotCaptureUnsupported:");
    }

    [Fact]
    public void SameAggregateAndProfileInDistinctDomainsResolveIndependently()
    {
        FoldedSnapshotQualification first = Qualification("d1", "aggregate", "profile");
        FoldedSnapshotQualification second = Qualification("d2", "aggregate", "profile");

        var registry = new FoldedSnapshotQualificationRegistry([first, second]);

        registry.Find("d1", "aggregate").ShouldBeSameAs(first);
        registry.FindByProfile("d2", "profile").ShouldBeSameAs(second);
    }

    private static FoldedSnapshotQualification Qualification(string domain, string aggregateType, string profileId)
        => new(domain, aggregateType, typeof(SnapshotCaptureTestState), profileId, "parity", SnapshotCaptureTestLimits.Value);
}

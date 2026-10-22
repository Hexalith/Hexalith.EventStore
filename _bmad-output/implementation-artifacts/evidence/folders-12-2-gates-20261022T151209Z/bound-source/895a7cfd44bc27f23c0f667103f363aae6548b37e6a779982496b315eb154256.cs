using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;

/// <summary>Deterministic branch coverage for the Story 4.5 final-shape classifier.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class AppendDurabilityFinalShapeClassifierTests
{
    /// <summary>
    /// One case per classification name the classifier can return. The theory data and the
    /// coverage test below both read this single list, so a name can never be covered by one and
    /// missed by the other.
    /// </summary>
    private static readonly (AppendDurabilityFinalShapeClassifier.Input Input, string Name, bool Sound)[] CaseRows =
    [
        (Input(finalSequence: 1, sequences: [1], messageIds: ["a"]), "gapless-1-event-stream", true),
        (Input(finalSequence: 2, sequences: [1, 2], messageIds: ["a", "b"]), "gapless-2-event-stream", true),
        (Input(finalSequence: 0, metadataPresent: false, sequences: [], messageIds: []), "no-metadata-no-events", true),
        (Input(finalSequence: 3, withinBounds: false, sequences: [1], messageIds: ["a"]), "final-sequence-out-of-bounds", false),
        (Input(finalSequence: 1, fullyRead: false, sequences: [1], messageIds: ["a"]), "unclassified-final-shape", false),
        (Input(finalSequence: 0, metadataPresent: false, sequences: [1], messageIds: ["a"]), "events-without-metadata", false),
        (Input(finalSequence: 0, metadataPresent: false, sequences: [], messageIds: [], nextEventPresent: true), "events-without-metadata", false),
        (Input(finalSequence: 2, sequences: [1], messageIds: ["a"]), "metadata-sequence-without-matching-events", false),
        (Input(finalSequence: 2, sequences: [1, 3], messageIds: ["a", "b"]), "non-contiguous-event-sequence", false),
        (Input(finalSequence: 2, sequences: [1, 2], messageIds: ["a", "a"]), "duplicate-event-message-ids", false),
        (Input(finalSequence: 1, sequences: [1], messageIds: ["a"], nextEventPresent: true), "event-beyond-metadata-sequence", false),
        (Input(finalSequence: 1, sequences: [1], messageIds: ["a"], identityMatches: false), "foreign-aggregate-identity-present", false),
        (Input(finalSequence: 1, sequences: [1], messageIds: ["a"], exactContendersOnly: false), "foreign-writer-present", false),
        (Input(finalSequence: 1, sequences: [1], messageIds: ["a"], lastModifiedMatches: false), "metadata-timestamp-mismatch", false),
    ];

    /// <summary>Gets one theory case per classification name the classifier can return.</summary>
    public static TheoryData<AppendDurabilityFinalShapeClassifier.Input, string, bool> Cases
    {
        get
        {
            TheoryData<AppendDurabilityFinalShapeClassifier.Input, string, bool> data = [];
            foreach ((AppendDurabilityFinalShapeClassifier.Input input, string name, bool sound) in CaseRows)
            {
                data.Add(input, name, sound);
            }

            return data;
        }
    }

    /// <summary>
    /// Verifies every classifier branch is stable and that its soundness partition matches. The
    /// case table covers all fourteen reachable names; the classifier returns no other name.
    /// </summary>
    /// <param name="input">The observed facts.</param>
    /// <param name="expectedName">The expected classification.</param>
    /// <param name="expectedSound">Whether the reviewed profile may exhibit that shape.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Classify_ReturnsExpectedShape(
        AppendDurabilityFinalShapeClassifier.Input input,
        string expectedName,
        bool expectedSound)
    {
        string actual = AppendDurabilityFinalShapeClassifier.Classify(input);

        actual.ShouldBe(expectedName);
        AppendDurabilityFinalShapeClassifier.IsSound(actual).ShouldBe(expectedSound);
    }

    /// <summary>Verifies every unsound name is one the classifier can actually return.</summary>
    [Fact]
    public void UnsoundClassifications_AreAllProducedByTheCaseTable()
    {
        HashSet<string> produced = [.. CaseRows.Select(row => row.Name)];

        foreach (string unsound in AppendDurabilityFinalShapeClassifier.UnsoundClassifications)
        {
            produced.ShouldContain(unsound, $"no case produces the unsound classification '{unsound}'");
        }
    }

    private static AppendDurabilityFinalShapeClassifier.Input Input(
        long finalSequence,
        IReadOnlyList<long> sequences,
        IReadOnlyList<string> messageIds,
        bool fullyRead = true,
        bool withinBounds = true,
        bool metadataPresent = true,
        bool nextEventPresent = false,
        bool identityMatches = true,
        bool exactContendersOnly = true,
        bool lastModifiedMatches = true)
        => new(
            fullyRead,
            withinBounds,
            finalSequence,
            metadataPresent,
            sequences,
            messageIds,
            nextEventPresent,
            identityMatches,
            exactContendersOnly,
            lastModifiedMatches);
}

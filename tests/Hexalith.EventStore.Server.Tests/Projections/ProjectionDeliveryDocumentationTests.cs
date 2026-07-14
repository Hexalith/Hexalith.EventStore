using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class ProjectionDeliveryDocumentationTests {
    private static readonly string _root = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void ConceptAndRunbook_GuardIdentityBoundsMigrationAndEvidenceLinks() {
        string concept = File.ReadAllText(Path.Combine(_root, "docs", "concepts", "projection-delivery.md"));
        string cutover = File.ReadAllText(Path.Combine(_root, "docs", "operations", "projection-delivery-v2-cutover.md"));
        string envelope = File.ReadAllText(Path.Combine(_root, "docs", "concepts", "event-envelope.md"));

        concept.ShouldContain("MessageId");
        concept.ShouldContain("SequenceNumber");
        concept.ShouldContain("CompletedReceiptLimit");
        concept.ShouldContain("ReservationLease");
        concept.ShouldContain("delivery-reconciliation");
        concept.ShouldContain("projection-delivery-v2-cutover.md");
        concept.ShouldContain("projection-delivery-v2-evidence.md");
        cutover.ShouldContain("not a rolling upgrade");
        cutover.ShouldContain("rolling downgrade is forbidden");
        envelope.ShouldContain("persisted EventStore `messageId`");
        envelope.ShouldNotContain("`cloudevent.id` (composite: `{correlationId}:{sequenceNumber}`)");
    }
}

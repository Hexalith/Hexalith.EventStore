namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>Contains the stored writer-protocol identity observed by the test lease.</summary>
internal sealed record WriterProtocolMarker(
    int SchemaVersion,
    int WriterProtocolVersion,
    string CutoverCommit);

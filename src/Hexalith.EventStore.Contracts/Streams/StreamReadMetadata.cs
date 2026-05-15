namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// Paging and range metadata returned with a public stream read page.
/// </summary>
/// <param name="FromSequence">The exclusive lower sequence bound used for the read.</param>
/// <param name="ToSequence">Optional inclusive upper sequence bound used for the read.</param>
/// <param name="LastSequenceReturned">The highest sequence returned in this page.</param>
/// <param name="LatestSequence">The latest sequence known for the stream scope.</param>
/// <param name="EventCount">The number of events returned in this page.</param>
/// <param name="IsTruncated">Whether more events are available after this page.</param>
/// <param name="NextContinuationToken">Optional opaque continuation token for the next page.</param>
public sealed record StreamReadMetadata(
    long FromSequence,
    long? ToSequence,
    long LastSequenceReturned,
    long LatestSequence,
    int EventCount,
    bool IsTruncated,
    ReplayContinuationToken? NextContinuationToken);

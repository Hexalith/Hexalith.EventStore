namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Independent safe commitments observed at the actual qualification HTTP transport boundary.</summary>
internal sealed record QualificationHttpExchange(string Address, DateTimeOffset Started, DateTimeOffset Ended, int Status,
    string RequestSha256, string ResponseSha256, string RequestSemanticsSha256, string ResponseSemanticsSha256, string Method = "POST");

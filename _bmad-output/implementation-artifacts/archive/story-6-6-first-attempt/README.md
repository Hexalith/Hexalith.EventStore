# Story 6.6 first attempt archive

The last commit containing these first-attempt documents and tracked evidence was `75a08f0069d8c2495d9dff20a0deb84edb6cc638`. The tracked evidence was removed from HEAD; ignored evidence was left in place.

The opt-in Dapr production logical reader and its transitive codecs and models remain because `AggregateActor` can activate that reader, so removing them would change live behavior. The bounded V1 parser, `EventLogicalDigest`, legacy command replay admission, `IAsyncDomainProcessor`, and `IAsyncAggregateReplay` also remain live. Obsolete public compatibility shells remain for the next major release.

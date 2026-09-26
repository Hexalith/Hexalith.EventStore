# 6.1-P1R: EventStore 3.108.1 release source revalidation

Recorded 2026-09-26 for the pending Projects 6.1-P1R revalidation. This is a source-coordinate record, not EventStore Owner acceptance of a new P1R tuple.

| Coordinate | Exact observation |
| --- | --- |
| Release version and tag | `3.108.1`, `v3.108.1` |
| Release tag commit | `b15ad59abca82d5980ef92a510c2379e05f4d46f` (`git rev-parse 'v3.108.1^{commit}'`) |
| Current checkout | `8ac62359c1ffdd520488ecd03bd9689c217e3788` (`git rev-parse HEAD`); 28 commits after the tag, with the tag an ancestor |
| Release package manifest | `tools/release-packages.json` at the tag, SHA-256 `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae`; 14 package IDs |
| Current manifest comparison | `git diff --quiet v3.108.1 HEAD -- tools/release-packages.json` exited `0`; the checkout retains the release manifest bytes |

The release package coordinate for the candidate is **EventStore package version `3.108.1` from tag `v3.108.1`**. The newer checkout is a separate source observation and must not replace the tag commit in the release tuple. The manifest inventories package projects; this revalidation did not build or validate the published package artifacts.

## Scoped API comparison

`git rev-parse <revision>:<path>` returned the **same blob at `v3.106.0`, `v3.108.1`, and current `HEAD`** for each of the seven previously scoped APIs:

| API file (under `src/`) | Identical Git blob at all three revisions |
| --- | --- |
| `Hexalith.EventStore.DomainService/IAsyncDomainProjectionHandler.cs` | `99ca7b0bc3e8bea370ecd2674f54f9f7f198933e` |
| `Hexalith.EventStore.Client/Projections/IReadModelStore.cs` | `fb07789943f28e7594e3f31c748204645dbb0c75` |
| `Hexalith.EventStore.Client/Projections/IReadModelBatchStore.cs` | `bf8952b3ebf868fb407539fcebb85fad2f203011` |
| `Hexalith.EventStore.Client/Projections/ReadModelWritePolicy.cs` | `ff0d5d14b5cb90ab0d31771c9bbf4cac63b7d2f3` |
| `Hexalith.EventStore.DomainService/IDomainQueryHandler.cs` | `693ff04e7d1eb32af9b515d14781c2a69c421f08` |
| `Hexalith.EventStore.Client/Queries/IQueryCursorCodec.cs` | `18e67dc0eaf3a3b2b5ae3dc1b52ddf73b073b6f7` |
| `Hexalith.EventStore.Client/Queries/QueryCursorScope.cs` | `53065de441d5863d56ae0953e772b3e1117bebee` |

The scoped diff is zero for these seven files, including `QueryCursorScope`. Separately, `git diff --name-only v3.108.1 HEAD -- src` reports **43 changed source paths after the tag**, none of them in this seven-file set. Those 43 paths still require EventStore Owner compatibility disposition; seven-file identity and an unchanged release manifest do not establish compatibility of the whole checkout or published package provenance.

The previously accepted Projects tuple remains EventStore `3.106.0` / `v3.106.0` / `76051c70cbf868c40edc00ca0344fa5bd8879b69` with Builds `ad52f350a2f0bc47849179ae17b4594dafff5363`. Its rollback remains EventStore `3.70.1` / `v3.70.1` / `f13f9925fdca53efa2ab8c90d396ab106f91bb9c` with Builds `7af20f8bafbfe561df6f7705913a0800603090b5`.

**Pending EventStore Owner decision:** confirm that the tagged source and `3.108.1` package family are the exact source/package coordinates to offer for a new P1R decision, and state how the later checkout's changes affect compatibility evidence. No four-role acceptance or G-6 approval is recorded here.

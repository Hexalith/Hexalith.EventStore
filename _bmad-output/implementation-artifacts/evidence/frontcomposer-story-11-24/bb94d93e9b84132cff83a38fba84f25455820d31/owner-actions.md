# FrontComposer Story 11.24 Owner Actions

The evidence subject is frozen at SHA-256
`9d074dfd0758a8934f122aab18659627dff1cf5d4c3e548b222cc0d79a881065`.
It binds EventStore source `bb94d93e9b84132cff83a38fba84f25455820d31`, package version
`3.91.1`, and consumer scope `Hexalith.FrontComposer Story 11.24` only.

This file is not an approval. The durable decision remains unavailable until both actions below
are completed by `github:jpiquot` against the unchanged subject.

## EventStore Owner

1. Review `review-subject.json` and every file in its `bound_evidence` array.
2. Publish a durable GitHub receipt in `Hexalith/Hexalith.EventStore` using the exact fields below.
3. After GitHub assigns the comment URL, edit the receipt so `durable_source` is that exact
   `https://github.com/Hexalith/Hexalith.EventStore/issues/...#issuecomment-...` URL.
4. Capture the unchanged receipt as
   `acceptances/9d074dfd0758a8934f122aab18659627dff1cf5d4c3e548b222cc0d79a881065/eventstore-owner.json`.

```json
{
  "schema": "hexalith.eventstore.frontcomposer-runtime-acceptance.v1",
  "subject_sha256": "9d074dfd0758a8934f122aab18659627dff1cf5d4c3e548b222cc0d79a881065",
  "subject_frozen_at": "2026-08-10T07:06:11Z",
  "actor": "github:jpiquot",
  "role": "eventstore-owner",
  "decision": "accepted",
  "source_sha": "bb94d93e9b84132cff83a38fba84f25455820d31",
  "version": "3.91.1",
  "consumer_scope": "Hexalith.FrontComposer Story 11.24",
  "accepted_at": "<UTC timestamp after 2026-08-10T07:06:11Z>",
  "durable_source": "<exact GitHub issue-comment URL>",
  "statement": "I accept this exact EventStore source and signed NuGet.org package identity for Hexalith.FrontComposer Story 11.24 only."
}
```

## Release Owner

1. Independently review the same unchanged subject and bound evidence.
2. Publish a separate durable GitHub receipt in `Hexalith/Hexalith.EventStore` using the exact fields below.
3. After GitHub assigns the comment URL, edit the receipt so `durable_source` is that exact URL.
4. Capture the unchanged receipt as
   `acceptances/9d074dfd0758a8934f122aab18659627dff1cf5d4c3e548b222cc0d79a881065/release-owner.json`.

```json
{
  "schema": "hexalith.eventstore.frontcomposer-runtime-acceptance.v1",
  "subject_sha256": "9d074dfd0758a8934f122aab18659627dff1cf5d4c3e548b222cc0d79a881065",
  "subject_frozen_at": "2026-08-10T07:06:11Z",
  "actor": "github:jpiquot",
  "role": "release-owner",
  "decision": "accepted",
  "source_sha": "bb94d93e9b84132cff83a38fba84f25455820d31",
  "version": "3.91.1",
  "consumer_scope": "Hexalith.FrontComposer Story 11.24",
  "accepted_at": "<UTC timestamp after 2026-08-10T07:06:11Z>",
  "durable_source": "<exact GitHub issue-comment URL>",
  "statement": "I authorize this exact EventStore source and signed NuGet.org package identity for migration by Hexalith.FrontComposer Story 11.24 only."
}
```

After both receipts are captured, rerun the focused successor tests. Only a green result permits
changing the durable record to literal `final_decision: available` and
`authorize_consumer_migration: true`.

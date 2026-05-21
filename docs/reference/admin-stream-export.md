# Admin Stream Export

`POST /api/v1/admin/backups/export-stream` exports one EventStore stream for a tenant, domain, and aggregate.

The Admin API remains a facade. It validates the Admin tenant scope, forwards the request to the EventStore stream-read endpoint, and returns a bounded `StreamExportResult`. EventStore owns stream authorization, actor traversal, payload readability, redaction policy, and state-store access.

## Bounds

- `AdminServer:MaxStreamExportEvents` defaults to `50000`.
- Exports are paged through `api/v1/streams/read` with pages no larger than `1000` events.
- If a stream is larger than the configured limit, the export contains the newest window only and marks the document with `truncated: true`, `exportLimit`, `latestSequence`, `fromSequence`, and `toSequence`.
- Empty or missing streams return `Success=false` with no downloadable content.

## Formats

Supported `StreamExportRequest.Format` values are exactly `JSON` and `CloudEvents`, compared case-insensitively after trimming.

Successful responses keep the compatibility shape:

```json
{
  "success": true,
  "tenantId": "tenant-a",
  "domain": "counter",
  "aggregateId": "counter-1",
  "eventCount": 2,
  "content": "{\"tenantId\":\"tenant-a\",\"events\":[]}",
  "fileName": "tenant-a_counter_counter-1_json_20260521T120000Z.json",
  "errorMessage": null,
  "errorCode": null
}
```

`content` contains the full export document. Streaming or chunked export transport remains deferred until measured export sizes require it.

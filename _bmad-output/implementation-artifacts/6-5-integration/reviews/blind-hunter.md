- Finding floor: 4,605,721 bytes ÷ 1,000 = 4,605.721 kB; N = min(floor(√4,605.721 + 1), 10) = 10.

- `6-5-integration/verify.py:315` reads the normative document with `read_text()`, which normalizes CRLF before `approval()` checks and hashes it. A CRLF copy therefore passes and reports the LF digest despite §12 requiring exact original bytes. Read and validate bytes before decoding.

- `verify.py:99` does not require exactly one provenance row for each A/B/C child. Removing all three rows still makes `inputs()` return success. Enforce the complete expected label/path set and reject missing or duplicate rows.

- `verify.py:90` never validates the displayed “Current immutable D import pins” table. Changing its first SHA-256 to 64 zeroes still passes `inputs()`. Compare every displayed path, revision and digest with the source manifest.

- `preserved-hold-predicates.json` is hash-pinned but its contents are never checked against the integrated hold tables. Consequently, the preservation gate does not establish that every recorded predicate retains its normative owner and exit. Validate the complete predicate set against D8 and the supplemental table.

- The supplemental A/B/C hold table does not extend D8’s closed hold-to-reason and owner-kind mappings. Names such as `HistoricalMessageIdCollision` and `RollbackReaderCapabilityHold` therefore lack the exact inventory reason and permitted owner value needed by the new discovery contract. Supply those mappings.

- The imported cursor known answer does not exercise D8’s required HS256 algorithm. Its signature equals `SHA256(fixture-prefix || canonical payload)` and differs from HMAC-SHA256 using that prefix as the key. Add an independent HMAC-SHA256 known answer while retaining the historical fixture explicitly as model evidence.

- D8 requires an “explicit inventory audience,” but its exact cursor payload has no audience field and the authenticated input defines no audience prefix. Specify the exact audience bytes and their placement so implementations cannot choose incompatible signing inputs.

- D8 describes cursor generation as hashing the scope-header generation and ordered subject/entry generations without specifying its complete preimage encoding. The child model uses canonical JSON `[headerGeneration, generations]`, but models are non-normative. Define the exact framing and a literal generation vector.

- `metadata-adapter-contract.md:72` requires separately authenticated commit/readback evidence without defining its retained schema, address, authenticated byte set or issuing authority. Specify how evidence binds backend identity and the complete participant set so recovery can verify it consistently.

- `metadata-adapter-contract.md:72` bounds retry delays but leaves lock acquisition, statement execution and transaction duration unbounded. A blocked transaction can therefore bypass the intended bounded recovery behavior. Define deadlines, cancellation handling and timeout-to-hold mapping.

- The adapter admits eight 100 MiB queue envelopes and requires complete predecessor/result preflight and readback, but defines no working-memory or buffering budget for those transactions. Specify bounded streaming or staging and qualification at maximum legal occupancy; durable storage ceilings alone do not bound the coordinator’s allocation.

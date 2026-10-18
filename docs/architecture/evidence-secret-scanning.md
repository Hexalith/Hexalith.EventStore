# Evidence secret scanning and retirement contract

Recorded before DW-517 implementation, 2026-10-14, against EventStore
`24d719292b06d8a9d09771d837f8dd4d8d9b8997`.

`SecretsProtectionTests` owns the tracked-file ingestion and reusable-content
secret guard. Git enumeration excludes submodule contents and untracked output;
recognized binary signatures and the existing exact UI build-artifact paths
retain their established handling. Text is decoded through the existing UTF-8,
UTF-16 and malformed-byte recovery path and then inspected by `FindViolations`.
An evidence filename, directory, extension or historical designation does not
establish inert content.

DW-517 restores that ingestion for evidence `*.ctrf.json`, every tracked text
file below `evidence/6-1-p1r-3110/verification/`, and
`6-5d-simplification/previous-candidate.md`, all below
`_bmad-output/implementation-artifacts/`. Their immutable historical bytes must
remain intact. The 6.5d historical archive decision permits preservation and
non-authoritative classification; it does not authorize secret-scan retirement.
No existing retirement registry entry covers these three categories.

The governing [Story 5.3 specification](../../_bmad-output/implementation-artifacts/spec-5-3-production-authentication-guards-and-secret-stripping.md)
requires a complete approved replacement lineage or explicit governed retirement
when historical evidence contains forbidden authentication material. Existing
retirements in `evidence/story-5-3-retired-captures.json` are content-bound:
exact governed path, retired status, non-authoritative flag, nonempty reason and
an exact SHA-256 of preserved bytes. Missing or malformed registry evidence,
invalid disposition, ungoverned paths and changed or unavailable retired bytes
must fail validation. Neither a directory-wide exclusion nor updating a hash to
silence a finding satisfies the contract. The two verified Story 5.3 approval tuples from immutable commit
`df3828740c244d8add3c40ce2bbfd0d2da680b30` anchor the existing registry checks;
a registry edit cannot re-pin those approvals. DW-517 grants no new retirement
approval and creates no new production registry entry. Any future retirement
needs its owning specification/approval and verified content binding.
The registry also retains two Story 6.6 OTLP dispositions. Their originals and
replacement/authorization lineage are absent and untracked in this checkout;
they already fall outside the governed path policy. They grant no usable
exclusion here, and attempted ingestion must reject them. These inherited
records are preserved rather than repaired or re-approved by DW-517.

The separate `evidence/6-1-p1r-remediation/source-candidate.diff` exception stays
bound to the existing fixed SHA-256 and its sealed-capture verifier. Only CRLF
pairs are normalized there, as previously authorized. Changed or missing capture
bytes fail. Legitimate synthetic fixtures retain their existing structural and
sealed-content recognition; adjacent injected credentials remain findings.
The exact password-free `ssh://git@github.com/...` identity exception remains
unchanged; password-bearing URIs, other schemes, hosts and usernames fail.

Tests must create isolated files and invoke the same reader and scanner used by
Git-tracked validation. Each formerly excluded category must detect runtime
synthetic assignments and credential URIs across supported encodings, and accept
benign text. Tests also exercise changed/missing/malformed retirement evidence,
path boundaries, binary/generated handling and the existing seal. Mutation
controls must fail when ingestion skips those paths, scanning is bypassed, or
retirement content verification is bypassed. Failures are reported as path and
line or integrity errors, without printing credential values.

CI keeps the owning Server project in its existing unfiltered deterministic
lane. The existing Gitleaks configuration, exclusions, shared workflow references,
other gates and dependency pointers are not changed by this repair. Local
verification is distinct from current hosted CI or release qualification.


## Approved bounded semantic recognition — 2026-10-14

The user explicitly approved the recommended semantic repair after reviewing
strict blocking, semantic recognition and governed retirement alternatives.
Every textual evidence path remains ingested and scanned. No new retirement,
file exclusion, hash re-pin, generalized test-file allowance or `fixture-*`
placeholder rule is authorized.

The repair may recognize demonstrably nonliteral Python credential expressions
(variable concatenation and interpolation without literal credential operands),
Python source within explicitly delimited shell here-documents in Markdown,
and exact inert negative-test markers within rejection/redaction tests. The
existing detector must still reject literals/fallbacks in those expressions,
changed probe values, operational use of markers, URI credentials and adjacent
secret material. Unknown, malformed or unterminated syntax grants no recognition.

For deliberate synthetic probes, recognize only the fixture-bearing expression
in a bounded Python unit-test context with its rejection/redaction assertion.
All surrounding bytes continue through every detector. The fake Docker
inspection fixture needs its matching no-retention assertion; a name/prefix
alone proves nothing. Source-origin or packet provenance does not approve an
artifact-wide skip. If safe compact recognition cannot be demonstrated, retain
that finding as a blocker rather than adding a general language parser.

Add real file-ingestion controls for each of the nine historical triggers, with
benign originals, replaced literals/markers, broken proof context, adjacent
credentials, source boundaries and applicable encodings. Re-run the existing
content-ingestion, retirement and seal bypass controls, plus a recognition
widening control. Independently review and run focused/full owning gates before
DW-517 closure. Unrelated Gitleaks findings, CI wiring, provenance and release
holds remain separate and are not waived by this approval.

## Implemented recognition boundaries — 2026-10-14

DW-517 uses focused partials of the existing guard and its ingestion tests. It
recognizes a variable-only two-member `account_key` tuple, plus the complete
historical lifecycle mutation tuple that pairs this source with its second
runtime member. Quoted/static members and malformed tuples remain findings.
Python environment-file composition requires a complete `write_text` call with
an empty named field, a single runtime reference and a newline-delimited suffix.
Python interpolated fields require a complete one-line f-string assignment;
every interpolation must be a simple runtime reference, and the credential
field must end at a value boundary. Literal operands, fallback expressions,
format/conversion syntax, unbalanced braces and static field suffixes do not
qualify. Other assignments and all credential-URI detection remain active.
Embedded Python string assignments are treated as literal bytes unless one of
these bounded source forms proves their runtime construction.

Python source in Markdown shell blocks requires a closed matching plain fence
and a complete `python`/`python3 - <<DELIMITER` stdin here-document. Delimiters
may be unquoted or symmetrically single/double quoted. The terminating line must
contain exactly the delimiter; nested here-doc openings, mismatches, trailing
terminator whitespace, missing termination and missing fences grant no source
recognition. Python lexical context is limited to the corresponding closed
source region, so earlier Markdown prose cannot establish string context.

The five deliberate probe occurrences retain exact inert marker values and
complete, executable `unittest.TestCase` method bodies. Receipt probes require
the owning rejection helper with its `assertRaises`/`validate` proof; redaction
probes require both the marker-removal assertion and `safe` validation; the
fake Docker script requires its full inspection projection and no-retention
assertions. Changed names, class context, fixture statements, assertions or
additional method statements revoke recognition. Lookalikes inside Python
literals do not qualify. Only the fixture occurrence is replaced with the
established redacted marker for scanning; assertions, adjacent assignments,
URI credentials and every other byte remain available to the existing detectors.
LF and CRLF probe copies use the same structural check. No path, content-hash,
retirement or marker-prefix exemption was added.

The semantic ingestion cases read the actual nine original historical
lines/methods and create isolated files across all eight existing decoder lanes.
Additional cases reject literal/URI substitutions, surrounding assignments,
operational marker use, broken proof/source boundaries and quoted or extended
methods. The scanner and all three new/inherited ingestion partials are also
read and scanned as real isolated files, ensuring untracked additions are
validated before they can enter a future tracked-file CI run.

## Semantic review corrections — 2026-10-14

Independent review tightened the implemented boundaries without changing the
approval or preservation contract above. Generic quoted tuple text is no longer
recognized. Only a standalone variable tuple or the complete approved lifecycle
mutation row qualifies. Runtime reference segments reject Python reserved words.
Supported standalone source also requires complete preceding quote/comment and
delimiter state, with no outstanding line continuation. The approved mutation
row can occur within its owning `faults = [` list; other unmatched prefixes do
not establish source context.

Interpolated credential fields now terminate relative to their actual string
body. The end of that body, whitespace or a semicolon is required; an opposite
quote, escaped quote or literal prefix/suffix does not establish a boundary.
The maintained executor's complete parenthesized two-line runtime composition is
recognized only with its existing inert database/options tail. Changed operands,
affixes and credential URIs remain detected. The two maintained negative unit
fixtures now generate their values at runtime and construct a standalone f-string
before the same invalid-packet assertions; no additional fixed probe allowance
was introduced, and historical artifacts were not edited.

Markdown shell ownership now walks the enclosing block from its beginning. A
block must contain only top-level supported Python stdin commands, blank lines
and shell comments. Unsupported shell commands/data/quotes, nested openings and
unterminated documents reject the enclosing block; recognition never restarts
at an inner opening inside `cat` data, a shell string or Python string contents.

Probe and rejection-helper bodies must be complete methods. Additional
operational statements after a blank line, decorators on the class/probe/helper,
duplicate same-name probe definitions and demonstrated TestCase/assertion/proof
symbol shadowing revoke masking. The required ordinary `unittest` import must
also be present. Actual file-ingestion regressions cover each review reproduction,
body-boundary widening, the complete maintained runtime expression and all three
maintained Python source files. The initial review-round failures and their
correction are retained in the parent command evidence; no failed gate was hidden.

Residual review also requires supported spaces/tabs around method names when
counting duplicate probe/helper definitions, and complete supported declaration
syntax at method boundaries. Single-line annotated proof-symbol assignments
revoke recognition. An owning `faults` list must have its matching closing
bracket in the same source region; the existing delimiter check rejects quoted
or mismatched closing candidates. Adjacent two-line strings must contain a field
separator before the inert database tail, so the tail cannot extend a credential
that ends at the first string boundary. Real-ingestion controls cover each case.

Proof-symbol annotations now use a conservative colon veto, including the owning
class-qualified symbol check. Any colon immediately after a protected proof
symbol revokes probe recognition without consuming or parsing a type expression;
unknown annotations, including quoted semicolons, therefore cannot establish
proof. Direct equals-assignment checks remain active. File-ingestion controls
cover self, owning-class, TestCase and other used rejection/redaction/inspection
symbols under this boundary.

## NuGet asset metadata recognition — 2026-10-18

The Story 6.4 prerequisite repair recognizes six exact NuGet identities at
validated dependency metadata positions: the existing three IdentityModel
identities, Azure.Security.KeyVault.Secrets,
Microsoft.Extensions.Configuration.UserSecrets and
Microsoft.AspNetCore.Authentication.BearerToken. Central package versions require
the root NuGet assets version (3 or 4), object-valued targets and libraries, a matching target framework, and
the exact `project/frameworks/<framework>/centralPackageVersions` position with
an ordinary NuGet version. Existing package dependency recognition retains its
library, version and SHA512 checks. No general package-name or version placeholder
exception is added.

The two named framework reference assemblies require their exact name/version
library, `referenceassembly` type, `serviceable=false`, empty package hash, matching
runtime target and matching compile-only DLL target. This is a separate structural
proof; an empty hash never satisfies the package-hash branch. Every surrounding
field still reaches all credential detectors. Malformed JSON, misplaced metadata,
changed values, unknown names, missing linkage and adjacent credentials remain
findings. Tests exercise canonical file ingestion across the existing decoder
lanes and renamed copies of original assets. Historical evidence bytes, retirement
records, sealed hashes, path handling and CI wiring remain unchanged.

The same repair recognizes literal-free C# `GetString()` reads, including a
postfix null-forgiving operator, while preserving operand/fallback detection.
Maintained negative fixtures now generate their input at runtime. Historical
copies retain their original bytes and their findings.

xUnit XML display names use only the existing nine approved negative-fixture
identities, after decoding the reporter's escaped quotes. Recognition is limited
to the `name` attribute at `assemblies/assembly/collection/test`; every other
attribute and output remains scanned. XML CR, LF and CRLF coordinates and the
located raw attribute's decoded value must agree before any masking. Numeric
entity and mixed-newline controls protect adjacent credentials. Malformed XML,
DTDs and unknown fixture identities grant no recognition. No approved-name hash
was added.

Seven historical findings remain blocking: five literal assignments in the
preserved Story 3.6 source copies and two unapproved URI-bearing displays in
Story 6.4 Server XML reports. The repair grants no disposition for them. The
existing replacement/retirement contract above still applies; local focused
test passes do not establish a green tracked-content gate or Story 6.4 readiness.


## Approved exact-file retirement — 2026-10-18

The interactive repository owner selected option **1** after reviewing the
finite five-file proposal. The [recorded decision](../../_bmad-output/implementation-artifacts/evidence/6-4-historical-retirement-20261018/retirement-decision.json)
binds the unchanged proposal, verbatim user selection and
[authority overlay](../../_bmad-output/implementation-artifacts/evidence/6-4-historical-retirement-20261018/authority-overlay.json).
No personal owner name, separate role acceptance or production approval is inferred.

This new decision authorizes only the five exact path/SHA-256 tuples in that
record: two immutable Server XML reports and three immutable authority-test
source snapshots. Their bytes remain intact. The overlay withdraws dependent
present qualification authority through additive tombstones; historical indexes,
receipts, outcomes and source hashes are not rewritten. It supplies no replacement
full-suite/source-review evidence, CI seal, P1R acceptance or Story 6.4 completion.
The earlier seven-finding observation above remains historical diagnostic truth.

`ApprovedStory64RetirementHashes` enrolls these five exact paths without widening
any directory pattern. Each registry entry also requires the independently pinned
owner-decision digest and exact binding record. The reader validates the decision,
proposal, user selection, authority overlay and preserved artifact bytes before
retiring a file. Missing or changed dependencies, re-pinning, duplicate entries,
authoritative status and adjacent paths fail closed. Removing registry enrollment
restores ordinary credential detection. The two established Story 5.3 approvals
and the two unusable inherited OTLP entries retain their previous scope.

Canonical-ingestion controls exercise all five tuples and adverse decisions.
The original URI/literal probes are still detected when their preserved bytes
are scanned directly; no new fixture identity, detector relaxation or broad
language/path allowance is introduced. Fresh measured local results are recorded
separately from this owner decision; hosted CI and release qualification remain
separate gates.

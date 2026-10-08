# Package refresh evidence, 2026-10-08

The validated selection advances 21 of the 23 proposed package rows: Aspire stable
packages and EventStore's AppHost SDK to 13.6.1, Keycloak/Kubernetes to
13.6.1-preview.1.26506.6, the five FrontComposer rows to 4.6.0, AngleSharp to
1.8.4, and both Swashbuckle rows to 10.3.0. The ten SDK inventory expectations
are aligned to 13.6.1. Actual external SDK observations remain separate in
[sdk-consumer-inventory.json](sdk-consumer-inventory.json).

Both Verify packages remain at 33.3.1. The same isolated consumer fails
SponsorCheck SC021 with candidate 33.3.2 and retained 33.3.1. This is an existing
consumer configuration blocker, not evidence of a candidate regression. No
license, sponsorship, exemption, or ignored-license property was invented. The
owner must configure an authorized valid property before paired build and
snapshot-adapter validation can accept the candidate.

The entire Parties family remains at 1.1.1 because Server, ServiceDefaults, and
UI are unresolved. Microsoft.OpenApi remains at the latest proven 2.x pin,
2.12.2: Microsoft.AspNetCore.OpenApi 10.0.12 requires `[2.12.0, 3.0.0)`.
The Dapr integration preview and Folders.Aspire stable conditional are unchanged.
[decisions.json](decisions.json) records candidates, selection, disposition,
family, evidence, and recheck trigger for all 304 rows across 146 families.
The original discovery audit and historical evidence were preserved.

The final package-mode Release restore and serialized solution build passed
with zero warnings or errors. AppHost passed 145/145, Admin.UI/bUnit passed
1,082/1,082, and focused Contracts governance passed 65/65. The isolated
consumer passed 20/20 and compiled a valid FrontComposer projection through its
published source generator. [final-build-results.json](final-build-results.json),
[focused-test-results.json](focused-test-results.json), and
[accepted-consumer-tests.log](accepted-consumer-tests.log) retain the commands
and results.

Catalog/default and Folders evaluations, Dapr, exception inventory,
consumer authority, and documentation checks passed. The earlier authority run used
the existing repository test source's 16 exact standalone-evidence exclusions
and passed for 82 projects. The current 15-exclusion run with sealed-file preflight
is recorded under review follow-up verification below. Concurrent user edits to that exclusion inventory
were preserved. The raw scan reports historical fixture declarations; an
intermediate configured scan overlapped the temporary version rollback and
reported inconsistent snapshots. Those logs are superseded by the final scan
against hash-checked stable catalog bytes in
[final-governance-results.json](final-governance-results.json).

The final explicit AppHost startup at 13.6.1 passed: all 30 modeled resources
were Running/Healthy, and all eight named required services were awaited.
Console logs contained no TypeLoadException, MissingMethodException,
MissingFieldException, or FileLoadException. Aspire was stopped successfully.
[final-runtime-results.json](final-runtime-results.json) binds the commands,
required service names, current revisions, and input hashes before and after
the run; its inputs remained stable. The evaluated Aspire graph contains only
13.6.1 stable packages and the two aligned previews.

An earlier candidate run had a Tenants source compile failure for missing
administrator-verifier APIs. The original dated baseline already records that
source host as Finished. An exact 13.6.0 recheck and the final 13.6.1 run both
became fully healthy; all observations are retained. Concurrent Builds,
Memories, Platform, and Tenants checkout movements were preserved and are not
attributed to this upgrade. Claims apply to the recorded current inputs.

The initial authoritative audit regeneration failed because the catalog was
dirty relative to then-current Builds revision
`5dd29d2c596c1e93c63977b098ad61407d20560a`; its exact command and result are in
[audit-regeneration-pending.json](audit-regeneration-pending.json). A later external
commit `520abb5898ad44b30c0744e707b53cd94741e6b1` incorporated the validated
catalog and inventory bytes. The revised audit provenance check is recorded
separately; the earlier failure remains a historical observation. Authoritative
regeneration and default-path validation now pass for all 304 rows and 146
families against that commit. The installed local audit preserves every prior
package and family history entry, matches all tested selections, and binds
committed catalog/consumer provenance. Exact commands and hashes are in
[authoritative-audit-finalization.json](authoritative-audit-finalization.json).

Aspire and FrontComposer have authoritative accepted classifications.
AngleSharp/bUnit and the two Swashbuckle families retain their externally
committed selections with explicit compatibility evidence. Their authoritative
classification remains retained because owning-Builds committed direct-consumer
discovery has no representative for those families. The accepted-classification
failure and rejected candidate are retained separately; no direct consumer was
fabricated and no validator was relaxed. This limitation does not prevent
selection/provenance finalization. After local installation/validation, an
external commit incorporated the exact audit bytes at observed Builds revision
`ad52c5bdd4361c59eedf12a16620150006403584`. EventStore's SDK edit was local at
that capture; later external root commits incorporated it. Final review observed
root revision `b830d982`; this task performed no Git mutations.
[finalized-input-provenance.json](finalized-input-provenance.json) records the
final observed revisions/hashes and asserts unchanged tested runtime inputs.
[final-checkout-state.json](final-checkout-state.json) records the earlier
movement; finalization supersedes its then-pending audit status. No commit, push,
publication, nested submodule initialization, or external consumer source edit
was performed by this task.

## Reproduce the isolated consumer

The exact executed commands are in
[isolated-consumer-commands.json](isolated-consumer-commands.json). The passing
test invocation was:

```bash
dotnet /tmp/eventstore-package-consumers-ctn1wj7d/bin/Release/net10.0/Probe.dll
```

[isolated-consumer-source](isolated-consumer-source/) contains the final project,
version-free package references, catalog import, annotated projection, test
source, and SDK selection as inert `.txt` files. Copy them into a fresh temporary
directory, remove the `.txt` suffix, and point the import to this checkout's
Builds catalog. Build the project in Release with `-m:1`, then invoke its built
assembly. The original candidate/retained Verify input is separately captured
in [isolated-consumer-inputs.json](isolated-consumer-inputs.json); those failures
must remain visible until the owner supplies the missing configuration.

## Spec matrix verification

The following executed check passes all six rows:

```bash
python3 _bmad-output/implementation-artifacts/evidence/latest-packages-2026-10-08/verify-decisions.py
```

| Matrix row | Executed proof |
| --- | --- |
| Candidate | All 304 decisions match evaluated catalog bytes; 21 compatibility-validated selections match their listed candidates and the isolated restored graph. Whole Verify pair retained after both SC021 failures. Authoritative classifications are recorded separately. |
| Aspire | 145 AppHost tests, 20 consumer cases, final package-mode build, aligned evaluated graph, eight awaited required services, all 30 states healthy, no binary failures, successful stop. |
| Preview | Default preview and Folders 13.0.0 condition evaluated independently; Dapr family and both catalog validators passed. |
| Incomplete family | All nine Parties rows remain 1.1.1; all three unresolved observations and the whole-family recheck trigger are asserted. |
| Compatibility | Retained 2.12.2 and the actual ASP.NET Core nuspec ceiling are asserted; unchanged builds and runtime passed. |
| Provenance | Discovery and prior-audit digests are preserved; all selections and committed catalog provenance match; all prior history entries remain present. Regenerated authoritative audit validates for 304 rows/146 families. Three owning-consumer classification limitations remain explicit. Historical dirty-catalog rejection is preserved. |

The 20 isolated cases map to the 13 Aspire public-assembly cases, four
FrontComposer public-assembly cases, the SwaggerUI assembly case, HTML parsing,
and the FsCheck/xUnit property case (100 generated inputs). FrontComposer
SourceTools is exercised during compilation of `CatalogProjection`; the
Swashbuckle aggregate and generated Swagger dependencies are also exercised by
the solution build and running gateway.

## Review follow-up verification

Earlier command results remain time-bound history. The current Contracts source
defines 15 exact exclusions. Its two sealed-file digests and checksum-file
entries passed before the unchanged consumer-authority validator passed for
82 projects. [consumer-authority-sealed-preflight.json](consumer-authority-sealed-preflight.json)
and [consumer-authority-after-sealed-preflight.json](consumer-authority-after-sealed-preflight.json)
bind the current source, exclusions, sealed bytes, catalog, and validator.
The reproducible driver is [consumer_authority.py](consumer_authority.py).

The unchanged isolated consumer passed another 20/20 run. Its actual restored
`project.assets.json` and runtime `Probe.deps.json` are retained unchanged as the
only two entries in [Probe.resolved-inputs.zip](consumer-resolved/Probe.resolved-inputs.zip),
a standard deterministic ZIP. The passing assembly digest is retained too.
[consumer-resolved-capture.json](consumer-resolved-capture.json) binds the archive
and uncompressed entry hashes, the version-free source, current central pins,
and the passing test log. Raw plaintext copies remain in the recorded temporary
directory. The historical absolute import is checked against its
recorded capture origin and owned catalog suffix/digest, so validation remains
portable across relocated checkouts.

The explicit isolated runtime check passed again and stopped successfully.
[review-runtime-results.json](review-runtime-results.json) binds all eight
waits, the actual 30-resource describe list, current before/after source hashes,
and [sanitized console output](review-runtime-console.log). The zero binary
failure counts are recomputed from that retained output. Environment/property
payloads were removed from the retained full describe output; resource names,
states, and health remain intact.

The verifier now uses explicit failures that remain active under Python
optimization. It checks actual describe resources, exact required service
names, current source hashes, resolved manifests, source/test/digest bindings,
and the sealed preflight. This targeted exercise passed:

```bash
python3 _bmad-output/implementation-artifacts/evidence/latest-packages-2026-10-08/exercise_verifier.py
```

[review-verifier-checks.json](review-verifier-checks.json) records an optimized
passing packet and thirteen malformed copies rejected without changing original
captures, plus a valid historical import origin at a different checkout path.
It also checks the two corrected project-overview version cells and
the packet's `-text` rule under `core.autocrlf=true`. Existing capture bytes were
preserved.

The targeted secrets check uses the existing scanner's exact
`DecodeTrackedText` and `FindViolations(path, content)` path for packet files,
including new untracked captures. Its portable-PDB source digest binds the implementation to current
unchanged test source; earlier helper setup failures remain historical logs.
The owning Server.Tests project was rebuilt with its tracked test policy,
Release/package mode, and no restore after the prior binary's source digest
proved stale. No scanner source or warning policy was changed.
The earlier plaintext scan reported 32 findings in the exact raw assets/deps
captures, all at legitimate NuGet package metadata (including KeyVault Secrets,
UserSecrets, BearerToken, and restored project/framework entries). Its structural
metadata exception covers only three IdentityModel package names in selected
positions. Its exact failures remain in
[targeted-secrets-scanner-raw-json.log](targeted-secrets-scanner-raw-json.log).
The actual manifest bytes now remain inside the ZIP, which the existing decoder
recognizes as binary evidence. The verifier requires exactly those two members,
checks their uncompressed hashes, and parses their untouched JSON bytes.
This preserves resolved inputs and uses the existing artifact policy without
rewriting metadata or changing the scanner. The stored-packet scan result is in
[targeted-secrets-scanner.json](targeted-secrets-scanner.json).

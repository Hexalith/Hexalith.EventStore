# Story 3.15 independent Test Architect decision

**Decision: ACCEPT** the technical evidence for exact subject
`66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6`,
as `bmad:murat`, at 2026-09-26 07:39 UTC. This is a **local, self-attested decision report**,
without independent external authentication. It is not the canonical packet receipt, an owner
decision, or a passing parity verdict. The packet remains at **0/3 receipts**; its verifier exits 1
and selects no deployed identity.

## Basis for acceptance

- `subject.json` hashes to the exact subject above. Its four limitations include the corrected
  fourth statement: the **two owner comments** are composed by repository tooling and posted with
  the rostered credential, while the Test Architect source is local and self-attested. The owner
  registry maps both owner roles to `github:jpiquot` and Test Architect to `bmad:murat`; this is
  three roles, not three independently authenticated people.
- The Story 3.14 predecessor verifier reproduces identity
  `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`.
  The Story 3.15 verifier runs predecessor, package, OCI, smoke, registry, inventory, and subject
  checks before receipt validation. Its current failure is specifically the missing three receipts.
  The technical inventory's **24/24** checksums match. The closure lists 14 package identities
  and two raw OCI children under index
  `sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`.
- The fresh retained Production-hosting capture ran from `2026-09-26T07:29:14.107654Z` to
  `07:30:34.182021Z`. `linux/amd64` ran child `sha256:4d42f969...` with 9 readiness attempts;
  `linux/arm64` ran child `sha256:ede853318...` with 31. Both platform logs and the aggregate
  record report `/alive`, HTTP **200**, **0** redirects, observed platform match, exit code 0,
  and cleanup pass. Their files match the inventory hashes. The pinned capture producer pulls
  each child by digest, runs it with `ASPNETCORE_ENVIRONMENT=Production` and
  `DOTNET_ENVIRONMENT=Production`, probes the published loopback port, and places `-q` as curl's
  first argument. The recording-fake test asserts that actual argument order and request target;
  failure cases cover non-200/redirect replies, platform mismatch, and cleanup failure.
- The current capture, handler, verifier, and assembler bytes match their subject-bound SHA-256
  values (`7d134165...`, `b2b55079...`, `0a768dda...`, and `30328249...`, respectively).
  A Release build completed with zero warnings and errors. The two focused Story 3.15 test
  classes passed **235/235**, with zero failures, errors, skips, or unrun tests. Their positive
  receipt case uses synthetic fixtures in a temporary copy; it does not supply a current receipt.

## Limits and gate boundary

The logs are canonical records emitted by the bound capture producer; they do not retain a raw
terminal transcript of each Docker or curl command. The capture tests check the producer's command
behavior with recording fakes, and the fresh retained observations satisfy the verifier's bounded
schema. This is sufficient for the frozen Story 3.15 evidence scope, not proof of an already
deployed production service. I did not rerun Docker or fetch live registry, NuGet, or GitHub data.
The retained GitHub comment envelopes prove internal consistency when checked by the verifier;
they are not independently fetched or signed. NuGet archive bytes are checked, but the derived
download URLs have no retained service-response attestation. These are disclosed evidence limits.
I ran the focused Story 3.15 classes, not a fresh full Contracts suite.

This acceptance covers only immutable deployed-runtime parity evidence. It grants no deployment,
publication, registry mutation, consumer removal, predecessor change, or broader FR36 readiness.
The four authority flags are all `false`. The current `closure.json` fields
`deployed_runtime_parity: available` and `selected_deployed_identity` are a **claim pending 3/3**,
not the present verdict. The separate `G-HIGH-RISK` and later publication/promotion gates remain
open. Any bound input change invalidates this subject and requires a new decision.

## Commands and outcomes

Run from the repository root unless a `cd` is shown:

```sh
cd _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
sha256sum -c technical-sha256.txt
sha256sum subject.json
```

Result: exit 0; 24 `OK` entries; subject digest
`66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6`.

```sh
sha256sum tools/capture-corrected-deployed-runtime-parity-smokes.py tools/deployed_runtime_parity_handlers/v1.py tools/validate-corrected-deployed-runtime-parity.py tools/assemble-corrected-deployed-runtime-parity.py
```

Result: exit 0; digests match the four subject-bound values listed above.

```sh
python3 tools/validate-corrective-release-evidence.py _bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/release-identity.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d
```

Result: exit 0, `pass: sha256:4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`.
An initial invocation used
`python3 tools/validate-corrective-release-evidence.py _bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d`
and exited 1 with `No such file or directory`; the corrected command above uses its actual
`release-identity.json`.

```sh
python3 tools/validate-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
```

Result: exit 1, `fail: exactly three packet-bound receipts are required`.

```sh
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.CorrectedDeployedRuntimeParityClosureTests -class Hexalith.EventStore.Contracts.Tests.Packaging.CorrectedDeployedRuntimeParitySmokeCaptureTests -noLogo
```

Result: build exit 0, 0 warnings and 0 errors; focused tests exit 0, 235 total, 0 errors,
0 failed, 0 skipped, 0 not run.

`git diff --check` returned exit 0 with no whitespace errors.

No Test Architect packet source or receipt, owner comment, credentialed action, or external post
was created for this decision.

# Selected logical model input audit

The owner's 2026-10-08 selection authorizes the Dapr logical model and delegated
schema/control choices. The current preflight now binds the
[selection amendment](../../story-6-6-dapr-logical-model-amendment.md),
[exact model](../../story-6-6-dapr-logical-model.md), independent Python vector
producer and vector bytes separately from the unchanged historical AD-13 approval.
This audit grants no catalog, source, production-key, registration or activation
authority. All O-01 through O-20 remain open.

The [current audit](../../6-6-obligation-audit.json) retains all historical row
hashes and dispositions, adds the third controlling amendment and exact logical
model input hashes, and updates only O-06/O-11's current input-binding requirements.
Missing, changed, duplicate or malformed model inputs refuse. Model registration
and activation claims refuse. The existing blocking CI preflight invokes these
new checks; hosted execution has not been observed.

At HEAD `9542d3c9f48bf9ce1c57f2ef68904703eaba56cc`, the
[command packet](selected-model-audit-2026-10-08/commands.json) and
[source receipt](selected-model-audit-2026-10-08/receipt.json) record:

- `python3 scripts/verify-event-evolution.py --mutations`: passed; all 28 policy
  mutations rejected in separate processes, each with a 10-second timeout.
- `python3 scripts/verify-dapr-logical-model-vectors.py`: passed all eight
  independently recomputed Python vectors, including explicit actor metadata
  presence in the selected 19-field source record.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 -warnaserror`:
  passed, zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*EventEvolutionObligationAuditTests'`:
  passed 26/26, zero failures/skips/not-run. Actual temporary-tree controls delete
  or change each selected input and check its owning refusal. Unrelated files and
  Git metadata remain editable.

All seven pinned current input/test files were unchanged during this verification.
This is a focused preflight test result; it does not qualify concurrently changing
runtime source, all parent consumers or the Dapr topology. The earlier historical
audit receipts retain their original scope and bytes. The parent baseline remains
`1329b35e52852952ecb2c94aabf100674e9691e3`.

`test -f deploy/dapr/production-profile.yaml` returned exit 1 with no output.
That required production profile is still absent. Actual catalogs, immutable
framework/native execution, serving source/key/peer pins, broker/fleet and live
component evidence remain separate qualification work. Model selection is resolved;
those remaining requirements cannot be inferred from local vectors or this audit.

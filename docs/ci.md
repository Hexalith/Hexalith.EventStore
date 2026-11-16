# EventStore CI and qualification

The current legacy workflows remain active until CiCd delivery adopts this
module. The managed product actions are prepared for that adoption; adding them
alone does not establish a release, an adopted perimeter or a successful run.
CiCd owns workflow files, CI configuration, the Builds gitlink and its
`.gitmodules` section. Change that policy in Hexalith.CiCd and let delivery apply
it. Preserve legacy publishing and its root npm/Husky dependencies until the
adoption handoff retires them.

Managed CI restores and builds `Hexalith.EventStore.slnx` once in Release with
`UseHexalithProjectReferences=false`. Every retained product test project is in
the solution. The shared classifier runs ordinary unit tests, then environment
setup, the unit hook, ordinary integration/browser tests and the integration
hook. Enabled project exclusions always map to a real hook or qualification job.

| Coverage | Managed execution |
| --- | --- |
| Ordinary API/unit/adapter/package contracts | Shared unit stage |
| Browser, AppHost and other ordinary integration suites | Shared integration stage; browsers and Dapr enabled |
| Payload vectors and invariant globalization | `after-unit-tests`; existing Release assemblies, minimum counts and fail-skips retained |
| Azure authority schema | `after-unit-tests`, only `AzureIdentityAuthoritySchemaTests`; no Azure authority or custody secrets |
| Manifest package inventory, archive validation and all package consumers | `after-unit-tests`; pack existing Release output, then compile isolated consumers |
| Hosted checkpointed process API | The DomainService package consumer executes the real process-host proof |
| Folded snapshot compatibility | `after-unit-tests`; pack existing current-module output and compile isolated old/current consumers |
| Server live sidecars | `after-integration-tests`; fixture-owned Dapr 1.18.2 sidecar/placement/scheduler and private Redis ports |
| Published compatibility | `after-integration-tests`; original authenticated 1.18.4 private archives and isolated historical consumers |
| Payload lifecycle | `after-integration-tests`; isolated pinned daprd 1.18.7 and PostgreSQL fixture |
| Kubernetes crypto | `after-integration-tests`; lock-verified daprd 1.19.1, kind 0.31.0 and pinned node image; explicit 14-test lane |
| Real RabbitMQ/Kafka matrix | `nightly`; both retained original and candidate runtime profiles, exclusive worker custody and truthful non-promoting results |
| Real Azure conformance | Explicit rendered manual qualification on the designated private runner and approval environment |

Product fixture preparation may install isolated runtimes and create disposable
backends. It never replaces shared Dapr 1.19.1 infrastructure. Private 1.18.2
resources have unique names, loopback ports and exact owned container IDs;
cleanup checks owner labels and preserves the shared infrastructure snapshot.
Catalog and payload 1.18.7 selectors are independent of the shared runtime.
The existing Platform boundary still applies: these fixtures are retained until
an approved Platform replacement passes the same product contracts.

The manual `payload-azure` job requires all of the following:

- A compatible published immutable Builds release and successful ordinary CI
  at the exact qualification source SHA.
- Explicit `workflow_dispatch` selection `qualification=payload-azure` and
  exact lowercase `authority_sha256`; the default selection is `none`.
- Runner group `payload-protection-azure-private-ephemeral`, labels
  `[self-hosted, linux, x64]`, and approval environment
  `payload-protection-azure-conformance`.
- Explicit environment secrets `PAYLOAD_AZURE_AUTHORITY_JSON` and
  `PAYLOAD_AZURE_CUSTODY_JSON`, mapped only to the qualification action step.
- Owner-provisioned private networking and the existing hash-pinned daprd
  1.18.7 executable. Shared preparation builds once with `environment: preserve`.

The action verifies digest, exact source, expiry and custody binding before
service commands. The unchanged `AzureLiveAuthority.Load` enforces the complete
closed authority, resource/identity/network bindings, owner receipt and fault
windows before fixture initialization or Azure calls. Healthy tests and the
externally prepared fault-observation class keep their minimum counts and
fail-skips behavior. This job creates no owner authority, changes no Azure
resource or network, and does not prepare faults. Its authority files are mode
0600 beneath the disposable job directory and are removed on exit. Ordinary CI,
unattended releases and nightly jobs receive no Azure authority.

Run disposable verification beneath `run-with-temp`; package feeds, source
exports and output scratch belong under its `TMPDIR`. Keep useful logs outside
that directory. Do not suppress dependency audits or product failures. The
preparation exception in CiCd's story 20 is limited to committed pre-adoption
product pushes and expires on accepted adoption. After adoption, every module
push requires successful `python3 references/Hexalith.Builds/check.py`.

Release publication follows CiCd's central scheduled nightly process. The story
remains open until the actual adopted perimeter, normal checker, green CI and
next scheduled publication or valid no-release outcome are recorded. A manual
Azure request still requires its separate owner authority; a missing prerequisite
or an unexecuted lane is reported as such.

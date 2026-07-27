# Story 2.12 Source-Lane Evidence — 2026-07-27

- Evidence date: `2026-07-27`
- Lane: source (Debug, `UseHexalithProjectReferences=true`)
- Lane status: `passed` (focused scope; see the recorded solution-level blocker)
- Package lane: `blocked` (unchanged — see `prerequisites.md`)
- Proof working copy: separate clean Tenants clone, not a nested submodule of the
  EventStore umbrella. Only Tenants-root-declared submodules were initialized; no
  `--recursive` and no `--remote` update was used.

This file is intentionally not named for a Tenants SHA. The proof commit below is a local,
unpublished, maintainer-unaccepted commit. The SHA-named receipt directory is created only
after an accepted Tenants commit exists.

## Authority Revalidation (same shell)

The complete official-main A/B/C verifier was extracted from the packet block and executed
from the EventStore repository root, then the source consumer procedure was executed in the
same shell without reassigning any artifact pin.

```text
EVIDENCE_COMMIT_A=b695ad3215cd873c41561635e4eb4d7ff29d56a2
POINTER_COMMIT_B=ed48057e9bf9cb5e5e8667fec84f7c70e4534eea
AUTHORIZATION_COMMIT_C=1b219d39cfa8f0349175c356001ba539bfb4aa92
AUTHORIZATION_VERIFICATION_PHASE=official-main
```

- Complete A/B/C verifier: **exit 0** (all committed evidence manifest hashes, live provider
  proof comparison, approval-role allowlist, and status transitions passed).
- The 2026-07-27 AWK defect recorded earlier in the story Debug Log did not recur; the
  verifier ran to completion from EventStore `main` at `347e0df0`.

## Published-Main Identity Regression (AC2)

The source consumer procedure was first run against Tenants `main` exactly as published.

| Fact | Value |
| --- | --- |
| Approved EventStore SHA | `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` |
| Tenants `main` head | `230a533d0f2e178425497b2c59f0136186ecc794` |
| Its `references/Hexalith.EventStore` gitlink | `737b3e5a7113de6105e233459203e988af0f78d4` |
| Its EventStore checkout | `737b3e5a7113de6105e233459203e988af0f78d4` |
| Consumer worktree clean | yes |
| EventStore worktree clean (incl. ignored) | yes |

Result: **fail closed** at `test "$GITLINK_SHA" = "$APPROVED_EVENTSTORE_SHA"`. The identity
mismatch is the sole failing assertion; cleanliness passed.

Root cause, from the Tenants gitlink history:

- `902065e` (`feat(deps): adopt approved EventStore source identity`) and its PR merge
  `db09a84` correctly set the gitlink to `fa2d1c99`.
- `a7ca142` (the conditional Gateway change) still carried `fa2d1c99`.
- The mechanical merge `230a533` (`build: merge feat/story-2-12-runtime-identity-adoption
  into main via /pushall`) combined `8e84bf1` and `a7ca142` and resolved the gitlink to the
  main-side value `737b3e5a`, silently discarding the approved runtime pin.

This is a mechanical merge clobber, not a recorded identity decision.

## Restored Pin And Green Source Guard

The approved pin was restored in the proof clone as local commit
`3c2aeaa299754b4f1a8575cfdf08f9a29f10b213`
(`fix(deps): restore the approved EventStore source identity`; subject validated by
commitlint, exit 0). Content is otherwise identical to Tenants `main` at `230a533d`,
including the already-published conditional Gateway alignment.

Re-running the complete A/B/C verifier and the source consumer procedure in one shell:

```text
VERIFIER_OK
SOURCE_CONSUMER_OK
EXIT=0
```

Gitlink and checkout both equal `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`.

## Resolved Source Graph (AC2, AC4 source half)

`dotnet restore src/Hexalith.Tenants/Hexalith.Tenants.csproj --force-evaluate
-p:Configuration=Debug -p:UseHexalithProjectReferences=true
-p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false -nodeReuse:false -m:1`
→ exit 0.

`obj/project.assets.json` EventStore libraries:

| Type | Library | Path root |
| --- | --- | --- |
| project | `Hexalith.EventStore.Admin.Abstractions/999.1.20-proof.fa2d1c9910f8` | `references/Hexalith.EventStore` |
| project | `Hexalith.EventStore.Client/999.1.20-proof.fa2d1c9910f8` | `references/Hexalith.EventStore` |
| project | `Hexalith.EventStore.Contracts/999.1.20-proof.fa2d1c9910f8` | `references/Hexalith.EventStore` |
| project | `Hexalith.EventStore.DomainService/999.1.20-proof.fa2d1c9910f8` | `references/Hexalith.EventStore` |
| project | `Hexalith.EventStore.Gateway/999.1.20-proof.fa2d1c9910f8` | `references/Hexalith.EventStore` |
| project | `Hexalith.EventStore.Server/999.1.20-proof.fa2d1c9910f8` | `references/Hexalith.EventStore` |
| project | `Hexalith.EventStore.ServiceDefaults/999.1.20-proof.fa2d1c9910f8` | `references/Hexalith.EventStore` |

- EventStore edges resolved as `type: package`: **0**
- EventStore edges resolved as `type: project`: **7**

Gateway and DomainService resolve identically, so no mixed Gateway-project /
DomainService-package graph is reachable in source mode.

Build: `dotnet build src/Hexalith.Tenants/Hexalith.Tenants.csproj --configuration Debug
--no-restore --warnaserror ... -m:1` → exit 0, **0 Warning(s), 0 Error(s)**. All seven
EventStore assemblies, including `Hexalith.EventStore.Gateway`, compiled from
`references/Hexalith.EventStore` at the approved checkout.

## Focused Test Matrix (Debug/source)

| Project | Restore | Build (`--warnaserror`) | Tests |
| --- | --- | --- | --- |
| `Hexalith.Tenants.Contracts.Tests` | exit 0 | 0 warn / 0 err | **115 passed, 0 failed** |
| `Hexalith.Tenants.Server.Tests` | exit 0 | 0 warn / 0 err | **738 passed, 0 failed** |
| `Hexalith.Tenants.UI.Tests` | exit 0 | 0 warn / 0 err | 1260 passed, **1 failed** |
| `Hexalith.Tenants.IntegrationTests` | **exit 1 (NU1102)** | not reached | not reached |

The `Contracts.Tests` pass includes the published `PackageGovernanceTests` host
Gateway/DomainService rule.

## Recorded Blockers (not passed, not weakened)

Both blockers below reproduce on Tenants `main` as published and are independent of the
restored EventStore gitlink. They were reproduced at the unmodified `230a533d` checkout.

### B1 — Solution-level restore fails: catalog pins unpublished package bytes

```text
dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Debug \
  -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false \
  -p:HexalithCommonsFromSource=false -nodeReuse:false -m:1
→ exit 1
references/Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj
  : error NU1102: Unable to find package Hexalith.EventStore.Client
    with version (>= 999.1.20-proof.fa2d1c9910f8)
  : error NU1102:   - Found 143 version(s) in nuget.org [ Nearest version: 3.82.0 ]
```

Reproduced identically at unmodified Tenants `main` `230a533d` (exit 1, same NU1102), so it
is pre-existing and not caused by the pin restore.

Cause: Tenants `main` already carries Builds gitlink
`0e464b5410b487cee50b9523da3eedd0eec74589`, whose catalog sets
`HexalithEventStoreVersion` to `999.1.20-proof.fa2d1c9910f8`. That version has never been
published, so every package-resolved `Hexalith.EventStore*` edge — including sibling
submodule projects such as `Hexalith.Memories.Server` — is unresolvable.

This means package identity was adopted in Tenants **before** the byte-availability receipt
passed, which the story's External Prerequisite Contract forbids.

### B2 — `IntegrationTests` unrestorable, and one UI composition test red

`Hexalith.Tenants.IntegrationTests` fails restore with the same NU1102 edge, so its lane
produced no evidence.

`TenantsUiCompositionTests` performs its own package-mode restore and correctly fails closed:

```text
src/Hexalith.Tenants.UI/Hexalith.Tenants.UI.csproj : error NU1102:
  Unable to find package Hexalith.EventStore.Client
    with version (>= 999.1.20-proof.fa2d1c9910f8)
src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj : error NU1102:
  Unable to find package Hexalith.EventStore.Contracts
    with version (>= 999.1.20-proof.fa2d1c9910f8)
```

This is the guard behaving correctly, not a test defect. It is red because package mode is
genuinely broken on `main`. No production policy or test was weakened to make it pass.

## Scope Statement

No Builds gitlink was changed. No package identity was adopted. No EventStore submodule
content was edited. No `Version`, `VersionOverride`, fallback property, or local
`PackageVersion` entry was added to Tenants. Nothing was pushed to any remote. The story
remains below `review`.

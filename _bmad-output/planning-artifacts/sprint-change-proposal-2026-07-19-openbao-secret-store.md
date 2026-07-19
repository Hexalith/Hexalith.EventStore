---
title: Sprint Change Proposal - OpenBao-Backed DAPR Secret Store
status: final
created: 2026-07-19
project: eventstore
mode: incremental
scope_classification: moderate
approval: approved
approved_by: Administrator
approved_on: 2026-07-19
finalized_on: 2026-07-19
handoff_status: recorded
handoff_recipients:
  - Product Owner
  - Developer
  - Architect
  - Test Architect
  - Platform/Operations owner
target_epic: 7
target_story: "7.6"
requirements:
  - FR34
  - NFR4
  - NFR17
architecture_decision: AD-24
---

# Sprint Change Proposal: OpenBao-Backed DAPR Secret Store

## 1. Issue Summary

Story 7.6 currently requires an approved secret store but does not name the
provider, bind the Aspire topology to that provider, require application code to
use the DAPR Secrets API, or prevent Kubernetes Secrets from becoming the
application secret system of record. The requested correction is to make
OpenBao the production operational/application secret store, expose it through
the canonical DAPR `openbao` component, add a real OpenBao resource to the
Aspire AppHost, and verify that application code retrieves a real secret through
DAPR rather than through a provider SDK.

The current implementation does not satisfy that outcome:

- `src/Hexalith.EventStore.AppHost/Program.cs` provisions no OpenBao resource.
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` exposes only
  the state-store and pub/sub components to the EventStore sidecars.
- No `secretstores.hashicorp.vault` component named `openbao` exists under
  `src/Hexalith.EventStore.AppHost/DaprComponents` or `deploy/dapr`.
- Credential-bearing production components under `deploy/dapr` still use
  environment placeholders rather than `secretKeyRef` with
  `auth.secretStore: openbao`.
- Application source contains no `GetSecretAsync` call. The Admin UI service
  credential is currently read directly from `IConfiguration`.
- `samples/deploy/kubernetes/secrets-template.yaml` presents Kubernetes Secrets
  and `secretstores.kubernetes` as the primary pattern.
- The security, Kubernetes, DAPR, deployment, and brownfield documentation still
  describes Kubernetes Secrets, environment files, or provider-neutral stores
  without an OpenBao-first contract.

DAPR officially supports OpenBao through the stable
`secretstores.hashicorp.vault` v1 component:
https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/.
The compatible Vault component accepts either an inline token or a mounted token
file; this proposal selects `vaultTokenMountPath` as the preferred production
bootstrap mechanism:
https://docs.dapr.io/reference/components-reference/supported-secret-stores/hashicorp-vault/.

Kubernetes Secrets are not prohibited absolutely because DAPR/OpenBao
authentication still has a bootstrap boundary. They are permitted only for the
least-privilege OpenBao bootstrap credential when the hosting platform cannot
provide an approved mounted or projected token. They are never the system of
record for database, broker, application, signing, or other operational secrets.

## 2. Impact Analysis

### 2.1 Epic And Story Impact

- Epic 7 remains valid. No new epic is required.
- Story 7.6 remains the correct implementation unit and is rewritten rather
  than split or replaced.
- Stories 5.6 through 5.9 must remain aligned because they govern AppHost
  component loading, production component parity, topology drift evidence, and
  operator documentation.
- Story 7.7 consumes Story 7.6 readiness behavior for missing, denied, or
  unavailable secrets.
- Stories 7.10 and 7.11 may reuse the real-host integration lane but do not
  acquire new scope.
- No completed story is invalidated and no epic needs resequencing.

### 2.2 Artifact Impact

| Artifact | Required adjustment |
| --- | --- |
| `prd.md` | Make FR34 and NFR17 OpenBao/DAPR-specific; retain NFR4 wording; add Story 7.6 to NFR4/NFR17 traceability. |
| `epics.md` | Replace Story 7.6 with the approved OpenBao-backed story contract. |
| `architecture.md` | Preserve AD-24 and add Aspire development topology, mounted-token preference, and default Kubernetes-store containment. |
| `sprint-status.yaml` | Keep `7-6-secret-store-configuration` at `backlog` until implementation begins; no new story ID. |
| Aspire AppHost/library | Add the OpenBao resource, canonical DAPR component, resource references, and shared-component propagation. |
| DAPR components | Add the OpenBao component/contract and replace production credential environment placeholders with OpenBao-backed references. |
| Admin UI | Retrieve its configured service credential through DAPR/OpenBao in production. |
| Kubernetes sample | Remove Kubernetes Secrets as the recommended application-secret pattern; retain only a clearly bounded bootstrap example if required. |
| Tests | Add AppHost model, structured YAML, application retrieval, negative security, and real OpenBao+DAPR integration evidence. |
| Documentation | Align security, Kubernetes, DAPR, deployment, integration, and operator guidance. |
| UX | No user-flow or visual-design impact. |

### 2.3 Technical Impact

The Aspire resource graph gains one infrastructure resource and one shared DAPR
component. Every consuming sidecar must reference the exact same component
identity; component duplication by application or deployment slice is forbidden.
Because the AppHost model changes, implementation validation must restart the
Aspire host before inspecting the resulting resource graph.

Production DAPR component credentials move from environment placeholders to
logical OpenBao maps. The platform overlay remains responsible for provider
addresses, engine/prefix selection, TLS trust, token projection, policies, and
secret values. The repository owns stable logical names, value shapes,
consumers, scopes, and validation.

The Admin UI becomes the first real application-code consumer. Its existing
service credential is retrieved with `DaprClient.GetSecretAsync("openbao", ...)`.
The existing authentication flow is not redesigned. A direct OpenBao/Vault HTTP
client or SDK is explicitly prohibited.

### 2.4 Delivery Impact

- Planning scope: Moderate.
- Implementation effort: Medium.
- Technical risk: Medium; bootstrap ordering, DAPR component initialization,
  least-privilege scope, and fail-closed readiness must agree.
- Schedule impact: Story 7.6 grows from manifest scanning into an actual Aspire,
  DAPR, application, and integration slice.
- MVP impact: None. This hardens an already committed operational requirement.

## 3. Recommended Approach

Use a **Direct Adjustment** within Epic 7.

1. Approve the revised Story 7.6, PRD wording, and AD-24 amendments.
2. Create red tests for the AppHost model, component YAML, DAPR application
   retrieval, and Kubernetes-store denial.
3. Add real local OpenBao and the canonical DAPR component to the Aspire model.
4. Move production DAPR component credentials to `openbao` references.
5. Migrate one real application secret consumer through the DAPR Secrets API.
6. Prove the positive, missing, and denied cases against real OpenBao and DAPR.
7. Align deployment samples and documentation.

Rollback or MVP reduction is not appropriate: the existing requirements already
commit the product to secret-store-backed configuration, and a provider-neutral
implementation would leave the current ambiguity unresolved.

## 4. Detailed Change Proposals

### 4.1 Story 7.6

**OLD**

#### Story 7.6: Secret-Store Configuration

**Requirements covered:** FR34, NFR4, NFR17

As a production operator,
I want deployment secrets resolved through approved secret stores,
So that committed component configuration contains no forgeable operational
credentials.

**Acceptance Criteria:**

**Given** production DAPR or application configuration requires a secret
**When** manifests are parsed
**Then** the value resolves through the named secret-store component and
`secretKeyRef`
**And** no plaintext key, token, password, signing key, or unsafe `{env:...}`
substitute remains in committed production posture.

**Given** a required secret or store is missing
**When** deployment validation runs
**Then** it fails before the dependent resource starts
**And** diagnostics name the configuration key without disclosing the value.

**NEW**

#### Story 7.6: OpenBao-Backed DAPR Secret Store

**Requirements covered:** FR34, NFR4, NFR17
**Architecture gate:** AD-24.
**Owner / review boundary:** Winston (Architect); Amelia (Developer) implements
the application and deployment slice; Murat (Test Architect) reviews the
real-provider and negative security evidence.
**Focused validation:** Aspire model inspection, structured DAPR YAML tests,
application secret-retrieval tests, and a real OpenBao+DAPR integration lane.

As a production operator,
I want operational and application secrets resolved through an OpenBao-backed
DAPR component,
So that applications remain provider-independent and Kubernetes Secrets are not
the system of record.

**Acceptance Criteria:**

1. The Aspire AppHost provisions a pinned, health-checked OpenBao resource for
   local development and registers the canonical DAPR component `openbao` using
   `secretstores.hashicorp.vault`, version `v1`.
2. Production configuration connects to externally managed OpenBao with TLS
   verification enabled. Authentication uses `vaultTokenMountPath` or an
   equivalent platform-projected bootstrap credential. A Kubernetes Secret is
   permitted only when no approved bootstrap mechanism is available; it never
   stores application or operational secrets.
3. State-store, pub/sub, and other DAPR components obtain credentials through
   `secretKeyRef` with `auth.secretStore: openbao`; committed production
   configuration contains no credential-bearing `{env:...}` placeholders.
4. Application code obtains required secrets through an injected DAPR client
   using `GetSecretAsync("openbao", ...)`. It contains no OpenBao/Vault client
   dependency, direct provider HTTP calls, or bulk-secret retrieval.
5. DAPR secret scopes are default-deny and explicitly allow only the logical
   secrets required by each application.
6. Missing, denied, or malformed secrets fail closed, gate readiness where
   required, identify only the logical key in diagnostics, and never disclose
   secret values.
7. Automated verification proves the AppHost resource graph, component
   references, manifest rules, and a real DAPR-to-OpenBao read against a seeded
   non-production secret.
8. Kubernetes deployment samples and operator documentation prefer OpenBao and
   describe the narrow bootstrap-only Kubernetes Secret exception.

### 4.2 PRD Requirements And Traceability

#### FR34

Replace the phrase:

> add secret-store-backed configuration

with:

> add OpenBao-backed DAPR secret-store configuration for production operational
> and application secrets, require application retrieval through the DAPR
> Secrets API, and restrict Kubernetes Secrets to documented bootstrap
> credentials only when no approved mounted or projected credential mechanism is
> available

The remainder of FR34 stays unchanged.

#### NFR17

**OLD**

> Operational hardening must support secret stores, DAPR app health checks,
> readiness-tagged health checks, resiliency targets, immutable image tags, and
> documented crypto-shred boundaries.

**NEW**

> Operational hardening must use the canonical DAPR `openbao` component for
> production operational and application secrets. Dependent DAPR components must
> use `secretKeyRef` with `auth.secretStore: openbao`; application code must use
> the DAPR Secrets API; and per-application access must be default-deny. OpenBao
> bootstrap credentials are platform inputs and may use Kubernetes Secrets only
> when no approved mounted or projected mechanism is available. Operational
> hardening must also support DAPR app-health checks, readiness-tagged health
> checks, resiliency targets, immutable image tags, and documented crypto-shred
> boundaries.

NFR4 wording remains unchanged. Add Story 7.6 to the NFR4 and NFR17
traceability rows. Keep FR34 assigned to Epic 7.

### 4.3 AD-24 Amendments

Preserve the existing AD-24 decision and add these rules.

#### Aspire Development Topology

> The Aspire AppHost provisions a pinned official OpenBao container for local
> development, exposes a health-checked endpoint, and makes the canonical
> `openbao` DAPR component available only to sidecars that require secrets.
> OpenBao development mode is explicitly non-production. Its bootstrap token is
> supplied through an Aspire secret parameter or protected temporary token file
> and is never committed or logged. Unit tests may use fakes, but integration and
> release evidence must exercise real OpenBao through DAPR.

OpenBao documents that its development server is in-memory and insecure:
https://openbao.org/docs/next/concepts/dev-server/.

#### Bootstrap Authentication

> Production prefers `vaultTokenMountPath` backed by a platform-projected,
> least-privilege token file. A Kubernetes Secret may hold only this bootstrap
> credential when no approved mounted or projected mechanism is available. It
> must not contain downstream application, database, broker, signing, or
> operational secrets.

#### Kubernetes Default-Store Containment

> Where DAPR automatically provisions the `kubernetes` secret store, every
> application's DAPR Configuration explicitly applies `defaultAccess: deny` to
> that store as well as to `openbao`. No application may retrieve secrets from
> the Kubernetes store unless a separately approved bootstrap-only exception
> identifies the exact consumer and key.

DAPR documents that the default Kubernetes store is automatically provisioned
and must be included in secret scoping to restrict it:
https://docs.dapr.io/reference/components-reference/supported-secret-stores/kubernetes-secret-store/.

### 4.4 Implementation Boundary

#### Aspire And DAPR Topology

- Update `src/Hexalith.EventStore.AppHost/Program.cs` to provision, health-check,
  and order local OpenBao before dependent sidecars.
- Update `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`,
  `HexalithEventStoreResources.cs`, `AspireDaprSharedComponents.cs`, and
  domain-module wiring to expose and propagate `openbao`.
- Add `src/Hexalith.EventStore.AppHost/DaprComponents/openbao.yaml` and the
  required per-application DAPR Configuration resources.
- Add `deploy/dapr/openbao-secret-contract.yaml` and the production component/
  overlay contract without embedding provider credentials.

#### Real Application Consumer

- Migrate the Admin UI service credential currently read from `IConfiguration`
  in `AdminApiAccessTokenProvider.cs` to a startup-only OpenBao logical map.
- Register/reuse the centrally pinned `Dapr.Client` and retrieve the map through
  `GetSecretAsync("openbao", ...)`.
- Permit an explicit development-only local fallback; production fails closed
  without the declared OpenBao secret.
- Preserve the existing authentication grant and access-token cache behavior.
- Add no OpenBao/Vault client package or direct provider call.

#### Production Components

Convert credential-bearing metadata in the PostgreSQL, Cosmos DB, RabbitMQ,
Service Bus, Kafka, Redis, and any other in-scope DAPR component from
credential-bearing environment placeholders to `secretKeyRef` with
`auth.secretStore: openbao`. Preserve non-secret endpoint and topology
configuration as ordinary values.

#### Kubernetes Migration

Replace `samples/deploy/kubernetes/secrets-template.yaml` as the recommended
application-secret pattern. Any retained Kubernetes Secret example is renamed
and documented as OpenBao bootstrap-only. Explicitly deny application access to
the default `kubernetes` DAPR store.

### 4.5 Verification

Add:

- AppHost model tests for the OpenBao resource, immutable image pin, endpoint,
  health/wait relationship, component identity, and consuming sidecar
  references.
- Structured YAML tests for `secretstores.hashicorp.vault`, canonical naming,
  `secretKeyRef`, `auth.secretStore: openbao`, TLS posture, app/component scopes,
  secret-contract parity, and prohibited credential fallbacks.
- Admin UI tests proving DAPR retrieval, cancellation, missing/denied-secret
  failure, bounded caching, development-only fallback, and redacted diagnostics.
- A real OpenBao+DAPR integration test that seeds a non-production credential,
  exercises the application consumer, and proves missing and denied access fail
  closed.
- A dependency/source scan rejecting OpenBao or Vault client packages, direct
  provider calls, `BulkGetSecret`, and production configuration reads that
  bypass the DAPR secret path.

Implementation validation uses the narrow test projects first:

1. `tests/Hexalith.EventStore.AppHost.Tests`
2. `tests/Hexalith.EventStore.Admin.UI.Tests`
3. `tests/Hexalith.EventStore.Server.Tests` DAPR component/security tests
4. `tests/Hexalith.EventStore.IntegrationTests` focused OpenBao lane

Then restart the changed AppHost and inspect the live Aspire model with
`aspire start --isolated --non-interactive`, `aspire wait`, and
`aspire describe` before running the focused integration proof.

### 4.6 Documentation

Align:

- `docs/guides/security-model.md`
- `docs/guides/deployment-kubernetes.md`
- `docs/guides/dapr-component-reference.md`
- `deploy/README.md`
- `docs/brownfield/deployment-guide.md`
- `docs/brownfield/integration-architecture.md`
- `docs/ci-secrets-checklist.md`

The documentation must distinguish OpenBao-held application/operational secrets
from the bootstrap token required for DAPR to authenticate to OpenBao, describe
default-deny scoping for both `openbao` and the default `kubernetes` store, and
state that payload-protection KEK custody is outside this change.

### 4.7 Explicitly Out Of Scope

- Production OpenBao HA, storage, initialization, unsealing, backup, and disaster
  recovery topology.
- Payload-protection KEK/HSM custody or AD-23 semantics.
- Executing a production Kubernetes deployment.
- Redesigning Keycloak or the Admin UI authentication flow.
- Adding a direct OpenBao/Vault dependency.
- Changing unrelated stories, release work, or current user-owned changes.

## 5. Implementation Handoff

### 5.1 Scope Classification

**Moderate.** The change remains within Epic 7 and Story 7.6 but crosses
planning requirements, architecture, Aspire orchestration, DAPR component
configuration, one application consumer, deployment examples, tests, and
operator documentation.

### 5.2 Recipients And Responsibilities

- **Winston / architecture:** approve AD-24 amendments, logical secret contract,
  component ownership, bootstrap boundary, and least-privilege scopes.
- **Amelia / development:** implement the AppHost/Aspire model, DAPR component
  wiring, production component migration, Admin UI retrieval path, and focused
  unit tests.
- **Murat / test architecture:** review YAML invariants, provider-import bans,
  failure/redaction behavior, and real OpenBao+DAPR evidence.
- **Platform/operator owner:** supply environment-specific OpenBao endpoint,
  engine/prefix, TLS trust, projected bootstrap token, ACL policies, secret
  values, and production OpenBao lifecycle.
- **Product/technical owner:** approve this course correction before
  implementation begins.

### 5.3 Implementation Sequence

1. Apply the approved PRD, epic/story, and AD-24 planning edits.
2. Add failing topology, YAML, application, and security tests.
3. Add the local OpenBao Aspire resource and canonical DAPR component.
4. Add the logical secret contract and default-deny configurations.
5. Migrate production component credentials.
6. Migrate the Admin UI service credential through the DAPR Secrets API.
7. Run narrow unit/manifest checks.
8. Restart and inspect the Aspire host.
9. Run the real OpenBao+DAPR integration proof.
10. Align documentation and run final focused validation.

### 5.4 Success Criteria

The implementation is complete only when:

1. Aspire models a healthy OpenBao resource and the canonical `openbao` DAPR
   component.
2. Every required sidecar references the singleton component and no unrelated
   sidecar gains access.
3. Credential-bearing production DAPR component metadata uses OpenBao-backed
   `secretKeyRef` values.
4. The Admin UI retrieves its production service credential through
   `DaprClient.GetSecretAsync` and contains no direct provider integration.
5. Both `openbao` and the default Kubernetes store are default-deny per
   application.
6. Kubernetes Secrets, if present, contain bootstrap material only and are
   justified by the deployment profile.
7. Missing and denied secrets fail closed without disclosing values.
8. Real OpenBao+DAPR integration evidence passes.
9. Documentation consistently describes OpenBao as the preferred production
   application/operational store.

This approved proposal authorizes the planning corrections and implementation
handoff. Source implementation is intentionally left to Story 7.6 execution.

## Appendix A - Change Navigation Checklist

### 1. Understand The Trigger And Context

- [x] Trigger identified as provider ambiguity and missing implementation in
  backlog Story 7.6.
- [x] Current AppHost, DAPR YAML, application code, Kubernetes sample, and
  documentation inspected.
- [x] Official DAPR/OpenBao component, bootstrap, and secret-scope behavior
  verified.

### 2. Epic Impact Assessment

- [x] Epic 7 remains valid.
- [x] Story 7.6 is the direct adjustment target.
- [x] Coupling with Stories 5.6-5.9 and 7.7 identified.
- [x] No new epic, resequencing, or completed-story invalidation required.

### 3. Artifact Conflict And Impact Analysis

- [x] FR34, NFR4, and NFR17 assessed.
- [x] Existing AD-24 draft preserved and its remaining gaps identified.
- [x] UX confirmed unaffected.
- [x] Deployment, tests, samples, and documentation impacts enumerated.

### 4. Path Forward Evaluation

- [x] Direct adjustment selected.
- [x] Rollback rejected because it preserves the ambiguity.
- [x] MVP reduction rejected because the capability is already committed.
- [x] Effort, risk, sequencing, and scope classification recorded.

### 5. Sprint Change Proposal Components

- [x] Issue summary completed.
- [x] Artifact and technical impact completed.
- [x] Recommended path completed.
- [x] Story, PRD, architecture, implementation, verification, and documentation
  edits reviewed incrementally and approved.
- [x] Implementation handoff and success criteria completed.

### 6. Final Review And Handoff

- [x] Internally consistent proposal assembled.
- [x] Final proposal approval recorded.
- [x] Planning artifacts updated.
- [x] Implementation authorized and handed off.

## Appendix B - Approval And Handoff Log

| Field | Recorded outcome |
| --- | --- |
| Approval | Administrator approved the complete proposal on 2026-07-19. |
| Issue addressed | Story 7.6 and the current implementation were provider-neutral and did not bind Aspire/application secret retrieval to OpenBao through DAPR. |
| Change scope | Moderate; Epic 7 backlog and implementation coordination. |
| Planning artifacts modified | `prd.md`, `epics.md`, `architecture.md`, and this approved proposal. |
| Tracker outcome | `7-6-secret-store-configuration` remains `backlog`; no story key or status change was required. |
| Source artifacts intentionally unchanged | AppHost, Aspire library, DAPR/deployment YAML, Admin UI, tests, Kubernetes samples, and documentation. |
| Routed to | Product Owner and Amelia/Developer, with Winston/Architect, Murat/Test Architect, and the Platform/Operations owner as approval/evidence boundaries. |
| Immediate next step | Select Story 7.6 for implementation and execute the approved sequence in section 5.3. |
| Completion gate | All nine success criteria in section 5.4 pass, including a real OpenBao+DAPR application-secret read and default-deny Kubernetes-store evidence. |

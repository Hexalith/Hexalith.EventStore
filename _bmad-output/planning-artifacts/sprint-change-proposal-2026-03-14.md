# Sprint Change Proposal — DDD Message Design Integration

**Date:** 2026-03-14
**Triggered by:** Brainstorming session 2026-03-13 — "DDD Message Design for Hexalith.Tenants"
**Author:** Jerome (facilitated by PM agent)
**Change scope:** Moderate
**Mode:** Incremental (each proposal reviewed individually)

---

## Section 1: Issue Summary

The brainstorming session of 2026-03-13 produced 59 ideas, 7 ADRs, and a refined message design for the EventStore's core contracts. These decisions conflict with several assumptions in the current PRD, Architecture, and Epics — specifically the command payload fields, event envelope schema, domain service return contract, ID format, and storage model.

This is not a pivot — the core feature set and epic structure remain valid. It is a refinement of the *how*: cleaner contracts, stronger security (server-derived metadata), and a more principled storage model.

**Key decisions from brainstorming:**
- Ultra-thin client command: 4+1 fields (messageId, aggregateId, commandType, payload, optional correlationId)
- Tenant and domain server-derived (from JWT and message type prefix respectively)
- Message type convention: `{domain}-{name}-v{ver}` kebab
- ULID as universal ID format
- Two-document event storage: metadata JSON + payload JSON
- 13-field event metadata envelope (up from 11)
- Domain service returns minimal output: aggregate type + event type + payload
- Immutable events with multi-version handlers (no upcasting)

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact Level | Summary |
|------|-------------|---------|
| **Epic 1: Core Pipeline** | **Major** | All 6 stories need AC updates. Command contract, event envelope, storage format, domain service return contract all change. New Story 1.7 added (MessageType + ULID). |
| **Epic 2: Developer SDK** | **Moderate** | Stories 2.1-2.3 need naming convention alignment. Registration maps to domain name = message type prefix. |
| **Epic 3: Event Distribution** | **Moderate** | CloudEvents carry full metadata. Broker partitioned by aggregateId. |
| **Epic 4: Security** | **Light** | Tenant from JWT strengthens security model. Auth terminology `domain` stays. |
| **Epic 5: Resilience** | **Light** | Command idempotency uses ULID messageId. Schema evolution strategy added (D17). |
| **Epic 6: Observability** | **Light** | CausationId model changes (= command messageId). |
| **Epic 7: Deployment** | **Minimal** | No direct conflicts. |
| **Epic 8: Query Pipeline** | **Minimal** | Largely independent. |

### Artifact Conflicts

| Artifact | Sections Affected | Severity |
|----------|-------------------|----------|
| **PRD** | Executive Summary, MVP Features 1-3, FR1/FR2/FR4/FR11/FR26/FR49, FR21/FR23, Command Payload Schema, Event Envelope, Composite Keys, API Spec, Auth Model, Risk section, 11→13 field sweep | Major |
| **Architecture** | D1 (storage), D3 (domain contract), SEC-1, Naming Patterns, Implementation Sequence, File listing, new decisions D12-D17 | Major |
| **Epics** | Stories 1.1-1.6 ACs, Stories 2.1-2.3 ACs, Epic 3 CloudEvents, new Story 1.7 | Major |
| **UX Design** | API consumer examples, Swagger payload fields | Moderate |

---

## Section 3: Recommended Approach

**Path:** Direct Adjustment (Option 1)

**Rationale:** The epic structure is sound — the brainstorming refines contract details, not feature scope. No epics added or removed. One new story (1.7: MessageType + ULID). All changes are acceptance criteria updates and architecture decision additions.

- **Effort:** Medium — rewriting ACs across ~10 stories, adding 6 architecture decisions (D12-D17), updating PRD schemas
- **Risk:** Low — no architectural rework, same implementation sequence, same testing tiers
- **Timeline impact:** Minimal — same scope, different contract details

---

## Section 4: Detailed Change Proposals

All proposals were reviewed and approved incrementally.

### PRD Changes (Proposals 1-10)

**Proposal 1: Command Payload Schema** — APPROVED
- 4+1 fields: messageId (ULID), aggregateId (ULID), commandType (kebab), payload, optional correlationId
- Removed: tenantId, domain, userId, causationId (all server-derived)

**Proposal 2: Event Envelope** — APPROVED
- 13-field metadata stored as separate JSON from payload (two-document storage)
- Added: messageId, aggregateType, globalPosition
- Removed: domain (derived from eventType prefix), serializationFormat (always JSON)

**Proposal 3: MVP Core Features 1-3** — APPROVED
- Feature 1: 13-field + two-document storage
- Feature 2: Identity tuple server-derived (tenant from JWT, domain from message type prefix)
- Feature 3: Ultra-thin client command description added

**Proposal 4: Functional Requirements** — APPROVED
- FR1, FR2, FR4, FR11, FR26, FR49 updated for new field names and ULID format

**Proposal 5: Composite Key Strategy & Pub/Sub Topics** — APPROVED
- Key patterns unchanged structurally. Two-document storage noted. Aggregate metadata gains messageId tracking
- Topic separator aligned with Architecture D6 (dot-separated)
- Broker partition key = aggregateId

**Proposal 6: Domain Service Integration (FR21, FR23, MVP Feature 5)** — APPROVED
- Domain returns aggregate type + event outputs (type + payload)
- EventStore enriches with all 13 metadata fields

**Proposal 7: API Endpoints & Auth Model** — APPROVED
- Auth column: `domain` terminology kept (not renamed to `bc`)
- Permission dimension: `domains[]` JWT claim stays

**Proposal 8: Executive Summary & Success Criteria** — APPROVED
- Pure function signature updated
- 11-field → 13-field in validation gate
- Message type naming irreversibility added to risk section

**Proposal 9: Terminology Sweep** — APPROVED (simplified)
- "domain" terminology KEPT — no rename to "bounded context"
- Added note: "In this system, 'domain' corresponds to a DDD bounded context — each domain is served by exactly one domain microservice"

**Proposal 10: Remaining Sweep** — APPROVED
- All `11-field` → `13-field` (~12 occurrences)
- `GUID or equivalent` → `ULID`
- Innovation #1 alignment
- Message type convention `{domain}-{name}-v{ver}` documented

### Architecture Changes (Proposals 11-14)

**Proposal 11: D1 Event Storage** — APPROVED
- Two-document storage (metadata + payload) within single state store value
- Aggregate metadata gains processed messageIds for idempotency

**Proposal 12: D3 Domain Service Error Contract** — APPROVED
- Return contract: `DomainResult` with aggregate type + list of (event type, payload)
- EventStore assembles kebab event type from .NET type
- Rejection handling, backward compatibility unchanged

**Proposal 13: New ADRs D12-D17** — APPROVED
- D12: ULID everywhere
- D13: Message type convention `{domain}-{name}-v{ver}`
- D14: Two-document event storage
- D15: Server-derived tenant & domain
- D16: Ultra-thin client command (4+1)
- D17: Immutable events, multi-version handlers

**Proposal 14: SEC-1, Naming Patterns, Implementation Sequence** — APPROVED
- SEC-1: 13 fields, updated field list
- Event type naming: added command/event/query kebab rows
- Implementation sequence: Contracts package gains MessageType, UlidId, CommandEnvelope
- Convention engine: domain name = message type prefix
- File listing: added MessageType.cs, UlidId.cs, CommandEnvelope.cs

### Epic Changes (Proposals 15-19)

**Proposal 15: Story 1.1 (Scaffold & Envelope)** — APPROVED
- EventMetadata 13 fields, CommandEnvelope 4+1 fields, MessageType value object, UlidId type
- Serialization tests updated for two-document format

**Proposal 16: Story 1.2 (Domain Processor)** — APPROVED
- DomainResult returns AggregateType + List<EventOutput>
- Domain service has zero knowledge of metadata

**Proposal 17: Story 1.3 (Actor & Persistence)** — APPROVED
- 13-field metadata population with sources documented
- Two-document JSON storage
- Aggregate metadata tracks processed messageIds
- globalPosition assigned per event

**Proposal 18: Story 1.4 (Command API)** — APPROVED
- Validation for messageId (ULID), commandType (MessageType factory), aggregateId (ULID)
- Tenant from JWT, domain from commandType prefix
- correlationId defaults to messageId

**Proposal 19: Remaining Stories + New Story 1.7** — APPROVED
- Stories 1.5, 1.6, 2.1, 2.2, 2.3: light AC updates for new contracts
- Epic 3: full metadata in CloudEvents, broker partition by aggregateId
- **New Story 1.7: MessageType Value Object & ULID Integration** added to Epic 1

---

## Section 5: Implementation Handoff

### Change Scope Classification: **Moderate**

Requires artifact updates across PRD, Architecture, and Epics — but no backlog reorganization, no epic restructuring, no scope changes.

### Handoff Plan

| Role | Responsibility | Deliverables |
|------|---------------|-------------|
| **PM (this agent)** | Produced this Sprint Change Proposal with all edit proposals | This document |
| **Architect** | Apply Architecture proposals 11-14: update D1, D3, SEC-1, add D12-D17, update naming/file listing | Updated architecture.md |
| **PM / Tech Writer** | Apply PRD proposals 1-10: update schemas, FRs, features, sweep | Updated prd.md |
| **SM / Dev** | Apply Epic proposals 15-19: update story ACs, add Story 1.7 | Updated epics.md |

### Success Criteria

- [ ] PRD reflects 13-field envelope, ultra-thin command (4+1), server-derived metadata, ULID format, `{domain}-{name}-v{ver}` convention
- [ ] Architecture includes decisions D12-D17, updated D1/D3/SEC-1, MessageType + UlidId in file listing
- [ ] Epics Stories 1.1-1.6 ACs updated, Story 1.7 added, Stories 2.1-2.3 aligned
- [ ] All documents internally consistent (no remaining 11-field references, no tenantId in command payloads)
- [ ] Brainstorming session 2026-03-13 listed as input document in PRD frontmatter

---

**Generated:** 2026-03-14
**Approved:** 2026-03-14
**Status:** Approved — sprint-status.yaml updated (Epic 1 reopened with Story 1.7, Epic 5 reopened with Stories 5.5/5.6)

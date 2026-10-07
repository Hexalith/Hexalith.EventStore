---
title: Phase 4 architecture handoff, 2026-10-07b (owner resolution of the inline assumptions; supersedes the 2026-10-07 handoff)
date: 2026-10-07
status: pending-owner-ratification
supersedes: phase-4-architecture-handoff-2026-10-07.md
architecture_sha256: 7fd805a871883a7594b9df89c9146b1e84a42a86ddab5d6ca500fbe13387fc09
ad26_subject_sha256: efd33e1ee81d2b988ae890400a2c3f6bb09a4a36ab884c812958fbc57a8b0a0e
ad26_last_authored_change: 2026-10-07T13:22:14+02:00 (working tree; not yet pushed)
ad26_records_earliest: 24 h after the push-activity time of the first push to main that carries ad26_subject_sha256; never before 2026-10-08T13:22:14+02:00
assurance: single-maintainer-attested (required); no owner record issued
readiness: blocked
---

# Phase 4 architecture handoff, 2026-10-07b

This handoff replaces [the 2026-10-07 handoff](phase-4-architecture-handoff-2026-10-07.md). That handoff bound spine SHA-256 `1ff06c5e…` and listed 11 inline `[ASSUMPTION]` clauses. The owner resolved all 11 on 2026-10-07. The earlier handoff's history is kept unchanged.

## Summary

- **Assumptions.** The owner, who holds every owner role, resolved all 11 inline assumptions. None was rejected or deferred. Three follow-up decisions came out of the reviewer gate, and the owner chose to fix the in-subject gate findings now instead of freezing the subject.
- **AD-26.** It is **not ratified**. Its production target is the only `[ASSUMPTION]` left in the spine, and the spine stays `status: draft`.
- **AD-26 was edited in this run.** The edits defined the ratification subject, made actor state one component per app, widened the restore scope, and replaced story routing with gate references. The 24-hour window for the AD-26 records therefore restarts. See [AD-26 records: timing](#ad-26-records-timing).
- **Housekeeping.** The six Implementation Status rows that had "no owning story yet" or "routes to correct-course" now name their owners from `sprint-change-proposal-2026-10-07-architecture-routing.md`. The Stack line names Builds gitlink `397c94a4`, and code coverage moved from `18.11.2` to `18.12.0`, the only Stack-relevant catalog change since `ba4ca78c`. AD-16 is unchanged.

## Owner decisions (2026-10-07, `single-maintainer-attested`)

| # | AD | Outcome | What the spine now says | Downstream |
| --- | --- | --- | --- | --- |
| 5 | AD-17 | Adopted as written | The `Contracts` declaration is the only source of the MessageId version. The route entry, generator metadata and inventory entry derive it and carry its digest. That digest is computed from the Story 5.12 codec's canonical bytes; generated hosts compute it at startup. | Story 5.12 command fields; Story 2.15 |
| 8 | AD-27 | Adopted as written | The platform-operation namespace is declared once in `Contracts` and cataloged. Its grammar cannot overlap AggregateIdentity. External callers reach it only through the AD-10 human-bearer operation claim, and it is never forwarded as a tenant. Each boundary has exactly one scope, taken from the request route, body or headers. | Story 5.12; Story 2.14 `system` migration |
| 10 | AD-33 | Adopted with refinements | Each entry admits exactly one credential kind, with an operation from that kind's vocabulary, declared in `Contracts` and derived into the entry. The kind applies only to the hop into the target, so message and projection entries are workload. A capability both kinds need becomes two operations with distinct keys. A resource-bound delegation is a binding, not a kind. | Story 5.12 kind and operation fields |
| 4 | AD-12 + PRD 7.1 | Adopted with refinements, PRD option A refined | The seal is the validator result of a Story 9.2 transition workflow covering six transitions, and it never blocks `main`. "Required" means the transition takes effect only when its validator finds the sealed run. A seal attaches to a transition, not to an owner record. A predecessor result is re-run inside the sealed run using its pinned validator identity. | Story 9.2; `/bmad-prd` item 7.1 |
| 9 | AD-28 | Adopted with refinement, issuer model chosen at the gate | DAPR 1.18 has no control that restricts actor invocation. Until a receiving-runtime denial is qualified, every actor method that discloses, admits or mutates validates an execution context. The invoking app signs the context with its own AD-24 key. The hosting app verifies it with verification-only material and accepts only allow-listed issuers. | No owning story yet (status row); NFR12 through Story 3.18 |
| 7 | AD-26 Ratification | Adopted with canonicalization, timing anchor chosen at the gate | The ratification subject is the AD-5 "Append race" paragraph plus the AD-26 section, without the status tag. Records bind the subject digest and the whole-file digest. The 24-hour window runs from the push-activity time of the latest push that changed the subject digest. | Story 9.3 extension (status row) |
| 1 | AD-5 + AD-26 target | Adopted, target aligned | Each actor-hosting app ID has one actor-state component scoped to it alone. Its physical target is plain metadata and bound into the digest. No other component targets that table, database grants go to one app ID in one namespace, and both are proven by G-APPEND. The AD-26 target stays `[ASSUMPTION]`. | Stories 5.6/5.7 extension, reader migration, OQ8 requalification (status row) |
| 2 | AD-10 | Adopted as written | Versioned validation profiles (human bearer, AD-36 workload, resource-bound delegation), each with fingerprints per host and profile. | Story 5.11 extension (status row) |
| 3 | AD-11 | Adopted as written | The Story 3.18 inventory is the only NFR12 authority. Until it exists, Story 2.15's manifest is a projection derived from the declarations, with no authority of its own. | Stories 3.18 and 2.15 |
| 6 | AD-24 | Adopted, keys named | Every internal proof uses dedicated keys. That covers the fenced and AD-28 execution contexts, the trusted-effect gateway proof and the erasure capability. Key retirement counts every proof that has not moved. | No owning story yet (status row) |
| 11 | AD-35 | Adopted as written | Domain operations go through the gateway (AD-3). Admin operations go only through Admin Server (AD-21). | McpCli contract |

Rationale and rejected alternatives are in the run memlog, after the entry "Update run 2026-10-07b opened".

## Assumptions still open

- **Inline `[ASSUMPTION]` clauses:** none.
- **Tagged decision:** the AD-26 production target, the heading tag on AD-26. Per-role owner records ratify, replace or keep withholding it.
- **Owner decisions deferred:** these are not tags. Each has a trigger.
  - **Before the Story 5.12 spec freezes:**
    - who owns the operation vocabularies (adversarial M1/H7; ServiceDefaults does not reference `Contracts`);
    - the AD-10 human-bearer operation claim type;
    - whether sidecar-reachable ingress endpoints are cataloged per (app ID, method). Until then, ingress kind, operation and resource-bound family are endpoint metadata (Story 5.14).
  - **At Story 5.5 exit:** NM-8, the bearer audience across relays.
  - Earlier deferred mediums are in the memlog with their revisit triggers.

## AD-26 records: timing

| Item | Value |
| --- | --- |
| Ratification subject digest | `efd33e1ee81d2b988ae890400a2c3f6bb09a4a36ab884c812958fbc57a8b0a0e` (10 lines, 7,908 bytes) |
| Whole-file SHA-256 (traceability only) | `7fd805a871883a7594b9df89c9146b1e84a42a86ddab5d6ca500fbe13387fc09` |
| Last authored change to the subject | **2026-10-07 13:22:14 +02:00**, in the working tree. Not yet pushed. `HEAD` = `origin/main` = `02e99bfa` still carries subject `7075e76a…`. |
| Window anchor (AD-26 Ratification, owner choice) | The push-activity time of the latest push to `main` that changed the subject digest to `efd33e1e…` |
| **Earliest allowed time for the AD-26 records** | **That push time + 24 h. Never before 2026-10-08 13:22:14 +02:00.** |
| Superseded times | 2026-10-08 09:34 +02:00, from the prior run's AD-26 bytes in `48ef7171`. 2026-10-08 12:56 +02:00, from subject `7075e76a…` pushed in `4b1377a7` at 10:56:02Z. |

- **Invariant check:** changing only the heading tag from `[ASSUMPTION]` to `[ADOPTED]` leaves the subject digest unchanged. This was verified. Recording a ratification therefore does not invalidate its own record.
- **Later edits:**
  - Any later edit to the AD-5 "Append race" paragraph or to the AD-26 section changes the digest and restarts the window.
  - An edit elsewhere leaves records valid, but each architecture update logs an AD-26-impact entry in the memlog.
  - This run's entry says the subject itself changed.

**How to find the anchor and check the digest:**

```bash
# 1. Push activity on main (timestamps are UTC)
gh api "repos/Hexalith/Hexalith.EventStore/activity?ref=main&per_page=20" \
  --jq '.[] | select(.activity_type=="push") | [.timestamp, .after] | @tsv'

# 2. For a pushed commit, compute its subject digest
git show <after-sha>:_bmad-output/planning-artifacts/architecture.md > /tmp/spine.md
python3 -I ad26_subject_digest.py /tmp/spine.md   # script below
```

The anchor is the latest push whose `after` commit yields `efd33e1e…` while the push before it did not.

## AD-26 records: content

The owner holds both roles and issues one record per role, Architecture and Platform deployment. Each record contains the following.

- **Label:** `single-maintainer-attested`, the level required while the role registry names one human.
- **Bound subject:** the ratification subject digest above, plus the whole-file SHA-256 at issuance.
- **Attestation evidence:** the owner's authentication.
- **Achieved level:** a record does not seal itself. The sealed run of the transition that consumes it, G-BASELINE's PASS, checks it and computes and labels the achieved level (AD-11, AD-12).
- **Evidence:** tool-persona reviews are listed as evidence, never as approvals.

| Outcome | What the record must contain |
| --- | --- |
| **Ratify the target** | The target as written in AD-26: <br>- self-managed Kubernetes with per-application DAPR sidecars; <br>- one stable `state.postgresql` v1 component with `actorStateStore: true` per actor-hosting app ID, scoped to that app alone, with its physical target as plain metadata; <br>- the production resiliency policy, an approved durable broker, AD-24 OpenBao, and OQ8 `oq8-postgresql-v1`. <br><br>It must also name four things: <br>1. the **broker** (`deploy/dapr` templates for `pubsub.kafka` and `pubsub.rabbitmq`; `pubsub.azure.servicebus.topics` is cloud-managed); <br>2. the **DAPR runtime pin**, at least `1.18.3` (current stable is `1.18.4`); <br>3. the **restore posture**, covering every state component the profile binds and scheduler state, with one consistency point or a declared order; <br>4. the **NFR7 class (c) envelope path**, envelope first under G-APPEND. |
| **Replace the target** | The replacement deployment mode, actor-state provider and version, per-app component posture, OQ8 profile, broker, secret boundary, migration and compatibility impact, and the effect on AD-11 publication authority. AD-26 is amended before any downstream repin, which restarts the window. |
| **Keep withholding** | Name the evidence still missing. Today that is: <br>- the Story 3.21 runtime-pin and broker qualification rows (backlog); <br>- the Story 7.22 restore drill (backlog); <br>- the activated catalog from Stories 5.12 and 5.13 (backlog); <br>- the per-app actor-state split, its reader migration, and the `oq8-postgresql-v1` requalification (no owning story); <br>- `ReleaseEvidenceCodec` (Builds). <br><br>The G-APPEND proof (Story 4.16) comes after ratification, so a ratification can name only the envelope path. |

Ratification alone grants no release, deployment, traffic, readiness or consumer-removal authority. `deploy/dapr/production-profile.yaml` is still absent.

## Routed items (not decided by this run)

| Owner | Item |
| --- | --- |
| Product owner, `/bmad-prd` (routing proposal Group 7) | **7.1:** apply refined option A, as the owner decided with #4. Replace Assurance Control (1) with: "(1) sealed CI validation, meaning the gate validator's result is retrieved from the CI platform for a required run of the guarded transition's seal workflow (Story 9.2), on the exact head SHA and workflow-file digest, under the platform's authenticated CI identity, never supplied by the author as a file; that run blocks only its transition (a high-risk gate result to PASS, a story recorded against a high-risk NFR to `done`, readiness, `release-available`, `production-promoted`, or consumer removal) and never `main`, and an owner attestation is checked by the sealed run of the transition that consumes it rather than sealed itself;". <br>**7.2:** anonymous UI assets and login callbacks. AD-16 changes only after this PRD decision, through `bmad-architecture`. <br>**7.3, 7.4:** editorial. <br>**7.5:** MediatR OR30. |
| Correct-course, before each story's own spec freezes (Story 5.12 first) | **Superseded story texts:** <br>- Story 5.12's assumption boundary. Its fields are now required: MessageId version and declaration digest, the platform-operation namespace, and the kind and operation. <br>- Story 2.15, only its "until" framing; the manifest stays derived from the declarations. <br>- Story 5.7: the neutral actor-invocation criterion, `keyPrefix: none`, and zero actor state. <br>- Story 5.13: the host set must include the enumerated generated API hosts. <br>- Story 5.14: Operations routes that accept either kind. <br>- Story 7.22: the restore scope. <br><br>**Extensions:** <br>- Story 9.2: the transition workflow beside the matrix validator. <br>- Story 9.3: the AD-26 subject digest with the canonicalization test vector, the memlog AD-26-impact record, and an open-tag check that reads tags, not quoted mentions. <br>- Story 5.11: per-host, per-profile fingerprints. <br>- Story 2.14: scope source, and Admin Server filling a missing tenant from the caller's first claim. <br><br>**Unowned work:** <br>- per-app actor-state components (Stories 5.6/5.7), reader data migration, `oq8-postgresql-v1` requalification, `statestore` default-name changes, and one-app-ID database grants; <br>- signed, allow-listed actor execution contexts, and moving every internal proof to dedicated keys. Both are NFR12-classified through Story 3.18. |
| Owner, at the Story 5.12 spec freeze | Who owns the operation vocabularies; the human-bearer operation claim type; whether ingress endpoints are cataloged. |
| Story 5.5 | Re-reconcile AD-36 when the story reaches `done`; NM-8. |
| Builds owner | `ReleaseEvidenceCodec` (Story 3.19 issue step); DAPR CLI `1.18.2` alignment; FrontComposer `4.6.0` and Fluent UI GA alignment. |
| Platform documentation | Unchanged from the 2026-10-07 handoff. In addition, the `deploy/dapr` state-store comments ("domain services have zero state store access") are now narrowed by AD-5: an app that hosts actors reaches its own actor-state component. |

## Observed, outside this run's scope

- A concurrent session committed and pushed intermediate states of this run:
  - spine, memlog and lens reviews inside `4b1377a7` ("feat(tests): …"), at 10:56:02Z; its `CI` run failed, according to the gate-closure review;
  - memlog and gate-closure review inside `02e99bfa`, at 11:20:47Z.
  
  Neither subject line describes the architecture change. The spine's closure fixes (`7fd805a8…`) and this handoff are uncommitted.
- Stories 3.21, 5.12, 5.13, 5.14 and 7.22 exist in `epics.md` and `sprint-status.yaml`. They are no longer cited inside the AD-26 subject, but status rows still name them, so the spine and those files should land together.

## Validation and evidence

- **Lint:** `uv run .claude/skills/bmad-architecture/scripts/lint_spine.py --workspace _bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05` returned `ok: true` with 0 findings on the final bytes `7fd805a8…`. `git diff --check` is clean.
- **Reviewer lenses:** run on the changed ADs only. All are tool-persona evidence, never approvals.
  - [Rubric walker](review-update-2026-10-07b-rubric-walker.md): CHANGES REQUIRED, 0 critical, 8 high.
  - [Technology reality](review-update-2026-10-07b-technology-reality.md): 0 critical, 3 high. It corrected a Dapr premise: `keyPrefix` never applies to actor state, and DAPR 1.18 cannot restrict actor invocation.
  - [Adversarial divergence](review-update-2026-10-07b-adversarial-divergence.md): FAIL, 2 critical, 8 high.
  - [Gate closure](review-update-2026-10-07b-gate-closure.md): of 21 prior critical and high findings, 16 closed, 4 partial, 0 open, 1 owner-deferred. It raised 3 new highs.
- **Fixes after the closure review:** the four partials and the three new highs (NH1–NH3) were fixed at 13:22:14 +02:00 with no further lens pass. The review loop stops here by design. The lows, and any mediums not addressed, stay in those review files as deferred evidence.
- **Digest:**
  - The reference script below and an independent implementation by the closure reviewer agreed on the previous subject (`7075e76a…`).
  - The current subject `efd33e1e…` is computed by the reference script.
  - The heading-tag flip leaves it unchanged.
- **Preservation:**
  - Written: `architecture.md`, the run memlog (append-only), and new files in this `reviews/` folder (the four 2026-10-07b reviews and this handoff).
  - Not touched: `prd.md`, `epics.md`, `sprint-status.yaml`, `src/`, submodules. No digest was repinned.
  - This run committed and pushed nothing.

## Reference: AD-26 ratification subject digest

```python
"""Compute the AD-26 ratification subject digest as defined in the spine's AD-26 Ratification paragraph."""

import hashlib
import sys

APPEND_RACE = "**Append race (NFR7 class (c)).**"
AD26_HEADING = "### AD-26 "


def subject_lines(text: str) -> list[str]:
    lines = text.split("\n")
    starts = [i for i, line in enumerate(lines) if line.startswith(APPEND_RACE)]
    if len(starts) != 1:
        raise SystemExit(f"expected exactly one Append race paragraph, found {len(starts)}")
    end = starts[0]
    while end + 1 < len(lines) and lines[end + 1] != "":
        end += 1
    paragraph = lines[starts[0] : end + 1]

    heads = [i for i, line in enumerate(lines) if line.startswith(AD26_HEADING)]
    if len(heads) != 1:
        raise SystemExit(f"expected exactly one AD-26 heading, found {len(heads)}")
    stop = next(
        (i for i in range(heads[0] + 1, len(lines)) if lines[i].startswith("### ") or lines[i].startswith("## ")),
        len(lines),
    )
    section = lines[heads[0] : stop]
    while section and section[-1] == "":
        section.pop()
    for tag in (" [ASSUMPTION]", " [ADOPTED]"):
        if section[0].endswith(tag):
            section[0] = section[0][: -len(tag)]
            break
    else:
        raise SystemExit("AD-26 heading carries no recognized status tag")
    return paragraph + section


data = open(sys.argv[1], "rb").read()
if b"\r" in data:
    raise SystemExit("spine contains CR bytes; canonical form is LF only")
lines = subject_lines(data.decode("utf-8"))
subject = "".join(line + "\n" for line in lines).encode("utf-8")
print(f"subject_sha256 {hashlib.sha256(subject).hexdigest()}")
print(f"subject_lines {len(lines)} subject_bytes {len(subject)}")
print(f"file_sha256 {hashlib.sha256(data).hexdigest()}")
```

**Test vector.** On the spine bytes with whole-file SHA-256 `7fd805a8…fc09`, the script outputs `subject_sha256 efd33e1ee81d2b988ae890400a2c3f6bb09a4a36ab884c812958fbc57a8b0a0e` from 10 lines and 7,908 bytes.

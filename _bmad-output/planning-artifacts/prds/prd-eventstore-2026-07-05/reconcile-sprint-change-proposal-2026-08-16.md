# PRD Input Reconciliation — Sprint Change Proposal 2026-08-16

## Input

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-16.md`
- Approval state: `approved-for-major-replan-handoff`, approved by Administrator on 2026-08-16.
- Reconciled against `_bmad-output/planning-artifacts/prd.md` and the PRD run `.memlog.md`.
- No `addendum.md` is present in the PRD run workspace.

## Reconciliation Verdict

The approved proposal requires a narrow PRD correction, not a requirement change. FR36, NFR9,
NFR11, and NFR16 remain normative and unchanged. The PRD must stop presenting immutable v3.94.1
as a positive deployed-runtime parity candidate: Story 3.13 owns its content-bound,
rejected/non-authorizing disposition; Story 3.14 owns separately authorized corrective release
work; and Story 3.15 owns independent positive deployed-runtime parity closure. Story 1.20 and
Story 3.12 remain done and are not reopened.

This reconciliation authorizes only the PRD planning edit. It authorizes no release, registry,
NuGet, deployment, consumer, Git, submodule, or external-state mutation and implies no human
acceptance receipt.

## Exact Approved PRD Deltas

### 1. Record the approved source and update date

- Add `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-16.md` to the PRD
  `source_artifacts` list.
- Change PRD frontmatter `updated` from `2026-08-01` to `2026-08-16` when applying this approved
  planning correction.
- Preserve `status: final`; this is an approved update to the final requirements baseline, not a
  reopening of product discovery.

### 2. Preserve the normative requirements verbatim

Do not edit the requirement text of:

- **FR36** — the owner-reviewed production-path parity packet and exact checked-out-runtime match
  remain required before a consumer removes local projection/query infrastructure.
- **NFR9** — reproducible releases and package-reference independence from local submodule state
  remain required.
- **NFR11** — manifest-governed package publication boundaries remain required.
- **NFR16** — persisted production-path/end-state integration evidence remains required.

The proposal rejects both waiving malformed OCI provenance and reducing/removing FR36. The current
requirement rows therefore remain authoritative without wording changes.

### 3. Correct the FR36 deployed-runtime disposition note

In section 6.8 and/or the corresponding section 11.3 follow-on note, add the approved distinction
using this substance:

> Story 1.20 remains the completed source/package parity gate. Story 3.13 records the immutable
> v3.94.1 candidate as rejected and non-authorizing because its config provenance is malformed and
> its retained authority forbids deployment. Story 3.15 owns positive deployed-runtime parity for
> the separately authorized corrective release produced by Story 3.14. Neither result reopens
> Story 1.20 or authorizes Parties 8.6, G5, deployment, or consumer migration.

The PRD may identify Story 3.13's exact terminal shape where useful, but it must not weaken it:

```json
{
  "candidate": "v3.94.1",
  "candidate_disposition": "rejected-non-authorizing",
  "deployed_runtime_parity": "unavailable-for-v3.94.1",
  "selected_deployed_identity": null,
  "deployment_authorized": false
}
```

Story 3.13 completion means only that three required reviewers accepted one unchanged,
content-bound negative disposition envelope. It does not mean that the v3.94.1 artifact passed,
that FR36 positive deployed parity closed, or that deployment or consumer migration is authorized.
The Administrator's approval of the change proposal is not one of those receipts.

### 4. Assign successor responsibilities without changing requirements

- **Story 3.14 — Corrective OCI Provenance Release:** owns reproduction and correction of the OCI
  provenance-label defect, focused release-contract evidence, a separately authorized new semantic
  release, and the release packet handed to Story 3.15. Its requirement coverage is FR22, FR25,
  NFR9, NFR11, NFR16, and NFR17. The PRD update itself does not authorize that release work or any
  external write.
- **Story 3.15 — Corrected Deployed Runtime Parity Closure:** owns independent mapping and
  verification of the new 3.14 release, three acceptances of one unchanged content-bound subject,
  and the positive deployed identity. Its requirement coverage is FR36, NFR12, and NFR16. Story
  completion still grants no deployment or consumer-migration authority.
- **Story 3.13 — v3.94.1 Deployed Runtime Evidence Disposition:** owns only the immutable v3.94.1
  rejection/non-authorizing disposition. It may complete independently of or in parallel with
  Story 3.14, but it is not a positive FR36 closure and is not a substitute for Story 3.15.

### 5. Replace the stale FR36 traceability mapping

Replace the current section 11.1 row:

> `FR36 | Epic 1 - Projection/query parity source/package closure; Epic 3 - deployed runtime parity closure in Story 3.13`

with a mapping that preserves both gates and names all successor responsibilities:

> `FR36 | Epic 1 - completed source/package parity gate in Story 1.20; Epic 3 - rejected/non-authorizing v3.94.1 evidence disposition in Story 3.13, corrective-release dependency in Story 3.14, and positive deployed-runtime parity closure in Story 3.15`

The 3.14 reference is a dependency/responsibility mapping, not a claim that 3.14 itself closes
FR36.

### 6. Update the high-risk NFR story-coverage rows

Preserve the NFR text and update only traceability to reflect the explicit proposal coverage:

| NFR | Required primary story coverage after reconciliation |
| --- | --- |
| NFR9 | `3.5, 3.8, 3.11-3.14` |
| NFR11 | `3.6, 3.12, 3.14, 8.8` |
| NFR16 | `1.9-1.15, 3.11-3.15, 4.9-4.15, 7.10, 8.2-8.11` |

Story 3.14 is added to NFR9, NFR11, and NFR16 because the approved story explicitly covers those
release/evidence responsibilities. Story 3.15 is added to NFR16 because it independently validates
raw identities and production runtime evidence. Story 3.15 is not added to NFR9 or NFR11 because
the proposal does not assign those requirements to it.

### 7. Replace the obsolete v3.94.1 positive pin in section 11.3

The section 11.3 paragraph currently states that deployed runtime parity is a Story 3.13 closure
and that Story 3.13 uses source `80d12ef5eee71a9fe3ea7be51171da4a71b69a28`, release `v3.94.1`,
and package version `3.94.1` for deployed-mode closure. Retaining the identities as historical
evidence is correct; treating them as the positive closure candidate is now incorrect.

Keep the paragraph's existing Story 1.20 source/package-gate explanation, including that Story
1.20 is complete and is not reopened. Replace only the deployed-runtime portion with the approved
note in delta 3. The revised paragraph must make these boundaries explicit:

- Story 1.20 remains done and remains the Parties Story 8.6 source/package parity gate.
- Story 3.12 remains done as the historical multi-platform publishing correction; creating a new
  release does not reopen it.
- The exact v3.94.1 identities remain immutable historical failed evidence under Story 3.13.
- Story 3.14 owns the corrective release and requires separate release authority.
- Story 3.15 alone owns the successor positive deployed-runtime parity closure.
- Epic 3 remains open through Story 3.15; this does not change the done state of Stories 1.20 or
  3.12.

## Conflicts And Gaps Found

1. **Current wording positively assigns v3.94.1 closure to Story 3.13.** Section 11.3 calls Story
   3.13 the deployed-mode closure and pins it to v3.94.1. The approved proposal proves that
   candidate has malformed source/URL/documentation labels, no revision label,
   `deployment_authorized: false`, and zero of three acceptances; immutable v3.94.1 cannot be a
   positive deployed-parity pass.
2. **FR36 traceability is stale.** Section 11.1 maps Epic 3 positive deployed-runtime parity to
   Story 3.13 and does not mention Stories 3.14 or 3.15.
3. **NFR traceability omits the corrective and successor evidence stories.** NFR9, NFR11, and NFR16
   do not yet include Story 3.14; NFR16 does not include Story 3.15.
4. **The approved proposal is absent from PRD provenance.** The `source_artifacts` list ends at the
   2026-08-01 proposal/report set, and the PRD `updated` date predates this approval.
5. **Story-status preservation needs an explicit boundary in the corrected note.** Current PRD text
   supports Story 1.20 as completed, and nothing currently reopens Story 3.12, but the new
   three-story disposition/release/closure sequence should explicitly state that both 1.20 and
   3.12 remain done so the successor work cannot be misread as reopening predecessors.

## Prior-Decision Compatibility

- The memlog decision to keep FR/NFR truth in the PRD and implementation slicing in `epics.md` is
  preserved. This correction records ownership and traceability at PRD altitude without copying
  the full Story 3.13-3.15 acceptance criteria into the requirements.
- The memlog's owner-ratified two-platform immutable OCI index guardrail remains unchanged.
- The prior addition of Story 3.12 to NFR9/NFR11/NFR16/NFR17 coverage remains valid. The new work
  adds successor coverage; it does not erase 3.12 history.
- The PRD's existing SM6 source/package-parity metric remains valid for completed Story 1.20 and
  Parties Story 8.6. It must not be rewritten to imply v3.94.1 deployed-runtime approval.
- MVP scope, FR/NFR counts, public APIs, runtime behavior, data schemas, UX, and consumer code are
  unchanged by this planning correction.

## Explicit Non-Deltas

Do not:

- alter FR36, NFR9, NFR11, or NFR16 normative wording;
- mark v3.94.1 as passing, selectable, deployment-authorized, or positively parity-complete;
- treat approval of the proposal as any Story 3.13 reviewer receipt;
- reopen or rewrite Story 1.20 or Story 3.12 evidence or done status;
- move positive FR36 deployed-runtime closure anywhere other than Story 3.15;
- remove FR36 or weaken the immutable-provenance fail-closed boundary;
- authorize a release, registry/NuGet write, deployment, consumer migration, Parties 8.6, G5,
  Git operation, submodule update, or any other external-state mutation.

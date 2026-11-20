# Phase 4 scope and traceability review

**Verdict: Major revisions required.** Three high, two medium and one low finding. The November 20 update captures all seven approved audit outcomes. The full PRD still gives different answers about scope, acceptance and outstanding work depending on which section a maintainer follows.

Reviewed the working PRD at SHA-256 `709069aa88cb92b08f9b51252f3ce63bef0aa351b904f6f72e5e64beef4d1d9e` against EventStore `607072c682be05885fccf9aefcf9f92289c4e98b`. File and line references below are relative to the repository root. The PRD and epic plan live in `_bmad-output/planning-artifacts/`. Story specifications and sprint status live in `_bmad-output/implementation-artifacts/`.

## Findings

**High 1. The two requirements inventories permit different acceptance decisions.** All 37 FR and 19 NFR IDs appear in both inventories. Removing the table or list markup leaves 38 exact text matches and 18 differences. Several differences change the product contract. The epic inventory's FR12 allows a gateway-supplied valid target. The PRD requires a version-valid MessageId, prohibits CorrelationId as the target and requires tenant checks before downstream work. The epic inventory's FR36 requires a parity packet and matching SHA. The PRD requires five separately proven outcomes including release, promotion and per-consumer removal authority. The epic plan's own FR36 completion rule agrees with the PRD. NFR2 and NFR3 in the epic inventory also omit portions of the tenant and all-host authentication contracts. The public canonicalizer explicitly implements normalized tenant/grant validation. By contrast, FR37 differs only in how it refers to §8.4. Route substantive differences through Story 9.3, mirror agreed language, retain the separate execution evidence and rerun the inventory comparison. [PRD §§6–7](../../../prd.md) · [Epic inventory lines 64, 112, 120 and 122 and completion rule at 405](../../../epics.md) · [TenantCanonicalizer.cs](../../../../../src/Hexalith.EventStore.Contracts/Identity/TenantCanonicalizer.cs)

**High 2. Three live story declarations disagree with the PRD's machine-readable coverage.** The repository's completion reader accepted 136 of 142 numeric story headings. Of the six refusals, three expose actionable mapping differences. Story 4.16 declares primary NFR7 class (c), which its PRD table omits. Story 4.17 declares supporting NFR7 and NFR16, both omitted. Story 6.6 declares NFR7, NFR12, NFR16 and NFR18, while the PRD also assigns post-MVP NFR19. These differences prevent derivation of the canonical completion claims from current planning inputs. Correct the three mappings against approved story coverage and run the reader again. The remaining three require historical dispositions. Stories 3.22 and 3.23 were superseded on October 23 and recorded done. Story 4.8 is explicitly a historical ledger whose closure obligations belong to its children. Preserve those dispositions when defining the executable population. The count excludes letter-suffixed specs 6.5a–d, which fall outside the check's numeric identity format. [PRD §11.2, lines 654, 663 and 666](../../../prd.md) · [Epic declarations at 3376, 3401, 3775, 4215, 4255 and 5431](../../../epics.md) · [Completion reader](../../../../../tools/phase_4_story_completion.py)

**High 3. The Epic 6 handoff identifies the wrong remaining work.** FR33's ownership row says Story 6.4 is unauthorized because the specification is absent. The NFR8 row says it is authorized and backlog. The tracker records 6.3 done and 6.4 in progress. The PRD also says 6.2 is backlog while the tracker records it done. More consequentially, NFR18, G-NFR18 and OR6 instruct owners to produce an absent document or assign an owner. The document exists, the named owner and guard are in the 6.7 spec and the tracker records 6.7 done. The owner attested an earlier digest. The current page hashes to `99395b0adf8f86c8c18bf58f57e863c52f91bf03d068eca7f000965972126a61`, whose re-attestation remains open. Reconcile FR33, NFR8, NFR18, G-NFR18 and OR6 to the current artifacts. Retain the NFR18 gate's refusal pending current evidence and approval. [PRD lines 405, 415, 632, 655, 701, 734 and 765](../../../prd.md) · [Tracker lines 226–242 and 689–701](../../../../implementation-artifacts/sprint-status.yaml) · [6.7 verification, lines 117–141](../../../../implementation-artifacts/spec-6-7-aot-and-trimming-posture-reference.md) · [AOT posture](../../../../../docs/reference/aot-and-trimming-posture.md)

**Medium 1. The MVP scope list omits Epic 9.** Section 9.1 lists Epics 1–7 and identifies Epic 8 as post-MVP. The epic plan identifies Epic 9 as MVP governance and the tracker records it in progress. Story 9.3 owns the baseline, 9.4 owns clause closure, and 9.5 owns coverage supporting SM9, SM10 and SM12. A planner using §9.1 could omit the work needed to evaluate the metrics and reach a readiness decision. Add Epic 9 and its purpose to §9.1. Its ownership attaches to governance, with no new functional requirement or dependence on Epic 8. [PRD §§9.1, 10 and 11.4](../../../prd.md) · [Epic 9 at lines 393, 8044, 8092 and 8128](../../../epics.md) · [Tracker lines 308–328](../../../../implementation-artifacts/sprint-status.yaml)

**Medium 2. Post-MVP planning still schedules a completed, bounded prerequisite.** Sections 6.9 and 11.3 describe the 8.12 successor as awaiting its implementation and approval, with SDK 1.19.0. The tracker and build spec record 8.12 done. The revision 4 supplement and build spec record the owner's approval of the Dapr.Protos/Dapr.Client 1.19.1 input and the scope of the verification. Update the bounded 8.12 and SDK account while retaining 8.7 activation, capacity and G5 as distinct obligations. [PRD lines 392 and 696](../../../prd.md) · [Build spec lines 251 and 315](../../../../implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography.md) · [Tracker lines 294–305](../../../../implementation-artifacts/sprint-status.yaml)

**Low 1. The metrics section mixes historical and current status.** The heading “Planning-recovery metrics (achieved)” includes SM3 explicitly marked unmet. SM4 says Stories 7.14 and 8.2 both remain backlog. The tracker records 7.14 backlog and 8.2 done. Use a neutral group heading and state the dated decomposition outcome without a stale current lifecycle assertion. Keep the decomposition achievement and its limited meaning. [PRD lines 563–572](../../../prd.md) · [Tracker lines 261 and 284](../../../../implementation-artifacts/sprint-status.yaml)

## November 20 acceptance cross-check

All seven findings have a product-level outcome in the current draft. The proposal and epic stories retain the detailed test cases. The four new tracker entries remain backlog. This review establishes requirements coverage only. [Approved proposal, lines 25–31](../../../sprint-change-proposal-2026-11-20.md) · [Sprint tracker](../../../../implementation-artifacts/sprint-status.yaml)

| Finding | PRD line | Source agreement |
| --- | --- | --- |
| Tenant/global Admin storage | 334 | Story 5.15 preserves the valid tenant `all`, exact authorized totals and safe behavior during rebuild and rollback. |
| Conditional query validators | 268 | Story 2.18 requires producer-bound strong tags, projection-race handling and truthful 200/304 results. The gateway's post-read lookup is the implementation path the story addresses. |
| Subscriber deduplication | 320 | Stories 5.9/7.1 require two durable effects from two identities and one repeated delivery. The publisher sets the CloudEvents ID from the stored MessageId. |
| Deployment claims and transition fixtures | 359, 715 | Stories 5.9/9.2 distinguish tested/configured/qualified/approved profiles and bounded fixture outcomes from live assurance. Stories 3.21, 7.22 and 3.19 retain qualification, restore and production approval. |
| Stream activity | 357 | Story 7.23 preserves case-sensitive identity, actual heads and counts across eviction and replay. Measurements precede any move off the command path. |
| Adapter failure privacy | 270 | Story 2.19 preserves recognized errors and correlation, and withholds arbitrary adapter text from the response and structured logs. |
| Advisory CI and onboarding | 303 | Stories 5.9/7.13 require truthful suite/job conclusions, maintained onboarding links and version inputs, and the FR25 lock policy. |

Source spot checks used `QueriesController.cs` at lines 387–403 and 449–458, `EventPublisher.cs` at lines 224–225 and the Builds 1.19.2 development qualification. [Query gateway](../../../../../src/Hexalith.EventStore/Controllers/QueriesController.cs) · [Publisher](../../../../../src/Hexalith.EventStore.Server/Events/EventPublisher.cs) · [Builds qualification](https://github.com/Hexalith/Hexalith.Builds/blob/2c33b2d749a2f7b8c67a15130a815996f83a51a5/docs/dapr-runtime-1.19.2.md)

## Reproducing the comparisons

Run from the EventStore root. The first comparison removes only the inventories' outer Markdown delimiters. The second uses the repository's coverage function directly and reports every refused numeric story.

```sh
PYTHONDONTWRITEBYTECODE=1 PYTHONPATH=tools python3 - <<'PY'
import re
from pathlib import Path
import phase_4_story_completion as check

prd = Path(check.PRD).read_bytes()
epics = Path(check.EPICS).read_bytes()
p = prd.decode().split("## 6. Features And", 1)[1].split("### 7.1", 1)[0]
e = epics.decode().split("## Requirements Inventory", 1)[1].split("### Additional Requirements", 1)[0]
requirements = dict(re.findall(r"^\| (N?FR\d+) \| (.*) \|$", p, re.M))
inventory = dict(re.findall(r"^(N?FR\d+): (.*)$", e, re.M))
print("ID sets match", requirements.keys() == inventory.keys())
print("Different text", [key for key, value in requirements.items() if inventory.get(key) != value])
stories = re.findall(r"^### Story (\d+\.\d+):", epics.decode(), re.M)
refused = 0
for story in stories:
    try:
        check.coverage(prd, epics, story)
    except check.C.Refusal as error:
        refused += 1
        print(story, error)
print("Accepted", len(stories) - refused, "of", len(stories))
PY
```

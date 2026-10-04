"""Reproduce the approved documentation splice from immutable integration inputs."""
from pathlib import Path
import ast
import hashlib
import json
import os
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / '_bmad-output/implementation-artifacts'
OUT = ART / '6-5-integration'
BASE = 'cbbe41501ba722731bf36b2c343efdef4ac714fb'
ALLOWED = [
    '_bmad-output/implementation-artifacts/spec-6-5-event-versioning-and-upcasting-spec.md',
    '_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md',
    '_bmad-output/implementation-artifacts/deferred-work.md',
    '_bmad-output/planning-artifacts/architecture.md',
]

def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args])

def digest(value):
    return hashlib.sha256(value).hexdigest()

def committed(path):
    return git('show', BASE + ':' + path)

def write(path, value):
    path.write_text(value, encoding='utf-8', newline='\n')

def replace_rule(doc, number, value):
    pattern = rf'^\[I-{number:02}\].*?(?=^\[I-\d\d\]|^### |^## )'
    result, count = re.subn(pattern, value + '\n\n', doc, count=1, flags=re.M | re.S)
    assert count == 1, number
    return result

def owner_policy(doc, ledger):
    """Apply the owner's administrative amendment without changing imported contracts."""
    rules={
        2: '**Source inventory.** The integration source snapshot is '+BASE+'. Historical A/B/C source baselines remain in §11.4; old line citations describe that snapshot. AggregateActor.cs:2207 then retained one 30-second recovery attempt; AdminStreamQueryController.cs full-read sites were 129, 337, 466, 599, 786, 1066 and 1284. Story 6.6 checks its affected sources/symbols at its current revision. The old source-manifest whole-tree entries are historical inventory, not a repository freeze. Its actual reviewed child/source/model inputs retain their exact pins; a changed input needs review before repinning. Normal owner commits, unrelated working-tree changes and submodule advancement do not fail this documentation gate.',
        44: '**Accepted dispositions and open follow-ups.** Each finding has one disposition class: rule, imported citation, Story 6.6 obligation, or non-story work. Jérôme Piquot has accepted the specification and requested the pragmatic validation amendment in §12. The 47 integrated ledger entries stay open with accepted spec dispositions and implementation/evidence follow-ups; accepting the design does not resolve runtime work. All 20 O-rows retain a Story 6.6 owner, blocking gate and closure evidence. The implementer updates an affected disposition when a material technical change receives owner review, and closes a follow-up only when its implementation/evidence is complete. This amendment historically preserves the five unrelated open entries and two earlier resolutions; ordinary later unrelated ledger additions, edits and closures are permitted. The gate checks only the 47 owned records by their source, summary and evidence identities. Historical BH37/pass-1/pass-2 reports are preserved.',
        47: '**Focused verification successor.** The old 6.5c integrity script, whole-tree manifest inventory and earlier captures remain historical evidence. The active §11.6 gate checks actual reviewed child/source/model pins, exact codec/literal constructors, verifier identities, corruption controls and the recorded approved content digest. Scope describes this documentation change; it does not freeze other files, commits or submodules. O-06/O-07/O-11 remain open Story 6.6 obligations for these focused checks.',
    }
    for number,value in rules.items():
        doc=re.sub(rf'^\[I-{number:02}\][^\n]*',f'[I-{number:02}] '+value,doc,flags=re.M)
    doc=doc.replace('status: normative-candidate','status: normative-approved')
    doc=doc.replace('AD-26 production-profile approval, its actual database/runtime pins, two-host evidence and §12 AD-13 approval remain separate and unapproved.',
                    'AD-26 production-profile approval remains pending; its actual database/runtime pins and two-host qualification evidence remain unproved. The separate §12 AD-13 conversational owner approval is recorded.')
    doc=doc.replace('**Story 6.6 is unauthorized until the content-bound human receipt in §12 validates.**',
                    '**The owner has approved this design; Story 6.6 is ready and starts when the owner requests it (§12).**')
    doc=doc.replace('An edited GitHub comment URL without immutable capture, wrong captured actor/source UTC, deletion/revocation, conditional text or stale digest leaves all six receipt fields unapproved and Story 6.6 unauthorized.',
                    'For specification approval, check the conversational owner approval recorded in §12 and the tooling-computed content digest; malformed approval bytes or a stale digest fail the local check. Production migration signatures and authenticated reviewer evidence remain required by their existing rules.')
    # Production-vector receipt instructions follow §12; historical child-check prose stays intact.
    lines=[]
    for line in doc.splitlines(keepends=True):
        if line.startswith(('Review-','For V1, admit a newly written event')):
            line=line.replace('and the six-field UNAPPROVED receipt remain unchanged','remain unchanged, and the current §12 owner approval validates')
            line=line.replace('and six-field UNAPPROVED receipt unchanged','unchanged, and validate the current §12 owner approval')
            line=line.replace('and the six-field UNAPPROVED receipt','; validate the current §12 owner approval')
            line=line.replace('and six-field UNAPPROVED receipt','; validate the current §12 owner approval')
        lines.append(line)
    doc=''.join(lines)
    parent={
        'BH2-11':'I44 open ledger implementation/evidence follow-ups with accepted spec dispositions; every O-row has a Story 6.6 owner/blocking gate/closure evidence; runtime work remains open',
        'BH2-12':'§12 owner approval in this conversation and tooling-computed digest; local checks finish Story 6.5, Story 6.6 starts on owner request; D-RESUME/D-SPLIT history is preserved',
        'BH2-13':'Reproducible parent base 288a61908f4661fed52bb791f928292fe7d90180 and historical integration snapshot '+BASE+'; actual reviewed input revisions/file/model hashes remain checked, without freezing unrelated owner changes',
        'BH2-14':'I47 focused source/model/byte gate; O-06/O-07/O-11 remain open implementation obligations; historical boundary captures are preserved and FW1 retains the relevant rerun gate',
    }
    for finding,value in parent.items():
        doc=re.sub(r'^\| '+finding+r' \|[^\n]*',f'| {finding} | {value} |',doc,flags=re.M)
    doc=doc.replace('**Review pass 2: proposed dispositions pending §12.**','**Review pass 2: accepted spec dispositions.**')
    doc=doc.replace('| Parent finding | Proposed disposition and exact gate |','| Parent finding | Accepted spec disposition and focused gate |')
    doc=doc.replace('O-06 successor boundary gate','O-06 focused input-pin checks').replace('O-07 successor boundary gate','O-07 focused missing-input checks').replace('O-11 successor boundary gate','O-11 focused bookkeeping/digest checks')
    doc=doc.replace('O-06/O-07/O-11 retain replacement boundary-gate ownership.','O-06/O-07/O-11 retain focused validation ownership.')
    doc=doc.replace('I44 keeps all 47 pending integrated ledger entries open with proposed dispositions; integrator amendment/refusal and obligation-owner closure triggers are explicit.',
                    'I44 keeps all 47 integrated implementation/evidence follow-ups open with accepted spec dispositions; material technical changes receive owner review and closure requires completed evidence.')
    doc=doc.replace('The gate pins the current baseline and every input revision/file/model hash,',
                    'The gate checks each actual reviewed input revision/file/model hash,')
    doc=doc.replace('removed verifier fence, forbidden path and an input pin','removed verifier fence, an actual input pin and malformed/stale approval bytes')
    doc=doc.replace('after any candidate, imported input, constructor or verifier change and before §12 presentation;',
                    'after a relevant candidate, imported input, constructor or verifier change;')
    doc=doc.replace('Historical child acceptance failure at its old correction baseline remains evidence; current preservation belongs to this run.',
                    'Historical child acceptance failure and whole-repository preservation captures remain evidence; active checks retain only the actual reviewed inputs.')
    doc=doc.replace('Any change requires rerun, fresh digest and fresh approval;',
                    'Relevant changes require focused checks and a tooling-refreshed digest; material technical changes also require owner review, while this requested policy amendment is already approved;')
    obligations={
        6:'Port the focused actual reviewed source/model pin checks into Story 6.6 validation. Missing or changed reviewed inputs refuse; unrelated owner commits and working-tree changes do not.',
        7:'Check missing actual reviewed inputs and incorrect source/model pins, with a focused positive control for unrelated changes and submodule advancement; no whole-repository untracked/missing-path freeze.',
        11:'Keep bookkeeping limited to the reviewed change; validate exact actual input/file/block hashes, verifier identities and approved content digest. The old whole-tree inventory is historical, not an active ancestry/gitlink freeze.',
    }
    for number,value in obligations.items():
        doc=re.sub(rf'^\| O-{number:02} \|[^|]*\|',f'| O-{number:02} | {value} |',doc,flags=re.M)
    title='### 11.7 Story 6.6 verification obligations and review focus points\n'
    doc=doc.replace(title,title+'\nAll 20 obligations remain open implementation/evidence follow-ups; owner approval accepts their design, not their completion.\n') if 'All 20 obligations remain open implementation/evidence follow-ups;' not in doc else doc
    section='''## 12. Owner approval and local checks

Owner approval in this conversation is sufficient. Jérôme Piquot stated “I Jérôme Piquot approve” and then requested “all this seems too complex for a project with one contibutor. make validation simple and pragmatic.” This administrative validation amendment is requested and approved. Story 6.5 can be marked done after the relevant local checks pass. Story 6.6 has an approved design and starts when the owner requests it; it has no separate authorization ritual. Material technical changes still receive owner review. Production cryptography, tenant/operator permissions, CAS/fences, deployment qualification and runtime test requirements retain their existing rules, including AD-26’s separate production-profile approval and qualification requirements. Imported D9's content-bound human approval is satisfied here by the conversational owner approval and recorded tooling digest; its provider requirements remain unchanged.

Tooling computes SHA-256 over the exact UTF-8, LF, no-BOM bytes from the start of this document through the newline before the unique full-line receipt marker. The six receipt fields below are excluded. Tooling records and checks that digest; the owner does not need to repeat it. The fields retain their order and fixed scope. Approver names the owner; ApprovalDateUtc is an administrative recording date, not proof of the conversation's source time. Authorization and ApprovalEvidence record the conversational approval and requested amendment; conversational evidence is sufficient. A malformed receipt or stale digest fails the local check. An updated digest alone does not approve a material technical change.

'''
    start=doc.index('## 12.')
    command=doc.index('Exact recomputation from repository root',start)
    doc=doc[:start]+section+doc[command:]
    # Convert only the 47 baseline-owned identities; preserve all unrelated current bytes.
    pattern=r'^- source_spec:[^\n]*(?:\n  [^\n]*)*'
    def identity(row):
        return tuple(next((line for line in row.splitlines() if line.startswith(prefix)),None)
                     for prefix in ('- source_spec: ','  summary: ','  evidence: '))
    baseline=committed('_bmad-output/implementation-artifacts/deferred-work.md').decode()
    owned={identity(row) for row in re.findall(pattern,baseline,re.M)
           if re.search(r'^  status: dispositioned pending approval',row,re.M)}
    assert len(owned)==47
    def disposition(row):
        if identity(row[0]) not in owned:return row[0]
        lines=[]
        for line in row[0].splitlines(keepends=True):
            if not line.startswith(('  status: open — proposed','  status: dispositioned pending approval')):
                lines.append(line);continue
            reference=re.search(r'(normative rule \[I-\d+\].*?|Story 6\.6 verification obligation O-\d\d.*?|Story 6\.6 successor boundary-gate obligation .*?)(?:\. It takes effect|; closes only|, pending exact|$)',line.rstrip('\n'))
            assert reference is not None, line
            citation=reference[1].replace('successor integration boundary gate','focused input/approval checks').replace('successor boundary-gate obligation','focused validation obligation')
            withdrawn=re.search(r'O-(?:06|07|11) withdrawn',line)
            if withdrawn:citation='Story 6.6 verification obligation '+withdrawn[0][:4]+', focused input/approval checks (§11.7)'
            lines.append('  status: open — accepted spec disposition (2026-10-04): '+citation+'; implementation/evidence follow-up remains open until completed in Story 6.6.'+ ('\n' if line.endswith('\n') else ''))
        return ''.join(lines)
    return doc,re.sub(pattern,disposition,ledger,flags=re.M)

def record_owner_approval(doc):
    pinned=('verify.py','independent-answers.mjs','source-manifest.json','metadata-adapter-contract.md','build-integration.py','preserved-hold-predicates.json')
    for name in pinned:
        pattern=r'(^\| Current uncommitted `_bmad-output/implementation-artifacts/6-5-integration/'+re.escape(name)+r'` \| `)[0-9a-f]{64}(` \|$)'
        doc,count=re.subn(pattern,lambda match:match[1]+digest((OUT/name).read_bytes())+match[2],doc,flags=re.M)
        assert count==1,name
    marker='<!-- APPROVAL RECEIPT: mutable fields below -->\n'
    body=doc.split(marker)[0]
    return body+marker+'ApprovalDigest: '+digest(body.encode())+'\nApprover: Jérôme Piquot\nApprovalDateUtc: 2026-10-04T13:52:31Z\nApprovalScope: Story 6.5 AD-13 normative artifact\nAuthorization: Owner approved the specification and requested this pragmatic validation amendment.\nApprovalEvidence: This conversation: “I Jérôme Piquot approve”; subsequent request to make validation simple and pragmatic.\n'

if __name__ == '__main__':
    if sys.argv[1:]==['--pragmatic-policy']:
        current_ledger=(ART/'deferred-work.md').read_text()
        doc,ledger=owner_policy((ART/'spec-event-versioning-upcasting.md').read_text(),current_ledger)
        write(ART/'spec-event-versioning-upcasting.md',record_owner_approval(doc))
        if ledger!=current_ledger:write(ART/'deferred-work.md',ledger)
        print('updated owner approval, focused validation and accepted open follow-ups')
        raise SystemExit(0)
    OUT.mkdir(exist_ok=True)
    manifest_path = OUT / 'source-manifest.json'
    if not manifest_path.exists():
        entries = {}
        for row in git('ls-tree', '-r', '-z', BASE).split(b'\0'):
            if not row:
                continue
            meta, raw_path = row.split(b'\t', 1)
            mode, kind, oid = meta.decode().split()
            path = raw_path.decode()
            entry = dict(mode=mode, kind=kind, oid=oid)
            if kind == 'blob':
                data = os.fsencode(os.readlink(ROOT / path)) if mode == '120000' else (ROOT / path).read_bytes()
                entry['worktreeSha256'] = digest(data)
                entry['blobSha256'] = digest(git('cat-file', 'blob', oid))
                if mode == '120000':
                    entry['linkTarget'] = os.readlink(ROOT / path)
            entries[path] = entry
        input_paths = [p for p in entries if p.startswith('_bmad-output/implementation-artifacts/6-5d-simplification/')]
        input_paths += [p for p in entries if re.search(r'/spec-6-5[abcd]-', p)]
        input_paths += [
            '_bmad-output/implementation-artifacts/story-6-5-review-pass-2-findings.md',
            '_bmad-output/implementation-artifacts/story-6-5-review-triage.md',
            '_bmad-output/implementation-artifacts/story-6-5-review-change-log.md',
            '_bmad-output/implementation-artifacts/story-6-5-design-notes.md',
        ]
        inputs = {}
        for path in sorted(set(input_paths)):
            inputs[path] = dict(sha256=digest((ROOT / path).read_bytes()), revision=git('log','-1','--format=%H',BASE,'--',path).decode().strip())
            text = (ROOT / path).read_text(errors='replace') if path.endswith('.md') else ''
            blocks = re.findall(r'^```python\n(.*?)^```$', text, re.M | re.S)
            if blocks:
                inputs[path]['pythonBlocks'] = [digest(b.encode()) for b in blocks]
        manifest = dict(schema='hexalith.story-6-5.integration-sources/1', baseline=BASE,
                        allowedFiles=ALLOWED, allowedDirectory='_bmad-output/implementation-artifacts/6-5-integration/',
                        entries=entries, inputs=inputs)
        write(manifest_path, json.dumps(manifest, indent=2, sort_keys=True) + '\n')

    doc = committed('_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md').decode()
    child = (ART / 'spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md').read_text()
    support = (ART / '6-5d-simplification/obligations.md').read_text()
    answers = (ART / '6-5d-simplification/known-answers.json').read_text()
    doc = doc.replace('baseline_commit: ccb4faf03256ef8eb627d49d6f6f0a032fcb4830', 'baseline_commit: ' + BASE)
    doc = doc.replace('Story 6.5a, 6.5b and 6.5c section candidates', 'Story 6.5a, 6.5b, 6.5c and reviewed 6.5d section candidates')
    doc = doc.replace('C0–C7 (publication, subscription and rollout).', 'C0–C7 (publication, subscription and rollout), and D1–D9 (hold lifecycle, resume and legacy admission). D1–D9 and the complete supporting schemas/literals in §11.6 replace the D9-owned rules; retired private fixtures create no deployed migration.')
    doc = doc.replace("(5) C1's per-tenant and deployment publication-retention ceilings supersede A8's rule that revision zero never first discovers after commit that its outcome is unrepresentable, for those two levels only, while A8's per-operation reservation before `Prepared` stands.", "(5) D1/D6 qualified whole-batch metadata reservation precedes the separate pin install and replaces C1's un-precharged per-tenant/deployment post-commit capacity exception. D6 fixed bootstrap/header/owner precharges and eight reserved queue envelopes govern readiness together with A8's per-operation reservation before `Prepared`; no individual ceiling grants admission. D4's ordered authoritative status mapping supersedes earlier scalar/private-failure interpretations, with preparation/conflict evidence preceding publication holds. D3's resume-window fences remain distinct from C2/C5 permanent operation-terminal fences; D7 captured physical custody is distinct from logical C3 route completion. D9's activation map governs these supersessions.")
    replacements = {
        6: '**Full replay.** D5 is the complete three-way activation, accounting/readable/count bound and scheduled idle-bootstrap exit. The signed activation uses the exact retained D06 schema in §11.6; catalog registration precedes fingerprint calculation. D8 owns discovery before the projection hold activates.',
        10: '**Authoritative status.** D4 supplies the complete ordered mapping, including conflict/preparation precedence, terminal pointer, published/no-op, active drain-limit, class-02/03 terminal evidence, class-01 exhaustion/retry, and pending/unknown. D3 successor-window and exact legacy classifications apply. No scalar or advisory status bypasses this order.',
        12: '**Execution scope.** D5 supplies slice-2 legacy CAS claims before archive/status/invocation, 256 transactional shard usage records, pinned H/cutover, exact expired-claim renewal, required compaction, bounded tombstones, HTTP 410 and eventual identity reuse after authenticated deletion. The shared command-execution-scope key and A8 required-class bytes remain unchanged. Idempotency-Key decisions resolving no execution retain their existing precedence; every decision resolving an execution uses this scope before retry selection. Missing provider readback is admission_evidence_hold, never absence.',
        14: '**Publication failures.** D3/D4 define immutable per-window drain-limit source/resolution, legacy reason eligibility and exclusive capsule recovery. Legacy status-6 writers keep shipped behavior; evidence-required failures use D4 and C5, never an advisory writer. D3 consumes the exact old source before rearming; drain-only keeps the exact active claim and all permanent C2 member fences. D6 reserves the next limit/resolution before effects.',
        15: '**Polling.** D4 operator-gated EventsStored carries Retryable=false and Retry-After: 60; automatically progressing EventsStored carries Retry-After: 1; CommandOutcomeHold carries Retry-After: 30; terminal status has none. Polling intervals confer no send, command replay or resume authority.',
        16: '**Outcome hold.** D4 gives the sole HTTP 503 form and closed reason set, including first-send membership and resume/ledger evidence. D8 derives discoverable authoritative causes and exact exits. Admission, scope capacity and registry capacity refuse before invocation; no 503 replaces the immutable first response once selected. Retry-exhaustion/drain-limit use D4 EventsStored, while evidence/preparation/membership incidents precede that projection.',
        17: '**Destination.** D5 gives the exact canonical hexalith.eventstore.destination/1 schema, 65,536-byte ceiling and admission equality. C1 destination-ID preimages/answers remain byte-identical; derivation vectors need not be admissible configurations. Destination/policy installation begins slice 2 and grants no send authority. The surviving I17 literal in §11.6 fixes canonical JSON bytes.',
        26: '**Kind bounds.** D6 owns checked complete-batch reservation, 449 MiB pins, 193 MiB ordinary retained/side objects, 256 MiB maximum quarantine and 1 GiB active resume window before original overhead. The eight separately precharged 100 MiB queue envelopes and bootstrap/header/owner reservations count toward pools. D6 readiness includes them; the former un-precharged capacity arithmetic is historical, never an admission capability.',
        28: '**Charge reattachment.** D6 retained charge schema records kind-qualified account, original length/overhead/amount and exact charge address. A matching reattach preserves the original charged account/amount; a mismatched kind/length is a conflict/refusal with unchanged bytes, never an ownership transfer. Current overhead governs only new charges. Checked authenticated erasure/deletion readback precedes once-only refund.',
        29: '**Ledger/backend.** D1/D6 require the same approved application-owned PostgreSQL metadata backend for controls, registry, ledger and queue, with one qualified serializable transaction over declared expected generations/fences. D6 capability/charge/counter/pin-batch retained schemas and §11.6 answers apply. Separate pin backend installation is reservation-bound and read back before any send; every 1..59-member batch reserves atomically, 60..1,000 fails AppendPreparationLimit before append.',
        30: '**Headroom.** D6 supplies kind-qualified tenant/capture-scope pools, fixed bootstrap precharges, reserve/unidentified/deployment bounds, checked net grant/refund and readiness inequalities. The encoded 195 MiB minimum is not sufficient readiness. Lowering ceilings evicts nothing and admits no excess increase; account or arithmetic uncertainty holds unchanged.',
        31: '**One capacity queue.** D6 supplies one deployment allocator and eight addressed fixed storage envelopes, immutable global tickets/precommit slots, same-row queued/parked/reserved/cleanup states, tenant-eligible fairness and no deployment bypass. No paired queue move, owner index or predecessor-image family survives. Discovery precedes row reservation; waiting work has zero pin capacity; qualified atomic whole batch and row refund precede separate pin install. Recovery requires original authority/WAL, never a hash-only reconstruction.',
        36: '**Held delivery.** D7 supplies bounded original observation, policy/count/24-hour capture, exact object charges/intents/readbacks, same-owner redrive/repair, above-max incident/quarantine, delivery-versus-erasure cleanup and tenant/deployment routes. Captured custody may acknowledge the physical copy; all logical route/effect obligations remain open. Original identity/reason/charge/discovery survive partial effects and repairs.',
        37: '**Complete discovery.** D8 supplies reserve-before-owner registry, addressed entries/count headers, one Operations epoch, authoritative reason derivation, scope-only generation cursors and exact tenant/deployment routes. Its exit table additionally includes every unrelated A/B/C predicate in the supplemental table below. All owners and evidence are charged before producer activation; registry is a locator, never independent lifecycle authority.',
        45: '**Publication resume.** Owner D-RESUME is implemented as the exact D1–D3 same-owner request/intent/action sequence, purpose-2d claims, original fixed retry horizons/deletion/refund and D8 inventory handle. GET precondition and POST are D3 tenant handle routes. No archived command/domain invocation or recreation of committed event/pin/result/first-response bytes is permitted. Permanent terminal operation fences remain distinct from resumed window fences.',
        46: '**Legacy status-6.** D3 is the sole capsule-before-cleanup, exclusive legacy recovery and original range/MessageId/classification contract. Extant original drain authority is necessary; missing historical authority is a non-resumable legacy_resume_evidence_unavailable incident. Dead letter corroboration supplies no invented classification. Signed capsule/repair/drain readbacks and bounded restart intents retain the same owner; no fresh-command replay is authorized.',
        47: '**Boundary verification successor.** The old 6.5c integrity script and old child acceptance pins remain immutable historical evidence, including their current failures. The new current-baseline §11.6 gate audits committed, index, worktree, untracked and root-gitlink state against an explicit allowed set, preserving every child/archive/checkpoint and all outside-scope bytes. O-06/O-07/O-11 remain Story 6.6 obligations for this successor, never exemptions or withdrawn gates.',
    }
    for number, value in replacements.items():
        doc = replace_rule(doc, number, f'[I-{number:02}] ' + value)
    old_i37 = re.search(r'^\[I-37\].*?(?=^### )', committed('_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md').decode(), re.M | re.S)[0]
    hold_names = sorted(set(re.findall(r'`([A-Z][A-Za-z]+)`', old_i37.split('(2) *Entry.*',1)[0])))
    write(OUT / 'preserved-hold-predicates.json', json.dumps(hold_names, indent=2) + '\n')
    # Exact reviewed D text and supporting bytes are inside the normative digest.
    d_body = child.split('## D1.', 1)[1].split('\nRun `python3 ', 1)[0]
    d_body = '## D1.' + d_body
    d_body = re.sub(r'^## D', '#### D', d_body, flags=re.M)
    d_body = d_body.replace('obligations.md', 'the imported wire reference in §11.6')
    doc = doc.replace('## 8. Numeric budgets and deterministic outcomes', '## 8. Numeric budgets and deterministic outcomes')
    pos = doc.index('## 8. Numeric budgets')
    supplemental_exits = {
        'ActorCommitEvidenceHold': 'A5/A9 actor append owner under the original operation fence: authenticate the complete original intent, post-save receipt and entire committed-generation row set. Complete commit preserves original truth; proven no-start plus no-future-commit permits same-capsule continuation if the source is unchanged, or A5 AbortedStale cleanup after source change. Partial/unknown/in-flight evidence remains held, with no duplicate append or ID release.',
        'AppendPreparationHold': '§7 append-preparation fenced owner and A7 no-op owner: reconcile the original capsule CAS, every encrypted chunk and no-save evidence; reuse exact Prepared bytes. After commit, restore the exact compact completion/result/witness/first-pin evidence. Only authenticated never-started/no-future-commit proof permits uncommitted cleanup; no regeneration or fresh append.',
        'ConsumerMembershipFenceUnavailable': 'C2 broker membership authority and the existing send/historical-execution owner: restore qualified atomic compare-and-accept, exact current membership/fence/lease and complete attempt/parent readback. For an owed historical route, authenticate the exact purpose-13 grant/renewal chain, latest exclusive owner and mapping/exclusion evidence. Uncertain send requires exact attempt lookup; no new nonce, send or effect without that proof.',
        'HistoricalMessageIdCollision': '§7 global IEventMessageReservationStore migration/readiness owner: retain both original events and hold rollout under the historical collision rule; no local uniqueness fallback, reassignment or overwrite. The existing authenticated global inventory/reservation proof must establish collision-free readiness before rollout. No automatic repair or erasure exit is granted.',
        'KeySpaceMigrationHold': '§10.1 named-projection key-space migration owner, with B7 scoped-store authority: inventory every overlapping physical key, fence old writers, then authenticate exact fenced copy/mapping and readback, or a declared nonoverlapping prefix plus bounded old-reader adapter. Preserve old keys/evidence and the last certified root/readable legacy path until replacement is proven.',
        'LegacyEvidenceConflict': 'A4 migration reviewer/readiness owner and A6 raw-corrupt disposition owner: authenticate the unique original encoding/source interval, reviewer docket, decoder closure, exact source/incarnation and approved disposition. Changed/conflicting historical bytes are preserved; backend restore/move requires independently approved source migration. No overwrite, guessed encoding or synthetic provenance exits the hold.',
        'LegacyHandoffCapacityHold': 'C4 handoff variant-head owner: an identical authenticated attempt reuses its retained ordinal/bundle. A new attempt requires qualified atomic reservation and readback of the complete reverse-index/attempt/side/link/head/receipt bundle with a free ordinal among the fixed 64. A distinct sixty-fifth attempt remains held; no eviction, partial variant or logical success is authorized.',
        'RawSourceUnavailable': 'B2 authenticated raw-source provider/read owner (A6 for raw-corrupt authority): restore bounded raw transport and exact fresh source proof, original backend/incarnation and complete provenance/capture/decision evidence before allocation/read/Apply. Typed reads, current cache, equal JSON or fabricated digest cannot substitute; source conflict follows A6 instead of claiming recovery.',
        'ReplayCommitAmbiguous': 'B4b replay/timeline fenced owner and B9 recovery token: freeze further Apply, retain operation/quota/pins and reconcile all selected objects/hashes/final-pointer presence. Exact matching committed pointer and complete objects return pinned bytes; old pointer plus authenticated no-future-commit proof alone permits orphan cleanup and a new page-1 operation. Mixed/missing/inconclusive evidence remains held.',
        'RollbackReaderCapabilityHold': 'A9/B8/C6 deployment routing/readiness owner: fence old-only endpoints and restore an authenticated capable reader/consumer for every retained V2/evolution/transcript version, with original keys/evidence and applicable read/Apply/handler capability. Returning writer mode to V1 cannot remove those obligations or authorize Apply/send/effect on an incapable endpoint.',
        'CommandOutcomeHold': 'D4 aggregate reason view of the existing authenticated execution/preparation, quota or first-send owner. Preserve D4 exact precedence and its nine closed original reasons: admission_evidence_hold, response_preparation_hold, publication_pin_capacity_hold, outcome_evidence_hold, outcome_evidence_conflict, resume_evidence_hold, quota_generation_exhausted, terminal_evidence_hold and first_send_membership_changed_hold. Owner mapping is admission_evidence_hold→gateway, publication_pin_capacity_hold/quota_generation_exhausted→quota-coordinator, first_send_membership_changed_hold→subscriber, and response_preparation_hold/outcome_evidence_hold/outcome_evidence_conflict/resume_evidence_hold/terminal_evidence_hold→coordinator. The current cause selects its existing D8 exit; there is no extra owner, duplicate locator, standalone command_outcome_hold cause, new exit or erasure authority.',
    }
    supplemental_mappings = {
        'ActorCommitEvidenceHold': ('actor_commit_evidence_hold','`actor`'),
        'AppendPreparationHold': ('append_preparation_hold','`coordinator` for §7 preparation; `actor` for A7 no-op'),
        'ConsumerMembershipFenceUnavailable': ('consumer_membership_fence_unavailable','`subscriber`'),
        'HistoricalMessageIdCollision': ('historical_message_id_collision','`operations`'),
        'KeySpaceMigrationHold': ('key_space_migration_hold','`projection`'),
        'LegacyEvidenceConflict': ('legacy_evidence_conflict','`operations` for A4/A6 offline evidence; `projection` for B2 retained projection reads'),
        'LegacyHandoffCapacityHold': ('legacy_handoff_capacity_hold','`subscriber`'),
        'RawSourceUnavailable': ('raw_source_unavailable','`gateway` for B2 gateway reads; `projection` for B2/B9 projection reads'),
        'ReplayCommitAmbiguous': ('replay_commit_ambiguous','`coordinator`'),
        'RollbackReaderCapabilityHold': ('rollback_reader_capability_hold','`gateway` for A9/C6 deployment routing; `projection` for B8 projection routes; `subscriber` for C6 subscriber routes'),
        'CommandOutcomeHold': ('D4 aggregate: original authenticated cause','`gateway` for admission; `quota-coordinator` for pin capacity/quota generation; `subscriber` for first-send; `coordinator` for preparation/outcome/resume/terminal'),
    }
    supplementary = '\n#### Unrelated A/B/C holds retained under D8\n\nEach predicate keeps its defining owner and existing exit below. D8 reserves its locator before producer activation and derives status from that owner; discovery grants no additional cleanup or erasure authority. D8’s closed inventory hold→reason union is extended exactly by the ten diagnostic predicate mappings below; D4 HTTP reasons remain unchanged. Each owner cell selects one existing encoded owner kind from the actual defining authority and scope. Conditional cases are deterministic; no alternative list or slash string is an encoded enum, no new owner family or ownership transfer is granted. CommandOutcomeHold is the existing D4 aggregate view: it derives its actual reason and exactly one existing owner from the current authenticated cause, rather than adding a standalone command_outcome_hold reason or second locator.\n\n| Predicate | Inventory reason | Closed owner-kind | Defining authority, owner and existing exit |\n| --- | --- | --- | --- |\n'
    for name in hold_names:
        if name in supplemental_exits:
            reason,owner=supplemental_mappings[name]
            supplementary += f'| `{name}` | `{reason}` | {owner} | {supplemental_exits[name]} |\n'
    doc = doc[:pos] + '#### Imported reviewed D1–D9\n\n<!-- imported-d-contract-start -->\n' + d_body + '\n<!-- imported-d-contract-end -->\n' + supplementary + '\n' + doc[pos:]
    # Amend actual imported seams, preserving their unowned bytes and vectors.
    c1start = doc.index('### C1.')
    c2start = doc.index('### C2.')
    c1 = doc[c1start:c2start]
    c1 = re.sub(r'At (?:a |the )?pin CAS[^\n]*', 'D6 reserves the whole admitted member batch and all original charges/counters in one qualified ledger transaction before the separate pin install. No send or A8 revision zero precedes every exact reservation-bound attachment/readback.', c1)
    c1 = c1.replace('at the pin CAS', 'at D6 whole-batch reservation before separate pin install')
    c1 = c1.replace('At the pin CAS', 'At D6 whole-batch reservation before separate pin install')
    c1 = re.sub(r"An object's charge is.*?(?=\n\nIf either counter)", "D6 defines each original charge by exact kind-qualified account, canonical encoded length, pinned original overhead and checked amount, under D1's qualified shared metadata transaction. Same-kind/length reattachment preserves that original account/amount; mismatched kind or length refuses unchanged. Complete batch charges and both tenant/capture-scope and deployment counters reserve atomically before append. The separate pin install requires exact reservation-bound attachment/readback before any send or A8 revision zero. Legacy side/object creation likewise reserves its original complete charge before the external write or obligation-fence phase. Authenticated final deletion/closure readback precedes once-only refund to the original account.", c1, flags=re.S)
    doc = doc[:c1start] + c1 + doc[c2start:]
    c2start, c3start = doc.index('### C2.'), doc.index('### C3.')
    c2 = doc[c2start:c3start]
    c2 = re.sub(r'^If membership changes.*?(?=\n\n)', 'Before any first send, D4 versions the existing first-send outcome at its existing key using retained purpose-2a resolution. Fresh complete atomic EmptyNamespace/InitialRowOnly proof and byte-identical ContinueSamePin under active membership/configuration are the sole restoration. Incompatible images remain FirstSendMembershipChangedHold; manual checking cannot override, repin, abandon or complete them.', c2, flags=re.M | re.S)
    c2 += '\nD3 window/member/send authority applies to every new acceptance/index. Each purpose-1c parent binds the exact window claim, member, MessageId, pin and namespace before registration, enqueue, duplicate resolution or acceptance. The unchanged public purpose-1c carrier is accompanied by the authenticated retained window binding; historical fixtures retain their exact bytes. Operation-terminal and closed-window fences are both consulted before duplicates. Signed 1..64 local attempts apply within each window; accepted/class-02/class-03 permanent member fences still prohibit drain-only sends.\n\n'
    doc = doc[:c2start] + c2 + doc[c3start:]
    doc = doc.replace('2xx only after every addressed route has a durable terminal decision (C3)', '2xx after every addressed route has a durable terminal decision (C3), or D7 exact captured physical custody; logical obligations remain open after custody')
    c4start,c5start=doc.index('### C4.'),doc.index('### C5.')
    c4=doc[c4start:c5start]
    c4 += '\nD7 exact retained carrier, locator/receipt, active charge, captured owner and original discovery permit physical-copy handoff independently of logical completion. A legacy handoff still requires its complete C4 manifest/route set; captured custody never marks a logical route or effect completed. Ordinary 193 MiB and advertised provider-qualified quarantine through 256 MiB apply; finite local attempts/24-hour bound and direct dead-letter capture are D7 policy.\n\n'
    doc=doc[:c4start]+c4+doc[c5start:]
    doc=doc.replace('then, for an operation resumed under [I-45], each earlier window-closure record and its authentication bytes in ascending window order.', 'then, for an operation resumed under D3, the complete current-window attempt set plus the authenticated active execution control/current-window claim and rolling prior closure accumulator/count. Authenticate the active window’s retained signed claim/carrier, current addressed owner generation/fence, operation/scope/window/member/pin bindings, immutable admission and exact closedCount/accumulator selected by that owner before consuming them. Verify every current observation and seal the current window. D3 fixed-deadline deletion of superseded claims/closure/effect sources stands: terminal verification never requires those reclaimed artifacts. A bare historical hash/count, unauthenticated owner or a window closure alone is not terminal authority; all permanent operation-terminal and accepted/class-02/class-03 member fences still apply.')
    doc=doc.replace('FirstSendMembershipChangedHold` and', 'FirstSendMembershipChangedHold` and')
    doc=re.sub(r'^\| `FirstSendMembershipChangedHold` \|.*$', '| `FirstSendMembershipChangedHold` | C2/D4 first-send change | CommandOutcomeHold HTTP 503, reasonCode=first_send_membership_changed_hold; unchanged original pin, no send; only fresh zero-send plus exact ContinueSamePin under active configuration exits. |',doc,flags=re.M)
    doc=re.sub(r'^\| `ScopeRetentionCapacityHold` \|.*$', '| `ScopeRetentionCapacityHold` | D5 exact shard ceiling | HTTP 503/Retry-After 30, reasonCode=scope_retention_capacity_hold before invocation; D8 discovery and D5 exact-shard deletion/compaction/capability exit. |',doc,flags=re.M)
    pos=doc.index('## 9. Consumer and cancellation matrix')
    outcome_rows='| `AdmissionEvidenceHold` | D5 claim/read/CAS unavailable | HTTP 503/Retry-After 30, admission_evidence_hold; no invocation. |\n| `RegistryCapacityHold` | D8 scope/shard/quota capacity | HTTP 503/Retry-After 30, registry_capacity_hold; bootstrap discovery and exact freed-slot/readback exit. |\n| `CommandStatusExpired` | D5 live authenticated tombstone | HTTP 410, https://hexalith.io/problems/command-status-expired; no fallback. After exact deletion later reuse is legal. |\n| `ActivationInventoryCapacityHold` | D5 944+ routes | Domain readiness hold, full_replay_inventory_capacity; complete ≤943-route catalog/inventory required. |\n| `AppendPreparationLimit` | D6 precommit batch/queue/storage refusal | Preappend ingress refusal with no event append; unchanged admitted owner/registry retained until authenticated no-commit cleanup. |\n\n'
    doc=doc[:pos]+outcome_rows+doc[pos:]
    # Four slices reconcile D9 prerequisites and BC activation timing.
    doc=doc.replace('No §10.2 change activates in slice 2.', 'D9 slice 2 first installs the qualified metadata ledger/shared transactions, registry/onboarding/epoch and all owner/bootstrap charges before any hold producer; then legacy claims and pinned H begin (BC-15). Destination/policy capabilities are stored; V2 remains dormant.')
    doc=doc.replace('Slice 3 also activates the [I-37] hold inventory and the [I-45] and [I-46] publication-resume operation, together with BC-02.', 'Slice 3 activates D5 replay inventory, D3 tenant resume safety route/BC-02 denial, capsule-before-cleanup and exclusive legacy recovery. Wait H and authenticate fenced cutover before slice 4. D8 registry prerequisites already exist from slice 2.')
    doc=replace_rule(doc,41,'[I-41] **Ordered activation and release.** D9 fixes the four slices: slice 1 activates no incompatible behavior; slice 2 provider-qualified metadata/registry/epoch/charge prerequisites precede legacy claims and BC-15; slice 3 activates replay inventory, BC-02 safety route/capsules and the slice-3 consumer changes; slice 4 waits H/cutover before evidence-required scopes/status, whole-batch pins/queue/windows, membership restoration and captured physical handoff/redrive. BC-09/BC-10/BC-16 activate slice 4. Readers understand exact schemas before writers; incompatible old readers are fenced. D-RELEASE permits compatible maintenance/security publication through the existing current-main manual workflow when API, wire, package-only consumer and focused inactive-path evidence prove compatibility. Dormant compatible preparation alone is not a shipped breaking change. Genuine breaking commits remain honestly classified under repository commit policy and require SemVer-major. Retain the publication hold whenever compatibility is unproven or incomplete breaking changes reached main; expose the complete approved incompatible set only in the major release. No maintenance lane, version override or CI mutation is created by this specification. Every runtime activation still requires §12 and the separate AD-26 profile/provider gates.')
    doc=doc.replace('| Surface and old contract (at `ccb4faf0`)', '| Surface and old contract (at `cbbe4150`)')
    doc=doc.replace('Dead-letter topic\'s own subscription redelivering without a finite budget', 'dead-letter topic\'s own subscription using D7 direct bounded capture')
    doc=doc.replace('with the dead-letter topic\'s own subscription redelivering without a finite budget.', 'with the dead-letter subscription using D7 direct capture, never another dead-letter cycle.')
    for bc in ('BC-01',*[f'BC-01{c}' for c in 'abcdefghijk'],'BC-07','BC-08','BC-09','BC-10'):
        doc=re.sub(r'(^\| '+bc+r' \|[^\n]*)\| 3 \|$', r'\1| 4 |',doc,flags=re.M)
    doc=doc.replace('retired IDs stay consumed ([I-12])', 'retained IDs conflict through D5 obligations/tombstone horizon; after authenticated deletion reuse is legal (BC-16)')
    pos=doc.index('\nBefore activation, Story 6.6\'s compatibility gate')
    doc=doc[:pos].rstrip()+ '\n| BC-15 | Legacy admission could proceed when no shared scope claim was available. | D5 slice-2 read/CAS claim outage or eight CAS losses returns admission_evidence_hold HTTP 503 before archive/status/invocation. | Qualified scope+shard transaction, continuous H claims and authenticated cutover; clients retry after 30 seconds. | 2 |\n| BC-16 | Status could fall back and retained command identity horizon was implicit. | D5 authenticated live tombstone returns HTTP 410 command-status-expired without fallback; exact late admission is 409 idempotency-expired, changed input conflicts; eventual reuse only after authenticated obligation-free deletion. | Pinned bounded H/tombstone retention, hourly/75% reconciliation and once-only shard refund. | 4 |\n'+doc[pos:]
    doc=re.sub(r'^\| BC-01k \|.*$', '| BC-01k | DaprHealthQueryService:375 counts PublishFailed/TimedOut; CommandStatusFilterHelper:23–36 and DaprStreamQueryService:535–548 processing = Received/Processing/EventsStored/EventsPublished, failed = PublishFailed/TimedOut; exact enum parsing selects only its exact value. | D4 authoritative list-time join adds nullable RecoveryReasonCode. Named failed includes the two EventsStored publication holds; processing excludes them. Exact EventsStored still includes all EventsStored values including holds; exact PublishFailed and all other enums select only their authoritative exact value, never alias named failed. ErrorPercentage includes publication holds. | Authenticate status/control at read time; unavailable authority is per-item evidence incident, never stale submission success. | 3 |',doc,flags=re.M)
    doc=re.sub(r'(^\| BC-01k \|[^\n]*)\| 3 \|$',r'\1| 4 |',doc,flags=re.M)
    doc=doc.replace('the slice-3 rows of §10.2 (BC-01, BC-01a–BC-01i, BC-01k, BC-02, BC-04, BC-05, BC-07, BC-08, BC-09, BC-10, BC-13 and BC-14), the [I-06] long-stream inventory and activation record and the BC-08 capture capacity ([I-41])', 'the slice-3 rows of §10.2 (BC-02, BC-04, BC-05, BC-13 and BC-14), D5 long-stream inventory/activation and existing reader/capture capability prerequisites. Preparation and qualification of D7 capture in slice 3 grant no captured physical handoff/redrive activation; BC-07/BC-08 replaced D7 behavior waits for slice 4 ([I-41])')
    doc=doc.replace('the slice-4 rows of §10.2 (BC-01j, BC-03, BC-06, BC-11 and BC-12)', 'the slice-4 rows of §10.2 (BC-01 and BC-01a–BC-01k, BC-03, BC-06–BC-12 and BC-16), including D4 authoritative status and D7 captured physical handoff/redrive')
    doc=doc.replace('`POST /api/v1/admin/publications/{tenantId}/{messageId}/resume`','`GET /api/v1/admin/publications/tenants/{tenantId}/{resumeHandle}/precondition` and `POST /api/v1/admin/publications/tenants/{tenantId}/{resumeHandle}`')
    doc=doc.replace('`POST /api/v1/admin/held-deliveries/{tenantId}/{entryKey}/redrive`','`POST /api/v1/admin/held-deliveries/tenants/{tenantId}/{entryKey}/redrive` and `POST /api/v1/admin/held-deliveries/deployment/{entryKey}/redrive`')
    doc=doc.replace('GET /api/v1/admin/holds/{tenantId}', 'GET /api/v1/admin/holds/tenants/{tenantId}')
    doc=doc.replace('The register holds 12 citations, 39 rules, 8 Story 6.6 obligations and 5 non-story entries, plus the owner decision D-RESUME.', 'The original register is preserved with proposed, approval-pending dispositions. O-06/O-07/O-11 are restored successor-boundary verification obligations. D1–D9 replace owned rules; the 54 child-routed and 13 parent pass-2 rows below each have one proposed disposition. Five non-story entries and two earlier resolutions stay unchanged. The integrator must amend or refuse an affected disposition under this rule; no approval-pending ledger entry is resolved.')
    doc=doc.replace('| D15 integrity-script path exemptions | ledger :5110 | rule | [I-47] |','| D15 integrity-script path exemptions | ledger :5110 | 6.6 obligation | O-06 successor boundary gate |')
    doc=doc.replace('| D16 untracked files in the integrity script | ledger :5114 | rule | [I-47] |','| D16 untracked files in the integrity script | ledger :5114 | 6.6 obligation | O-07 successor boundary gate |')
    doc=doc.replace('| D15 addendum: bookkeeping set | ledger :5189 | rule | [I-47] |','| D15 addendum: bookkeeping set | ledger :5189 | 6.6 obligation | O-11 successor boundary gate |')
    doc=doc.replace('[I-47]; §11.7 withdraws O-06, O-07 and O-11.', '[I-47]; O-06/O-07/O-11 retain replacement boundary-gate ownership.')
    table=support.split('<!-- pass2-dispositions-start -->\n',1)[1].split('<!-- pass2-dispositions-end -->',1)[0]
    parent_rows={
        'VG2-1':'§11.6 derives all 16 same-attempt pairs from pinned A reduce_set, including frozen Accepted, with owning corrupt-transition refusal; O-03/O-04 bounded coverage is not a per-guard proof claim',
        'VG2-5':'§11.6 exact B label/value comparison', 'VG2-6':'§11.6 inline C offset/destination values',
        'VG2-7':'§11.6 exact verifier identity/count and missing-block refusal',
        'BH2-11':'I44 integrator amendment/refusal trigger and owner, open ledger statuses with proposed dispositions; every O-row has Story 6.6 owner/blocking gate/immutable closure evidence',
        'BH2-12':'§12 named exact receipt gate, five UNAPPROVED fields; story remains in progress; execution change log records D-RESUME/D-SPLIT supersessions without deleting history',
        'BH2-13':'Committed reproducible parent base 288a61908f4661fed52bb791f928292fe7d90180 and current baseline '+BASE+'; source-manifest exact full revisions/file/block hashes, no scratchpad prerequisite',
        'BH2-14':'I47 successor documentation boundary gate; O-06/O-07/O-11 restored under D-CLOSE, current committed/worktree/untracked scope checks and FW1 rerun gate',
        'BH2-15':'I41 D-RELEASE compatible maintenance/security publication through existing current-main manual workflow with compatibility/inactive-path evidence; complete major and hold policy retained',
        'BH2-17':'§11.6 independent Node reconstruction of all five surviving parent preimages and integer-safe D constructors; retired integration codecs are historical only',
        'E2-27':'D6 original-account/length/kind attach conflict/refusal, unchanged bytes; O-10 actual provider proof',
        'E2-33':'§11.6 exact labeled B results and owning swapped-label refusal; no substring comparison',
        'E2-37':'BC-01k authoritative exact-status EventsStored/PublishFailed semantics, distinguished from named failed/processing and unavailable list-time evidence',
    }
    extra='\n**Review pass 2: proposed dispositions pending §12.** The exact 54 child rows follow; one owner per raw finding, with local proof distinguished from runtime proof.\n'+table+'\n| Parent finding | Proposed disposition and exact gate |\n| --- | --- |\n'
    extra+=''.join(f'| {k} | {v} |\n' for k,v in parent_rows.items())
    pos=doc.index('### 11.6 Integrated child known answers')
    doc=doc[:pos]+extra+'\n'+doc[pos:]
    # Keep every unowned A/B and signed fixture; discard obsolete parent codec/models.
    start=doc.index('Integration codec known answers (label, exact byte length, SHA-256):')
    end=doc.index('### 11.7 Story 6.6 verification obligations')
    original=doc[start:end]
    surviving=('I08-outcome-prep','I08-key-preimage','I09-preparation-write','I17-destination-config','I33-preimage')
    lines=[l for l in original.splitlines() if any(l.startswith(k+' ') for k in surviving)]
    replacement='Integration codec known answers (label, exact byte length, SHA-256):\n\n```text\n'+'\n'.join(lines)+'\n```\n\nThe independent Node constructor and separate Python encoder reconstruct these five surviving parent preimages from the fixed K04 scope, original UTC and marker fields. D-owned private predecessors are historical only; the exact reviewed replacement schemas and all literals follow inside the normative digest.\n\n<!-- imported-d-wire-reference-start -->\n'+support+'\n<!-- imported-d-wire-reference-end -->\n\n<!-- imported-d-known-answers-start -->\n```json\n'+answers.rstrip()+'\n```\n<!-- imported-d-known-answers-end -->\n\n#### Complete integration verification gate\n\nThe gate pins the current baseline and every input revision/file/model hash, compares exact labeled A/B values and all inline C offset/destination vectors, derives all 16 I-11 state pairs from the pinned A reduce_set, and independently constructs every surviving parent and D literal. V17/V20/V22/V23 signed verifiers retain their exact block bytes and identities. Missing/duplicated/replaced blocks fail before execution. Owning corruption controls cover changed transition, swapped B label, changed inline offset, removed verifier fence, forbidden path and an input pin; these are bounded demonstrated regressions, not proof every guard is covered.\n\n```bash\npython3 _bmad-output/implementation-artifacts/6-5-integration/verify.py\n```\n\nFW1: rerun this gate and the D verifier/mutations/focused-regressions after any candidate, imported input, constructor or verifier change and before §12 presentation; Story 6.6 must install the blocking automatic gate before activation. This documentation run changes no CI.\n\n'
    doc=doc[:start]+replacement+doc[end:]
    # Keep obligation IDs, replacing obsolete subjects and naming gate/evidence.
    obligations={
        3:'Port pinned A/B/C models and the current integration gate with D bounded behavior/independent constructors into blocking CI. Retire superseded C01d capacity/C11h polling expectations in the port; D6 fixed precharges and D4 precedence govern. FW1 automatic execution is mandatory before activation.',
        6:'Successor boundary gate rejects every committed/index/worktree outside-scope change; no owner/automation exemption. Preserve immutable current-baseline inputs and gitlinks.',
        7:'Successor boundary gate rejects every untracked nonignored outside-scope path and missing tracked path; no path-only bypass.',
        10:'D1/D6 provider whole-batch transaction and separate reservation-bound pin install crash/readback: original accounts/amounts survive overhead change; mismatched attach refuses unchanged; all counters/charges plus eight-envelope fair queue grant/refund/parking/erasure are atomic. No paired queues/cross-counter move.',
        11:'Successor boundary gate confines bookkeeping to the explicit allowed set and validates exact file/block hashes, verifier identities and source ancestry; absent/untracked/committed deviations refuse.',
        18:'D9 two-host shared-PostgreSQL owner fence/generation/readback, scope+shard transaction, registry/reserve-before-owner and Operations epoch transfer; external capture charge-only/object-written/incarnation cleanup, cursor scope/generation and per-row evidence incident; D5 idle bootstrap and complete signed replay activation; I08/I09 crash evidence and I11 all-pair/conflicting-row refusal; unchanged bytes after every unavailable/refused phase.',
        19:'D-RELEASE current-main manual workflow compatibility gate: API/wire/package-only consumers and focused inactive-path evidence for compatible maintenance/security/dormant preparation; retain publication hold if unproven or incomplete breaking main, honestly classify genuine breaking commits and expose complete approved incompatible set only in SemVer-major. No new lane/version override.',
        20:'D3/D7 exact signed production resume/redrive authorization and provider readbacks, separate producer-disable/broker-reject crash phases, window versus permanent operation fence, complete C2 observations and accepted exclusion, original legacy actor range/ordered MessageIds/StoredDigests/classification restoration, fixed horizons/reclamation/once-only refund and lost-ack repeated lifetime resumes. No obsolete reconciliation/repair families.',
    }
    for n,value in obligations.items():
        doc=re.sub(rf'^\| O-{n:02} \|.*$',f'| O-{n:02} | {value} | Integration/D9 |',doc,flags=re.M)
    ostart=doc.index('| ID | Obligation | Source |',doc.index('### 11.7'))
    oend=doc.index('\n\n**Review focus',ostart)
    ot=doc[ostart:oend]
    ot=ot.replace('| ID | Obligation | Source |','| ID | Obligation | Source | Owner / blocking gate / closure evidence |').replace('| --- | --- | --- |','| --- | --- | --- | --- |')
    ot='\n'.join(l+' Story 6.6 Platform implementation and verification owner; preactivation blocking suite and applicable AD-26 provider qualification; exact immutable source/profile, test IDs, persisted bytes/readbacks, result and retained evidence digest. |' if l.startswith('| O-') else l for l in ot.splitlines())
    doc=doc[:ostart]+ot+doc[oend:]
    doc=doc.replace('Story 6.6 ports the three child models and the §11.6 verifier blocks', 'Story 6.6 ports the pinned child models and current §11.6 gate')
    # Refresh authoritative inventory facts, preserving historical claims separately.
    doc=replace_rule(doc,2,'[I-02] **Current source inventory.** This integration baseline is '+BASE+'. Historical A/B/C source baselines remain in §11.4; no old line citation implies present proof. Current AggregateActor.cs:2207 retains exactly one 30-second recovery attempt. AdminStreamQueryController.cs full-read sites are 129, 337, 466, 599, 786, 1066 and 1284 (seven). BC-01k cites the current named/exact filter predicates. The source-manifest pins every current outside-scope file/gitlink, with a read-only inventory of all table paths. Story 6.6 repeats source/symbol verification at its own baseline, records drift explicitly and never silently repins an input. Since the old parent baseline 138 source/test/docs paths changed; this is inventory drift, not authorization to change them.')
    doc=doc.replace('AggregateActor.cs:2206','AggregateActor.cs:2207')
    doc=doc.replace('`AggregateActor.cs:2207` at `ccb4faf0`','`AggregateActor.cs:2207` at `'+BASE+'`')
    pass1_updates = {
        'VG-1': "D6 fixed precharges/eight-envelope readiness and reviewed bounded cases replace the former integration capacity model; §11.6 runs D constructors/gate, O-03 ports D6 instead of superseded C01d asserts.",
        'VG-2': "The current §11.6 gate compares exact §11.4 file/model pins and labeled A/B answers, inline C values and current executable pins.",
        'VG-3': "§11.6 independently reconstructs all five surviving parent preimages and exact reviewed D schemas/literals; obsolete I12/I31/I37 private codecs are historical only.",
        'BH-1': "I44 keeps all 47 pending integrated ledger entries open with proposed dispositions; integrator amendment/refusal and obligation-owner closure triggers are explicit.",
        'E1': "I11 and the §11.6 gate derive all 16 same-attempt pairs from pinned A reduce_set, preserving Accepted/Failed receipt and observation-proof freezing.",
        'E26': "I01, A8 and C1/C6 admission wording use D1/D6 whole-batch reservation and fixed precharges before separate pin install; no un-precharged exception remains.",
    }
    for finding,value in pass1_updates.items():
        doc=re.sub(r'(^\| '+re.escape(finding)+r' \| [^|]+ \|)[^\n]*',r'\1 '+value+' |',doc,flags=re.M)
    manifest=json.loads(manifest_path.read_text())
    dpaths=[p for p in manifest['inputs'] if '/6-5d-simplification/' in p or '/spec-6-5d-' in p]
    pins='\n#### Current immutable D import pins\n\n| Path | Exact full committed revision | File SHA-256 |\n| --- | --- | --- |\n'+''.join(f'| `{p}` | `{manifest["inputs"][p]["revision"]}` | `{manifest["inputs"][p]["sha256"]}` |\n' for p in dpaths)
    pins+='\nThe current input manifest pins the complete child directory, including archives, checkpoints and execution records; none is rewritten or repinned. The imported D schema/contract/literal blocks are checked against the exact pinned source transformation. Historical child acceptance failure at its old correction baseline remains evidence; current preservation belongs to this run. D9 provider/crash gates remain unproved.\n\n'
    pos=doc.index('### 11.5 Disposition register')
    doc=doc[:pos]+pins+doc[pos:]
    write(ART/'spec-event-versioning-upcasting.md',doc)
    ledger=(ART/'deferred-work.md').read_text()
    doc=doc.replace("A closed entry's `status:` line reads `dispositioned pending approval`, because its disposition takes effect only when the §12 receipt validates.", "Every pending entry's status remains `open` and carries a proposed disposition; it takes effect only when the §12 receipt validates. The Story 6.5 integrator owns re-evaluation on any candidate amendment, receipt refusal or invalidation, in the same update that changes this register/ledger; the named Story 6.6 verification owner closes an obligation only on its blocking-gate immutable evidence.")
    doc=doc.replace('**Operator surfaces.** The hold-inventory actors, index, gauge and Admin view ([I-37]); the publication-resume operation with its audit, window-closure and reconciliation records ([I-45], [I-46]);', '**Operator surfaces.** D8 addressed owner registry, single Operations epoch, authoritative inventory gauge and scope-bound Admin paging; D3 publication resume with same execution-control owner, signed audit/window closure and capsule-bound legacy phase;')
    write(ART/'spec-event-versioning-upcasting.md',doc)
    write(ART/'deferred-work.md',ledger)
    # Machine-readable transition contract is derived from the immutable A model.
    import contextlib
    import io
    a=(ART/'spec-6-5a-event-contract-writer-and-migration-evidence.md').read_text()
    code=re.findall(r'^```python\n(.*?)^```$',a,re.M|re.S)[0]
    ns={}
    with contextlib.redirect_stdout(io.StringIO()):
        exec(code,ns)
    pairs=[]
    for before in range(4):
        for after in range(4):
            try:
                ns['reduce_set'](ns['publication_set']([1,after]),ns['publication_set']([1,before]))
                admitted=True
            except (AssertionError,ValueError):
                admitted=False
            pairs.append([before,after,admitted])
    transition='The following labeled contract is derived by executing the pinned A K09 reduce_set for every same-attempt pair; the integration gate recomputes all 16, including frozen Accepted.\n\n```text\nI11SameAttemptPairs='+json.dumps(pairs,separators=(',',':'))+'\n```\n\n'
    pos=doc.index('\n[I-12]')+1
    doc=doc[:pos]+transition+doc[pos:]
    # Capture custody is distinct from the logical all-route terminal path.
    doc=doc.replace('Physical acknowledgement for an **identified addressed Binary or legacy JSON** delivery requires the committed handoff pointer and **every** addressed route\'s authenticated durable terminal decision and matching `Completed` effect receipt or permitted `Filtered` proof read back.', 'Physical acknowledgement for an **identified addressed Binary or legacy JSON** delivery requires either D7 exact captured physical custody (with every logical obligation still open) or the committed handoff pointer and **every** addressed route\'s authenticated durable terminal decision and matching `Completed` effect receipt or permitted `Filtered` proof read back.')
    doc=doc.replace('The old JSON broker delivery is acknowledged only under C4\'s rule:', 'The old JSON broker delivery without a D7 retained-custody handoff is acknowledged only under C4\'s logical-completion rule:')
    doc=doc.replace('Old JSON acknowledgement requires broker acceptance', 'Old JSON acknowledgement without D7 retained-custody handoff requires broker acceptance')
    doc=doc.replace("Each pin is charged at its exact size at the pin CAS against C1's publication-retention ceilings; a full counter returns `PublicationPinCapacityHold`, creates no pin and keeps the committed outbox pending.", "D6 atomically reserves the complete admitted batch and original per-tenant/deployment charges before append; the separate pin install authenticates exact reservation-bound attachment/readback before send or A8 revision zero. Waiting existing committed work remains `PublicationPinCapacityHold` under D6 without partial grants or an uncharged pin.")
    doc=doc.replace(", except at C1's per-tenant and deployment publication-retention levels ([I-01] (5))", ". D1/D6 additionally require whole-batch retained-capacity reservation, fixed precharges and complete queue/owner readiness before append ([I-01] (5))")
    doc=doc.replace("Without that verified evidence, **status inspection** of a head at private `failed` deterministically yields `CommandOutcomeHold`, never terminal PublishFailed or stop-polling semantics. This status hold does not block private outcome creation, head advancement or first-response pinning:", "D4 supplies the complete ordered status mapping: a private `failed` observation alone never establishes terminal PublishFailed. Authenticate active-window failure classes, drain-limit/exhaustion and all earlier evidence/conflict/preparation predicates before choosing its exact nonterminal hold or progressing EventsStored form. Status inspection does not block private outcome creation, head advancement or first-response pinning:")
    doc=re.sub(r'^\| 8\. Storage ceilings \|.*$', '| 8. Storage ceilings | B6 retains its 1 GiB per-operation and shared deployment replay-storage ceilings and separate 64 GiB named-state cap. D1/D6 govern qualified whole-batch pin/charge/counter reservation before append, fixed bootstrap precharges and all eight 100 MiB queue envelopes; separate pin/object install requires exact reservation-bound readback. D6 tenant/capture-scope and deployment readiness, original charges and once-only authenticated deletion/refund apply. Existing committed waiting work remains PublicationPinCapacityHold without a pin/send/revision zero, under D4 status precedence; no un-precharged post-commit exception admits new work. |',doc,flags=re.M)
    doc=doc.replace("acknowledges the old message only after broker acceptance and every route's terminal decision (C4)", "acknowledges the physical old copy after D7 exact retained custody, keeping every logical obligation open, or after broker acceptance and every route's terminal decision (C4)")
    # Exact adapter documentation is bound into the same normative digest.
    adapter=(OUT/'metadata-adapter-contract.md').read_text()
    import importlib.util
    import sys
    sys.dont_write_bytecode=True
    cursor_spec=importlib.util.spec_from_file_location('integration_cursor_constructor',OUT/'verify.py')
    cursor_module=importlib.util.module_from_spec(cursor_spec)
    cursor_spec.loader.exec_module(cursor_module)
    cursor_block='''#### D8 authenticated cursor framing and independent positive vector

The existing `payload.schema` literal `hexalith.eventstore.hold-cursor/1` is the authenticated inventory audience. HS256 signs the exact canonical UTF-8 payload including that literal; verification requires this exact schema and the original authorized scope, generation, continuation and expiry. No public audience field is added. The reviewed inventory_page generation preimage is exactly canonical JSON `[scopeHeaderGeneration, ordered [subject, entryGeneration] pairs]`: the outer second element is the full ordered pair array for the authorized scope, in D8 registry order. An authenticated absent scope header is JSON `null`, not zero or omitted; unavailable header evidence remains an incident. SHA-256 of those exact bytes is `payload.generation`. Owner revisions, foreign-scope entries and shard-global revisions are excluded. The envelope signature is lowercase hex HMAC-SHA256 over the exact canonical payload using the resolved server-only cursor key; it remains distinct from purpose 2d.

The preserved child `cursor_sign` prefix-hash and `public.cursor-envelope` literal are immutable historical model evidence, not HS256 production authorization. Their bytes remain in the imported literal block. The following small Python/Node independent HS256 positive answer uses public fixture key bytes 00..1f, header generation 7, ordered entries a/2 and é/3, and original expiry 9000000000 ticks. Its authenticated-absent generation vector is `[null,[]]`. These vectors fix framing and standard HMAC arithmetic, and prove no production key, authorization, rotation, backend or provider behavior.

<!-- inventory-cursor-known-answers-start -->
```json
'''+json.dumps(cursor_module.cursor_known_answers(),ensure_ascii=False,sort_keys=True,indent=2)+'''
```
<!-- inventory-cursor-known-answers-end -->

'''
    adapter_block='#### Exact application-owned metadata adapter\n\n<!-- imported-adapter-contract-start -->\n'+adapter+'\n<!-- imported-adapter-contract-end -->\n\n'
    pos=doc.index('## 8. Numeric budgets')
    doc=doc[:pos]+cursor_block+adapter_block+doc[pos:]
    pinned=('verify.py','independent-answers.mjs','source-manifest.json','metadata-adapter-contract.md','build-integration.py','preserved-hold-predicates.json')
    pin_table='#### Current integration executable/document pins\n\nThese files are current uncommitted content, with no invented committed revision. Their exact SHA-256 values are part of the AD-13 approval digest. Any change requires rerun, fresh digest and fresh approval; the gate checks the table generically without a self-hash cycle. Verification-results and review captures are execution evidence rather than authorizing source.\n\n| Current path | Exact SHA-256 |\n| --- | --- |\n'
    pin_table+=''.join(f'| Current uncommitted `_bmad-output/implementation-artifacts/6-5-integration/{name}` | `{digest((OUT/name).read_bytes())}` |\n' for name in pinned)
    pos=doc.index('### 11.5 Disposition register')
    doc=doc[:pos]+pin_table+'\n'+doc[pos:]
    doc,ledger=owner_policy(doc,ledger)
    write(ART/'spec-event-versioning-upcasting.md',record_owner_approval(doc))
    write(ART/'deferred-work.md',ledger)
    print('spliced D1–D9, wire schemas/literals, dispositions, outcomes, compatibility and successor gates')

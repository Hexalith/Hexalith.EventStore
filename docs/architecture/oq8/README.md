# OQ8 governing-design provenance

EventStore retains the immutable governing design and its owner's permission
notice as documentation. Both files are copied directly from the source Git
objects at the commit below, without newline conversion or other edits.

<!-- oq8-governing-design:begin -->
```text
source-repository: github.com/Hexalith/Hexalith.Folders
source-path: docs/exit-criteria/oq8-idempotency-design.md
source-commit: a9cfea91c8a987ef7a836c216e633a92321fc3c2
design-version: 1.0.0
design-sha256: 1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8
local-design: docs/architecture/oq8/oq8-idempotency-design.md
source-license-path: LICENSE
source-license-commit: a9cfea91c8a987ef7a836c216e633a92321fc3c2
source-license-sha256: 6af66baf053023d456706de321e3cea6a6edbb288ffec10f673186b1befe7ac2
local-license: docs/architecture/oq8/LICENSE
permission-basis: MIT License; Copyright (c) 2026 Hexalith; copying associated documentation with the copyright and permission notice retained.
```
<!-- oq8-governing-design:end -->

The source repository's pinned [MIT notice](LICENSE) is the recorded owner
permission: it expressly covers associated documentation and allows copying
provided the copyright and permission notice are included. EventStore retains
the complete notice with the [10,474-byte design](oq8-idempotency-design.md).
The notice is 1,065 bytes. This permission permits retention; it supplies no
new architecture approval or authorization for an operational transition.

Run `python3 tools/validate-oq8-governing-design.py` from EventStore. The check
uses fixed pins in the validator, verifies both files' exact bytes and current
identity bindings, and requires no Folders checkout, network access, submodule
initialization, or deleted evidence tree. The dedicated
`.github/workflows/oq8-governing-design.yml` runs independent fixture and live-evaluation jobs. The live job reports PASS or FAIL in logs and the GitHub summary, bound to the source SHA and workflow digest.
Proof is recorded in Story 4.17 and CI under the current AD-12 policy.

Successful validation establishes governing-design availability only. Runtime
conformance, G-BASELINE, G-HIGH-RISK and other controls retain their own
dispositions. Story 4.14/4.15 results remain historical and bounded; this copy
grants no readiness, release, deployment, consumer migration, or Folders closure
authority. CI evidence remains pending until a separately authorized push and
a passing workflow run on the delivered source.

## Manual assured availability decision

Ordinary push, PR and manual runs with all decision inputs empty remain
approval-free availability diagnostics. Their PASS is not an assured gate PASS.
The optional manual decision path below supports only G-OQ8 governing-design
availability. It requires a completed successful evaluation of the dedicated
`phase-4-high-risk-gate-pass.yml` workflow on the exact current `main` SHA.
See the [consumer spec](../../../_bmad-output/implementation-artifacts/spec-9-2-mandatory-oq8-gate-consumer.md)
and [closed contract](../../../tools/phase-4-transition.schema.json).

These are operator instructions, not evidence that any approval or workflow run
exists. Dispatching and posting comments require a separately authorized human
action. Do not change any substantive request input after obtaining approvals;
current-main, input, role-registry or approval drift requires verification again.

1. Start from a clean EventStore checkout of the delivered current `main`, with
   the consumer files present. From the repository root, prepare the closed
   request and genuine predecessor identity locally:

   ```bash
   export OQ8_DECISION_WORK=_bmad-output/implementation-artifacts/evidence/oq8-decision-work
   python3 -B - <<'PY'
   import hashlib
   import json
   import os
   from pathlib import Path
   import subprocess
   import sys

   sys.path.insert(0, 'tools')
   import phase_4_transition as control

   work = Path(os.environ['OQ8_DECISION_WORK'])
   work.mkdir(parents=True, exist_ok=True)
   bind = lambda path: {'path': path, 'sha256': hashlib.sha256(Path(path).read_bytes()).hexdigest()}
   head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
   matrix = json.loads(Path(control.MATRIX).read_text())
   row = next(row for row in matrix['gates'] if row['gate'] == 'G-OQ8')
   registry = json.loads(Path(control.REGISTRY).read_text())
   humans = {identity['github_id'] for identity in registry['identities'] if identity['kind'] == 'human'}
   level = 'single-maintainer-attested' if len(humans) == 1 else 'independent'
   output = subprocess.check_output([sys.executable, '-B', control.OQ8_VALIDATOR])
   subjects = row['subject_paths']
   request = {
       'schema': control.REQUEST_SCHEMA, 'repository': control.REPOSITORY,
       'transition': 'high-risk-gate-pass', 'head_sha': head, 'subject_mode': 'files',
       'subjects': [bind(path) for path in subjects],
       'evidence': [bind(path) for path in (control.OQ8_VALIDATOR, *control.OQ8_INPUTS) if path not in subjects],
       'matrix': bind(control.MATRIX), 'registry': bind(control.REGISTRY),
       'required_level': level, 'author': row['author'], 'gate': 'G-OQ8', 'nfr_ids': [],
       'approvals': [], 'predecessors': [{
           'adapter': 'oq8-governing-design-v1', 'commit': head,
           'validator': bind(control.OQ8_VALIDATOR), 'inputs': [bind(path) for path in control.OQ8_INPUTS],
           'output_sha256': hashlib.sha256(output).hexdigest(), 'assurance_level': level,
       }],
   }
   control.validate_request(request)
   (work / 'request.json').write_text(json.dumps(request, indent=2) + '\n')
   PY
   ```

   This generates a request, not an approval or seal. The work directory is
   ignored local scratch; never commit its contents. The verifier independently
   computes the required level from the authenticated registry.

2. With a GitHub token available as `GH_TOKEN` or `GITHUB_TOKEN` that can read
   repository contents, Actions, checks and issue/PR comments, obtain copyable
   role-specific approval payloads:

   ```bash
   python3 -B tools/validate-phase-4-transition.py evaluate \
     --expected-transition high-risk-gate-pass \
     --request "$OQ8_DECISION_WORK/request.json" \
     --result "$OQ8_DECISION_WORK/approval-diagnostics.json"
   ```

   Missing approvals produce exit 1 and `APPROVAL_MISSING`; inspect any other
   failure before continuing. Copy each complete object in `approval_payloads`
   into its own issue/PR conversation comment in this repository. The actual
   human role holder posts it, unedited, at least 24 hours after the authenticated
   latest subject-changing push to `main`, not the commit timestamp. Every
   evaluator role needs a separate comment. With an independent roster, each
   approver must be a human distinct from the author. A generated payload whose
   `decision` says `approved` is only a template until the owner posts it.

3. Record the numeric IDs of those real comments as a JSON array in
   `$OQ8_DECISION_WORK/comment-ids.json`. With authenticated `gh` read access,
   add references using the comments' exact current UTF-8 body bytes:

   ```bash
   python3 -B - <<'PY'
   import hashlib
   import json
   import os
   from pathlib import Path
   import subprocess

   work = Path(os.environ['OQ8_DECISION_WORK'])
   request = json.loads((work / 'request.json').read_text())
   request['approvals'] = []
   for comment_id in json.loads((work / 'comment-ids.json').read_text()):
       endpoint = f'repos/Hexalith/Hexalith.EventStore/issues/comments/{comment_id}'
       comment = json.loads(subprocess.check_output(['gh', 'api', endpoint]))
       request['approvals'].append({
           'comment_id': comment['id'],
           'body_sha256': hashlib.sha256(comment['body'].encode('utf-8')).hexdigest(),
           'role': json.loads(comment['body'])['role'],
       })
   (work / 'request.json').write_text(json.dumps(request, indent=2) + '\n')
   PY
   ```

   Only approval references are added; they are excluded from the approval
   request digest. The verifier checks comment identity, role, body, timestamps
   and the 24-hour window itself. Editing or deleting an approval invalidates it.

4. If separately authorized, open **Actions → Phase 4 high-risk-gate-pass → Run
   workflow**, select branch `main`, set `mode` to `evaluate`, and paste the
   complete `request.json` text into `request`. Leave both seal coordinate inputs
   empty. Wait for the attempt and **Phase 4 transition attempt** job to finish
   successfully. Record that exact evaluation run's numeric run ID and attempt
   number, available from the run page or read-only API
   `repos/Hexalith/Hexalith.EventStore/actions/runs/RUN_ID`. Confirm `head_sha`
   matches the request, `run_attempt` is the selected attempt, `status` is
   `completed`, and `conclusion` is `success`. A running, failed, ordinary OQ8 or
   consume-mode run cannot supply the evaluation seal. Keep the request unchanged.

5. Choose either separately authorized manual consumer invocation on `main`:

   | Workflow | Inputs |
   | --- | --- |
   | **OQ8 governing-design availability** | `decision_request`: complete unchanged `request.json`; `sealed_run_id`: completed dedicated evaluation run ID; `sealed_attempt`: its exact attempt number |
   | **Phase 4 high-risk-gate-pass** | `mode`: `consume`; `request`: the same JSON; `sealed_run_id` and `sealed_attempt`: those same evaluation coordinates |

   Both invoke `tools/validate-phase-4-gate-decision.py`. For an authorized local
   invocation against the same platform facts, the equivalent command is:

   ```bash
   python3 -B tools/validate-phase-4-gate-decision.py \
     --request "$OQ8_DECISION_WORK/request.json" \
     --run-id ACTUAL_EVALUATION_RUN_ID --attempt ACTUAL_EVALUATION_ATTEMPT \
     --result "$OQ8_DECISION_WORK/decision.json"
   ```

   Use exact option names; abbreviated options are rejected. Exit 0 means this
   invocation issued a bound availability PASS; exits 1 and 2 mean verification
   refusal and malformed input respectively. Both workflows always upload
   diagnostics. Inspect `result.json` for scope `governing-design-availability`,
   required/achieved/downstream-minimum assurance, bound inputs and seal coordinates.

Each decision reports one fresh platform verification. A downloaded candidate,
raw availability PASS or saved decision cannot authorize later consumption;
later consumers must verify again through the platform. No runtime conformance,
NFR closure, readiness, release, deployment, migration, removal or Folders closure
permission follows, and other Story 9.2 consumers remain outstanding.

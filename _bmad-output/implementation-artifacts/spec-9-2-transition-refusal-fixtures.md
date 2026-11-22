---
title: 'Story 9.2 gate refusal fixtures'
type: 'chore'
created: '2026-11-22'
status: 'done'
route: 'oneshot'
story_key: '9-2-phase-4-high-risk-gate-matrix-and-assurance-control'
baseline_commit: '2ca76828bf1b9e666b1ed309da4a96f7252290b2'
---

<frozen-after-approval reason="20 November sprint amendment to Story 9.2">

## Intent

Exercise three independent gate requests through the Phase 4 evaluator. An unbound business gate and the deferred runtime parity gate must each return `GATE_UNBOUND`. A request missing the tenant gate's declared subjects must return `SUBJECT_MISMATCH`. Observe whether the evaluator starts a command in each case and exercise the observer against the real OQ8 positive control. Run the complete transition suite and the live matrix separately.

</frozen-after-approval>

## Implementation Notes

The three cases run the real CLI with the production matrix and synthetic GitHub responses. The tenant request includes a valid tenant subject and omits other required paths. The OQ8 positive case observes the validator and fixture scripts in separate subprocess launches.

The live matrix command `python3 -B tools/validate-phase-4-gate-risk-matrix.py _bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json` exited 1 at the baseline. Five standard controls passed. All twelve high-risk gates had achieved assurance of none. G-TENANT reported a missing seal, missing attestation and assurance below the required level.

The pre-edit isolated Aspire run built the AppHost. The CLI could not create its connection directory at the read-only path `/home/agent/.aspire` and exited 2. The resource query returned no running AppHost. These fixtures exercise the Python transition CLI and its subprocess boundary.

All 45 tests discovered from `tools/tests/test_phase_4_transition.py` passed in two isolated `python3 -B -m unittest -v` workers (23 and 22 tests). In a disposable test checkout, adding a harmless Python subprocess immediately before the normal G-APPEND refusal made the new refusal test fail. The observer recorded that exact command.

`python3 -B -m unittest discover -s tools/tests -p test_phase_4_gate_risk_matrix.py` passed 147/147. Two gate-decision CLI controls also passed.

## Review Triage Log

- Medium, fixed observer reinstallation truncating the command log. A nested Python process that installed the fixture again preserved its parent's launch record.
- Medium, fixed the positive control accepting a script filename in an unrelated argument. It checks both script positions on separate isolated Python invocations. The focused real OQ8 consumption test passed.
- Medium, removed environment-based observer activation. Only the four opted-in fixture interpreters install the observer in their temporary startup file. Running the positive test with a pre-existing caller log preserved its bytes.

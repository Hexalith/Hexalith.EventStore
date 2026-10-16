# Independent review of the first hardening diff

Reviewed artifact: `review-v1.diff`, SHA-256 recorded in `review-v1-diff.json`.
Three context-free reviewers used the same inherited model capability. They did
not edit source or retained evidence. Every finding is classified separately
before grouping below; no finding is omitted or deferred.

| ID | Reviewer | Verdict | Verified finding | Resolution group |
| --- | --- | --- | --- | --- |
| B1 | Blind | medium | Strict timestamp comparisons reject equal values deliberately produced by `preparation.stamp()` on wall-clock reversal. Existing timestamp tests confirm the producer contract. | Timestamp ordering |
| B2 | Blind | medium | Rehashed evidence containing a 120-second daemon start imports because the importer measures from the first probe and adds one-second allowances. | Startup deadline |
| B3 | Blind | medium | Rehashed daemon inspection can report host namespaces/ports/socket; initial SDK inspection can contradict final safe configuration. Literal create options alone do not settle contradictory observations. | Observed role configuration |
| B4 | Blind | medium | Coordinated Docker path/hash substitution in both proofs and literal `_proof` output imports despite the fixed acquisition/copy path. | Executable identity |
| B5 | Blind | medium | An executor root with session different from PID imports, contradicting `start_new_session=True`. | Root identity |
| B6 | Blind | medium | Owned inventory can contain a protected supervisor PID with different start ticks because exclusions compare whole dictionaries. | Protected PIDs |
| B7 | Blind | medium | The second private cleanup can claim the root present after the first established no remaining owned processes. | Successive process observations |
| B8 | Blind | medium | Coordinated proofs can contain `glibc=None` or omit mount namespaces and import. | Complete toolchain proof |
| B9 | Blind | medium | A rejected `unix:///run/docker.sock?token=secret` is retained before endpoint validation. | Refusal redaction |
| E1 | Edge | medium | Independent reproduction of the rejected Unix URI credential retention. | Refusal redaction |
| E2 | Edge | medium | Repeated producer timestamps conflict with the strict new importer ordering. | Timestamp ordering |
| E3 | Edge | medium | Independently reproduced a passed import with a sixty-second daemon start. | Startup deadline |
| V1 | Verification | medium | An in-memory `_inner` mutation removing root assignments and numbered/timed cleanup passed all twelve private-command/hardening tests: the Docker fixture fabricates private proof. Add a real supervisor-to-importer positive control. | Real supervisor regression |
| V2 | Verification | medium | Rehashed sixty-second startup receipt imports; first-probe anchoring misses the start duration. | Startup deadline |
| P1 | Parent | medium | The real live smoke's Docker Desktop SDK bind inspection has a translated physical source under `/run/desktop/mnt/host/wsl/docker-desktop-bind-mounts/`, while literal creation requests the exact workspace and in-SDK source hashes match. `Source == Destination` rejects this actual producer when the new finalizer validates it. | Canonical bind-source compatibility |

All groups route to a direct patch: they strengthen checks of already retained
state or add focused regression controls, without selecting packages, altering
the frozen user intent, adding operational authority, or changing public APIs.

The blind reviewer computed its floor as
`min(floor(sqrt(65,856 / 1,000) + 1), 10) = 9` and supplied nine findings.
The edge reviewer returned the three trigger/location/consequence records
represented above. The verification reviewer demonstrated the missing-producer
fields mutation and separately reproduced the startup timing defect. Final
patch/retest bindings are recorded separately; the original diff and successful
pre-review verification remain historical evidence of this review iteration.

P1 is supported through the exact literal requested source/destination, complete
in-SDK source binding and consistent repeated mount inspections. The observed
engine source must be an absolute nonsocket path. This accounts for canonical
engine path translation without admitting a substituted requested workspace.

## Independent recheck of the second diff

All original independent findings were rechecked against `review-v2.diff`.
Normal and optimized hardening suites passed fifteen tests each; the isolation
suite passed twenty-eight. The recheck identified these additional outcomes:

| ID | Reviewer | Verdict | Verified finding | Resolution |
| --- | --- | --- | --- | --- |
| B10 | Blind | medium | Coordinated engine source paths containing `..`, duplicate leading slash or `/.` import. Absolute/nonsocket checks do not establish canonical syntax. | Add lexical POSIX canonical-path guards and coordinated negative controls. |
| B11 | Blind | medium | A real new registered process discovered and successfully removed by the second cleanup is rejected by the blanket empty-before requirement. Both actual cleanup `remaining` lists are empty and private execution passes. | Distinguish new identities from previously absent identities using per-attempt ownership proof; retain resurrection refusal. |
| E4 | Edge | medium | Independently reproduced `/engine/mapped/../workspace` import. Requested mount and source closure remain enforced. | Same patch as B10. |
| V3 | Verification | medium | An in-memory omission of real `private_proof`'s new `docker_version` output passes seventeen relevant tests because those tests do not inspect its returned measurement. | Assert actual collector output with Docker/OS boundary doubles and prove mutation sensitivity. |

Each additional finding routes to a direct patch within the existing proof
contract and controls. No prerequisite state or historical artifact is changed.

## Final producer and identity control recheck

The blind reviewer reran the original real-process late-discovery reproduction:
ownership grew from two to three identities, both cleanups finished empty,
controller exit was zero, and receipt import passed. Blind and edge reviewers
also confirmed malformed engine paths refuse, valid mappings pass, and ownership
snapshots reject shrinking inventories, protected aliases and resurrection.
Neither found a new material implementation defect.

The verification reviewer confirmed the actual `private_proof` collector's
client/server output is observed and its compiled omission is detected. It also
verified the late-discovery fixture calls the real cleanup on a real registered
process. All thirty isolation tests and eighteen normal/optimized hardening
tests passed. One additional test-control finding was classified:

| ID | Reviewer | Verdict | Verified finding | Resolution |
| --- | --- | --- | --- | --- |
| V4 | Verification | medium | New per-attempt ownership snapshots mask existing root-session/protected-PID negatives because the mutations leave snapshots inconsistent. Removing the root guard or restoring dictionary-based exclusions still leaves that test green, while a coherent invalid receipt imports. | Make identity mutations coherent across root, final inventory and each attempt snapshot/observation; isolate root/alias refusal and retain nonempty-remaining as cleanup-failure coverage. Test-only patch. |

## Closure

The final verification reviewer independently rechecked V4 against
`review-v4.diff`. Root changes now update every matching identity reference;
protected aliases consistently appear in final and per-attempt inventories.
Compiled session-guard omission and both PID-to-dictionary regressions admit the
coherent bad receipts, while the real validator refuses them. Both affected
tests passed normally and under optimized Python. No unresolved V4 finding or
new material verification gap remained.

All twenty independently classified rows above, including the parent compatibility
finding, are resolved. No finding was deferred. Runtime/fixture bytes from the
successful v3 live smoke are unchanged by the last test-only correction; final
source applicability is retained with the final verification.

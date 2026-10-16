# P1R supported-case correction and qualification isolation

The correction is verified. Four-role acceptance, same-baseline conformance and support disposition remain pending; P1R is unusable and Story 6.1 remains blocked. This is repair verification against existing signed 3.121.0 packages and a distinct Debug/source consumer. A future immutable successor still requires publication, owner selection and complete qualification.

The [final controller audit](verification-d24fddc3cffa4ce9a2e3322b4d9725c2/final-independent-validation.json) passed all nine required checks after owned teardown. The [independent measurement audit](verification-d24fddc3cffa4ce9a2e3322b4d9725c2/attempt/independent-verification/independent-validation.json) passed 45 checks and reconstructed **72 cases / 3,218 assertions, zero failures**, across ten complete lanes. Cleanup passed **7/7** and dependent restore **12/12**. Each published package role passed **89/89** signature, assets, lock-graph and physical-assembly checks.

`logical-event-read-rejected` was triggered by historical fixture construction. `snapshot-nine` merged `count=9` into a serialized `DomainServiceCurrentState`, retaining `events`, `currentSequence` and other replay DTO fields. Production correctly rejected `SnapshotStateShapeCollision`; fallback replay from one then fell below retained floor five. The [diagnosis](diagnosis.json) retains exact SQL command/output hashes for all six cases, including both package/source fixture instances.

The correction replaces only the stopped-writer Counter snapshot state with `{"count":9}` and sets sequence nine. It preserves envelope version, identity, timestamp, protection fields, historical event bytes and second-tenant state. Production `src/` has no changes from `v3.121.0`. No reader exception, corruption/protection/floor rule, supported direction or assertion definition was relaxed. The selected package/source seed still creates a genuine version-one folded snapshot through the manual API after nine actual events, then appends three actual events; this historical diagnostic mutation does not replace that path or authorize production P2 enrollment.

All six cases retain operation `supported`. The original sixteen failing checks now pass:

| Lane | Case | Formerly failed checks now passing | Current case assertions |
| --- | --- | ---: | ---: |
| metadata-write | 3.110.0-to-3.121.0 | 3 | 52/52 |
| metadata-write | 3.70.1-to-3.121.0 | 3 | 52/52 |
| retained-covered | 3.110.0-to-3.121.0 | 1 | 45/45 |
| retained-covered | 3.70.1-to-3.121.0 | 1 | 45/45 |
| checkout | retained-covered | 2 | 91/91 |
| checkout | metadata-write | 6 | 105/105 |

The checkout cases use the historical 3.110.0 writer separately against selected-package and Debug/source readers. Supported metadata writes also pass append/restart to thirteen and retained-floor-five preservation checks.

| Complete repair lane | Passed/attempted |
| --- | ---: |
| metadata-write | 226/226 |
| retained-covered | 298/298 |
| retained-uncovered | 596/596 |
| missing-event | 540/540 |
| invalid-evidence | 354/354 |
| checkout | 704/704 |
| post-upgrade-restore | 61/61 |
| failure-cleanup | 115/115 |
| reminder-recovery | 131/131 |
| logical-event-evolution | 193/193 |

These are eight canonical lanes plus both adopted additions, not the successor's full seventeen canonical lanes plus two additions. The original [final failed qualification](../6-1-p1r-31210-published-run/review-round-1/README.md) remains unchanged: nineteen lanes, 163 cases, 4,774 assertions, sixteen failed assertions, cleanup 6/7 and dependent restore 11/12. No old result was rewritten or promoted.

The new isolation wrapper runs Python, .NET, Dapr and provider containers with a private Docker daemon and shared private network/PID namespaces. Its SDK, daemon/CLI and Python images are digest pinned. The SDK runner has no host Docker socket, host network or published ports. It fixes the Docker endpoint and client configuration, measures distinct daemon IDs and actual namespaces, and retains executable hashes and literal commands. Ordinary `observe`/`run` CLI entry points enter this wrapper automatically; immutable input/tag/HEAD admission remains enforced before provisioning.

The original strict equality assertion still compares the complete nonempty private shared inventory. A separately owned sentinel is shared from the executor's perspective. A separate negative invocation stops it: exactly `shared-preserved` fails and executor cleanup leaves it intact. The driver then restarts its own sentinel before the fresh positive invocation. Positive cleanup preserves that complete inventory exactly; only afterward does the driver remove its own sentinel by exact identity and label. Separately owned outer-daemon activity creates, starts, stops and removes a distinct container while the private cases execute; [its measured lifecycle](verification-d24fddc3cffa4ce9a2e3322b4d9725c2/outer-activity.json) cannot invalidate the private inventory.

Outer cleanup repeatedly checks exact IDs and invocation labels. Unexpected private containers or unavailable private inventories preserve their parent daemon/volume; unavailable final supervisor cleanup also preserves the SDK. The [final absence scan](all-owned-resources-absent.json) confirms all 34 registered outer resources and six scratch directories from nine retained isolation registries are absent. No unowned resource was removed. [Owned recovery](owned-recovery-437cbce972924a4a8a46ea5cb7e56195/) separately records exact ownership and empty private inventory before recovering the first failed verification setup.

Independent controls retain failures rather than converting them into passes:

- [Outer receipt substitution](outer-proof-negative-8f6b3d32cd7b468c811bfa9026268c85/control.json): replacing the activity ID with the owned SDK ID fails the required outer-activity check; the copied final audit exits two with validity false. Original passing receipts remain untouched.
- [Final-audit interruption](root-final-audit-negative-80c481ebb21c4b5ea7341da554cf61b1/controls.json): real SIGINT/SIGTERM retain nonpassing results and incomplete-check refusal.
- [Automatic public CLI entry](root-live-cli-control-4426572521464ef89b5526fc0cb781cf/control.json): real private execution of a deliberately malformed observation refuses, retains structured locations and completes owned cleanup.
- [Recovery-contract recheck](inventory-contract-recheck-84d8eb74eef3465096963681cab2d053/result.json): all twelve actual receipts and six meaningful foreign/mismatched recovery negatives pass 41 independent controls.

Focused regression verification passed **136 published Python controls + 23 isolation controls**, and **178 Server tests**, with no skipped Server tests. New paired Server tests accept pure folded legacy state, reject the replay DTO collision and preserve durable bytes. Fixture controls verify complete envelope/other-state preservation and active-writer refusal before provider access. The existing corruption, protection, retained-floor, manual-snapshot and migration controls remain in the test run. Exact commands and results are retained in [root-focused-checks.json](root-focused-checks.json), [post-review controls](root-postreview-controls-3cff389132cf4936af456b3590fea8fc/checks.json) and [final recovery-contract suite](audit-contract-controls-3c27ed047516451481183b846803b9f0/command.json). CI now includes the isolation suite.

Three independent review layers and their follow-up checks produced 21 findings. All are patched, including real interruption handling, output-path protection, script/source closure, complete receipt inventory, final outer audit, unknown-resource preservation and future-version witness enrollment. [Triage](review-triage.json) records every finding; none is deferred. The final [source diff](final-repair-source-v2.diff) and [SHA-256 manifest](final-repair-source-v2.json) retain the repair delta from the observed external baseline. Earlier review snapshots remain intact.

All historical and failed evidence is preserved. The [independent preservation check](preservation-result.json) matched **174,690 files / 13,942,121,371 bytes** across twelve prior P1R evidence directories, with zero missing, extra or changed files. All three protected Projects documents and 68 untouched tooling snapshots match; only the four authorized pre-existing executor/CLI/test/README paths differ. Rollback remains null, comparisons remain 3.70.1/3.110.0, Dapr remains 1.18.2, PostgreSQL remains `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`, and `p1r-executed-checks-v1`, Reminder recovery and logical-event evolution are retained.

Failed bootstrap attempts remain at their original new paths. The first root verification [attempt](verification-f0f123e0ef044b7792acc38d270b4039/) stopped before cases because a helper parsed image-pull output as an ID; the helper now uses a CID file. The [first complete attempt](verification-b7bf447465214da1b9857655ae61ddf7/) measured all 3,218 assertions passing, but its independent audit incorrectly expected a top-level restore invocation. Its nonpassing audit remains unchanged. The correction binds restore's entire embedded cleanup receipt to the actual cleanup invocation; the successful attempt above starts from a new frozen source/script closure.

A concurrent external `/pushall` committed the starting tooling/evidence while this task was active. [The observation](concurrent-git-observation.json) records Projects HEAD `8a9b739a30ee2b501db53756b508efb535d10c41` and EventStore HEAD `5c10a3c73f381abb75016125192401f516818797`. Those external commits were preserved. This agent did not stage, commit, push, publish, reset or change dependencies.

The [concrete successor proposal](successor-proposal.md) requires landing and source freeze only under later authorization, exact source/version/Builds identities, complete finite fixture and strict witness enrollment, default successful source CI, protected one-use publication authority, the fourteen-package release manifest, independently observed signed consumer archives, new owner-selected inputs, all seventeen canonical lanes plus both additions, strict isolation/cleanup and independent package qualification. It explicitly preserves the pending four-role/conformance/disposition and separate Story gates. No version, owner acceptance or usability transition is fabricated here.

The [final independent handoff recheck](final-handoff-recheck.json) passes 46 controls with no material discrepancy. The [final workspace check](final-workspace-state.json) confirms all nine source files match the verified diff, the frozen intent is unchanged, production source is unchanged, protected Story documents are unchanged, both external HEADs/canonical indexes are preserved and no paths are staged.

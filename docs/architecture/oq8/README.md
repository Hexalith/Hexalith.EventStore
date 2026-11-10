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

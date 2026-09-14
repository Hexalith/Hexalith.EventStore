# Story 8.3 Preflight Evidence

Preflight completed on 2026-09-14 before Story 8.3 source edits. The committed
baseline was `e8886ec4c277460de3d3208b3fc0b9c261c4967d` on `main`. The only
pre-existing worktree changes were the active Story 8.3 specification and
`sprint-status.yaml`; no `src/`, `tests/`, frozen fixture/verifier, solution, or
release-manifest byte was dirty.

## Authorization And Source Binding

Packet `AR-20260914-01` explicitly approves Story 8.2 and authorizes Story 8.3
for normative digest
`de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`.
The exact section 1.2 recomputation command reproduced that digest with unique
markers, LF-only input, and no UTF-8 BOM.

| Bound artifact | Recomputed SHA-256 | Expected result |
| --- | --- | --- |
| Story 8.2 completion evidence | `5230ffcb3b1c581eff58a135ca82091b757eab62b201049bec5ba185f0eaa1b4` | Match |
| Story 8.2 specification | `bb193a6f3fd30e240a519d7d328cdc761dda81acf1ba914e9621252280fd887d` | Match |
| Shared payload-protection authority | `542f0b6e4ebe24c02a403ed7af511a03d1a4b6ef5c83b789254fbb055a563c82` | Match |
| Baseline `sprint-status.yaml` from committed HEAD | `28407232c0d0da5f5504ce9bdc25bb05905547d5f72fbb89e022fa594d968e98` | Match; the workflow-owned file then advanced Story 8.3 to `in-progress` |
| Complete sorted `Contracts/Security` per-file hash stream | `a01dc5576702f08dc0a95caaa8a4158e457c2f1d85dd4337fc145d126a02fa2e` | Match |
| `IEventPayloadProtectionService.cs` | `bf642ba897581dae1870c524e10ee8c97824e4c4c1675ddcd0cbfe14f8f6781e` | Match |
| `PayloadProtectionV2ContractTests.cs` | `02915db53d0a375aae067261961a5b9860b7dba833413e1bf39fe3f9c9b96abf` | Match |
| Contracts test project | `1f996cc51b85147d641e0faedfd867bf379965b6d751643d95a1035eed474067` | Match |
| Fixture manifest | `cde3940952789d58f4b415c1fe36202f21cf93a534bd3f11741af3975f7570e9` | Match |
| Node verifier | `5b0892d8ed6fa3dbe29159b0a6777636206350ec43b0f33b22b2a706c040b06a` | Match |
| Python verifier | `f6a5003726478c445635653c8c0bd4c73a92001cdd65c4bdacf217d094851cf9` | Match |
| Python verifier dependency lock | `70c867286e0e8fae9c36dff5d479b0a017c1b2892e03382cbf772608714cce74` | Match |
| `tools/release-packages.json` | `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae` | Match; 14 entries |

The immutable fixture hashes embedded in the matched manifest also reproduced:
G-001 `a821f36321bd5a020b35f89b1e3d18e7dc3070e3dbf694db5c0413847a012610`,
NIST `528a81472dc6bd4db653bf01dc52a16d5e092c7bcde2ef622f42878ba794a319`,
and ownership `a3886eac22cf3b77c210ae2bb166f237980bc5e8cf1e9cbaf8c569aa6b4dc087`.

## Executable Preflight

| Command | Result |
| --- | --- |
| `node scripts/payload-protection/verify-golden-vectors.mjs` | PASS V001-V003; Node.js v26.4.0, OpenSSL 3.5.7 |
| `python3 scripts/payload-protection/verify-golden-vectors.py` | PASS V001-V003; Python 3.14.4, OpenSSL 3.5.5, cryptography 46.0.5 |

## Living-Source Recheck

The 2026-09-14 recheck used authoritative sources only:

- [NIST SP 800-38D final](https://csrc.nist.gov/pubs/sp/800/38/d/final)
  remains the November 2007 final publication and carries a revision planning
  note.
- [NIST SP 800-38D Rev. 1 second pre-draft call](https://csrc.nist.gov/pubs/sp/800/38/d/r1/2prd)
  was published June 1, 2026, closed comments July 31, 2026, and explicitly
  says no actual draft document is available. It therefore does not replace
  the frozen algorithm or vector behavior.
- [.NET 10 `AesGcm` constructors](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm.-ctor?view=net-10.0)
  confirm the required-tag-size constructor, unsupported-tag error, and
  platform-support failure used by the core.
- [.NET 10 `CryptographicOperations.ZeroMemory`](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory?view=net-10.0)
  confirms that the API fills the supplied mutable buffer with zeros.

All authorization, source, fixture, executable-vector, and release-inventory
checks passed; no preflight drift blocked implementation.

# Payload-Protection Golden Verification

These independent scripts reconstruct Story 8.1 G-001 and the NIST AES-256-GCM
Count 0 control from atomic fixture inputs. They share fixture data but no
encoding or cryptographic implementation.

The Python verifier uses the pinned dependency in `requirements.txt`; install
it with `python3 -m pip install -r scripts/payload-protection/requirements.txt`.

Run from the repository root:

```bash
node scripts/payload-protection/verify-golden-vectors.mjs
python3 scripts/payload-protection/verify-golden-vectors.py
```

To regenerate the complete derived-value review output without writing files:

```bash
node scripts/payload-protection/verify-golden-vectors.mjs --emit-derived
python3 scripts/payload-protection/verify-golden-vectors.py --emit-derived
```

Both derived objects must be byte-for-byte equivalent after JSON parsing before
reviewed fixture values are changed with an approved normative revision. The
normal verifier mode also proves that vector ownership expands to V001–V138
exactly once, executes Story 8.2-owned V001–V003, and leaves the remaining 135
cases visibly predecessor-gated.

Normative digest:
`de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`.

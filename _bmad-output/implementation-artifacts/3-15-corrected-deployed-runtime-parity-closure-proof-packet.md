# Story 3.15 Corrected Deployed Runtime Parity Closure Proof Packet

## Decision

The technical evidence is acceptance-ready, but deployed-runtime parity remains unavailable until
three real content-bound receipts exist. The retained production verifier fails closed with no
selected identity while the receipt list is empty.

Canonical subject:
`sha256:bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709`.

Positive identity after all three receipts pass:
`registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`.

## Bound evidence

- Frozen predecessor identity: `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`.
- Source/release: `f343bb0153e9cdcb8b12ec10153813072f5ad38d`, `v3.96.2`.
- Packages: 14 NuGet.org repository-signed archives independently rehashed and cross-mapped to the
  14 Story 3.14 GitHub release assets.
- OCI: independent raw index, two child manifests, and two configs; exact `linux/amd64` and
  `linux/arm64` graph.
- Runtime: bounded Production `/alive`, HTTP 200, zero redirects, cleanup pass for both immutable
  children.
- Technical inventory: 24 exact retained files, SHA-256
  `2d066fb5a5c48c7dd9184f382c098bac36b9e531cd6d86dfc5aa231747ceea86`.
- Owner registry: SHA-256
  `534268c2b5fbf39709e558f9806670d4ed8dd70574574f63babf903dc23e54fc`.
- Trusted verifier handler: SHA-256
  `d0eb781f4eeecaccdf4ca895a2fbc21ad80ad41f5f9192c007968954b1a79fa4`.
- Trusted dispatcher: SHA-256
  `aaa5d0676c4f7edc59aade4ae743b02fe6082633b3b53e8261555ac404e9930a`.

The subject and receipt directories sit outside the technical inventory to avoid a checksum cycle.
The subject binds the inventory and every decision input; `closure.json` binds the subject and the
three subject-addressed receipt files.

## Missing acceptances

The required receipt paths are:

```text
acceptances/bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709/eventstore-owner.json
acceptances/bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709/release-owner.json
acceptances/bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709/test-architect.json
```

Each receipt also requires its authenticated retained source beneath the sibling `sources/`
directory. These files have not been created or inferred. After authorized collection, rerun the
assembler, the production verifier, the focused suite, and `git diff --check`.

## Authority boundary

Even a passing receipt set proves immutable evidence only. `deployment_authorized`,
`consumer_removal_authorized`, `publication_authorized`, and `grants_mutation_authority` remain
false. Deployment and consumer removal require separate owner decisions outside this packet.

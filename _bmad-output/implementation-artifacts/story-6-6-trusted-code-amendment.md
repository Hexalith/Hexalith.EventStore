# Story 6.6: Reviewed trusted-code loader amendment

**Decision date:** 2026-10-06

**Authority:** The owner replied “do recommended” to the recommendation to select
option 3 in [the loader-policy comparison](evidence/story-6-6/dependency-loader-options-2026-10-06.md).

**Status:** Approved implementation policy. Admission and activation remain fenced
until their separate qualification requirements pass; Story 6.6 remains in progress.

This amendment replaces the earlier AD-13 §5 loader assurance where it required
universal refusal before effects from undeclared explicit loads or nested code.
The [historical approved specification](spec-event-versioning-upcasting.md), its
approval digest, and the [failed loader probe](evidence/story-6-6/dependency-loader-qualification-2026-10-05.md)
remain unchanged. The [Dapr-only amendment](story-6-6-dapr-only-amendment.md) and all
other compatible schemas, bounds, consumer checks and activation gates still apply.

## Trust and admission contract

- Every catalog implementation and its transitive dependencies are trusted
  application code under the deployer's review and release control. Tenant input
  must not supply executable code, assembly paths or arbitrary CLR type selection.
  If a deployment cannot satisfy that assumption, keep its evolution capability
  unavailable. Restricted workers require a separate design and qualification;
  this decision does not introduce another process or service.
- Before invoking a catalog route, admit its authoritative immutable manifest,
  exact options and artifact bytes. Inventory all D/V/A/E/F/S implementations,
  handlers, registration adapters, filters, receipt providers and codecs. Check
  complete declared roots and edges, managed/native identities, resolved versions
  or ABIs, contexts and file hashes against gateway and serving-peer pins.
  Explicitly inventory shared framework dependencies. Missing, changed, ambiguous
  or mismatched artifacts refuse admission before catalog callbacks.
- The deployment must keep the checked artifacts immutable and ensure execution
  uses those same artifacts. A path hash followed by loading a replaceable file
  does not establish that binding. A caller-supplied graph or successful local
  hash check alone proves neither the complete catalog nor serving-peer readiness.
- Undeclared explicit managed/native loads, reflection-based loading and nested
  dependency loads are prohibited by the reviewed code policy. Any necessary
  dynamic-loading exception must declare its artifacts and permitted path in the
  catalog and receive review and qualification before admission. Reflection over
  already declared types does not authorize loading arbitrary tenant-selected types.

## Detection and capability loss

When loader observations reveal a policy violation, invalidate the affected
process's evolution capability. Refuse subsequent catalog calls and new work
requiring that capability. An in-flight route that observes capability loss must refuse an
uncommitted success/proof; already committed truth retains the existing Dapr
reconciliation and idempotency rules.

Observations cannot undo effects that already executed. This policy supplies no
hostile-code confinement or universal before-effect refusal guarantee for code
that violates its declared graph. Hashes establish artifact identity, not safe
behavior. The failed explicit managed/native controls remain evidence of that
limit, rather than becoming passing prevention controls under a new label.

## Qualification and delivery

1. Qualify the existing local manifest/graph checks with exact pins and artifact
   bytes, and refusals for missing or changed files, wrong identities, versions,
   contexts, incomplete edges and invalid roots. These are local checks only.
2. Obtain the actual authoritative per-domain manifests, complete reviewed catalog
   and serving-peer inventory. Demonstrate that admitted routes use the immutable
   checked artifacts and that documented dynamic paths follow their declared graph.
   Retain the review/release identity and exact artifacts for the required lifetime.
3. Exercise observations and subsequent capability loss, including explicit managed,
   native, reflection and late loading. Measure startup/call cost within the existing
   64 MiB manifest and 65,536-row limits. Describe any pre-observation effects honestly.
4. Complete consumer, compatibility, Dapr recovery and production/fleet qualification
   before changing activation fences. The separately approved AD-26 production
   profile and its evidence remain required.

Until those requirements pass, retain compatible V1 behavior and dormant
preparation. This amendment authorizes implementing and qualifying the selected
policy; it does not authorize V2 writes, registration migration, deployment,
publication, production promotion or Git history changes. No M1–M8 task or O-row
closes merely because the owner selected a trust policy.

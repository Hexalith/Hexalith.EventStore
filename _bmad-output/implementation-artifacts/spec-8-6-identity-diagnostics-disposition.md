---
title: 'Story 8.6 managed-identity diagnostic disposition candidate'
type: 'security-architecture-amendment-candidate'
created: '2026-11-03'
status: 'awaiting-owner-decision'
implementation_authorized: false
candidate_normative_sha256: 'a0a746aeab4de17c1b1b95cfa37fbf42a119e29af0c7354fe174d09512b389d9'
story_key: '8-6-azure-key-vault-production-adapter-conformance'
---

# Story 8.6 Managed-Identity Diagnostic Disposition Candidate

The implemented adapter has one failing SDK diagnostic sentinel. This is a concrete policy proposal, not an approval or a passing conformance claim.

<!-- HX-PP-IDENTITY-DIAGNOSTICS-BEGIN -->

## Exact diagnostic-policy change proposed

This candidate supplements section 6 of the approved [Azure SDK exception](spec-8-6-crypto-boundary-amendment.md), normative SHA-256 `fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394`, together with the approved [Core pin correction](spec-8-6-azure-core-pin-correction.md), normative SHA-256 `360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e`. Original approval bytes, engine authority, frozen intent and predecessor disposition remain unchanged. This candidate has no authority until the project owner approves its exact normative digest.

Permit one diagnostic-data exception: the exact configured **ManagedIdentityClientId** may be present in the pinned vendor's **Azure-Identity** EventSource payloads for **LogMsalAlways (event 24)** and **ManagedIdentityCredentialSelected (event 26)**. The exception applies only to that selected identifier in those named SDK events. It does not permit application/adapter logging of the raw identifier, or its appearance in other event types, exception output, HTTP instrumentation, activities, metrics or application logs.

This explicitly relaxes the original safe-fingerprints-only rule for those vendor diagnostic payloads. Hosts or exporters enabling these sources can observe the selected client identifier. It does not claim the SDK suppressed or redacted it. The current reproduction contains two matching event-24 emissions and one event-26 emission; that observed count is not a promise about every production acquisition.

## Preserved restrictions and acceptance

All public SDK content/logging/tracing/unsafe-identity switches remain disabled. Do not enable SDK diagnostics from the adapter, replace the SDK, change package pins, remove deterministic user-assigned identity support, add a credential chain, use reflection, patch vendor code, or manipulate global listeners to hide events.

Access tokens, identity-header secrets, provider/vault/key URIs, key names/versions, plaintext, DEKs and other protected values retain the original prohibitions across every required observer. All request-count, cancellation, foreground-token-refresh, buffer ownership/clearing, exact-version, remote-only, custody and conservative-reconciliation requirements remain in force.

Retain the native managed-identity subprocess sentinel and every existing test. Its updated expectation must identify the permitted event source/name/ID and exact selected-identifier category; it must reject that identifier in any other channel and still scan complete payloads for every prohibited canary. Keep the physical-request-count and typed-failure assertions. Do not skip or disable the test or broadly exempt Azure-Identity/MSAL events. Live observers must apply the same narrow policy, report only safe categories/counts/fingerprints, and avoid exporting captured raw messages. The separately supplied live authority must explicitly bind this approved diagnostic-policy digest.

After approval, record this supplemental digest in the owning story/context and boundary register before adjusting expectations. Rerun the adapter suite and affected guards; retain the observed vendor-emission evidence. SDK, event schema/source, credential profile or newly exposed identifier/channel changes require fresh disposition. This amendment grants no Azure environment, resource/network/identity/fault mutation, live-run, Server, 8.7, production, release or G5 authority.

## Owner decision requested

Approve this exact identifier-only vendor-diagnostic exception for continued local Story 8.6 implementation and validation. The decision must identify the human accurately and remains single-maintainer-attested unless separate reviewers actually participate. Declining or withholding this decision preserves the original rule and the failing sentinel; it does not authorize another workaround.

<!-- HX-PP-IDENTITY-DIAGNOSTICS-END -->

## Observed evidence

Source HEAD at this checkpoint is `692da20b403e9fc5e3bb101660cbaf5dab97862c`, reached through external history changes that the implementation agents preserved. The test/verification-note working-tree delta is recorded separately in the [owning story](spec-8-6-azure-key-vault-production-adapter-conformance.md).

With public diagnostic switches disabled, the real pinned managed-identity SDK, a fake user-assigned client ID and a loopback 503 endpoint emit three matching records: Azure-Identity.LogMsalAlways twice and ManagedIdentityCredentialSelected once. All matches contain the configured client-ID canary; none contains the identity-header canary. A parent-agent reproduction also attached first-chance exception observation and found the same three EventSource-only matches. This local probe made no Azure calls and demonstrates no access-token/header-secret/vault-URI/key-URI disclosure in the exercised path. It does not certify every SDK failure path.

The retained failing test is `AzureManagedIdentityRetryTests.V081_ManagedIdentityHasOnePhysicalRequestPerAcquisition`. The complete adapter run before the parent diagnostic cleanup was 138 total / 137 passed / one failed / zero skipped. The parent reproduced the failure independently; it remains required and has not been exempted.

The selected net10.0 graph is Azure.Core 1.64.0, Azure.Identity 1.21.0, Keys 4.10.2 and existing transitive Microsoft.Identity.Client 4.92.0. Identity's net10.0 facade forwards the inspected identity implementations to Core; the emitter source identity is `254a1b693af968afee40958e77d0762b44a1b13c`, with the package hashes already bound by the approved exception/correction. Local inspection retained `LogMsalAlways` calling `WriteEvent(24, message)` directly and `ManagedIdentityCredentialSelected` checking informational EventSource enablement before `WriteEvent(26, credentialType, id)`. Neither emitter checks a per-client diagnostic flag. No additional supported suppression was found in the inspected public surface.

Raw local observations, decompiled emitter snippets, 65-component dependency inventory and graphs remain under `/tmp/story86-dependencies/`; independent parent build/sentinel logs are `/tmp/story86-parent-build.log` and `/tmp/story86-parent-sentinel.log`. These are disposable local evidence, not CI or Azure receipts. Actual Azure conformance and final workflow review remain pending. No diagnostic allowance has been applied.

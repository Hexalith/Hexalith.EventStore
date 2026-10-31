---
title: Qualify existing Works Development receivers
status: in-progress
created: 2026-10-31
---

The Works workspace upgrade requires the existing domain runtime, gateway, Admin and Operations hosts to participate in one real Development catalog. This candidate preserves the native transport qualification source at `5938ab989d905bf37ed4f72136ea65ff6094a802`, including the earlier native request buffering fix, and the packaging qualification branch at `c7b927c30f3af7a48d2f0d6aad5b822308cf0113`. Neither historical source nor its records are relabeled as current acceptance.

The domain auxiliary API binds one concrete POST endpoint to its existing canonical workload policy, exact request-owner assembly, actual installed delegate and static tenant/domain extractor. The existing receiver fence and observed auxiliary inventory retain the same owner binding. Missing generation, altered code/delegate/policy, foreign tenant/domain and target overrides refuse before business work. An owner may classify a malformed request as 400 only after the code and generation checks pass.

The SDK subscription mapper now provides its own private delegate witness. Its configured topic and existing channel policy are observed without turning ordinary Dapr pub/sub deliveries into workload JWT calls. Operations similarly observes only its five existing compiled dead-letter operator endpoints, with the original human, channel, tenant and role checks retained. These additions create no public message operations or permissions.

The existing Parties identity-reader client uses the canonical query wire and a domain-equal legacy projection hint. Internal identity queries on that wire require an authenticated signed service surface, the installed protected-operation vocabulary and the existing configured identity admission authority's nonempty exact-scope proof. The dispatch target is canonicalized only after that proof; other internal queries and foreign projection/actor/entity scopes remain refused. Works tests own their isolated metadata over the public Parties DTOs; the current Parties package is not claimed to contain catalog metadata or to qualify a production Parties service.

Ordinary command admission retains Mutation retention. Trusted effects already bypass the ordinary gateway terminal-record path and retain their independent SDK target receipt and joint lifecycle gate; the Works upgrade does not change that lifecycle.

The owned infrastructure tool fixture now waits for captured descendant process identities to exit after killing the tree. One five-second cleanup deadline covers parent exit, descendant acknowledgement and both redirected streams. The original seven-second/no-live-child controls remain, with additional children that close both pipes before cancellation.

Local verification on this candidate:

- 25 domain auxiliary/subscription tests, 107 Operations tests, 136 gateway/native receiver/metadata tests, 157 ServiceDefaults tests and 8 process-tool tests passed, with no skips.
- Corresponding builds passed with zero warnings and errors.
- The consuming Works private Debug lane passed 115 guarded HTTP, catalog inventory and request cancellation tests, with no skips.

Required hosted CI, the complete real four-host Works cohort, actual SDK publication and subsequent package-mode consumer qualification remain pending. A broader DomainService test run was interrupted after source changed and is not recorded as a passing run. These local results do not authorize publication without the exact-source required gates.
